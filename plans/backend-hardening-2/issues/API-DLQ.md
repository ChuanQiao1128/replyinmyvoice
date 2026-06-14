## Context

- Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: `backend-dotnet` (.NET 8, Azure Functions isolated worker). Base branch = `delivery/backend-hardening-2` (NEVER `main`).
- The **deployed** Service Bus consumer is `RewriteJobFunction` — `.github/workflows/dotnet-azure.yml` `deploy` job publishes only `ReplyInMyVoice.Functions.csproj`. `ReplyInMyVoice.Worker` is built but NOT deployed, so its dead-letter logic does not protect prod.
- Read first:
  - `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs` — current consumer: binds `string messageBody`; `catch (JsonException) { ...; throw; }` at line 24-28; `throw new InvalidOperationException(...)` for null job / empty `AttemptId` at line 30-33. No `ServiceBusMessageActions`, no dead-letter.
  - `backend-dotnet/src/ReplyInMyVoice.Functions/host.json` — `serviceBus` extension has NO `maxDeliveryCount` and NO `autoCompleteMessages`.
  - `backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker.cs:69-111` — the parity reference: dead-letters with reason codes `invalid_json`, `invalid_job`, `attempt_not_found`, and rethrows transient failures.
  - `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/RewriteJob/ProcessRewriteJobHandler.cs:11-26,49` — `RewriteJobAttemptNotFoundException` is the unprocessable-attempt signal.
  - `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteJobUseCaseTests.cs` — test/fake style (hand-written fakes; project uses xUnit + FluentAssertions, NO Moq).
- SDK facts (already on disk): `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` 5.24.0 supports binding `ServiceBusReceivedMessage message` + `ServiceBusMessageActions messageActions` in the same trigger. `ServiceBusMessageActions` is an abstract class whose `DeadLetterMessageAsync`/`CompleteMessageAsync` are overridable virtuals — subclass it for a test double. `Azure.Messaging.ServiceBus.ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(...))` constructs a test message. Test project already references `ReplyInMyVoice.Functions`.

## Constraints

