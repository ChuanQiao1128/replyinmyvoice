# Rewrite Engine Contract

## 1. Boundary

The rewrite engine is a black-box dependency behind
`backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IRewriteEngineClient.cs`.
An engine swap may touch only these boundary files:

- `IRewriteEngineClient` implementations.
- Optional `IRewriteProvider` implementations behind
  `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/RewriteProviderEngineClient.cs`.
- The DI block in
  `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`.
- Startup validation in `ValidateReplyInMyVoiceRuntimeConfiguration`.

Startup validation must name required environment variables only. Never write secret values into
source, docs, tests, logs, or prompts.

## 2. Request DTO

`ReplyInMyVoice.Domain.Contracts.RewriteRequest` is the request contract shared by the frontend
proxies, application job handler, and engine boundary. It has seven fields:

- `MessageToReplyTo`
- `RoughDraftReply`
- `Audience`
- `Purpose`
- `WhatHappened`
- `FactsToPreserve`
- `Tone`

Do not extend this DTO without reviewing the frontend proxies and any client contract that builds
rewrite requests.

## 3. Success ResultJson

On success, `RewriteEngineResult.Success` must be `true` and `ResultJson` must be a JSON object
with these fields:

| Field | Required | Consumer |
| --- | --- | --- |
| `rewrittenText` | Non-empty string | `ProcessRewriteJobHandler` validates it before finalizing quota; v1 and webhooks return it. |
| `changeSummary` | JSON array | `ProcessRewriteJobHandler` validates it; UI normalization tolerates an empty array. |
| `riskNotes` | JSON array | `ProcessRewriteJobHandler` validates it; UI normalization tolerates an empty array. |
| `naturalness.draftAiLikePercent` | JSON number, 0-100 | v1 and webhook result bodies require it. |
| `naturalness.rewriteAiLikePercent` | JSON number, 0-100 | v1 and webhook result bodies require it. |

Optional pass-through metadata:

| Field | Notes |
| --- | --- |
| `naturalness.changePoints` | UI and cost logger tolerate it being absent. |
| `naturalness.label` | Lowercase value such as `lower`, `low_signal`, or `still_high`; missing or unknown becomes `unavailable` in `lib/rewrite-response.ts`. |
| `optimization.*` | Optional analytics metadata. Cost logging defaults missing strategy and scenario to `unknown`. |
| Extra fields | Consumers must tolerate additive metadata. |

If the handler sees success JSON without `rewrittenText`, `changeSummary`, or `riskNotes`, it
releases quota with `provider_json_parse_failed`. If v1 or webhook mapping sees a succeeded
attempt without the two `naturalness` numbers, it reports the result as failed with
`engine_unavailable`.

## 4. Error codes

Failure is `RewriteEngineResult.Success=false` plus an `ErrorCode`. The error-code set is open:
consumers must tolerate unknown values and either pass them through or fall back to
`engine_unavailable`.

`ReplyInMyVoice.Domain.Contracts.RewriteEngineErrorCodes` defines the canonical wire strings:

| Code | Meaning |
| --- | --- |
| `provider_timeout` | Adapter or job-handler timeout fallback. |
| `provider_failed` | Adapter or job-handler unexpected failure fallback. |
| `provider_json_parse_failed` | Success JSON missing handler-required fields. |
| `request_json_parse_failed` | Stored request JSON could not be parsed. |
| `reservation_expired` | Pending reservation expired before processing. |
| `processing_timed_out` | In-progress reservation expired in cleanup. |
| `quality_signal_unavailable` | Required writing-signal check was unavailable. |
| `naturalness_gate_failed` | Naturalness quality gate failed. |
| `fact_gate_failed` | Fact preservation gate failed. |
| `structure_gate_failed` | Send-ready structure gate failed. |
| `policy_intent_gate_failed` | Reserved quality gate; the current engine folds it into `fact_gate_failed`. |
| `rewrite_quality_failed` | Recommended terminal quality failure for engines. |
| `engine_unavailable` | Consumer fallback for missing or unknown failure details. |

`QualityGateNotCharged` is the exact five-code set mapped by both Next proxies to the not-charged
422 response:

- `quality_signal_unavailable`
- `structure_gate_failed`
- `naturalness_gate_failed`
- `fact_gate_failed`
- `policy_intent_gate_failed`

New quality codes require updating both proxy `qualityFailureCodes` sets,
`lib/rewrite-failure-reasons.ts`, and `RewriteEngineErrorCodes` in the same PR.

`EngineEmittable` is the canonical recommended set for new engine implementations, not a closed
public list. The current engine can pass through model-client codes such as `model_http_<status>`,
`model_timeout`, `model_empty`, `model_candidate_missing`, `model_json_parse_failed`,
`model_network_failed`, `model_not_configured`, and `rewrite_model_failed`; v1 and webhook bodies
surface those values verbatim when they are stored on the attempt.

## 5. ProviderCalls

`RewriteEngineResult.ProviderCalls` must be non-empty on success and failure. `RewriteCostLogger`
writes no row when `ProviderCalls` is empty.

`RewriteProviderCallCapture` is adapter-internal. It exists only for the current
`IRewriteProvider` path used by `RewriteProviderEngineClient`. Engines implementing
`IRewriteEngineClient` directly must populate `ProviderCalls` explicitly.

Writing-signal calls are currently not recorded in `ProviderCalls`; fixing that belongs in the
next engine implementation.

## 6. Swap-recipe

1. Implement `IRewriteEngineClient`, or implement `IRewriteProvider` behind
   `RewriteProviderEngineClient`.
2. Register the implementation in `ServiceCollectionExtensions.cs`.
3. Add required environment-variable names to `ValidateReplyInMyVoiceRuntimeConfiguration`.
4. Return success `ResultJson` with the required shape above.
5. Return expected failures as `Success=false` with an open-set `ErrorCode`.
6. Populate `ProviderCalls` for every completed engine call.
7. Make `RewriteEngineContractTests` and `tests/unit/rewrite-engine-contract.test.ts` pass.
8. Update the sandbox shape pins only if the shape intentionally changes:
   `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` and
   `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`.

## 7. Test pins

Contract-adjacent tests that should remain green during an engine swap:

- `RewriteEngineContractTests`
- `RewriteJobUseCaseTests`
- `WebhookOutboxUseCaseTests`
- `RewriteApiTests`
- `RewriteCostTrackingTests`
- `InfrastructureServiceCollectionTests`
- `tests/unit/rewrite-engine-contract.test.ts`
- `tests/unit/rewrite-response.test.ts`
