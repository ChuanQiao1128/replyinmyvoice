# P2-09: Redesign /developers (accurate async docs + quickstart + discovery) + drop analyze-signal

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §A + §E. Live contract: `plans/rewrite-api-v1/SPEC.md` §API and Job Contracts.
- `app/developers/page.tsx` is currently STALE/fictional: it advertises `POST /api/v1/analyze-signal` (does NOT exist), shows a **synchronous** `200 OK { rewrittenText, signal }` (the real API is **async**: `202 {id}` then poll), and carries an "In active development · not yet public" badge (it IS public). It also has no auth/error/rate-limit/quickstart docs and no link to key management.
- Header/nav: `components/site-header.tsx`.

## Changes required
Rewrite `app/developers/page.tsx` to a market-standard developer page reflecting the LIVE async API:
1. **Overview**: commercial use cases (drop in-voice, fact-preserving rewrites into a CRM / help desk / support inbox); metering in plain words — you pay per **succeeded** rewrite; an API call and a website rewrite draw from the **same** balance; **no free tier** (a key works only when the account has paid quota).
2. **Quickstart**: create a key (link to `/developers/keys`) → `POST /api/v1/rewrite` with `{ "draft": "…" }` → `202 { id, status:"processing" }` (+ `Location`) → poll `GET /api/v1/rewrite/{id}` → `succeeded { rewrittenText, signal }`. Copy-paste `curl` for submit AND poll.
3. **Authentication**: `Authorization: Bearer rmv_live_…`; key lifecycle; advise never logging keys.
4. **API reference**: the THREE real endpoints with request/response JSON; a full **error table** — `400 invalid_request` / `400 input_too_long` (≤300 words, ≤2400 chars) / `401 invalid_key` / `402 quota_exhausted` / `409 idempotency_conflict` / `429 rate_limited` — with an "uncharged" note (only a succeeded rewrite costs 1); **rate limits** (per-key RPM 60; `X-RateLimit-*` headers); **Idempotency-Key**.
5. **Guides**: poll with backoff (1–2 s); handling `failed`/timeout (uncharged, safe to resubmit).
6. **Pricing & quota** + **Data & privacy** (bounded **30-day** retention; never "we don't store your data").
7. **signal**: describe ONLY as an informational naturalness reference that may evolve and is not a guarantee; it is NOT accepted in the request. (See Do NOT for wording limits.)
8. **REMOVE** the `analyze-signal` endpoint entirely; **REMOVE** the "not yet public" badge; correct the response example to the async shape.
9. **Discovery**: add a **"Developers"** link to `components/site-header.tsx` nav; ensure the `/developers` primary CTA points to `/developers/keys` ("Get your API key").

## Acceptance (machine-checkable)
- [ ] `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` → CLEAN (this file is scanned).
- [ ] No occurrence of `analyze-signal` or `not yet public` under `app/developers`.
- [ ] `npm run typecheck` green; `npm run test` green (UPDATE any pinned `/developers` source-string test — grep `tests/` for old strings first).

## Do NOT
- Do NOT use the words detection/detector/bypass/undetectable/humanizer/evade ANYWHERE — describe `signal` only positively ("naturalness reference"); do not even say "not a detector".
- Do NOT invent endpoints, fields, or SDKs that don't exist. Do NOT claim it "passes AI detection".
- Do NOT change the actual API handlers — this is the marketing/docs page + nav only.
