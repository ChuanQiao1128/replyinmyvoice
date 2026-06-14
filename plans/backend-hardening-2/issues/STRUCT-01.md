## Context

Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: .NET 8 / Azure Functions under `backend-dotnet`. Base branch = `delivery/backend-hardening-2` (NEVER `main`). Wave spec: `plans/backend-hardening-2/SPEC.md` (issue STRUCT-01).

Read first:
- `.github/workflows/dotnet-azure.yml` — `build-test` job lines 67-68 (`Publish API` → `backend-dotnet/artifacts/api`, NEVER consumed downstream); `deploy` job line 197 (`--startup-project backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj`); the `sqlserver-migration` job (~lines 150-160) which ALREADY runs `dotnet ef database update` with `--startup-project ...ReplyInMyVoice.Infrastructure.csproj` against an mssql:2022 service container, proving the design-time factory works.
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContextDesignTimeFactory.cs` — existing `IDesignTimeDbContextFactory<AppDbContext>`, reads `ConnectionStrings__DefaultConnection`/`ConnectionStrings:DefaultConnection`/`DATABASE_URL`, `UseSqlServer(...)`. No change to behavior needed.
- `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` — 1,736-line shadow host, `public partial class Program;` at line 1736; re-maps prod routes but is never deployed and lacks `HttpHardeningMiddleware`.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj` line 29 references the Api project; `ApiAuthEmailResolverTests.cs` uses `ReplyInMyVoice.Api.AuthEmailResolver`. Therefore DEMOTE Api, do not delete.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/SqlConnectionStringResolverTests.cs` — mirror this style (namespace `ReplyInMyVoice.Tests`, FluentAssertions, `sealed` class, `[Fact]`) for the new test.

## Constraints

- Banned terms anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient`, `ResultJson` shape, or the error-code set (frozen black box).
- No secret values in tracked files; the factory's fallback connection string is a dev-only placeholder already present — keep it, do not add real credentials.
- Keep all 799 existing tests green; ADD tests.
- Worker must NEVER push, open a PR, or touch `main`. One PR per issue onto `delivery/backend-hardening-2`.
- Do NOT add/alter any EF migration. Do NOT delete the `ReplyInMyVoice.Api` project.

## Changes required

1. `.github/workflows/dotnet-azure.yml`: delete the `Publish API` step (lines 67-68) entirely — it builds `backend-dotnet/artifacts/api`, which no later step consumes. Keep `Publish Worker` and `Publish Functions`.
2. `.github/workflows/dotnet-azure.yml`: in the `deploy` job's `Apply database migrations` step, change `--startup-project backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj` (line 197) to `--startup-project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj` so the LIVE Azure SQL migration runs through the same `AppDbContextDesignTimeFactory` the `sqlserver-migration` gate already validates. Leave `--project` and `--context AppDbContext` unchanged.
3. Add `backend-dotnet/tests/ReplyInMyVoice.Tests/MigrationStartupProjectTests.cs` (namespace `ReplyInMyVoice.Tests`): instantiate `new AppDbContextDesignTimeFactory().CreateDbContext(Array.Empty<string>())` and assert (a) it returns a non-null `AppDbContext`, and (b) `context.Database.ProviderName` equals `"Microsoft.EntityFrameworkCore.SqlServer"` — pinning that the design-time factory used for production migrations targets SqlServer. Dispose the context.
4. Optionally add a one-line XML doc comment to `AppDbContextDesignTimeFactory.cs` noting it is the startup project for both the CI migration gate and the prod deploy migration step (no behavior change). Skip if it risks churn.

## Acceptance

- `! grep -nE 'Publish API|artifacts/api' .github/workflows/dotnet-azure.yml`
- `! grep -nE 'startup-project[^\n]*ReplyInMyVoice\.Api' .github/workflows/dotnet-azure.yml`
- `grep -c 'startup-project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj' .github/workflows/dotnet-azure.yml | grep -qE '^[2-9]'`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~MigrationStartupProjectTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `! grep -RniE 'humanizer|bypass|undetect|detector|evade' backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContextDesignTimeFactory.cs backend-dotnet/tests/ReplyInMyVoice.Tests/MigrationStartupProjectTests.cs .github/workflows/dotnet-azure.yml`

## DO NOT

- Do NOT delete `ReplyInMyVoice.Api` (tests reference `AuthEmailResolver` + `partial class Program`); only stop publishing it and stop using it as the migration startup project.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the error-code set.
- Do NOT lift duplicated literals (STRUCT-02) or rewire V1 EF (STRUCT-03).
- Do NOT add or modify any EF migration; no schema change.
- Do NOT commit secrets; keep the factory's existing dev-only fallback string as-is.
- Do NOT push, open a PR, or touch `main`; never base off anything but `delivery/backend-hardening-2`.