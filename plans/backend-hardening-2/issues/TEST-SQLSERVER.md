## Context

Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: .NET 8 / Azure Functions under `backend-dotnet`. Wave spec: `plans/backend-hardening-2/SPEC.md` → issue **TEST-SQLSERVER** (finding #26).

The test suite has 28 `UseSqlite` behavioral tests and **zero** on SQL Server, but prod is Azure SQL. SQLite tolerates SQL-Server-only schema and the SQLite fixtures build schema with `EnsureCreatedAsync()` instead of real migrations, so provider-specific concurrency / UNIQUE / partial-index behavior is never exercised on the real engine.

Read these first (ground every change in them):
- `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/QuotaConcurrencyTests.cs` — its private `QuotaFileDbFixture` (parallel `Task.Run` + `TaskCompletionSource` gate) is the race pattern to mirror.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs` — webhook replay (duplicate `evt_*` → `processed:false`); `StripeEvent` PK is `EventId` (`AppDbContext.cs:135`).
- `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureRepositoryTests.cs:232,249` — RowVersion stamping via `TryReserveSlotAsync`.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/DbFixture.cs`, `TestCollectionBehavior.cs`, `ReplyInMyVoice.Tests.csproj`.
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs` — UNIQUE `(UserId, IdempotencyKey)` at line 84; partial indexes `HasFilter("[...] IS NOT NULL")` at 55/193/512.
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContextDesignTimeFactory.cs` — `UseSqlServer`; 28 migrations exist under `Migrations/`. Infrastructure already references `Microsoft.EntityFrameworkCore.SqlServer` 8.0.19.
- `.github/workflows/dotnet-azure.yml` — the `sqlserver-migration` job already runs a `mcr.microsoft.com/mssql/server:2022-latest` service; reuse that pattern.

## Constraints

- Banned substrings anywhere (CI grep guard, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the error-code set (engine is a frozen swappable black box).
- Do NOT modify production code — this is test + CI only. `AppDbContext`, repositories, handlers stay byte-for-byte unchanged.
- No secret values in tracked files. The container SA password is a generated/literal test-only credential, not a real secret; keep it local to the fixture/CI and never reference `.env.local`/`.dev.vars`/`globalapikey/`.
- Keep the existing **799** SQLite tests green; ADD the SQL Server suites — do not migrate or delete SQLite tests (SQLite stays the fast inner loop).
- `TestCollectionBehavior.cs` disables cross-collection parallelization assembly-wide; the new container collection must run serially under that. Use ONE shared container per collection (do not start a container per test).
- Target framework stays `net8.0`. Use a `Testcontainers.MsSql` version compatible with net8.
- The worker must NEVER push, open a PR, or touch `main`. Base branch = `delivery/backend-hardening-2`.

## Changes required

1. `backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj`: add `<PackageReference Include="Testcontainers.MsSql" Version="3.*" />` (or the latest 3.x that resolves on net8).
2. New `backend-dotnet/tests/ReplyInMyVoice.Tests/SqlServer/SqlServerDbFixture.cs`: an `IAsyncLifetime` collection fixture that:
   - builds an `MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build()` container, `StartAsync()` in `InitializeAsync`;
   - exposes `CreateContext()` returning `new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(container.GetConnectionString()).Options)`;
   - in `InitializeAsync`, after the container is up, calls `await db.Database.MigrateAsync()` (NOT `EnsureCreated`) so the prod schema — partial indexes + any CHECK constraints — is materialized from migrations;
   - provides a `CreateUserAsync()` seed helper mirroring `QuotaFileDbFixture`;
   - disposes the container in `DisposeAsync`.
3. New `backend-dotnet/tests/ReplyInMyVoice.Tests/SqlServer/SqlServerCollection.cs`: `[CollectionDefinition("SqlServer")] public sealed class SqlServerCollection : ICollectionFixture<SqlServerDbFixture> {}`.
4. New `SqlServerQuotaConcurrencyTests.cs` (`[Collection("SqlServer")]`, every test `[Trait("Category","SqlServer")]`): port `Parallel_reserves_with_one_period_slot_remaining_grant_exactly_one` and `Parallel_reserves_with_one_credit_remaining_consume_exactly_one` using the shared container fixture, asserting exactly one `Created` and the rest `QuotaExceeded`. Seed/reset rows per test (unique `UserId`) so the shared DB stays isolated.
5. New `SqlServerIdempotencyAndWebhookReplayTests.cs` (trait-gated): (a) inserting two `RewriteAttempts` with the same `(UserId, IdempotencyKey)` throws `DbUpdateException` on the second `SaveChangesAsync` (UNIQUE at `AppDbContext.cs:84`); (b) inserting two `StripeEvent` rows with the same `EventId` (the PK) conflicts on the second insert — exercising the webhook-replay dedupe path on real SQL Server.
6. New `SqlServerRowVersionConcurrencyTests.cs` (trait-gated): load a `UsagePeriod` in two contexts, save in the first (RowVersion changes), then a stale save in the second throws `DbUpdateConcurrencyException`; and assert a `Modified` save stamps a fresh `RowVersion` (mirror `InfrastructureRepositoryTests.cs:232,249`).
7. `.github/workflows/dotnet-azure.yml`: in the existing `sqlserver-migration` job (which already has the mssql service), add a step after migrations that runs the trait-gated suite, e.g. `dotnet test backend-dotnet/ReplyInMyVoice.sln -c Release --filter "Category=SqlServer"`. Reuse the existing service/SA-password env; do not add a second SQL container. If Testcontainers' own container conflicts with the job service, prefer pointing the fixture at the job service via `DOTNET_RUNNING_IN_CONTAINER`-style env OR run Testcontainers standalone — pick one and keep the default `build-test` job's `dotnet test` unchanged (it already excludes nothing because the SQL suites self-skip when no Docker/container is available is NOT acceptable — instead the default job must filter `Category!=SqlServer`).
8. Update the `build-test` job's `dotnet test` step in `.github/workflows/dotnet-azure.yml` to add `--filter "Category!=SqlServer"` so the fast CI job never needs Docker.

## Acceptance

- cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "Category!=SqlServer"
- grep -RnE "MigrateAsync|UseSqlServer" backend-dotnet/tests/ReplyInMyVoice.Tests/SqlServer/
- test -z "$(grep -Rn EnsureCreated backend-dotnet/tests/ReplyInMyVoice.Tests/SqlServer/ 2>/dev/null)"
- test "$(grep -Rln 'Trait("Category", *"SqlServer")' backend-dotnet/tests/ReplyInMyVoice.Tests/SqlServer/ | wc -l | tr -d ' ')" -ge 3
- grep -q 'Category!=SqlServer' .github/workflows/dotnet-azure.yml
- test -z "$(grep -RniE 'humanizer|bypass|undetect|detector|evade' backend-dotnet/tests/ReplyInMyVoice.Tests/SqlServer/ 2>/dev/null)"

## DO NOT

- Do NOT modify any production source under `backend-dotnet/src/` (no `AppDbContext`, repository, handler, or migration edits in this issue).
- Do NOT migrate or delete the existing SQLite suites — SQLite stays the fast inner loop.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the error-code set.
- Do NOT add a new EF migration (DATA-CHECK / DATA-ROWVERSION own that); the fixture just runs whatever migrations exist.
- Do NOT introduce banned substrings (`humanizer|bypass|undetect|detector|evade`) anywhere, including comments/identifiers.
- Do NOT print/commit secret values or read from `.env.local`/`.dev.vars`/`globalapikey/`.
- The worker must NEVER push, open a PR, or merge to `main`; commit on the integration branch only.