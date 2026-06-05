# Rewrite Public API (v1) — System Specification

- **Status:** Draft for owner review
- **Date:** 2026-06-04 (rev 2 — asynchronous submit + poll; supersedes the rev 1 synchronous design)
- **Owner:** ChuanQiao1128 (TimeAwake Ltd)
- **Goal state:** Commercially usable third-party HTTP API for the rewrite engine
- **Execution plan:** [`DELIVERY-PLAN.md`](./DELIVERY-PLAN.md) (phased issues, gates, GA target)
- **Related:** `app/developers/page.tsx` (current marketing placeholder), `plans/commercialization-roadmap.md` (M8 = B2B API + Keys), `AGENTS.md`

---

## Context

`replyinmyvoice.com` runs a C#/Azure backend (`backend-dotnet/`) behind a thin Next.js UI. The rewrite engine is live but reachable **only** by a signed-in Entra user from the site itself (same-origin enforced at the Next proxy). The `/developers` page already advertises a `POST /api/v1/rewrite` with `Authorization: Bearer rmv_live_…`, but no key auth, no `/api/v1/*` endpoint, and no key-management UI exist yet — it is marketing only (`app/developers/page.tsx:143` "In active development · not yet public").

This spec defines the smallest **commercially usable** slice that lets an external developer create a key, submit a rewrite over HTTP, poll for the result, have their paid quota metered, and see what remains.

### Locked decisions (owner, 2026-06-04)

1. **Metering = per-call count** (not per-word, not per-token). A *succeeded* rewrite costs 1.
2. **Input cap = 300 English words per call** (with a character backstop).
3. **Response = asynchronous (submit + poll).** `POST /api/v1/rewrite` returns `202 { id }` in milliseconds; the caller polls `GET /api/v1/rewrite/{id}` for the result. *A synchronous long-hold design was considered and **rejected**: engine latency is unbounded (best-of-N, strong-model escalation), serverless + Cloudflare is ill-suited to long-held connections, and the website itself already polls. Reusing the existing async pipeline is simpler and safer.*
4. **Quota = shared pool with the website.** An API rewrite consumes the same per-user count quota (`UsagePeriod` / `RewriteCredit`) as a website rewrite. No separate B2B meter.
5. **No free tier.** A key only works when the owning account already has paid quota. Pay first, then call.
6. **v1 scope = the rewrite endpoint only** (`POST /api/v1/rewrite` + its `GET /api/v1/rewrite/{id}` result fetch). `analyze-signal` is deferred.

### Source-of-truth facts (verified in code)

- Engine orchestration (async today): `RewriteHttpFunctions.CreateRewriteAttempt` (`backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs:27`) → `RewriteRequestService.CreateAttemptAsync` → `QuotaService.ReserveAsync` (`backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs:24`) writes a `RewriteAttempt(Pending)` + `UsageReservation(Pending)` + `OutboxMessage("RewriteJobCreated")`, and **already returns `202 + attemptId`** (`RewriteHttpFunctions.cs:120-122`).
- Result fetch (async today): `GetRewriteAttempt` (`RewriteHttpFunctions.cs:125`) returns `RewriteAttemptResponse(AttemptId, Status, ResultJson, ErrorCode)` — **the website already polls this**.
- Worker: `RewriteJobProcessor.ProcessAsync` (`RewriteJobProcessor.cs:36`) marks processing, calls `IRewriteProvider.RewriteAsync`, then `QuotaService.FinalizeSuccessAsync` (success, charge 1) or `QuotaService.ReleaseAsync` (failure/timeout — **refunded, not charged**). Stuck attempts are swept by `ReleaseExpiredReservationsAsync` (`QuotaService.cs:345`) at the reservation TTL (15 min).
- Quota plan: `AccountService.GetUsagePlan` (`AccountService.cs:360`) → `AccountUsagePlan(Scope, PeriodKey, QuotaLimit)`. Paid = 90 (Testing = 10000), period key `paid:{subId}:{periodEnd}`. Free baseline from `FREE_BASELINE_REWRITES`. One-time packs top up via `RewriteCredit` when the period is exhausted (`QuotaService.FindUsableCreditAsync`, `QuotaService.cs:530`).
- Auth today: `FunctionAuthResolver.ResolveUserAsync` (Entra JWT + dev header only). **No API-key path exists.**
- Tables already migrated, no logic attached: `ApiKey` (`ApiKey.cs`) with `KeyHash` (unique index), `PlanTier`, `Scope`, `RateLimitPerMinute=60`, `MonthlyQuota=1000`, `CurrentPeriodUsage`, `ExpiresAt`, `RevokedAt`; and `ApiKeyUsage` (per-call log: endpoint, status, latency, cost).
- Request contract: `RewriteRequest(MessageToReplyTo?, RoughDraftReply, Audience?, Purpose?, WhatHappened?, FactsToPreserve?, Tone)` (`RewriteRequest.cs`).
- Result JSON shape (validated at `RewriteJobProcessor.cs:534`): `{ rewrittenText, changeSummary:[], riskNotes:[], naturalness:{ draftAiLikePercent, rewriteAiLikePercent, changePoints }, optimization:{ strategy, scenario, attemptsUsed, failedAttempts } }`.
- Same-origin gate: `lib/http.ts:10` `requireSameOrigin`, applied in `app/api/rewrite/route.ts:159`.

