## Context

Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: .NET 8 / Azure Functions under `backend-dotnet`. Base branch = `delivery/backend-hardening-2` (NEVER main). Wave spec: `plans/backend-hardening-2/SPEC.md` (your section: DATA-EXPIRY).

Read these first (every claim is anchored here):
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/CreditExpiry/SendCreditExpiryRemindersHandler.cs` — the bug: send (lines 44-49) happens BEFORE mark (line 55) + `SaveChangesAsync` (line 56). No atomic claim, no leader guard → two concurrent timer runs both list the same `ExpiryReminderSentAt == null` row, both send, both mark.
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/RewriteCreditRepository.cs` — `ListExpiryReminderCandidatesAsync` (lines 233-264) is the candidate scan; `MarkExpiryReminderSentAsync` (lines 266-275) is the current in-memory (non-atomic) mark; `TryConsumeForReservationAsync` (lines 51-65) is the EXACT atomic-claim pattern to copy (`ExecuteSqlInterpolatedAsync`, conditional `WHERE`, returns rows-affected, stamps `RowVersion`).
- `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs` — entity (`ExpiryReminderSentAt` at line 14, `RowVersion` at line 21).
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs:189-206` — RewriteCredit config; `RowVersion` is `IsConcurrencyToken()` (line 201). No filtered index yet for the reminder scan.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/CreditExpiryReminderTimerFunction.cs` — timer trigger that invokes the handler.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/CreditExpiryUseCaseTests.cs` + `backend-dotnet/tests/ReplyInMyVoice.Tests/DbFixture.cs` — DbFixture uses ONE shared in-memory SQLite connection, so two `CreateContext()` handlers run a real relational `UPDATE` and can simulate concurrency.

## Constraints

- Base = `delivery/backend-hardening-2`. Worker must NEVER push, open a PR, or touch `main`.
- Do NOT change `IRewriteEngineClient` / `ResultJson` / engine error-code set (frozen black box).
- Banned substrings anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- No secret values in tracked files; validate env at runtime in the handler, not at import.
- Migration must be additive and safe against existing LIVE Azure SQL data (index-add only; no column change/drop/backfill). Must apply on SQL Server AND keep SQLite tests green (filtered-index `HasFilter` is honored on SQL Server, relaxed on SQLite — fine).
- Keep all existing skip conditions in the handler (missing email, no remaining balance, `ExpiresAt is null`).
- Keep the existing 799 tests green; add the new tests.

## Changes required

1. **Add a claim port** to `IRewriteCreditRepository` (`.../Abstractions/IRewriteCreditRepository.cs`): `Task<bool> TryClaimExpiryReminderAsync(Guid creditId, DateTimeOffset sentAt, CancellationToken ct = default);` Keep or remove `MarkExpiryReminderSentAsync` — if the handler no longer calls it, remove it from the interface and repo to avoid dead code; otherwise leave it. Decide and note in the commit message.
2. **Implement the atomic claim** in `RewriteCreditRepository.cs`, modeled on `TryConsumeForReservationAsync` (lines 51-65): an `ExecuteSqlInterpolatedAsync` `UPDATE RewriteCredits SET ExpiryReminderSentAt = {sentAt}, RowVersion = {Guid.NewGuid()} WHERE Id = {creditId} AND ExpiryReminderSentAt IS NULL`; return `rows == 1`. This is the leader guard: only one concurrent run wins the row.
3. **Reorder the handler** `SendCreditExpiryRemindersHandler.HandleAsync` to **claim-before-send**: after the existing skip checks (email present, `ExpiresAt` present, `remaining > 0`), call `TryClaimExpiryReminderAsync`; if it returns `false`, `continue` (another run already owns it — do not send, do not count). Only if the claim wins, call `notifier.TrySendCreditExpiringAsync(...)` and increment `sentCount`. (Net effect: at-most-once send; a rare notifier failure after a winning claim leaves the row marked-but-unsent, which is the deliberate at-most-once tradeoff — note this in the commit message.) Remove the now-redundant `unitOfWork.SaveChangesAsync` for the mark if the claim writes directly via SQL; keep `IUnitOfWork` only if still needed.
4. **Add a filtered index** in `AppDbContext.cs` RewriteCredit config (near line 192): a partial index supporting the candidate scan, e.g. `entity.HasIndex(x => new { x.ExpiryReminderSentAt, x.ExpiresAt }).HasFilter("[ExpiryReminderSentAt] IS NULL AND [ExpiresAt] IS NOT NULL");` (match the existing `HasFilter` style at lines 193-194).
5. **Generate ONE additive EF migration** for the new index via `dotnet ef migrations add AddCreditExpiryReminderClaimIndex --project src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj --startup-project src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj`; commit the migration + `.Designer.cs` + updated `AppDbContextModelSnapshot.cs`. Verify the generated `Up` only `CreateIndex` (no `AlterColumn`/`DropColumn`).
6. **Add tests** in `CreditExpiryUseCaseTests.cs`: (a) a happy-path assertion that the existing single-run test still passes (it asserts `ExpiryReminderSentAt == now` and a fresh `RowVersion`); (b) a NEW double-run test: seed one eligible credit, run `HandleAsync` twice (sequentially over the same shared connection, OR with a notifier whose first call lets a second handler instance claim — simplest deterministic form: invoke two handler instances built from `fixture.CreateContext()` against the same eligible row and assert the notifier received the reminder exactly once and total `sentCount == 1`). Reuse the existing `FakeCreditExpiryNotifier`.

## Acceptance

cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~CreditExpiryUseCaseTests
cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release
cd backend-dotnet && dotnet ef migrations list --project src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj --startup-project src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj
cd backend-dotnet && ! grep -RniE "humanizer|bypass|undetect|detector|evade" src/ReplyInMyVoice.Application/UseCases/CreditExpiry src/ReplyInMyVoice.Infrastructure/Repositories/RewriteCreditRepository.cs

## DO NOT

- Do NOT push, open a PR, or merge to `main`; commit only to a branch off `delivery/backend-hardening-2`.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the engine error-code set.
- Do NOT introduce a distributed lock, outbox row, or new Service Bus message — the in-row RowVersion-guarded `UPDATE ... WHERE ExpiryReminderSentAt IS NULL` IS the claim.
- Do NOT alter/drop columns or backfill data in the migration; index-add only.
- Do NOT remove the existing skip conditions (missing email, no remaining balance, null `ExpiresAt`), the timer schedule, or the `CREDIT_EXPIRY_REMINDER_WINDOW_DAYS` setting.
- Do NOT introduce any banned substring (`humanizer|bypass|undetect|detector|evade`) in code, comments, names, or SQL.