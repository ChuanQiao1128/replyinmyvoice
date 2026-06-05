# GA-02: OpenAPI 3.1 spec for /api/v1 + served + linked from /developers

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Live async v1 contract (authoritative: `plans/rewrite-api-v1/SPEC.md` §API and Job Contracts):
  - `POST /api/v1/rewrite` body `{ "draft": string }` → `202 { id, status:"processing" }` (+ `Location: /api/v1/rewrite/{id}`).
  - `GET /api/v1/rewrite/{id}` → `200` `{ id, status:"processing" }` | `{ id, status:"succeeded", rewrittenText, signal:{ draft, rewrite } }` | `{ id, status:"failed", error:{ code, message } }`.
  - `GET /api/v1/usage` → `200 { scope, periodKey, quota, used, remaining, periodEnd }`.
  - Auth: HTTP bearer `rmv_live_…`. Error body `{ error:{ code, message } }`; statuses 400 `invalid_request`/`input_too_long`, 401 `invalid_key`, 402 `quota_exhausted`, 409 `idempotency_conflict`, 429 `rate_limited`. Response headers `X-RateLimit-Limit/-Remaining/-Reset`, `Retry-After` on 429. Input: `draft` ≥10 chars, ≤300 words, ≤2400 chars. `Idempotency-Key` header supported on submit.
- Dev page: `app/developers/page.tsx`.

## Changes required
1. Author **`public/openapi.json`** — a valid **OpenAPI 3.1** document describing exactly the 3 endpoints above, with: request/response schemas, the shared `Error` object, the `succeeded`/`processing`/`failed` result variants, bearer `securityScheme`, the rate-limit response headers, and the `Idempotency-Key` parameter. `info.title "Reply In My Voice API"`, `info.version "1.0.0"`, `servers: [{ url: "https://replyinmyvoice.com" }]`.
2. Serve it at **`GET /api/v1/openapi.json`** via a Next route (`app/api/v1/openapi/route.ts`) returning the JSON (no auth, `Content-Type: application/json`, cacheable). It may read `public/openapi.json` or inline the same object.
3. **Link it from `/developers`** — an "OpenAPI specification" link/button (to `/api/v1/openapi.json`).

## Acceptance (machine-checkable)
- [ ] `public/openapi.json` parses as JSON and is OpenAPI 3.1 (`openapi` starts with `"3.1"`, has `paths` for the 3 endpoints). Add a `tests/unit/openapi-spec.test.ts` that loads it and asserts the 3 paths + the error schema + bearer security exist.
- [ ] `GET /api/v1/openapi.json` route returns the document (add/extend a route test).
- [ ] `/developers` contains a link to `/api/v1/openapi.json`.
- [ ] `npm run typecheck` + `npm run test` green; banned-term grep clean (never describe `signal` as detection).

## Do NOT
- Do NOT describe a synchronous response shape — it is `202` + poll. Do NOT invent endpoints/fields not in `SPEC.md` (no `analyze-signal`).
