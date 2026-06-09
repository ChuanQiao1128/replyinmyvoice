# DDD-51: Migrate WebhookOutbox use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the WebhookOutbox use-cases from
Infrastructure/Services/WebhookDispatcherService.cs and
Infrastructure/Services/OutboxDispatcherService.cs into Application handlers that depend on
repository interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP
both old services in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDispatcherService.cs,
backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/OutboxDispatcherService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

WebhookDispatcherService.cs exposes one public use-case:
- `DispatchDueAsync` — claim a batch of due webhook deliveries, attempt HTTP dispatch for each,
  mark delivered or increment failure count with back-off; returns count of dispatched deliveries

OutboxDispatcherService.cs exposes one public use-case:
- `DispatchDueAsync` — claim a batch of due outbox messages, dispatch each to its registered
  handler, mark sent or increment failure count; returns count of dispatched messages

Both are timer-triggered batch commands with the same structural pattern: claim-with-retry,
dispatch, mark-success-or-failure. They share no entity types.

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old WebhookDispatcherService and OutboxDispatcherService untouched. No DB schema change
  / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests stay green, assertions unchanged.
- HTTP dispatch in `WebhookDispatcherService` is an Infrastructure concern — define
  `IWebhookDeliverySender` in Application/Abstractions (the interface already exists in
  WebhookDispatcherService.cs as a local interface — promote it); Infrastructure wires
  `HttpWebhookDeliverySender`.
- Outbox message routing in `OutboxDispatcherService` dispatches to registered handlers by
  message type — define `IOutboxMessageHandler` in Application/Abstractions; Infrastructure
  provides the concrete handler registrations.
- Both `DispatchDueAsync` handlers must preserve the optimistic-claim-with-retry pattern (avoids
  double-dispatch under concurrent timers) using IUnitOfWork.
- Back-off logic (delay before retry) and batch-size caps must match the existing implementation.

## Changes required
1. `Application/UseCases/WebhookOutbox/*.cs` — command + handler per dispatcher:
   - `DispatchDueWebhooksCommand.cs` + `DispatchDueWebhooksHandler.cs`
   - `DispatchDueOutboxCommand.cs` + `DispatchDueOutboxHandler.cs`
2. `Application/Abstractions/IWebhookDeliveryRepository.cs` — interface for claiming due
   deliveries, marking delivered, and marking failed-attempt for webhook deliveries.
3. `Application/Abstractions/IWebhookDeliverySender.cs` — promote the existing local interface
   for HTTP dispatch; Infrastructure wires `HttpWebhookDeliverySender`.
4. `Application/Abstractions/IOutboxMessageRepository.cs` — extend the existing interface
   (already in Application/Abstractions) with claim-batch, mark-sent, and mark-failed-attempt
   methods if not already present.
5. `Application/Abstractions/IOutboxMessageHandler.cs` — interface for per-message-type dispatch
   (type discriminator + handle method); Infrastructure registers concrete handlers.
6. Extend Infrastructure/Repositories with `WebhookDeliveryRepository` implementation; ensure
   `OutboxMessageRepository` implements the new claim/mark methods; register in
   ServiceCollectionExtensions.cs.
7. `ServiceCollectionExtensions.cs` — register `DispatchDueWebhooksHandler`,
   `DispatchDueOutboxHandler`, repositories, `HttpWebhookDeliverySender`, and concrete outbox
   message handlers with `AddScoped` / `AddTransient` as appropriate.
8. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs` — cover
   both handlers: successful dispatch, failed-attempt back-off, concurrent-claim idempotency (two
   handlers claim same batch); use fakes for `IWebhookDeliverySender` and
   `IOutboxMessageHandler`; SQLite in-memory for claim/mark operations.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~WebhookOutboxUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/WebhookOutbox/DispatchDueWebhooksHandler.cs`

## DO NOT
- Do NOT delete or edit WebhookDispatcherService, OutboxDispatcherService, or any other existing
  services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
