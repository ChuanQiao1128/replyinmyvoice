# DDD-66: Shell Worker BackgroundServices onto Application handlers (strangler replace)

## Context
Per docs/ddd-migration-playbook.md, switch the three `BackgroundService` workers in
`ReplyInMyVoice.Worker` from resolving and calling old Infrastructure services directly to
invoking the Wave-2 Application handlers for RewriteJob, WebhookOutbox, and Quota.
Behaviour is unchanged; existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker.cs`
- `backend-dotnet/src/ReplyInMyVoice.Worker/OutboxDispatcherWorker.cs`
- `backend-dotnet/src/ReplyInMyVoice.Worker/ExpiredReservationCleanupWorker.cs`
- Application/UseCases/RewriteJob/ProcessRewriteJobCommand.cs + ProcessRewriteJobHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/WebhookOutbox/DispatchDueOutboxCommand.cs + DispatchDueOutboxHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Quota/ReleaseExpiredReservationsCommand.cs + ReleaseExpiredReservationsHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobProcessorTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/OutboxDispatcherTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`

## Constraints
- Inject Application handlers via DI. Because these are `BackgroundService` workers they resolve
  services per-loop-tick via `IServiceScopeFactory`. Replace the per-scope `GetRequiredService<OldService>()`
  call with `GetRequiredService<RelevantHandler>()` and build the Command inside the scope.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP the old service classes (`RewriteJobProcessor`, `OutboxDispatcherService`,
  `ExpiredReservationCleanupService`) and their DI registration. The service `ReplyInMyVoice.Worker`
  project also registers infrastructure — do not touch the DI registrations for the old services
  unless the brief explicitly says so.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required

### ServiceBusRewriteWorker.cs
Current: resolves `RewriteJobProcessor processor` from scope and calls `processor.ProcessAsync(job, ct)`.

1. In `ProcessMessageAsync`: replace
   `var processor = scope.ServiceProvider.GetRequiredService<RewriteJobProcessor>();`
   `await processor.ProcessAsync(job, args.CancellationToken);`
   with
   `var handler = scope.ServiceProvider.GetRequiredService<ProcessRewriteJobHandler>();`
   `await handler.HandleAsync(new ProcessRewriteJobCommand(job.AttemptId), args.CancellationToken);`
   (verify the exact `HandleAsync` / `ExecuteAsync` method name from the handler source on
   `origin/delivery/ddd-restructure` before writing).
2. The `RewriteAttemptNotFoundException` catch is kept — verify whether the handler wraps or
   re-throws that exception type unchanged.
3. Constructor and outer `ExecuteAsync` boilerplate are unchanged.

### OutboxDispatcherWorker.cs
Current: resolves `OutboxDispatcherService dispatcher` from scope and calls
`dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, _instanceId, batchSize: 25, ...)`.

1. In the `while` loop body: replace
   `var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcherService>();`
   `await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, _instanceId, batchSize: 25, ...);`
   with
   `var handler = scope.ServiceProvider.GetRequiredService<DispatchDueOutboxHandler>();`
   `await handler.HandleAsync(new DispatchDueOutboxCommand(DateTimeOffset.UtcNow, _instanceId, BatchSize: 25), stoppingToken);`
2. Constructor and loop/timer scaffolding are unchanged.

### ExpiredReservationCleanupWorker.cs
Current: resolves `ExpiredReservationCleanupService cleanup` from scope and calls
`cleanup.RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken)`.

1. In the `while` loop body: replace
   `var cleanup = scope.ServiceProvider.GetRequiredService<ExpiredReservationCleanupService>();`
   `var released = await cleanup.RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken);`
   with
   `var handler = scope.ServiceProvider.GetRequiredService<ReleaseExpiredReservationsHandler>();`
   `var released = await handler.HandleAsync(new ReleaseExpiredReservationsCommand(DateTimeOffset.UtcNow), stoppingToken);`
   Map the handler return value to the same `released` count used in the existing log statement.
2. Constructor and loop/timer scaffolding are unchanged.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~RewriteJobProcessorTests|FullyQualifiedName~OutboxDispatcherTests|FullyQualifiedName~QuotaServiceTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
