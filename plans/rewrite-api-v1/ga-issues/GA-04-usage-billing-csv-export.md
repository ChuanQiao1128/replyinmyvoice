# GA-04: Usage & billing CSV export

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. The developer dashboard (`/developers/keys` tabs, `components/developers/usage-panel.tsx` + `billing-panel.tsx`) already consumes:
  - usage: `GET /api/me/api-usage/recent` (per-call rows `{ createdAt, endpoint, statusCode, latencyMs, keyLast4 }`) and `/summary`,`/series`.
  - billing: `GET /api/me/billing/history` (rows `{ type, date, description, amount, currency, status, receiptUrl|hostedInvoiceUrl }`).
- These `/api/me/*` Next routes are **same-origin + Entra token forward** (pattern: `requireSameOrigin(request)` + `getCurrentAccessToken()` → `fetch(getAzureApiBaseUrl()+path, { headers:{ Authorization } })`). See `app/api/me/billing/history/route.ts`.

## Changes required
1. **Two new same-origin Next export routes** that fetch the existing Azure JSON and return **`text/csv`** with `Content-Disposition: attachment; filename=...`:
   - `app/api/me/api-usage/export/route.ts` → CSV of recent usage rows (columns: createdAt, endpoint, statusCode, latencyMs, keyLast4). Accept `?limit=` (cap 1000).
   - `app/api/me/billing/export/route.ts` → CSV of billing history (columns: date, type, description, amount, currency, status, url).
   - Enforce `requireSameOrigin` + auth (401 if no token) exactly like the sibling routes. Hand-roll CSV (escape quotes/commas/newlines) — **no CSV library**.
2. **Frontend**: an "Export CSV" button on the Usage tab and on the Billing tab (`components/developers/usage-panel.tsx`, `billing-panel.tsx`) that downloads from these routes.

## Acceptance (machine-checkable)
- [ ] A unit test for the CSV serializer (quoting/escaping) and a route test asserting `text/csv` + `Content-Disposition` + 401 without auth (`tests/unit/...export...test.ts`).
- [ ] Export routes enforce same-origin and forward the Entra token (no client-supplied user id).
- [ ] `npm run typecheck` + `npm run test` green; banned-term grep clean.

## Do NOT
- Do NOT add a CSV/dependency package. Do NOT expose another user's data or drop same-origin. Do NOT change the existing usage/billing JSON endpoints.
