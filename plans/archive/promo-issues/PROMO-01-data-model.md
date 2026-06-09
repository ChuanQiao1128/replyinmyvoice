# PROMO-01 — Promo data model + EF migration (Phase 1, TIER 1, CANARY)

Wave: promo-wave · Authoritative spec: `plans/promo-code-trial-spec.md` (read §6). Backend = C#/.NET Azure Functions + Azure SQL (EF Core) under `backend-dotnet/`.

## Context
First building block for a redeemable promo-code trial. Add two tables; no behavior change yet (free baseline stays 3 in this issue).

## Changes required
1. `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCode.cs` — entity per §6 (`Code` normalized+unique, `DisplayCode`, `Description`, `Kind` enum, `CreditsGranted`, `GrantTtlDays`, `ValidFrom`, `ValidUntil`, `MaxRedemptionsGlobal?`, `MaxRedemptionsPerUser`, `RedemptionCount`, `IsActive`, `CreatedAt`, `UpdatedAt`, `Guid RowVersion`). Add `enum PromoCodeKind { TrialCredits }`.
2. `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCodeRedemption.cs` — entity per §6 (`PromoCodeId`, `UserId`, `RewriteCreditId`, `CreditsGranted`, `CodeSnapshot`, `RedeemIpHash?`, `Status` enum, `RedeemedAt`, `ReversedAt?`, `RowVersion`). Add `enum PromoCodeRedemptionStatus { Applied, Reversed }`.
3. `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs` — add `DbSet`s; `OnModelCreating`: `HasIndex(Code).IsUnique()`; `HasIndex(new {PromoCodeId, UserId}).IsUnique()`; indexes on `PromoCodeId`, `UserId`, `RedeemIpHash`, `RedeemedAt`; string-converted enums w/ `HasMaxLength`; `RowVersion` concurrency tokens; FK `PromoCodeId→PromoCode` `Restrict`, `UserId→AppUser` `Cascade`, `RewriteCreditId` plain indexed Guid (NO cascade FK — avoids SQL Server multiple-cascade-path); **CHECK constraints** (`CreditsGranted>0`, `GrantTtlDays>0`, `MaxRedemptionsPerUser>=1`, `MaxRedemptionsGlobal IS NULL OR >0`, `ValidUntil>ValidFrom`, `RedemptionCount>=0`).
4. EF migration `_AddPromoCodes` in `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/` (use the existing `dotnet ef migrations add` flow; mirror naming convention).

## Acceptance (machine-checkable)
- `dotnet build backend-dotnet/ReplyInMyVoice.sln` succeeds.
- New xUnit tests (SQLite, mirror existing test style) prove: unique `Code`; unique `(PromoCodeId,UserId)`; a CHECK rejects `CreditsGranted=0` and `ValidUntil<=ValidFrom`.
- `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj` green; full migration applies on a fresh SQLite/LocalDB.
- No other entity modified except adding the two nav/DbSet references.

## Constraints / Do NOT
- Do NOT change `GetUsagePlan`, quota, or any consumption logic in this issue.
- Do NOT use `migrate reset`/`--force-reset`/drop tables.
- No secret values in tracked files. Banned terms `humanizer|bypass|undetect|detector|evade` must not appear.
- Do NOT push, open PRs, merge, or run deploy commands — the driver handles git/PR.
