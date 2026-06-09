# Rewrite Public API v1 — Delivery Plan (to GA)

- **Status:** Planned — NOT started. Do not launch the delivery wave until owner says go.
- **Date:** 2026-06-04 (rev 2 — switched from synchronous to asynchronous submit+poll)
- **Design source of truth:** [`SPEC.md`](./SPEC.md) (contracts, data model, decisions). This file is the authoritative **phased execution plan** and **supersedes SPEC.md §Rollout Plan**.
- **Execution form:** `dynamic-delivery-workflow` — each issue implemented by a Codex worker via a detached background daemon, diff-scoped safety gates, canary-first, merged into an **integration branch (never main)**.

> **Architecture note (rev 2):** the public API is **asynchronous** — `POST /api/v1/rewrite` returns `202 { id }` in milliseconds; the caller polls `GET /api/v1/rewrite/{id}` for the result. This reuses the existing async pipeline (`reserve → outbox → worker → finalize`, the same one the website already polls), so there is **no long-held connection, no Cloudflare wall-clock risk, and no per-key in-flight concurrency machinery**. A synchronous long-hold design was considered and rejected.

---

## Final Target — Definition of Done for GA (the bar; miss it → re-run)

A previously-unrelated developer can, with no help from us:

1. Sign up → **pay** → receive usable quota.
2. Self-create an API key (`rmv_live_…`, plaintext shown once).
3. Follow the **public docs** to wire the API into **their own production project**.
4. Call it for real and reliably get a rewrite back: **`POST` returns an `id` immediately; poll `GET /{id}` (or use the SDK/webhook) for the result**, from a stable public host, with a clear terminal state every time.
5. Read remaining quota programmatically (`GET /api/v1/usage`) and in the portal.

…and across **rate-limiting, payment-failure, duplicate-submit, and worker-failure** conditions the system always:

- **Bills correctly:** a succeeded rewrite consumes exactly 1 count; every rejected/failed/timed-out attempt consumes 0. *(core invariant)*
- **Is observable and operable:** every call is logged/auditable; quota is visible; abuse is throttled.
- **Causes zero regression** to the existing website rewrite path.
- **Stays clean:** all outward copy passes the banned-term gate (`humanizer|bypass|undetect|detector|evade`) and never over-promises ("natural/concise/faithful", never "passes AI detection").

> **Re-run rule:** each Phase has a **Gate** below. If a Gate fails, the failing issues (and anything downstream that depends on them) re-run under the workflow until the Gate passes. Nothing merges to `main` and nothing launches until its Gate is green. The Final Target above is the last gate.

> **Human-only gate (not automatable):** the **first real Stripe charge** is performed by the owner (AGENTS.md hard limit). Automation verifies the full money loop in **Stripe test mode** only.

> **Quality gate (human judgement, separate axis):** the API ships the engine's *current* output quality as a black box. "All Phases green" proves the API works, **not** that rewrite quality meets your bar. Do **not** open the door to real external customers until you are satisfied with engine quality. This is an explicit owner sign-off before Phase 4 GA, tracked but not machine-checked.

---

## Delivery mechanism (`dynamic-delivery-workflow`)

- One GitHub issue per checkpoint below; each carries its own machine-checkable acceptance criteria.
- A Codex worker implements each issue in isolation; a watchdog sentinel restarts on stall (survives session suspend/restart).
- Per-issue safety gates run **diff-scoped**: banned-term grep, secret scan, scope check, typecheck/test/build.
- Canary-first: validate the pipeline on one issue before fanning out.
- Merges land on an **integration branch** (e.g. `delivery/rewrite-api-v1`); `main` only after the Phase Gate is green and owner approves — `main` auto-deploys prod (`cf:deploy` + EF migrate on live Azure SQL).

---

## Phase 1 — Production-ready core API (asynchronous)
**Goal:** a paying user can put the API into their **production** project and call it for real — safely (can't be DoS'd, can't silently overrun, can't be left without a terminal state).

> Both public endpoints largely **reuse existing logic**: `POST` mirrors `CreateRewriteAttempt` (already returns `202 + id`), `GET /{id}` mirrors `GetRewriteAttempt` (already polls). The new work is API-key auth, the `/api/v1/*` surface, the 300-word cap, and the response mapping.