- Banned substrings anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient`, the `RewriteJob` record, `RewriteEngineResult`, or `ProcessRewriteJobHandler` (frozen). Keep deserializing `RewriteJob` exactly as today so an added `CorrelationId` field later does not break this path.
- No secrets / connection strings in tracked files. Keep existing `host.json` values (`maxConcurrentCalls`, `maxAutoLockRenewalDuration`) unchanged.
- Keep the existing 799 tests green; add tests. Touch at most the 3 files in scope.
- Base branch is `delivery/backend-hardening-2`. The worker must NEVER push, open a PR, or touch `main`.

## Changes required

1. `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs` — change the `Run` trigger to bind the message + actions instead of a raw string:
   - Signature: `[ServiceBusTrigger("%SERVICEBUS_QUEUE_NAME%", Connection = "ServiceBus")] ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, CancellationToken cancellationToken`.
   - Deserialize `RewriteJob` from `message.Body` (a `BinaryData`; use `message.Body.ToString()` or `JsonSerializer.Deserialize<RewriteJob>(message.Body)`). On `JsonException`: `await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "invalid_json", deadLetterErrorDescription: ex.Message, cancellationToken)` and return (do NOT rethrow).
   - If `job is null` → dead-letter reason `invalid_job`, description "Message body did not contain a rewrite job." If `job.AttemptId == Guid.Empty` → dead-letter reason `invalid_job`, description "Message body did not contain a valid attempt id." Return after dead-lettering.
   - Call `processRewriteJobHandler.HandleAsync(new ProcessRewriteJobCommand(job.AttemptId), cancellationToken)` inside try/catch:
     - `catch (RewriteJobAttemptNotFoundException ex)` → log warning, `await messageActions.DeadLetterMessageAsync(message, "attempt_not_found", ex.Message, cancellationToken)` and return.
     - On success, `await messageActions.CompleteMessageAsync(message, cancellationToken)`.
     - `catch (Exception ex)` (transient) → log error and **rethrow** (preserve redelivery). Do not dead-letter transient failures here; `maxDeliveryCount` governs eventual DLQ.
   - Keep the existing `using ReplyInMyVoice.Application.UseCases.RewriteJob;`/`Domain.Contracts` usings; add `using Azure.Messaging.ServiceBus;` (the type lives in the Azure.Messaging.ServiceBus assembly, transitively referenced via the extension package — if the type does not resolve, add a `PackageReference` to `Azure.Messaging.ServiceBus` matching the version already restored, but prefer no new package if it resolves).
2. `backend-dotnet/src/ReplyInMyVoice.Functions/host.json` — under `extensions.serviceBus` add `"maxDeliveryCount": 5` and `"autoCompleteMessages": false` (so the explicit `CompleteMessageAsync`/`DeadLetterMessageAsync` calls are authoritative). Leave `maxAutoLockRenewalDuration` and `maxConcurrentCalls` unchanged. Valid JSON only.
3. `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobFunctionTests.cs` (new) — `public sealed class RewriteJobFunctionTests` in namespace `ReplyInMyVoice.Tests`:
   - Add a `private sealed class RecordingMessageActions : ServiceBusMessageActions` test double overriding `DeadLetterMessageAsync(...)` and `CompleteMessageAsync(...)` to record the reason/description and call count (return `Task.CompletedTask`).
   - Build messages with `ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(...))`.
   - Use a small fake `ProcessRewriteJobHandler` seam — since `RewriteJobFunction` depends on the concrete `ProcessRewriteJobHandler`, construct a real handler over in-memory fakes (mirror `RewriteJobUseCaseTests` `CreateHandler`/`DbFixture`) OR, simpler, drive only the pre-handler branches that don't need the handler (invalid JSON / null job / empty AttemptId) with the real handler never reached. For the `attempt_not_found` case, use a `DbFixture` with no attempt so the handler throws `RewriteJobAttemptNotFoundException`. For the transient case, inject a fake engine that throws a generic exception after a valid attempt is reserved and assert the function rethrows.
   - Tests (xUnit `[Fact]`, FluentAssertions):
     - `Run_deadletters_when_body_is_not_valid_json` → reason `invalid_json`, no throw.
     - `Run_deadletters_when_attempt_id_is_empty` → reason `invalid_job`.
     - `Run_deadletters_when_attempt_is_missing` → reason `attempt_not_found`.
     - `Run_rethrows_on_transient_handler_failure` → `await action.Should().ThrowAsync<Exception>()` and assert DeadLetter was NOT called.
     - `Run_completes_message_on_success` → CompleteMessage called once, no dead-letter.

## Acceptance

- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteJobFunctionTests` passes (all new facts green).
- `cd backend-dotnet && python3 -c "import json;c=json.load(open('src/ReplyInMyVoice.Functions/host.json'));sb=c['extensions']['serviceBus'];assert isinstance(sb.get('maxDeliveryCount'),int) and sb['maxDeliveryCount']>0, sb;assert sb.get('autoCompleteMessages') is False, sb;print('host.json OK', sb['maxDeliveryCount'])"` prints `host.json OK <n>`.
- `cd backend-dotnet && grep -n "DeadLetterMessageAsync" src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs` shows at least the three reason paths (`invalid_json`, `invalid_job`, `attempt_not_found`).
- `cd backend-dotnet && ! grep -RniE "humanizer|bypass|undetect|detector|evade" src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs src/ReplyInMyVoice.Functions/host.json tests/ReplyInMyVoice.Tests/RewriteJobFunctionTests.cs` exits 0 (no matches).
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` stays green (799 baseline + new tests, 0 failures).

## DO NOT

- Do NOT modify `ReplyInMyVoice.Worker/ServiceBusRewriteWorker.cs`, `IRewriteEngineClient`, the `RewriteJob` record, or `ProcessRewriteJobHandler`.
- Do NOT dead-letter transient handler failures (anything other than `JsonException`, null/empty job, or `RewriteJobAttemptNotFoundException`) — rethrow so Service Bus redelivers; `maxDeliveryCount` is the backstop.
- Do NOT change `maxConcurrentCalls` or `maxAutoLockRenewalDuration` in `host.json`, and do NOT add secrets/connection strings to any tracked file.
- Do NOT introduce banned substrings (`humanizer|bypass|undetect|detector|evade`).
- Do NOT push, open a PR, merge, or touch `main`. Work only on `delivery/backend-hardening-2`. Stay within the 3 scoped files.