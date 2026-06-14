## Context

Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: `backend-dotnet/` (.NET 8, Azure Functions isolated worker). Wave spec: `plans/backend-hardening-2/SPEC.md` (issue **DATA-DBOPTS**, finding #9). Base branch is `delivery/backend-hardening-2` — never `main`.

Read these first to ground every change:
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs:61-78` — `AddDbContext<AppDbContext>` → `UseSqlServer(...).EnableRetryOnFailure(maxRetryCount:5, maxRetryDelay:10s)`. **No `CommandTimeout`** (defaults to 30s). The `Func<AppDbContext>` factory is at lines 80-84.
- `ServiceCollectionExtensions.cs:413-453` — existing `ReadClampedInt` / `ReadBoundedInt` helpers and the UPPER_SNAKE `_SEC` env convention (e.g. `OPENAI_TIMEOUT_SEC`, line 310).
- `backend-dotnet/src/ReplyInMyVoice.Functions/Program.cs` and `backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs` — both call `AddReplyInMyVoiceInfrastructure`; neither checks pending migrations at startup.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/HealthFunction.cs:59-122` — `ReadinessHealth` already builds a checks envelope (`database`, `serviceBus`, ...) and returns 200/503. Records `ReadinessChecks`/`DatabaseReadinessCheck` at lines 329-349. `IsSqlite()` at 304.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs:580-596` — `..._enables_sql_server_retry_strategy_for_default_connection` shows how to resolve `DbContextOptions<AppDbContext>` and `new AppDbContext(options)` from a built provider; `BuildProvider` helper at 684.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/HealthFunctionReadinessTests.cs:80-100` — SQLite-in-memory `AppDbContext` construction + `HealthFunction` invocation + JSON envelope assertions.
- `.github/workflows/dotnet-azure.yml:140-150` — CI applies migrations via `dotnet ef database update` in a job separate from app startup (context: why drift is silent).

## Constraints

- Banned substrings anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`. Run `grep -rniE "humanizer|bypass|undetect|detector|evade" src tests` clean before finishing.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the error-code set.
- No secret values in tracked files; read config via `IConfiguration` at runtime only.
- Keep dual-provider support (SQL Server in prod, SQLite for tests). `CommandTimeout` is a SqlServer-only option — set it ONLY inside the `UseSqlServer` lambda, never on the SQLite path (SQLite ignores it / can throw).
- Keep the existing 799 tests green; only ADD/extend tests.
- Env var naming: `SQL_COMMAND_TIMEOUT_SEC` (UPPER_SNAKE, `_SEC` suffix, matching the file's convention). Reuse `ReadClampedInt`.
- Base = `delivery/backend-hardening-2`. The worker must NEVER push, open a PR, or touch `main`.

## Changes required

1. **Configurable CommandTimeout** in `ServiceCollectionExtensions.cs`, inside the `if (!string.IsNullOrWhiteSpace(connectionString))` SqlServer branch (lines 67-72). Read `SQL_COMMAND_TIMEOUT_SEC` via `ReadClampedInt(configuration, "SQL_COMMAND_TIMEOUT_SEC", defaultValue: 30, min: 30, max: 600)` and pass it to the options, e.g. `options.UseSqlServer(connectionString, sqlOptions => { sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null); sqlOptions.CommandTimeout(commandTimeoutSeconds); })`. Default 30s preserves current behavior; clamp floor 30 (never shorten), ceiling 600. Do NOT touch the SQLite branch.

2. **Document the AddDbContextPool decision.** Add a concise XML doc comment (or `//` comment) immediately above the `services.AddDbContext<AppDbContext>(...)` call explaining that pooling was evaluated and rejected because the `Func<AppDbContext>` factory (lines 80-84) news up additional contexts and the scoped repositories rely on plain `AddDbContext` semantics. No banned terms.

