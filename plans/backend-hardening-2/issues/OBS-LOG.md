## Context

Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: .NET 8 / Azure Functions under `backend-dotnet`. Wave spec: `plans/backend-hardening-2/SPEC.md` → issue **OBS-LOG** (finding #22). 799 tests currently pass; this change must keep them green and ADD tests.

Read these first (anchors verified):
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Quota/ReserveQuotaHandler.cs` — primary-ctor DI (L10-16); outcome sites: `Created` L141-144, `Existing` L53-58, `Conflict` L50, `QuotaExceeded` L92.
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Quota/FinalizeQuotaSuccessHandler.cs` — ctor L8-13; claimed transition L38-42 (returns `true` L59), no-op returns L29/L36 (`false`).
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Quota/ReleaseQuotaHandler.cs` — ctor L8-14; claimed L25-30, credit-release L34 vs slot-release L38, attempt→Failed L43-49.
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Quota/MarkQuotaProcessingHandler.cs` — ctor L8-11; transition L25 (`true`), not-pending no-op L20-23 (`false`).
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Quota/ReleaseExpiredReservationsHandler.cs` — ctor L7-10; per-reservation reason `processing_timed_out`/`reservation_expired` L36-38, claim L40-44, batch result L65.
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeEvent/IngestStripeWebhookHandler.cs` — ctor L8-11; results `Accepted` L35, `AlreadyProcessed` L40, `AlreadyPending` L46/56/63. `IngestStripeWebhookCommand` carries `RawBody` — NEVER log it.
- **Pattern to mirror exactly**: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeEvent/ProcessPendingStripeEventsHandler.cs` — `ILogger<T>` injected in primary ctor (L16), structured `{EventName}` first token (L124-130, L297-303).
- Test factories to update: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/QuotaUseCaseTests.cs` L432-462 (5 `Create*Handler` helpers); `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeEventUseCaseTests.cs` L1841 (`new IngestStripeWebhookHandler(...)`).

## Constraints

- Branch `delivery/backend-hardening-2` only. NEVER push, open a PR, or touch `main`.
- Banned substrings anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient` / `ResultJson` / engine error-code set (frozen black box).
- Secrets hygiene: never pass `RawBody`, `RequestJson`, `ResultJson`, `PayloadJson`, `RequestHash`, or `IdempotencyKey` values into any log call. Log only ids (attempt id, reservation id, user id, event id), enum statuses, result kinds, reason codes, and batch counts.
- Add NO new NuGet package. `Microsoft.Extensions.Logging.Abstractions` is already on `ReplyInMyVoice.Application.csproj`; the test project gets it transitively. Use `NullLogger<T>.Instance` and a tiny in-test recording `ILogger<T>`.
- Keep DI working: handlers are registered via `AddScoped<T>()` (`ServiceCollectionExtensions.cs:155-159,190`); the container auto-resolves the added `ILogger<T>` — do not change those lines unless the build forces it.

## Changes required

1. In each of the 6 handlers, add a last primary-ctor parameter `ILogger<HandlerType> logger` (mirroring `ProcessPendingStripeEventsHandler` L16) and `using Microsoft.Extensions.Logging;`.
2. Emit exactly one structured log line per terminal outcome, first token a stable `{QuotaLifecycleEvent}` string constant (snake_case), e.g.:
   - `ReserveQuotaHandler`: `quota_reserved` (Created, include AttemptId), `quota_reserve_existing`, `quota_reserve_conflict` (Warning), `quota_exhausted` (Warning).
   - `MarkQuotaProcessingHandler`: `quota_marked_processing` on transition; `quota_mark_processing_skipped` (Debug/Information) when not Pending.
   - `FinalizeQuotaSuccessHandler`: `quota_finalized` when claimed; `quota_finalize_noop` when ineligible/already-finalized — include AttemptId + reservation id.
   - `ReleaseQuotaHandler`: `quota_released` (include whether credit vs slot path + ErrorCode).
   - `ReleaseExpiredReservationsHandler`: per-reservation `quota_reservation_expired` (Information, include reservation id + reason) and a batch-summary line with `releasedCount`.
   - `IngestStripeWebhookHandler`: `stripe_webhook_ingested` with `EventId`, `Type`, and the `StripeWebhookIngestResult` — never `RawBody`.
   Use Information for normal transitions, Warning for conflict/exhausted/duplicate-ingest, Debug for benign no-ops. Use message templates with named placeholders ONLY (no string interpolation of secret fields).
3. Update the 5 test factories in `QuotaUseCaseTests.cs` (L432-462) to pass `NullLogger<HandlerType>.Instance` (or the recording logger in the new tests). Update `StripeEventUseCaseTests.cs:1841` `new IngestStripeWebhookHandler(...)` to pass `NullLogger<IngestStripeWebhookHandler>.Instance`.
4. Add a private recording `ILogger<T>` test double inside `QuotaUseCaseTests.cs` (captures `(LogLevel, EventName, formatted message)`); add `[Fact]` tests asserting: (a) a successful reserve logs `quota_reserved` at Information with the attempt id; (b) a quota-exceeded reserve logs `quota_exhausted` at Warning; (c) finalize logs `quota_finalized` on the happy path; (d) NONE of the captured messages contain the draft text `roughDraftReply` / request-json substring. Keep these in the existing `QuotaUseCaseTests` class so the `--filter ~QuotaUseCaseTests` run covers them.

## Acceptance

- `cd backend-dotnet && grep -lE "ILogger<ReserveQuotaHandler>|ILogger<FinalizeQuotaSuccessHandler>|ILogger<ReleaseQuotaHandler>|ILogger<MarkQuotaProcessingHandler>|ILogger<ReleaseExpiredReservationsHandler>|ILogger<IngestStripeWebhookHandler>" src/ReplyInMyVoice.Application/UseCases/Quota/ReserveQuotaHandler.cs src/ReplyInMyVoice.Application/UseCases/Quota/FinalizeQuotaSuccessHandler.cs src/ReplyInMyVoice.Application/UseCases/Quota/ReleaseQuotaHandler.cs src/ReplyInMyVoice.Application/UseCases/Quota/MarkQuotaProcessingHandler.cs src/ReplyInMyVoice.Application/UseCases/Quota/ReleaseExpiredReservationsHandler.cs src/ReplyInMyVoice.Application/UseCases/StripeEvent/IngestStripeWebhookHandler.cs | wc -l | grep -qx 6`
- `cd backend-dotnet && grep -RniE "humanizer|bypass|undetect|detector|evade" src/ReplyInMyVoice.Application/UseCases/Quota src/ReplyInMyVoice.Application/UseCases/StripeEvent/IngestStripeWebhookHandler.cs tests/ReplyInMyVoice.Tests/Application/QuotaUseCaseTests.cs; test $? -eq 1`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~QuotaUseCaseTests"`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~StripeEventUseCaseTests"`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`

## DO NOT

- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the engine error-code set.
- Do NOT add a NuGet package; no `Microsoft.Extensions.Diagnostics.Testing`.
- Do NOT log `RawBody`, `RequestJson`, `ResultJson`, `PayloadJson`, `RequestHash`, or `IdempotencyKey` values.
- Do NOT add correlation-id / traceparent plumbing (that is OBS-CORR) or change any handler's return type or transaction logic.
- Do NOT introduce banned substrings anywhere.
- Do NOT push, open a PR, or touch `main`. Integration branch `delivery/backend-hardening-2` only.