---

## Goals

- A working, metered async rewrite API: `POST /api/v1/rewrite` (submit) + `GET /api/v1/rewrite/{id}` (result), callable by any HTTP client with a valid key.
- Self-service API key lifecycle: create (plaintext shown once), list, revoke.
- Programmatic remaining-quota read: `GET /api/v1/usage`.
- Human-visible usage panel in the signed-in portal (this month, remaining, call history).
- Reuse the existing engine, async pipeline, and finalize/release semantics with **no behavior change to the website rewrite path**.

## Non-Goals (v1)

- `POST /api/v1/analyze-signal` (deferred).
- A separate B2B subscription tier / independent API meter (shared pool by decision).
- A free API tier.
- Synchronous long-hold responses (rejected — see decision 3), batch, or streaming.
- Webhook result callbacks and MCP (post-GA). An official SDK that hides polling is **in plan** for Phase 4, not v1 core.
- Per-word or per-token billing.
- Exposing `tone`, `audience`, `purpose`, or other engine inputs — v1 request is `{ draft }` only; `tone` is fixed to `warm` (the website workspace also hardcodes warm; project memory `tone-presets-not-in-product`).

---

## Current System

```
Third-party client                         (NO PATH TODAY — blocked)
        │
        ▼
replyinmyvoice.com (Next.js / Cloudflare)
  app/api/rewrite/route.ts ── requireSameOrigin() ──► 403 for off-site callers
        │ (same-origin only, Entra session)
        ▼
Azure Functions  /api/rewrite  (FunctionAuthResolver: Entra JWT only)  ── returns 202 + attemptId
        │ QuotaService.ReserveAsync → OutboxMessage
        ▼
Outbox → Worker → RewriteJobProcessor.ProcessAsync → IRewriteProvider → Finalize/Release
        ▲
Front-end polls GET /api/rewrite-attempts/{id}     (async result delivery — ALREADY the shape we want)
```

Net: the engine works, quota works, the flow is **already async (submit + poll)** — but every door is Entra + same-origin. `ApiKey`/`ApiKeyUsage` tables exist with **zero** attached logic.

---

## Proposed Architecture

The public API is a **key-authed mirror of the existing async endpoints**. No long-held connections, no inline engine execution, no Cloudflare wall-clock risk.

### Submit path

```
Third-party client ── POST /api/v1/rewrite  Authorization: Bearer rmv_live_xxx   { "draft": "…" }
        │
        ▼
[NEW] Next route app/api/v1/rewrite  (NO same-origin; pass Authorization through; millisecond response)
        │
        ▼
[NEW] Function v1/rewrite (POST):
   1. ApiKeyAuthResolver → userId            (else 401)
   2. validate { draft }  (≤300 words, ≤2400 chars; else 400, no reservation)
   3. GetUsagePlan(user)                      ← shared pool
   4. CreateAttemptAsync → reserve + outbox   (402 if no quota/credit)
   5. write ApiKeyUsage row; return 202 { id, status:"processing" }   ← milliseconds
        │
        ▼
Outbox → Worker → RewriteJobProcessor (UNCHANGED) → FinalizeSuccess (charge 1) / Release (charge 0)
```

### Result path

```
Third-party client ── GET /api/v1/rewrite/{id}  Authorization: Bearer rmv_live_xxx
        │
        ▼
[NEW] Function v1/rewrite/{id} (GET):
   1. ApiKeyAuthResolver → userId            (else 401)
   2. load attempt; must belong to this user (else 404)
   3. map status →
        processing → 200 { id, status:"processing" }
        succeeded  → 200 { id, status:"succeeded", rewrittenText, signal }
        failed/exp → 200 { id, status:"failed", error:{ code, message } }
```

