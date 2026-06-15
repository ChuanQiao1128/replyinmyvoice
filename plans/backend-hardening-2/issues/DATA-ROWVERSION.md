## Context

- Repo root: `/Users/qc/Desktop/CloudFlare`; backend at `backend-dotnet/`.
- Optimistic-concurrency token `Guid RowVersion` lives on 25 EF entities, all declared `public Guid RowVersion { get; set; } = Guid.NewGuid();` (e.g. `src/ReplyInMyVoice.Domain/Entities/AppUser.cs:21`, `RewriteCredit.cs:21`) and mapped `.IsConcurrencyToken()` in `src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs` `OnModelCreating` (lines 51-522).
- The token is re-stamped by hand at **63 sites across 32 files** (confirmed via `grep -rn "RowVersion = Guid.NewGuid()" src --include="*.cs"`). A forgotten stamp on a future mutation silently breaks the concurrency guard.
- `AppDbContext.SaveChanges` / `SaveChangesAsync` overrides already exist at `AppDbContext.cs:35-47` but only stamp consent; the consent helper itself hand-stamps at `AppDbContext.cs:608`.
- VERIFIED: zero raw-SQL / `ExecuteUpdateAsync` / `SetProperty(...RowVersion...)` value paths exist (grep returned none). The only raw `"RowVersion"` strings are EF migration `*.Designer.cs` snapshot column definitions — leave those untouched. So all 63 stamps are tracked-entity property writes and are all removable; the override will re-stamp them.
- Tests use `UseSqlite` via `tests/ReplyInMyVoice.Tests/DbFixture.cs`; namespace `ReplyInMyVoice.Tests`. Existing assertions that must stay green: `InfrastructureRepositoryTests.cs:249,287,321,401`, `StripeBillingServiceTests.cs:48`, `Application/ApiKeyUseCaseTests.cs:113`, `Application/CreditExpiryUseCaseTests.cs:79`. The `.Should().Be(originalRowVersion)` (unchanged) cases at `InfrastructureRepositoryTests.cs:287` and `StripeBillingServiceTests.cs:48` exercise paths where the entity is never `Modified`, so the new override must NOT stamp them.
- Spec: `plans/backend-hardening-2/SPEC.md:36-39` (DATA-ROWVERSION); finding #5 in `plans/interview/ARCH-OPTIMIZATION-2026-06-14.md`.

## Constraints

- Base branch = `delivery/backend-hardening-2`. NEVER push, open a PR, or touch `main`.
- Do NOT change the `IRewriteEngineClient` contract, `ResultJson` shape, or the rewrite error-code set.
- Banned substrings anywhere: `humanizer | bypass | undetect | detector | evade`.
- No secret values in tracked files; validate env at runtime in the handler, not at import.
- No EF migration / schema change — the token column already exists; this is behavioral only.
- Keep dual-provider support: the override runs under SQLite (tests) and SQL Server (prod) identically — use `ChangeTracker.Entries<IConcurrencyStamped>()` only, no provider-specific SQL.
- Keep all 799 existing tests green; add new tests.

## Changes required

1. New marker `src/ReplyInMyVoice.Domain/Contracts/IConcurrencyStamped.cs` in namespace `ReplyInMyVoice.Domain.Contracts`: `public interface IConcurrencyStamped { Guid RowVersion { get; set; } }`.
2. Make all 25 entities under `src/ReplyInMyVoice.Domain/Entities/` that declare `Guid RowVersion` implement `IConcurrencyStamped` (add `: IConcurrencyStamped` and the `using`). The list is exactly the files returned by `grep -rl "Guid RowVersion" src/ReplyInMyVoice.Domain/Entities --include="*.cs"`.
3. In `AppDbContext.cs`, add a private method e.g. `StampConcurrencyTokens()` that does `foreach (var entry in ChangeTracker.Entries<IConcurrencyStamped>().Where(e => e.State == EntityState.Modified)) entry.Entity.RowVersion = Guid.NewGuid();`. Call it from BOTH overrides (`SaveChanges(bool)` at line 35 and `SaveChangesAsync(...)` at line 41) AFTER the consent stamping and BEFORE `base.SaveChanges*`. Run it in the same order in both so sync and async behave identically.
4. In `StampConsent` (`AppDbContext.cs:604-609`) remove the manual `user.RowVersion = Guid.NewGuid();` line — the consent stamp mutates the user, marking it `Modified`, so the new generic pass covers it. (Keep `ConsentAcceptedAt` / `UpdatedAt` assignments.)
5. Remove all remaining manual `.RowVersion = Guid.NewGuid()` assignments from production source (the 31 non-AppDbContext files): Application use-case handlers (Account, ApiKey, Billing, PromoAdmin, Quota, RewriteJob, StripeEvent), Functions/RewriteHttpFunctions.cs, and Infrastructure Repositories/Services. Each removed assignment is on an entity that is added-or-modified within the same `SaveChangesAsync`, so the override re-stamps it. Do not remove the property initializers on the entities (`= Guid.NewGuid();` defaults stay — they cover the Added case).
6. New test `tests/ReplyInMyVoice.Tests/RowVersionStampingTests.cs` (namespace `ReplyInMyVoice.Tests`, reuse `DbFixture`): (a) seed an entity, reload, mutate one scalar field, `SaveChangesAsync`, assert `RowVersion` changed; (b) load an entity and `SaveChangesAsync` without mutating it, assert `RowVersion` unchanged; (c) a sync `SaveChanges()` path asserts the same stamp-on-modified behavior. Use a real `IConcurrencyStamped` entity such as `AppUser` or `UsagePeriod`.

## Acceptance

- cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release
- cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RowVersionStampingTests
- test "$(grep -rn 'RowVersion = Guid.NewGuid()' backend-dotnet/src --include='*.cs' | grep -v '/Migrations/' | grep -v '/Data/AppDbContext.cs' | wc -l | tr -d ' ')" = "0"
- cd backend-dotnet && [ "$(grep -rl 'Guid RowVersion' src/ReplyInMyVoice.Domain/Entities --include='*.cs' | wc -l | tr -d ' ')" = "$(grep -rl 'IConcurrencyStamped' src/ReplyInMyVoice.Domain/Entities --include='*.cs' | wc -l | tr -d ' ')" ]
- cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release
- cd backend-dotnet && ! grep -RniE "humanizer|bypass|undetect|detector|evade" src/ReplyInMyVoice.Domain/Contracts/IConcurrencyStamped.cs src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs

## DO NOT

- Do not change `IRewriteEngineClient`, `ResultJson`, or the rewrite error-code set.
- Do not add/edit any EF migration or migration `*.Designer.cs` snapshot file.
- Do not remove the entity-level `= Guid.NewGuid()` property initializers (they stamp the Added case).
- Do not stamp `Added`/`Unchanged`/`Deleted` entities — only `Modified`.
- Do not introduce banned substrings (`humanizer|bypass|undetect|detector|evade`).
- Do not print/commit secrets. Do not push, open a PR, or touch `main`; work only on `delivery/backend-hardening-2`.