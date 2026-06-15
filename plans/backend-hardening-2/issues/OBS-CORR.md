## Context

Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: `.NET 8` / Azure Functions under `backend-dotnet`. Wave spec: `plans/backend-hardening-2/SPEC.md` → section **OBS-CORR** (finding #20). Base branch is `delivery/backend-hardening-2` (NEVER `main`).

Goal: a correlation id chosen at HTTP ingress must survive `HTTP → outbox → Azure Service Bus → worker` so the worker's logs can be tied back to the originating request.

Read these first (anchors are real):
- `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteJob.cs:3` — `record RewriteJob(Guid AttemptId)`, no correlation/traceparent.
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobCreatedOutboxMessageHandler.cs:19-29` — builds `new RewriteJob(payload.AttemptId)` and DROPS `message.CorrelationId`.
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/CreateRewriteAttemptHandler.cs:201-214` — `CreateRewriteJobOutboxMessage` already sets `CorrelationId = attemptId.ToString()`; the inbound request id never reaches it.
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/CreateRewriteAttemptCommand.cs:5-12` — no correlation field.
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Queueing/AzureServiceBusRewriteJobPublisher.cs:9-18` — sets `MessageId`/`Subject` only; no `CorrelationId`/`ApplicationProperties`. Body = `JsonSerializer.Serialize(job)`.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs:19-38` and `backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker.cs:69-111` — both deserialize the WHOLE body into `RewriteJob`; no `Activity`/log scope.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Http/HttpHardeningMiddleware.cs:14,31-33,48-57` — already resolves `X-Correlation-Id` (1–64 chars, `[A-Za-z0-9._-]`, else new GUID) into `http.Items["CorrelationId"]`.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs:205-214` — builds `CreateRewriteAttemptCommand` without the correlation id.
- Existing round-trip test to extend: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs:906-907` (`InMemoryRewriteJobPublisher.PublishedJobs`).

## Constraints

- Base = `delivery/backend-hardening-2`. Keep all 799 existing tests green; add tests for every behavioral change. Test base: `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` (add `--filter FullyQualifiedName~<Class>`).
- Do NOT change `IRewriteEngineClient` / `ResultJson` / engine error-code set (frozen black box).
- Do NOT add an EF migration — the outbox `CorrelationId` column already exists (`OutboxMessage.cs:20`).
- Do NOT change the Functions `[ServiceBusTrigger]` binding signature (`string messageBody`). Correlation must ride in the JSON body so it round-trips through the in-memory publisher too.
- Banned substrings anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- No secret values in tracked files; validate env at runtime in the handler, not at import.
- Worker must NEVER push, open a PR, or touch `main`.

## Changes required

1. `RewriteJob.cs`: extend to `public sealed record RewriteJob(Guid AttemptId, string? CorrelationId = null, string? Traceparent = null);`. Defaulted params keep existing positional construction (`new RewriteJob(attemptId)`) compiling.
2. `CreateRewriteAttemptCommand.cs`: add a `string? CorrelationId = null` parameter (append, defaulted, to avoid breaking other callers).
3. `CreateRewriteAttemptHandler.cs`: in `CreateRewriteJobOutboxMessage`, prefer `command.CorrelationId` when present, else keep the existing `attemptId.ToString()` default. Thread `command.CorrelationId` into that call.
4. `V1RewriteHttpFunctions.cs` (~line 205): read the ingress correlation id from the `HttpContext` (the value middleware stored at `http.Items["CorrelationId"]`, header name `HttpHardeningMiddleware.CorrelationHeaderName`) and pass it as `CreateRewriteAttemptCommand.CorrelationId`. If the host context is unavailable, pass `null` (handler falls back to the attempt-id default).
5. `RewriteJobCreatedOutboxMessageHandler.cs:29`: stop dropping the id — construct `new RewriteJob(payload.AttemptId, message.CorrelationId)` (optionally generate a fresh W3C `traceparent` for the job here and pass it as the 3rd arg).
6. `AzureServiceBusRewriteJobPublisher.cs`: also set `message.CorrelationId = job.CorrelationId` (when non-empty) and `message.ApplicationProperties["traceparent"] = job.Traceparent` (when non-empty) as belt-and-suspenders SB-level metadata. The body already carries them; do not remove the body serialization.
7. `RewriteJobFunction.cs` and `ServiceBusRewriteWorker.cs`: after deserializing the job, open an `ILogger.BeginScope` with `{ ["CorrelationId"] = job.CorrelationId, ["AttemptId"] = job.AttemptId }` (and, if `job.Traceparent` parses, start/restore an `Activity` from it) around the `ProcessRewriteJobHandler.HandleAsync(...)` call so the existing log lines carry the id. Use `using var scope = logger.BeginScope(...)`.
8. New test `tests/ReplyInMyVoice.Tests/Application/CorrelationIdPropagationTests.cs` (namespace `ReplyInMyVoice.Tests.Application`):
   - Outbox-handler test: build an `OutboxMessage` with a known `CorrelationId` + a `RewriteJobCreated` payload, run the real `RewriteJobCreatedOutboxMessageHandler` against an `InMemoryRewriteJobPublisher`, assert `publisher.PublishedJobs.Single().CorrelationId` equals the input.
   - End-to-end ingress test (extend the pattern at `RewriteApiTests.cs:906-907`, or add here using the same `InMemoryRewriteJobPublisher`): POST a V1 rewrite with header `X-Correlation-Id: <known>`; assert the published `RewriteJob.CorrelationId == <known>` for that `AttemptId`.
   - Consumer scope test: assert the worker/function opens a log scope containing the correlation id for the job's `AttemptId` (use a captured-scope `ILogger` test double, e.g. extend the existing fakes in `tests/ReplyInMyVoice.Tests/Application/RewriteJobUseCaseTests.cs`).

## Acceptance

cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~CorrelationIdPropagationTests
cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteApiTests
cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release
grep -RniE "humanizer|bypass|undetect|detector|evade" backend-dotnet/src backend-dotnet/tests || true

## DO NOT

- Do NOT modify `IRewriteEngineClient`, `ResultJson`, or the engine error-code set.
- Do NOT add an EF migration or change `OutboxMessage` columns (the `CorrelationId` column already exists).
- Do NOT change the `[ServiceBusTrigger]` binding to `ServiceBusReceivedMessage`/`ServiceBusMessageActions`; keep `string messageBody`.
- Do NOT remove the body-level serialization of the job (the SB-level `CorrelationId`/`ApplicationProperties` are additive, not a replacement) — tests rely on the in-memory publisher round-trip.
- Do NOT log secrets or request bodies in the new scope; only `CorrelationId`, `AttemptId`, `Traceparent`.
- Do NOT introduce banned substrings (`humanizer|bypass|undetect|detector|evade`).
- Do NOT push, open a PR, merge, or touch `main`; work only on `delivery/backend-hardening-2`.