**Key insight — mirror, don't rebuild.** `POST` reuses the `CreateRewriteAttempt` reserve→202 logic; `GET /{id}` reuses `GetRewriteAttempt`'s read; the worker is untouched. New work is only: API-key auth, the `/api/v1/*` surface, the 300-word cap, and response mapping. The synchronous "inline `ProcessAsync`" idea from rev 1 is gone.

### Components and ownership

| Component | New / Reuse | Responsibility |
|---|---|---|
| `app/api/v1/rewrite/route.ts` (POST) | **New** | Public submit, no same-origin, header pass-through, ms response |
| `app/api/v1/rewrite/[id]/route.ts` (GET) | **New** | Public result poll pass-through |
| `app/api/v1/usage/route.ts` | **New** | Public usage read pass-through |
| `app/(portal)/…/api-keys` page | **New** | Signed-in key management UI |
| `ApiKeyAuthResolver` (Functions/Auth) | **New** | `Bearer rmv_live_…` → pepper+hash → `ApiKeys` lookup → userId |
| `ApiKeyService` (Infrastructure/Services) | **New** | Generate / hash / list / revoke; write `ApiKeyUsage` |
| `V1RewriteHttpFunctions` | **New** | `v1/rewrite` (POST submit) + `v1/rewrite/{id}` (GET result) |
| `ApiKeyHttpFunctions` | **New** | `keys` CRUD (Entra-authed) + `v1/usage` (key-authed) |
| `CreateRewriteAttempt` / `GetRewriteAttempt` logic | **Reuse** | Mirrored under key auth |
| `QuotaService`, `RewriteRequestService`, `GetUsagePlan` | **Reuse** | Reserve / finalize / release, shared pool |
| Worker / `RewriteJobProcessor` | **Reuse (unchanged)** | Processes the outbox exactly as today |
| `ApiKey`, `ApiKeyUsage`, `RewriteAttempt`, `UsagePeriod`, `RewriteCredit`, `UsageReservation` | **Reuse** | No new tables expected |

---

## Data Model

No new tables anticipated for v1 (one nullable column on `ApiKey`). Field usage:

**`ApiKey`** (`ApiKey.cs`)
- `KeyHash` — `API_KEY_PEPPER`+SHA-256 (hex, lowercase) of the full plaintext `rmv_live_<random>`. Unique index present (`AppDbContext.cs:338`). Plaintext **never** stored.
- `Last4` — **new nullable column** for masked list display (`rmv_live_••••1234`).
- `Name` — user label.
- `RateLimitPerMinute` (default 60) — enforced in Phase 1 (`API-08`).
- `MonthlyQuota` / `CurrentPeriodUsage` — **not the primary meter** (shared pool). `CurrentPeriodUsage` = per-key analytics counter; `MonthlyQuota` an optional safety ceiling. Primary metering stays on `UsagePeriod`/`RewriteCredit`.
- `ExpiresAt` / `RevokedAt` — resolver rejects if `RevokedAt != null` or `ExpiresAt <= now`.
- `Scope` — defaults `"[]"`; reserved for future per-endpoint scoping. v1 ignores.

**`ApiKeyUsage`** — one row per `v1/*` call: `ApiKeyId`, `RequestId` (= attemptId on submit), `Endpoint`, `StatusCode`, `LatencyMs`, `CostUsdEstimate`, `CreatedAt`.

**Quota tables** — unchanged. The API path calls the same `ReserveAsync(...)` with the same `(periodKey, quotaLimit)` from `GetUsagePlan`, so an API rewrite and a website rewrite draw down the identical counters.

### Key format & hashing

- Plaintext: `rmv_live_` + 32 bytes CSPRNG, base62 (~43 chars). `rmv_test_` reserved for a future sandbox (not v1).
- Store: `KeyHash = lower(hex(SHA256(API_KEY_PEPPER ‖ plaintext)))`. Lookup is a single indexed equality — no per-row bcrypt (full-entropy key, not a password). Pepper is an env-stored global secret (defense-in-depth).
- Display: plaintext returned **once** at creation; list shows `rmv_live_••••<Last4>`.

---

## API and Job Contracts

### `POST /api/v1/rewrite` — submit (key-authed)

```
POST https://<host>/api/v1/rewrite
Authorization: Bearer rmv_live_xxx
Content-Type: application/json
Idempotency-Key: <optional; auto-generated if absent>

{ "draft": "order is delayed, ships next week" }
```
Accepted `202`:
```json
{ "id": "5f3c…", "status": "processing" }
```
- `Location: /api/v1/rewrite/5f3c…` header included.
- Reservation is made now; **charge happens only if the attempt later succeeds**.

