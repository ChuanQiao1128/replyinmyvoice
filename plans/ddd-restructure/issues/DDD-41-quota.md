# DDD-41: Migrate Quota use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the Quota use-cases from
Infrastructure/Services/QuotaService.cs into Application handlers that depend on repository
interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP the old
service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

QuotaService.cs (624L) exposes five public use-cases:
- `ReserveAsync` — atomically reserve quota for a rewrite attempt (with retry logic and credit
  waterfall); returns a `ReserveRewriteResult` discriminated union
- `FinalizeSuccessAsync` — mark a reservation as consumed after a successful rewrite
- `MarkProcessingAsync` — transition a reservation to the "processing" state before the engine runs
- `ReleaseAsync` — release a reservation (failure/cancel path)
- `ReleaseExpiredReservationsAsync` (two overloads) — batch-release expired reservations for
  timer-triggered cleanup

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old QuotaService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests for QuotaService stay green, assertions unchanged.
- `ReserveAsync`, `FinalizeSuccessAsync`, `MarkProcessingAsync`, and `ReleaseAsync` all commit
  state — use IUnitOfWork with retry-strategy semantics matching the existing implementation.
- `ReleaseExpiredReservationsAsync` is a batch command — the handler must keep the same batch-size
  loop semantics to avoid large transaction locks.
- The outbox enqueue step inside `ReserveAsync` (webhook delivery) should go through
  `IOutboxMessageRepository` (already in Application/Abstractions).

## Changes required
1. `Application/UseCases/Quota/*.cs` — command/query + handler per use-case:
   - `ReserveQuotaCommand.cs` + `ReserveQuotaHandler.cs`
   - `FinalizeQuotaSuccessCommand.cs` + `FinalizeQuotaSuccessHandler.cs`
   - `MarkQuotaProcessingCommand.cs` + `MarkQuotaProcessingHandler.cs`
   - `ReleaseQuotaCommand.cs` + `ReleaseQuotaHandler.cs`
   - `ReleaseExpiredReservationsCommand.cs` + `ReleaseExpiredReservationsHandler.cs`
2. Extend Application/Abstractions if any new repository methods are required for the reserve/
   release path (e.g. batch-load expired reservations). Mirror existing `IUsageReservationRepository`
   and `IUsagePeriodRepository` style.
3. Extend Infrastructure/Repositories with matching implementations; register in
   ServiceCollectionExtensions.cs.
4. `Application/Common/*.cs` — result DTO for `ReserveQuotaResult` (mirrors `ReserveRewriteResult`
   discriminated union) returned by `ReserveQuotaHandler`.
5. `ServiceCollectionExtensions.cs` — register the new handlers (+ any new repositories) with `AddScoped`.
6. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/QuotaUseCaseTests.cs` — cover all
   handlers using SQLite in-memory, including the reserve-retry and expired-batch paths.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~QuotaUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Quota/ReserveQuotaHandler.cs`

## DO NOT
- Do NOT delete or edit QuotaService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
