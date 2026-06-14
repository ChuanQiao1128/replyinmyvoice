## Context

- Repo root: `/Users/qc/Desktop/CloudFlare`; backend solution: `backend-dotnet/ReplyInMyVoice.sln` (.NET 8, Azure Functions). Base branch for this work: `delivery/backend-hardening-2` (NEVER `main`).
- Goal (wave finding #7): add DB-level CHECK constraints enforcing money/quota invariants on `RewriteCredits` and `UsagePeriods`, which today are enforced only in C#. Copy the existing `PromoCodes` precedent.
- Read these first to ground every change:
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs:461-481` — `PromoCode` block; pattern is `entity.ToTable(x => { x.HasCheckConstraint("CK_...", "[Col] >= 0"); });`.
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs:68-78` — `UsagePeriod` block; columns `QuotaLimit`, `UsedCount`, `ReservedCount` (`int`).
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs:189-206` — `RewriteCredit` block; columns `AmountGranted`, `AmountConsumed` (non-null `int`); `OriginalAmountGranted` is nullable — do not reference it.
  - `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs:9-11` and `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/UsagePeriod.cs:9-11` — confirm property names.
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260603023140_AddPromoCodes.cs:38-43` — `table.CheckConstraint(...)` emission style.
  - `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoCodeSchemaTests.cs` — exact test pattern: SQLite `:memory:`, `EnsureCreatedAsync()`, violating write → `await act.Should().ThrowAsync<DbUpdateException>()`. SQLite enforces model CHECK constraints under `EnsureCreated`, so no SQL Server needed for the test.
  - `backend-dotnet/.github/../.github/workflows/dotnet-azure.yml:74` `sqlserver-migration` job (`mssql/server:2022-latest`, `dotnet ef database update`) — the prod-safety gate the migration must pass.

## Constraints

- Banned terms anywhere (CI grep guard, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient`, `ResultJson` shape, or any error-code set (engine is a frozen black box).
- Migration MUST be additive and reversible: `Up` adds both constraints, `Down` drops both. No data rewrites, no column changes, no `DROP`/`ALTER COLUMN`.
- Do NOT add `UsedCount + ReservedCount <= QuotaLimit` (credit overflow legitimately exceeds base quota). Only the two constraints named below.
- Keep the existing 799 tests green and ADD tests. SQLite fast path only — do not add a SQL Server/Testcontainers dependency.
- No secret values in any tracked file. Worker must NEVER push, open a PR, or touch `main`. Do NOT run `dotnet ef database update` against any real DB.

## Changes required

1. In `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`, in the `RewriteCredit` entity block (lines ~189-206), add a `entity.ToTable(x => { x.HasCheckConstraint("CK_RewriteCredits_Consumed_Range", "[AmountConsumed] >= 0 AND [AmountConsumed] <= [AmountGranted]"); });` call (mirror the PromoCode `ToTable`/`HasCheckConstraint` form exactly).
2. In the same file, in the `UsagePeriod` entity block (lines ~68-78), add `entity.ToTable(x => { x.HasCheckConstraint("CK_UsagePeriods_Counts_NonNegative", "[UsedCount] >= 0 AND [ReservedCount] >= 0"); });`.
3. Generate a new EF migration named `AddCreditQuotaCheckConstraints`: `cd backend-dotnet && dotnet ef migrations add AddCreditQuotaCheckConstraints --project src/ReplyInMyVoice.Infrastructure --startup-project src/ReplyInMyVoice.Api`. The `Up` must call `migrationBuilder.AddCheckConstraint(...)` for both; the `Down` must call `migrationBuilder.DropCheckConstraint(...)` for both. This regenerates `AppDbContextModelSnapshot.cs` and the `.Designer.cs` — commit those.
4. Add `backend-dotnet/tests/ReplyInMyVoice.Tests/CreditQuotaSchemaTests.cs` modeled on `PromoCodeSchemaTests.cs` (SQLite `:memory:` + `EnsureCreatedAsync`). Cases: (a) `RewriteCredit` with `AmountConsumed > AmountGranted` → `DbUpdateException`; (b) `RewriteCredit` with `AmountConsumed < 0` → `DbUpdateException`; (c) valid `RewriteCredit` saves; (d) `UsagePeriod` with `UsedCount = -1` → `DbUpdateException`; (e) `UsagePeriod` with `ReservedCount = -1` → `DbUpdateException`; (f) valid `UsagePeriod` saves. Use real required FK rows (seed an `AppUser`) following the existing fixtures so the only failure cause is the CHECK.

## Acceptance

- `cd backend-dotnet && grep -q "CK_RewriteCredits_Consumed_Range" src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs && grep -q "CK_UsagePeriods_Counts_NonNegative" src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`
- `cd backend-dotnet && grep -q "AddCheckConstraint" src/ReplyInMyVoice.Infrastructure/Migrations/*_AddCreditQuotaCheckConstraints.cs && grep -q "DropCheckConstraint" src/ReplyInMyVoice.Infrastructure/Migrations/*_AddCreditQuotaCheckConstraints.cs`
- `cd backend-dotnet && dotnet ef migrations has-pending-model-changes --project src/ReplyInMyVoice.Infrastructure --startup-project src/ReplyInMyVoice.Api`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~CreditQuotaSchemaTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `cd backend-dotnet && ! grep -RniE "humanizer|bypass|undetect|detector|evade" src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs src/ReplyInMyVoice.Infrastructure/Migrations/*_AddCreditQuotaCheckConstraints.cs tests/ReplyInMyVoice.Tests/CreditQuotaSchemaTests.cs`

## DO NOT

- Do NOT add `UsedCount + ReservedCount <= QuotaLimit` or any constraint referencing `QuotaLimit` / `OriginalAmountGranted`.
- Do NOT modify any table other than `RewriteCredits` and `UsagePeriods`, and do NOT add/alter/drop columns.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or error-code sets.
- Do NOT add SQL Server / Testcontainers to the test path; SQLite `EnsureCreated` is sufficient.
- Do NOT run `dotnet ef database update` against any real/Azure SQL DB; the LIVE "no existing row violates" check is a human/supervisor pre-merge gate — note it in the PR body only.
- Do NOT push, open a PR, merge, or operate on `main`; work only on the `delivery/backend-hardening-2`-based worktree.