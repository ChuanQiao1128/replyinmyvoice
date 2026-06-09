# WH-01: API result webhooks (per-key URL + HMAC-signed delivery, out-of-band)

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. API rewrites are async: submit reserves + outbox → worker `RewriteJobProcessor.ProcessAsync` → `QuotaService.FinalizeSuccessAsync` (success) or `ReleaseAsync` (fail/timeout). The attempt (`RewriteAttempt`) carries `UserId`; API-originated calls are logged in `ApiKeyUsage` (links `ApiKeyId` → `RequestId` = attemptId on submit). Outbox/timer pattern to mirror: `OutboxMessage` + `OutboxDispatcherTimerFunction`. Keys: `ApiKey` entity, `ApiKeyService`, `ApiKeyHttpFunctions`, `components/developers/api-keys-panel.tsx`.
- Goal (MVP): when an **API-originated** rewrite reaches a terminal state, POST a signed JSON notification to the key's configured webhook URL, **out-of-band** — the rewrite path itself must not change or be blocked by webhook delivery.

## Changes required
1. **Per-key webhook config**: add `WebhookUrl` (string?, nullable) + `WebhookSecret` (string?, nullable) to `ApiKey` (+ EF migration). UI in `api-keys-panel.tsx`: set/clear a webhook URL per key; generate a `WebhookSecret` shown once on set. Endpoint(s) in `ApiKeyHttpFunctions` + `app/api/keys/[id]/webhook/route.ts` (Entra-authed, owner-only).
2. **Delivery record**: new `WebhookDelivery` entity (`Id, ApiKeyId, RewriteAttemptId, Url, Status(Pending/Delivered/Failed), AttemptCount, LastError?, CreatedAt, DeliveredAt?, RowVersion`) + migration.
3. **Enqueue on completion (out-of-band)**: after an **API-originated** attempt is finalized/released, enqueue a `WebhookDelivery(Pending)` IF the owning key has a `WebhookUrl`. Determine API-origin via the `ApiKeyUsage` row for the attempt (RequestId = attemptId). **Do NOT modify the core finalize/release correctness** — only add an additive enqueue step (guard so any webhook error cannot fail the rewrite).
4. **Dispatcher** (mirror `OutboxDispatcherTimerFunction`): a timer that delivers pending `WebhookDelivery`s — POST body `{ id, status, rewrittenText?, signal?, error? }` with header `X-RIMV-Signature: sha256=<hex HMAC-SHA256(WebhookSecret, rawBody)>`; bounded retries (e.g. 5) with backoff; mark Delivered on 2xx, Failed after retries. Never log the secret.
5. **Docs** `plans/rewrite-api-v1/webhooks.md`: payload shape + a signature-verification snippet.

## Acceptance (machine-checkable)
- [ ] xUnit: finalizing an **API** attempt whose key has a `WebhookUrl` creates a `WebhookDelivery(Pending)` with the correct payload; a **website (non-API)** attempt creates NONE; the signature equals `HMAC-SHA256(secret, body)` (hex); the dispatcher marks Delivered on a 200 (fake sender) and Failed after N failures; the rewrite finalize path still succeeds even if enqueue/delivery throws.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` + `npm run test` green (update pinned keys-panel copy tests).
- [ ] Banned-term grep clean; no secret VALUES in source.

## Do NOT
- Do NOT block, alter, or risk the core rewrite reserve→finalize/release correctness — webhook work is strictly additive and failure-isolated.
- Do NOT fire webhooks for non-API (website) attempts. Do NOT log `WebhookSecret`. Do NOT call deploy commands.
