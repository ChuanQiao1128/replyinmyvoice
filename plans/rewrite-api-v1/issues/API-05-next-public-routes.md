# API-05: Public Next.js routes for /api/v1/rewrite (POST + GET), no same-origin

**Tier:** 2 · **Owner:** Codex · **Depends on:** API-03, API-04

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §Proposed Architecture (Components).
- Existing internal proxy to mirror for forwarding, but DROP same-origin and forward the Bearer key: `app/api/rewrite/route.ts` (uses `requireSameOrigin` from `lib/http.ts:10`; Functions base URL + forwarding live in `lib/azure-api.ts`).
- Public surface is `/api/v1/*`; the trust boundary is the API key (forwarded verbatim), NOT same-origin.

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets. Do NOT push/touch `main`.
- **Do NOT loosen or touch the existing `app/api/rewrite/route.ts` same-origin gate** — add NEW routes only.

## Changes required
1. `app/api/v1/rewrite/route.ts` — `POST`: NO `requireSameOrigin`; read the incoming `Authorization` header and forward it verbatim to the Functions `v1/rewrite` endpoint; return the Functions response (status + body) unchanged.
2. `app/api/v1/rewrite/[id]/route.ts` — `GET`: forward to Functions `v1/rewrite/{id}` with the `Authorization` header; return as-is.
3. Resolve the Functions base URL from the same env `lib/azure-api.ts` already uses (do not hardcode). If the existing helper injects an Entra token, add a variant/path that forwards the caller's `Authorization` instead.

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` and `npm run test` green.
- [ ] A request without `Authorization` is rejected (401 surfaced from backend); a request with a Bearer key reaches the backend (assert via a mocked fetch in a unit test).
- [ ] Banned-term grep clean on `app components public lib`.
- [ ] `app/api/rewrite/route.ts` is unchanged (same-origin still enforced there).

## Do NOT
- Do NOT add same-origin to the v1 routes. Do NOT modify the existing website rewrite proxy or backend.