3. **New `DbMigrationStatus` helper** at `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/DbMigrationStatus.cs`. A static class with `public static async Task<MigrationStatusResult> EvaluateAsync(DbContext db, CancellationToken ct)` that: if the provider is relational and supports migrations, calls `db.Database.GetPendingMigrationsAsync(ct)`; returns a small record `MigrationStatusResult(bool HasPendingMigrations, IReadOnlyList<string> PendingMigrations, string? Error)`. Catch non-`OperationCanceledException` exceptions and return them as `Error` (so a transient DB blip never crashes startup/readiness). For a SQLite context created via `EnsureCreated` (no migrations history), `GetPendingMigrationsAsync` returns the full applied list as "pending" — handle this by treating SQLite/`EnsureCreated` contexts as "no drift" (e.g. when `db.Database.GetAppliedMigrationsAsync` is empty AND the model was created, OR simply: gate the SQLite case to `HasPendingMigrations=false`). Keep it provider-aware and defensive.

4. **Surface in readiness** in `HealthFunction.cs`: add a `migrations` entry to the `ReadinessChecks` record and the `ReadinessHealth` aggregation. Compute it via `DbMigrationStatus.EvaluateAsync(db, cancellationToken)` only when `database.Ok`. Add a new `MigrationReadinessCheck(bool Ok, int PendingCount, string[]? Pending, string? Error)` record (JSON names `ok`/`pendingCount`/`pending`/`error`). **Default behavior: drift is REPORTED but does not flip overall `ok` to false** (do not 503 on pending migrations by default) — i.e. include `migrations` in the envelope and set its own `Ok=false` when pending, but only fold it into the top-level `ok` if a new off-by-default flag `Health:FailOnPendingMigrations` (read like the other `ReadPositiveInt`/`ReadNonNegativeInt` config keys) is true. This avoids accidentally 503-ing prod mid-deploy. No secrets logged.

5. **Tests:**
   - `InfrastructureServiceCollectionTests.cs`: add 3 facts — default `CommandTimeout` is 30 (resolve `DbContextOptions<AppDbContext>`, read the `SqlServerOptionsExtension` via `options.FindExtension<Microsoft.EntityFrameworkCore.Infrastructure.Internal.RelationalOptionsExtension>()?.CommandTimeout` or the SqlServer-specific extension), configured value (`SQL_COMMAND_TIMEOUT_SEC=120` → 120), and clamp (`10` → 30 floor, `9999` → 600 ceiling). Use a SqlServer connection string like the existing `..._enables_sql_server_retry_strategy...` test so the SqlServer branch is exercised.
   - New `backend-dotnet/tests/ReplyInMyVoice.Tests/DbMigrationStatusTests.cs`: build a SQLite in-memory `AppDbContext` (pattern from `HealthFunctionReadinessTests`), `EnsureCreatedAsync`, assert `EvaluateAsync` returns `HasPendingMigrations == false`; and a negative case proving the `Error` path is captured without throwing (e.g. a disposed/closed connection) returns `Error != null` and does not throw.
   - `HealthFunctionReadinessTests.cs`: extend the existing happy-path test (or add one) asserting the readiness JSON now contains `checks.migrations` with `ok == true` and `pendingCount == 0` for the migrated SQLite context, and that overall `ok` stays true.

## Acceptance

- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~DbMigrationStatusTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~InfrastructureServiceCollectionTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~HealthFunctionReadinessTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `cd backend-dotnet && grep -rniE "humanizer|bypass|undetect|detector|evade" src tests` (must print nothing)

## DO NOT

- Do NOT switch `AddDbContext` to `AddDbContextPool` (incompatible with the `Func<AppDbContext>` factory) — document the rejection instead.
- Do NOT set `CommandTimeout` on the SQLite branch; SqlServer branch only.
- Do NOT make pending migrations 503 the readiness endpoint by default — report-only unless `Health:FailOnPendingMigrations=true`.
- Do NOT change `IRewriteEngineClient` / `ResultJson` / error codes; do NOT add or run EF migrations; do NOT edit the CI `sqlserver-migration`/deploy jobs.
- Do NOT introduce banned terms `humanizer|bypass|undetect|detector|evade` anywhere; no secret values in tracked files.
- The worker must NEVER push, open a PR, or touch `main`; commit only to `delivery/backend-hardening-2`.