- `API-01` **`ApiKeyService`:** generate `rmv_live_` + 32B CSPRNG; `API_KEY_PEPPER`+SHA-256 → `KeyHash`; store `Last4`; list; revoke. **Accept:** unit tests; plaintext never persisted; lookup by hash works; revoke sets `RevokedAt`.
- `API-02` **`ApiKeyAuthResolver`:** `Bearer` → pepper+hash → `ApiKeys` lookup → reject revoked/expired. **Accept:** valid→userId; unknown/revoked/expired/missing → `401`.
- `API-03` **`POST /api/v1/rewrite` (async submit):** validate (≤300 words + ≤2400 chars) → `GetUsagePlan` → `CreateAttemptAsync` (reserve + outbox) → **return `202 { id, status:"processing" }`** with `Location: /api/v1/rewrite/{id}`. Reuses `CreateRewriteAttempt` logic under key auth. **Accept:** `curl` returns `202+id` in ms; reservation made (not yet charged); `400` on >300 words; `401` invalid key; `402` no quota — **none of these charge**.
- `API-04` **`GET /api/v1/rewrite/{id}` (fetch result):** key-authed; only the key-owner's attempts. Map to `{ id, status }` plus `{ rewrittenText, signal }` when succeeded, `{ error }` when failed. **Accept:** processing→`processing`; succeeded→result + **quota now charged exactly 1**; failed/timeout→`failed` + **charged 0**; other user's id → `404`.
- `API-05` **Public Next routes `app/api/v1/rewrite` (POST) + `/{id}` (GET):** no same-origin; pass `Authorization` through; **millisecond responses, no long-held connection** (this is why the Cloudflare wall-clock concern is gone). **Accept:** cross-origin submit+poll with a valid key works; without key → `401`.
- `API-06` **Keys CRUD Functions (Entra-authed):** `POST/GET/DELETE /api/keys`. **Accept:** create returns plaintext once; list shows masked `Last4`; revoke works; accessing another user's key → `404`.
- `API-07` **Key-management UI (portal):** create modal / show-once / copy / list with `Last4` / revoke. **Accept:** UI flow works; source-string contract tests updated (project memory `workspace-copy-test-pins-ui`).
- `API-08` **Per-key rate limit + per-call audit:** per-key RPM (default 60) → `429`; write one `ApiKeyUsage` row per submit. **Accept:** exceeding RPM → `429` uncharged; `ApiKeyUsage` row per call. *(No in-flight concurrency counter needed — submit is millisecond-cheap; worker/queue absorbs processing load. A per-key concurrency cap is optional, deferred.)*
- `API-09` **`GET /api/v1/usage` (key-authed):** `{ scope, quota, used, remaining, periodEnd }`. **Accept:** numbers equal the backend `UsagePeriod`/credit math.
- `API-10` **Idempotency-Key on submit:** prevent duplicate-submit double-reserve. **Accept:** same key+body → same `id`; same key+different body → `409`.
- `API-11` **Terminal-state guarantee for API attempts:** confirm worker `provider_timeout` / reservation-TTL / `ReleaseExpiredReservations` paths drive every API-originated attempt to a terminal state (succeeded/failed/expired), so `GET /{id}` never hangs forever. **Accept:** a stuck/slow attempt reaches `failed`/`expired` within TTL and is **uncharged**; mostly verification of existing logic.

**Phase 1 Gate:** all above accepted + billing-invariant test green (charge on succeeded only) + **zero website regression** (full backend suite + source-string tests). → *Paying users can go to production.*

---

## Phase 2 — Billing loop & operability
**Goal:** the money loop is proven end-to-end and users can run on it (see usage, get blocked when out, survive failed renewals).

- `BILL-01` **Paid→API quota end-to-end (shared pool):** a test-mode subscription / credit pack drives API quota (likely zero new code — verification + tests). **Accept:** test subscription → succeeded API rewrite decrements the shared `UsagePeriod`/`RewriteCredit`.
- `BILL-02` **Usage panel (portal):** this-month used / remaining / call history (source `ApiKeyUsage`). **Accept:** panel numbers equal backend.
- `BILL-03` **Exhaustion → purchase guidance** (no free tier). **Accept:** on `402`, response + UI surface a buy path.
- `BILL-04` **`invoice.payment_failed` handling** (known gap, project memory `payment-audit-wave`): a failed renewal **downgrades** quota instead of silently granting it. **Accept:** simulated failed-payment webhook → quota falls back to free baseline.
- `OBS-01` **Observability baseline:** full `ApiKeyUsage` + structured logs; wire PostHog/Sentry **when keys provided** (currently pending — logs are the fallback). **Accept:** every API call is traceable in logs/table.

**Phase 2 Gate:** test-mode full chain (subscribe → submit → poll → decrement → exhaust → renew/fail-downgrade) passes; usage panel consistent. *(First real charge = owner-performed, not a blocker for this gate.)*

---

## Phase 3 — Hardening & scale
**Goal:** withstand real-world abuse, malformed traffic, and load.

