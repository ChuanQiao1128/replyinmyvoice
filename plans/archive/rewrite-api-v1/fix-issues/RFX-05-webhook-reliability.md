# RFX-05: Webhook delivery reliability (FIX-03, FIX-09, FIX-20)

**Tier:** 1 (merged to base; may contain a migration) · **Owner:** Codex · **Depends on:** RFX-04
Detailed findings: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#3, #9, #20).

## Context
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDispatcherService.cs`: 30s claim lock (~148) < the HTTP client's 100s default timeout, timer every 30s (`WebhookDispatcherTimerFunction.cs`) → slow receiver gets duplicate deliveries; claim txn has no serialization-retry (~111-156); a delivery with permanently-missing data throws every tick and never terminalizes (~158-172).
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDeliveryService.cs` (~27-39): webhook origin is found via the best-effort `ApiKeyUsage.RequestId` row, which can be missing (swallowed write) → webhook suppressed or misattributed.
- HMAC (`WebhookDispatcherService.cs` ~258-264) has no timestamp → receivers can't bound replay.

## Changes required
1. **FIX-03 no duplicate delivery:** set the webhook HTTP timeout **shorter than** the claim lease; introduce an `InProgress` status (or a renewable lease longer than the HTTP timeout) so the next timer tick can't re-claim an in-flight delivery; include a stable delivery/event id (and ideally an `Idempotency`/event-id header) so receivers can dedupe.
2. **FIX-09 reliable origin:** persist the submitting `ApiKeyId` on `RewriteAttempt` (or `UsageReservation`) at submit time and enqueue webhooks from that source of truth instead of the best-effort `ApiKeyUsage` lookup (+ EF migration if adding a column). 
3. **FIX-20 resilience:** add serialization-failure retry to the claim transaction (match the rest of the codebase); give permanently-failing/poison deliveries a terminal `Failed` state after max attempts instead of throwing forever; add a timestamp to the signed payload/headers (e.g. `X-RIMV-Timestamp`) and include it in the HMAC so receivers can reject stale deliveries.

## Acceptance (machine-checkable)
- [ ] xUnit: a slow delivery is not double-claimed within one HTTP-timeout window (lease > timeout); a delivery whose attempt/data is missing reaches terminal `Failed` after max attempts (no infinite throw); webhook enqueue uses the persisted `ApiKeyId` (not ApiKeyUsage) and still fires only for API-originated attempts; signature covers the timestamp.
- [ ] `cd backend-dotnet && dotnet test` green; `dotnet build` green (migration compiles, **no double-cascade FK** — see CROSS-REVIEW #11).
- [ ] Banned-term grep clean; secret never logged.

## Do NOT
- Do NOT block/alter the core rewrite finalize/release path — webhook work stays out-of-band + failure-isolated. Do NOT fire webhooks for non-API (website) attempts.