### `GET /api/v1/rewrite/{id}` — result (key-authed, owner-only)

```json
// still running
{ "id": "5f3c…", "status": "processing" }

// done
{ "id": "5f3c…", "status": "succeeded",
  "rewrittenText": "Hi Sam — your order's running a little behind; it ships next week.",
  "signal": { "draft": 78, "rewrite": 24 } }

// failed / timed out (uncharged)
{ "id": "5f3c…", "status": "failed", "error": { "code": "engine_unavailable", "message": "…" } }
```
- `rewrittenText` ← engine `rewrittenText`; `signal.draft`/`signal.rewrite` ← `naturalness.draftAiLikePercent`/`rewriteAiLikePercent`. **Informational naturalness reference (lower reads more naturally), not a guarantee**; never framed as detection (banned terms; project memory `stop-chasing-ai-detection`).
- An `{id}` not owned by the key's user → `404`.

### `GET /api/v1/usage` (key-authed)

```json
{ "scope":"paid", "periodKey":"paid:sub_…:2026-07-01T…", "quota":90, "used":12, "remaining":78, "periodEnd":"2026-07-01T00:00:00Z" }
```
From the numbers `AccountService.GetOrCreateAccountSummaryAsync` already computes (`AccountService.cs:74-161`).

### Key management (Entra-authed, signed-in portal)

| Method | Route | Body / Result |
|---|---|---|
| `POST` | `/api/keys` | `{ name }` → `{ id, name, key:"rmv_live_…", createdAt }` (plaintext once) |
| `GET` | `/api/keys` | `[{ id, name, maskedKey, lastUsedAt, createdAt, revokedAt }]` |
| `DELETE` | `/api/keys/{id}` | revoke (sets `RevokedAt`); `204` |

### Input validation (submit)

- `draft` required, trimmed length ≥ 10 chars (existing rule, `RewriteHttpFunctions.cs:323`).
- **Word cap:** `draft.Split(whitespace, RemoveEmptyEntries).Length ≤ 300` → else `400 input_too_long`.
- **Char backstop:** `draft.Length ≤ 2400` → else `400 input_too_long`. Both checked **before** reservation, so rejects never charge.

---

## State and Error Handling

### API key states
`active` → (`DELETE`) → `revoked`; `active` → (clock past `ExpiresAt`) → `expired`. Resolver treats `revoked`/`expired` as `401`.

### Attempt lifecycle (reused verbatim)
`Pending` → `Processing` → `Succeeded` (charge 1) | `Failed`/`Expired` (refunded — **not charged**). The `GET /{id}` status maps: `Pending`/`Processing`→`processing`; `Succeeded`→`succeeded`; `Failed`/`Expired`→`failed`. Stuck attempts reach a terminal state via `ReleaseExpiredReservationsAsync` at the reservation TTL, so polling always terminates. Source: `QuotaService.cs:197/279/345`.

### Error codes

**Submit `POST /api/v1/rewrite`:**

| HTTP | code | When | Charged? |
|---|---|---|---|
| `202` | — | accepted, queued | only if it later succeeds |
| `400` | `invalid_request` | malformed JSON / missing `draft` / < 10 chars | No |
| `400` | `input_too_long` | > 300 words or > 2400 chars | No |
| `401` | `invalid_key` | missing / unknown / revoked / expired key | No |
| `402` | `quota_exhausted` | no period quota and no usable credit | No |
| `409` | `idempotency_conflict` | same `Idempotency-Key` reused with a different body | No |
| `429` | `rate_limited` | per-key RPM (default 60) exceeded | No |

**Result `GET /api/v1/rewrite/{id}`:** always `200` with `status` ∈ {`processing`,`succeeded`,`failed`} for the owner; `404` if not found / not the key-owner's. A `succeeded` fetch reflects a charge of exactly 1; `failed`/timeout reflects 0.

Error bodies: `{ "error": { "code": "...", "message": "..." } }`. **Billing invariant:** only a *succeeded* attempt consumes quota; every reject/fail/timeout consumes 0 — must be covered by tests.

### Idempotency
`Idempotency-Key` on submit reuses `ReserveAsync` idempotency: same key + same body → same attempt `id`; same key + different body → `409`. Protects against duplicate submits.

---

## Security and Privacy

