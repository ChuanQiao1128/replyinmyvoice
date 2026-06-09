# DDD-48: Migrate StripeEvent use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the StripeEvent use-cases from
Infrastructure/Services/StripeEventService.cs into Application handlers that depend on repository
interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP the old
service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

StripeEventService.cs (1388L) exposes four public use-cases:
- `TryMarkProcessedAsync` — idempotency-guard: attempt to lock a Stripe event row for processing;
  returns bool (false = already processed / in-flight)
- `ProcessWebhookEventAsync` — main webhook dispatch: route a Stripe event to the correct internal
  handler (checkout, subscription, invoice, entitlement, refund, dispute) within a DB transaction
  with post-commit side-effects
- `ProcessExpiredPaymentGraceAsync` — batch job: find users in payment-grace whose grace window
  expired, cancel their subscription access, enqueue notifications
- `ProcessPaymentGraceRemindersAsync` — batch job: find users in grace who need a reminder
  notification and enqueue them

StripeEventService.cs is 1388L. It is acceptable to migrate the PRIMARY use-cases first:
`TryMarkProcessedAsync` and `ProcessWebhookEventAsync` (the webhook dispatch path). Leave a
`// TODO(DDD): remaining StripeEventService use-cases (ProcessExpiredPaymentGrace, ProcessPaymentGraceReminders)`
note if the full migration is too large for one Codex pass — never break the build; the old
service remains the fallback.

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old StripeEventService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests stay green, assertions unchanged.
- Stripe SDK types used for webhook parsing (e.g. `Stripe.Event`) are Infrastructure concerns;
  the Application command should carry only the parsed, already-validated payload as primitive /
  domain types — define a `StripeWebhookPayload` value type in Application/Common if needed.
- Post-commit side-effects (notification enqueue, subscription cancellation enqueue) are
  Infrastructure concerns; define narrow `IStripeEventNotifier` and
  `IStripeSubscriptionCancellationService` interfaces in Application/Abstractions.
- The batch handlers (`ProcessExpiredPaymentGrace`, `ProcessPaymentGraceReminders`) must preserve
  the same batch-size loop to avoid large locks.
- `TryMarkProcessedAsync` uses optimistic DB locking — preserve this via IUnitOfWork.

## Changes required
1. `Application/UseCases/StripeEvent/*.cs` — command/query + handler per use-case:
   - `TryMarkStripeEventProcessedCommand.cs` + `TryMarkStripeEventProcessedHandler.cs`
   - `ProcessStripeWebhookCommand.cs` + `ProcessStripeWebhookHandler.cs`
   - `ProcessExpiredPaymentGraceCommand.cs` + `ProcessExpiredPaymentGraceHandler.cs` (may be TODO stub)
   - `ProcessPaymentGraceRemindersCommand.cs` + `ProcessPaymentGraceRemindersHandler.cs` (may be TODO stub)
2. `Application/Abstractions/IStripeEventRepository.cs` — interface for event idempotency lock,
   begin-processing, mark-processed, and mark-failed operations.
3. `Application/Abstractions/IStripeEventNotifier.cs` — interface for enqueuing failed-payment,
   subscription-paused, grace-reminder, and payment-recovered notifications.
4. `Application/Abstractions/IStripeSubscriptionCancellationService.cs` — interface for the
   enqueue-cancel-subscription side-effect (if not already defined in DDD-40).
5. Extend `IAppUserRepository` and `IRewriteCreditRepository` in Application/Abstractions if
   new mutation methods are needed (e.g. sync entitlement, revoke credits on refund).
6. Extend Infrastructure/Repositories with matching implementations; register in
   ServiceCollectionExtensions.cs.
7. `Application/Common/*.cs` — `StripeWebhookPayloadDto` carrying event type + parsed object data
   passed to `ProcessStripeWebhookCommand`.
8. `ServiceCollectionExtensions.cs` — register all new handlers, repositories, and notifier
   services with `AddScoped`.
9. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeEventUseCaseTests.cs` — cover
   `TryMarkProcessed` (idempotency), `ProcessWebhookEvent` (checkout / invoice / entitlement paths),
   and at least one batch handler; use fakes for `IStripeEventNotifier`; SQLite in-memory for DB.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~StripeEventUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeEvent/ProcessStripeWebhookHandler.cs`

## DO NOT
- Do NOT delete or edit StripeEventService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
