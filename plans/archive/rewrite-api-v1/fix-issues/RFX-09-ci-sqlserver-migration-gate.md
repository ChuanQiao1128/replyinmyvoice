# RFX-09: CI SQL Server migration gate (FIX-11)

**Tier:** 2 · **Owner:** Codex · **Depends on:** none
Detailed finding: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#11). This is the ROOT CAUSE of the WebhookDeliveries cascade bug that only surfaced at deploy: the C# tests use `UseSqlite` + `EnsureCreatedAsync` everywhere, so SQL-Server-only rules (multiple-cascade-paths) and the EF migrations themselves are never exercised before prod.

## Context
- Test fixtures: `backend-dotnet/tests/ReplyInMyVoice.Tests/*.cs` (all DB fixtures use SQLite + EnsureCreated). 
- CI workflows: `.github/workflows/` (the `dotnet-azure` deploy job runs `dotnet ef database update` on LIVE Azure SQL — that is currently the FIRST place migrations meet SQL Server).
- Migrations: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/`.

## Changes required
Add a CI gate that exercises the EF migrations against **SQL Server** BEFORE the deploy job, so cascade-path / migration errors fail the PR/build instead of the production deploy. Pick the lightest robust option:
1. Preferred: a CI job that spins up a SQL Server container (e.g. `mcr.microsoft.com/mssql/server` service) and runs `dotnet ef database update` (all migrations from empty) against it; fail the build on error. Run on PRs to the integration branch and on main.
2. Acceptable fallback if a service container is impractical: generate the full idempotent SQL script (`dotnet ef migrations script --idempotent`) and run a static check that fails on a known-bad pattern, plus document the gap — but prefer option 1.
Wire it so the existing `build-test` must pass it before `deploy` runs. Document the new gate in `plans/rewrite-api-v1/scheduled-jobs.md` or a short `docs/` note.

## Acceptance (machine-checkable)
- [ ] A new CI step/job runs the migrations against SQL Server (or the documented fallback) and is required before deploy; it is green on the current migration set.
- [ ] No change to app runtime code; `cd backend-dotnet && dotnet test` still green; `npm run typecheck` green.
- [ ] Banned-term grep clean.

## Do NOT
- Do NOT point the CI migration check at the real prod Azure SQL — use an ephemeral SQL Server instance/container. Do NOT remove the existing deploy-time `database update`.
