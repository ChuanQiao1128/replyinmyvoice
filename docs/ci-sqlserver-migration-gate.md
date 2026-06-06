# CI SQL Server Migration Gate Specification

## Context

The .NET backend test fixtures use SQLite for fast service and API tests, while production persistence is Azure SQL. SQLite is still useful for unit-speed coverage, but it does not exercise SQL Server cascade rules or the EF Core migration chain.

The `dotnet-azure` workflow also applies migrations during the `deploy` job. That deploy-time update must remain, but it should not be the first place migrations meet SQL Server.

## Goals

- Run the full EF Core migration chain from an empty database against SQL Server in CI.
- Fail pull requests to `delivery/api-fixes` and pushes to `main` before Azure deployment when SQL Server rejects a migration.
- Keep the gate independent of production Azure SQL, Azure Key Vault, and Azure app settings.

## Non-Goals

- No runtime application code changes.
- No replacement for the deploy-time Azure SQL migration update.
- No dependency on real production or dev Azure SQL databases for CI validation.

## Current System

`.github/workflows/dotnet-azure.yml` builds and tests `backend-dotnet/ReplyInMyVoice.sln`, then deploys the Azure Functions app on `main`. The deploy job obtains the live Azure SQL connection string from Key Vault and runs `dotnet ef database update`.

## Proposed Architecture

The workflow now includes a `sqlserver-migration` job that starts an ephemeral SQL Server 2022 service container and runs:

```bash
dotnet ef database update \
  --project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj \
  --startup-project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj \
  --context AppDbContext
```

The deploy job requires both `build-test` and `sqlserver-migration`.

## Data Model

No schema or entity changes are introduced by the gate. The job validates the existing EF migrations and current `AppDbContext` model against an empty SQL Server database named `ReplyInMyVoiceMigrationGate`.

## API and Job Contracts

- Trigger: `pull_request` targeting `main` or `delivery/api-fixes`, plus existing push triggers.
- Service dependency: `mcr.microsoft.com/mssql/server:2022-latest`.
- Connection source: `ConnectionStrings__DefaultConnection` scoped to the migration step.
- Deploy dependency: `deploy.needs` includes both `build-test` and `sqlserver-migration`.
- Readiness: runner-side port wait plus bounded `dotnet ef database update` retries, avoiding dependence on SQL Server image command-line tools.

## State and Error Handling

The SQL Server container is discarded after the job. If SQL Server does not open port 1433, the job exits non-zero. If the migration command still fails after bounded retries, the final non-zero exit blocks deploy. The deploy-time Azure SQL migration step remains in place for the real target database.

## Security and Privacy

The gate uses a local-only SQL Server service container. It does not read Azure SQL secrets, write to production data, or call Azure deployment commands. The service password is derived from the GitHub Actions run id at runtime and is not a reusable credential.

## Rollout Plan

1. Add the workflow gate.
2. Require the gate before deploy through `deploy.needs`.
3. Keep existing SQLite tests for fast service coverage.
4. Keep the deploy-time `dotnet ef database update` for the real Azure SQL target.

## Verification Plan

- `cd backend-dotnet && dotnet test`
- `npm run typecheck`
- Static workflow review confirms `sqlserver-migration` uses SQL Server and `deploy.needs` includes the gate.
- Banned-term grep remains clean for changed files and the configured project scan.

## Open Questions

- The first GitHub-hosted run will confirm end-to-end container startup and migration timing for the current `mcr.microsoft.com/mssql/server:2022-latest` tag.

## Data Module Review

Findings:
- No new EF model, migration, or persistence invariant change is needed for RFX-09.
- The main persistence risk is process coverage: SQLite tests do not prove SQL Server migration validity.

Open Questions:
- None blocking local implementation.

Suggested Tests:
- CI should run the migration chain against SQL Server from an empty database.
- Existing backend tests should remain green to preserve service behavior coverage.