- `HARD-01` **Load test:** burst submits; confirm per-key RPM holds, the queue/worker absorbs processing without unbounded backlog, and poll latency stays bounded. **Accept:** load report + limits enforced + no backlog blow-up.
- `HARD-02` **Abuse & key-leak handling:** anomalous-usage signal + revoke/rotate runbook. **Accept:** revoke is immediate; rotation flow documented and tested.
- `HARD-03` **Input/error hardening:** oversized/malformed payloads, unified error body, boundary cases. **Accept:** fuzz/boundary inputs never `500`; all errors match the contract shape.
- `HARD-04` **Monitoring & alerts:** error-rate / poll-latency / queue-depth / quota-anomaly alerts (depends on `OBS-01`; degrades to log queries if PostHog/Sentry keys absent). **Accept:** threshold breach is alertable (or query-ready).
- `HARD-05` **Data-retention purge job:** delete API-originated `RequestJson`/`ResultJson` after 30 days. **Accept:** rows older than 30 days are purged; deletion endpoint works.

**Phase 3 Gate:** limits hold under load; leaked keys handled; purge runs; alerting ready.

---

## Phase 4 — Developer experience & public GA
**Goal:** a cold developer self-serves from signup to a working integration; the API goes public.

- `DX-01` **Runnable docs:** rewrite `app/developers/page.tsx` — submit+poll flow, curl + 2–3 language samples, auth, error/status table, key flow (remove "in development"). **Accept:** a cold developer reaches a first successful rewrite in ~10 min following only the page.
- `DX-02` **Official SDK helper (hides polling):** a small client that exposes `rewrite(draft)` and internally submits + polls, so integration is one call despite the async wire protocol. **Accept:** sample app gets a rewrite with a single SDK call.
- `DX-03` **Onboarding:** sign up → create key → "needs quota" → first call. **Accept:** a fresh account completes the path.
- `DX-04` **API ToS + 30-day data-retention policy + pricing page.** **Accept:** legal/policy copy in place; banned-term clean.
- `DX-05` **(optional, post-GA):** OpenAPI document + webhook callbacks (poll-free) + MCP. **Accept:** OpenAPI validates; others may defer.
- `LAUNCH-01` **GA cutover:** public page no longer says "not public"; monitoring ready; end-to-end real path green. **Accept:** owner quality sign-off recorded; full real path (signup→pay→key→prod submit→poll→result) green.

**Phase 4 Gate = Final Target.** Owner quality sign-off + cold-developer self-serve success + ToS/policy/pricing live + public surface ready.

---

## Cross-cutting acceptance (must hold in EVERY phase)

- **Billing invariant:** a succeeded rewrite charges 1; every rejected/failed/timed-out attempt charges 0. Asserted in tests on any issue touching the call path.
- **Zero website regression:** the existing same-origin `/api/rewrite` path and copy contract tests stay green.
- **Banned-term gate:** `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` clean before any merge.
- **No over-promising:** outward copy markets natural/concise/faithful only.
- **Secrets at runtime:** `API_KEY_PEPPER` validated in-handler; it is consumed by the C# backend only, so it is set on the Functions app settings **only** — never in `wrangler.jsonc` `vars` (committed plaintext). Set live on prod 2026-06-05. Never printed/committed.
- **Never `main` directly:** integration branch → Phase Gate → owner approval → `main` (auto-deploys prod).

---

## Risks & things to watch (self-review, rev 2)

1. **Async is two steps for the caller.** Mitigated by clear submit+poll docs (`DX-01`) and an official SDK that hides polling behind one `rewrite()` call (`DX-02`); webhooks later (`DX-05`). This is the standard shape for slow AI tasks, so developer friction is low.
2. **`invoice.payment_failed` is a pre-existing gap** (project memory `payment-audit-wave`): without `BILL-04`, a customer whose renewal fails keeps spending quota for free. Now in scope.
3. **Engine quality is a separate black-box axis.** Green Phases ≠ good rewrites. The owner quality sign-off before `LAUNCH-01` is the guard; until then, internal/trusted testers only.
4. **PostHog/Sentry keys are pending.** `OBS-01`/`HARD-04` alerting degrades to log queries until keys are provided.
5. **90 rewrites/period is small for B2B API volume.** Not a bug — a growth/pricing question. v1 reuses it (shared pool); higher-volume API packs are a post-launch item.
6. **Poll-latency UX:** clients should poll with sensible backoff; document a recommended interval (e.g. 1–2 s) and consider a `GET /{id}?wait=N` long-poll convenience later to cut round-trips.

*(Rev 1 risks "sync-timing spike" and "serverless concurrency cap" are removed — the async design eliminates both.)*

---

## Sequencing summary

```
Phase 1:  API-01 → API-02 → API-03 (POST) → API-04 (GET) → API-05 (Next routes)
                                            → API-06 → API-07   (keys + UI, parallel)
                                            → API-08 → API-09 → API-10 → API-11
Phase 2:  BILL-01 → BILL-02 → BILL-03 → BILL-04 → OBS-01
Phase 3:  HARD-01 … HARD-05
Phase 4:  DX-01 → DX-02 → DX-03 → DX-04 → (DX-05) → LAUNCH-01  (+ owner quality sign-off)
```
Phases run in order; within a phase, independent issues may fan out. Each Phase Gate is a hard checkpoint before the next.
