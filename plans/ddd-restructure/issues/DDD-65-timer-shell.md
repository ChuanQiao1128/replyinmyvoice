# DDD-65: Shell timer Functions onto Application handlers (strangler replace)

## Context
Per docs/ddd-migration-playbook.md, switch the five timer-triggered Azure Functions from calling
the old Infrastructure services to invoking the Wave-2 Application handlers in
Application/UseCases/WebhookOutbox, Application/UseCases/StripeEvent,
Application/UseCases/StripeReconciliation, and Application/UseCases/RewriteJob.
Behaviour is unchanged; existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/OutboxDispatcherTimerFunction.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PaymentGraceExpiryFunction.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeReconciliationTimerFunction.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/WebhookDispatcherTimerFunction.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs`
- Application/UseCases/WebhookOutbox/DispatchDueOutboxCommand.cs + DispatchDueOutboxHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/WebhookOutbox/DispatchDueWebhooksCommand.cs + DispatchDueWebhooksHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/StripeEvent/ProcessExpiredPaymentGraceCommand.cs + handler
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/StripeEvent/ProcessPaymentGraceRemindersCommand.cs + handler
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/StripeReconciliation/ReconcileStripeCommand.cs + ReconcileStripeHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/RewriteJob/ProcessRewriteJobCommand.cs + ProcessRewriteJobHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/OutboxDispatcherTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeReconciliationServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobProcessorTests.cs`

## Constraints
- Inject the Application handlers via DI (already registered in Wave 2). In each migrated method,
  build the Command and call the handler; remove inline service calls on that path.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP the old service classes (`OutboxDispatcherService`, `StripeEventService`,
  `StripeReconciliationService`, `RewriteJobProcessor`, `WebhookDispatcherService`) and their DI
  registration. Remove a constructor param only if no remaining method in the file uses it.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required

### OutboxDispatcherTimerFunction.cs
Current constructor: `OutboxDispatcherService dispatcher, ILogger<OutboxDispatcherTimerFunction> logger`

1. `Run` (timer `*/15 * * * * *`): replace
   `dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, Environment.MachineName, batchSize: 10, ...)` with
   `DispatchDueOutboxHandler` called with
   `new DispatchDueOutboxCommand(DateTimeOffset.UtcNow, Environment.MachineName, BatchSize: 10)`.
2. Adjust constructor: add `DispatchDueOutboxHandler`; drop `OutboxDispatcherService` if fully unused.

### WebhookDispatcherTimerFunction.cs
Current constructor: `WebhookDispatcherService dispatcher, ILogger<WebhookDispatcherTimerFunction> logger`

1. `Run` (timer `*/30 * * * * *`): replace
   `dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, Environment.MachineName, batchSize: 10, ...)` with
   `DispatchDueWebhooksHandler` called with
   `new DispatchDueWebhooksCommand(DateTimeOffset.UtcNow, Environment.MachineName, BatchSize: 10)`.
2. Adjust constructor: add `DispatchDueWebhooksHandler`; drop `WebhookDispatcherService` if fully unused.

### PaymentGraceExpiryFunction.cs
Current constructor: `StripeEventService stripeEvents, ILogger<PaymentGraceExpiryFunction> logger`

1. `Run` (timer `0 0 14 * * *`): replace
   `stripeEvents.ProcessPaymentGraceRemindersAsync(now, ...)` with
   `ProcessPaymentGraceRemindersHandler` called with
   `new ProcessPaymentGraceRemindersCommand(now)`.
   Replace `stripeEvents.ProcessExpiredPaymentGraceAsync(now, ...)` with
   `ProcessExpiredPaymentGraceHandler` called with
   `new ProcessExpiredPaymentGraceCommand(now)`.
2. Adjust constructor: add `ProcessPaymentGraceRemindersHandler`, `ProcessExpiredPaymentGraceHandler`;
   drop `StripeEventService` if fully unused.

### StripeReconciliationTimerFunction.cs
Current constructor: `StripeReconciliationService reconciliation, ILogger<StripeReconciliationTimerFunction> logger`

1. `Run` (timer `0 15 2 * * *`): replace
   `reconciliation.ReconcileAsync(windowStart, windowEnd, completedAt, ...)` with
   `ReconcileStripeHandler` called with
   `new ReconcileStripeCommand(windowStart, windowEnd, completedAt)`.
   Map the handler result back to the existing log statement (discrepancy count, window dates).
2. Adjust constructor: add `ReconcileStripeHandler`; drop `StripeReconciliationService` if fully unused.

### RewriteJobFunction.cs
Current constructor: `RewriteJobProcessor processor, ILogger<RewriteJobFunction> logger`

1. `Run` (ServiceBus trigger): the JSON deserialization and validation of `RewriteJob` is kept
   unchanged. Replace `processor.ProcessAsync(job, cancellationToken)` with
   `ProcessRewriteJobHandler` called with `new ProcessRewriteJobCommand(job.AttemptId)`.
2. Adjust constructor: add `ProcessRewriteJobHandler`; drop `RewriteJobProcessor` if fully unused.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~OutboxDispatcherTests|FullyQualifiedName~StripeEventServiceTests|FullyQualifiedName~StripeReconciliationServiceTests|FullyQualifiedName~RewriteJobProcessorTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
