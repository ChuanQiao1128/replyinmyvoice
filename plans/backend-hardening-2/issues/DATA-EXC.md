## Context

- Repo root: `/Users/qc/Desktop/CloudFlare`; backend at `/Users/qc/Desktop/CloudFlare/backend-dotnet`. Base branch is the integration branch `delivery/backend-hardening-2` (NEVER `main`).
- Read first (ground every change here):
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/UnitOfWork.cs:85-104` — `IsRetryableTransactionRace` + `IsRetryableDbUpdateRaceException`; currently string-matches `IX_UsagePeriods_UserId_PeriodKey`, `IX_RewriteAttempts_UserId_IdempotencyKey`, `"serialization"`, `"deadlock"`, `"3960"`, and `"database is locked"`; the only typed path is `DbUpdateConcurrencyException` and `SqliteException { SqliteErrorCode: 5 or 6 }`. No `SqlException.Number` path exists.
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyRateLimiter.cs:145-157` and `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/UserRewriteRateLimiter.cs:151-163` — duplicate `IsSqliteBusy` (SQLite 5/6 + `"database is locked"`/`"database table is locked"`) plus index-name string matches.
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs:106` — `services.AddScoped<IUnitOfWork, UnitOfWork>();` is where DI lives.
  - `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IUnitOfWork.cs` — abstraction style/namespace (`ReplyInMyVoice.Application.Abstractions`).
  - `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureRepositoryTests.cs:1-12` — test conventions (FluentAssertions, `namespace ReplyInMyVoice.Tests`, `Microsoft.Data.Sqlite`).
- This is SPEC section **DATA-EXC** / finding #8 in `backend-dotnet/../plans/backend-hardening-2/SPEC.md`.
- `Microsoft.Data.SqlClient` is already transitively available to Infrastructure via `Microsoft.EntityFrameworkCore.SqlServer 8.0.19` (no new PackageReference needed). `Microsoft.Data.SqlClient.SqlException` has NO public constructor.

## Constraints

- Banned substrings anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the error-code set. Do NOT change `IUnitOfWork` public signatures or transaction/isolation semantics.
- Keep dual-provider support: SQLite signals (`SqliteErrorCode` 5/6, `"database is locked"`, `"database table is locked"`) MUST remain retryable. SQL Server signals are added, not substituted.
- No EF migration, no schema change, no secrets in tracked files. Validate nothing at module import.
- Worker must NEVER push, open a PR, or touch `main`. Keep existing 799 tests green and ADD tests.
- Because `SqlException` cannot be `new`-ed in tests, the classifier MUST read the SQL error number via a duck-typed/reflective seam (a public `int Number` property, walked across `InnerException`), NOT a hard `is SqlException` cast — so a tiny test fake exposing `Number` can exercise the SQL-number path. Still also handle a real `SqlException` if one ever appears (the reflective `Number` read covers it).

## Changes required

1. New `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IDbExceptionClassifier.cs`: interface `IDbExceptionClassifier` with `bool IsRetryableConcurrencyRace(Exception exception);` (single predicate is enough for current call sites). Namespace `ReplyInMyVoice.Application.Abstractions`. XML-doc the SQL numbers it recognizes.
2. New `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/DbExceptionClassifier.cs`: `public sealed class DbExceptionClassifier : IDbExceptionClassifier`. `IsRetryableConcurrencyRace` returns true when ANY exception in the `InnerException` chain is: `DbUpdateConcurrencyException`; OR a `SqliteException { SqliteErrorCode: 5 or 6 }`; OR exposes a public `int Number` whose value is in `{1205, 2627, 2601, 3960}` (deadlock, unique key, unique index, snapshot conflict) — read the `Number` via reflection (`GetType().GetProperty("Number")`) so both real `SqlException` and a test fake match; OR whose `Message` contains `"database is locked"` or `"database table is locked"` (SQLite fallback). Provide an internal `static` helper so call sites that are themselves `static` can delegate without a DI dependency, and have the instance method call the static. Do NOT match on index names or the literal `"3960"` string.
3. Edit `UnitOfWork.cs`: replace the body of `IsRetryableTransactionRace`/`IsRetryableDbUpdateRaceException` so it delegates to `DbExceptionClassifier`'s static helper. Remove the `IX_UsagePeriods_UserId_PeriodKey`, `IX_RewriteAttempts_UserId_IdempotencyKey`, `"serialization"`, `"deadlock"`, and `"3960"` string matches. Keep the SQLite + `DbUpdateConcurrencyException` outcomes identical.
4. Edit `ApiKeyRateLimiter.cs` and `UserRewriteRateLimiter.cs`: have their `IsSqliteBusy` (and the SQLite branch of `IsRateLimitRaceException`) delegate to the same `DbExceptionClassifier` static helper for the SQLite/concurrency signals; you MAY keep the limiter-specific index-name match (`IX_ApiKeyRateLimitWindows_*` / `IX_UserRewriteRateLimitWindows_*`) local since those are window-uniqueness collisions, but the busy/concurrency decision must route through the classifier. Behavior must be unchanged.
5. Edit `ServiceCollectionExtensions.cs`: register `services.AddSingleton<IDbExceptionClassifier, DbExceptionClassifier>();` near line 106 (the classifier is stateless → Singleton is fine).
6. New `backend-dotnet/tests/ReplyInMyVoice.Tests/DbExceptionClassifierTests.cs`: define a private test-fake exception type exposing `public int Number {get;}` and verify, against `new DbExceptionClassifier()`: 1205, 2627, 2601, 3960 → true; 547 (FK) → false; a `DbUpdateConcurrencyException` → true; a `SqliteException` constructed with error code 5 (or 6) → true (use `new SqliteException("msg", 5)` if the ctor allows, else assert via a fake exposing the SQLite path through message text); an exception whose message contains `"database is locked"` → true; a plain `InvalidOperationException` → false; and a nested case where the SQL-number-bearing fake is the `InnerException` of an outer `DbUpdateException` → true.

## Acceptance

- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~DbExceptionClassifierTests`
- `cd backend-dotnet && grep -nE "3960|IX_UsagePeriods_UserId_PeriodKey|IX_RewriteAttempts_UserId_IdempotencyKey" src/ReplyInMyVoice.Infrastructure/Repositories/UnitOfWork.cs || true`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `cd backend-dotnet && grep -rniE "humanizer|bypass|undetect|detector|evade" src/ tests/ || true`

(First command: new tests pass over both provider signals. Second: prints nothing — no index-name/`3960` strings left in UnitOfWork. Third: full 799+ suite green. Fourth: no banned substring.)

## DO NOT

- Do NOT change `IRewriteEngineClient`, `ResultJson`, the error-code set, or `IUnitOfWork` public signatures / isolation behavior.
- Do NOT drop SQLite signals or add a `Microsoft.Data.SqlClient` PackageReference (it is already transitive).
- Do NOT add an EF migration, Testcontainers, or any schema change (Testcontainers is TEST-SQLSERVER).
- Do NOT introduce banned substrings; do NOT write secret values into tracked files.
- Do NOT push, open a PR, merge, or touch `main` — commit only to a worktree branch off `delivery/backend-hardening-2`.