- **Key handling:** plaintext shown once; only pepper+SHA-256 stored; revoke immediate (`RevokedAt`); never logged. Rotation in v1 = create-new + revoke-old.
- **Same-origin stays intact** for `app/api/rewrite/route.ts` — v1 adds a *separate* public surface (`app/api/v1/*`) whose trust boundary is the API key. Do not loosen the website's same-origin gate.
- **Secrets:** no secret values in source. New env `API_KEY_PEPPER` read at runtime in-handler; set on **both** the Worker (`wrangler.jsonc` vars) and the `replyinmyvoice-func-dev` Function app settings (project memory `pricing-packs-cutover-live`); never printed/committed.
- **Banned terms:** API copy, field names, error messages, docs must not contain `humanizer | bypass | undetect | detector | evade`. Run `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` before completion.
- **No over-promising:** market "natural / concise / faithful", never "passes AI detection".
- **Data retention — bounded, not zero:** the async path reuses `RewriteAttempt`, which **persists the input** (`RequestJson`) and output (`ResultJson`) — required by the worker and by idempotency. So v1 is *not* zero-storage. Posture: (a) keep `RewriteAttempt` storage, (b) **purge API-originated attempts after 30 days** (owner-decided 2026-06-04), (c) expose deletion (mirror existing `DELETE /me/rewrites/{id}`). Publicly claim a **bounded 30-day retention**, never "we don't store your data".

---

## Decisions (resolved with owner, 2026-06-04)

1. **Async submit + poll** (decision 3 above) — synchronous long-hold rejected.
2. **Data-retention window = 30 days** + purge job + deletion endpoint.
3. **`signal` is exposed** on a succeeded result, documented as an informational naturalness reference, **not a guarantee**, never detection.
4. **Masked-key storage** = nullable `Last4` on `ApiKey`.
5. **Rate limiting, two layers, computed separately:**
   - **Per-key throttle = RPM (default 60)** → `429`, uncharged. Each key has its own window; keys don't share a throttle. *(No in-flight concurrency counter needed — submit is millisecond-cheap and the worker/queue absorbs processing load; a per-key concurrency cap is optional/deferred.)*
   - **Per-account count quota (the money)** — all of an account's keys draw down the **same** shared `UsagePeriod`/`RewriteCredit`; exhausting it stops every key with `402`. Mental model: throttle = per-key tap; quota = per-account tank.
6. **Pricing:** reuse the existing paid plan (90/period) and packs; **no new Stripe price, no separate API meter** for v1. Higher-volume API packs are post-launch.
7. **Key hashing uses `API_KEY_PEPPER`** before SHA-256.

---

## Verification Plan

### Automated (xUnit; skills `dotnet-backend-testing`, `resilience-test-generation`)
- **Auth:** valid key → userId; unknown/revoked/expired/missing → `401`.
- **Billing invariant:** a *succeeded* attempt decrements quota by exactly 1; every reject/fail/timeout by 0 (assert `UsedCount`/`ReservedCount`).
- **Submit:** valid → `202 + id` + reservation; >300 words → `400`; 2401 no-whitespace chars → `400`; no quota → `402`; all uncharged.
- **Poll:** owner sees `processing`→`succeeded`(+result)/`failed`; non-owner id → `404`.
- **Quota:** exhausted period with usable `RewriteCredit` → succeeds against credit; with none → `402`.
- **Idempotency:** same key+body → same `id`; same key+different body → `409`.
- **Terminal state:** a stuck/slow attempt reaches `failed`/`expired` within TTL via `ReleaseExpiredReservationsAsync`, uncharged (poll never hangs forever).
- **Shared pool:** a website rewrite then an API rewrite for the same user draw the same `UsagePeriod` counter.
- **No-regression:** existing `RewriteHttpFunctions` / same-origin website path + source-string contract tests stay green (project memory `workspace-copy-test-pins-ui`).

### Manual
- `curl` submit → poll happy path + each error code.
- Portal: create key (plaintext once), copy, submit+poll, see usage tick, revoke → next call `401`.
- Banned-term grep clean.

---

## Rollout & Implementation

The authoritative phased plan — production-ready Phase 1 (async core) → Phase 2 billing/operability → Phase 3 hardening → Phase 4 developer experience & GA, each with an acceptance gate and a final Definition of Done — lives in **[`DELIVERY-PLAN.md`](./DELIVERY-PLAN.md)**. Each checkpoint there becomes one GitHub issue, delivered via `dynamic-delivery-workflow` into an integration branch (never `main`).
