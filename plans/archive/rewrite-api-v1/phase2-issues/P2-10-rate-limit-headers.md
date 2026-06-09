# P2-10: X-RateLimit-* response headers on /api/v1/* (CANARY)

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §F (OPS-01).
- The v1 handlers already enforce a per-key RPM limit (default `ApiKey.RateLimitPerMinute=60`) and return `429` when exceeded. Find the rate-limit helper (e.g. `IsV1RateLimitedAsync`) and the v1 endpoints in `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` (POST submit, GET `{id}`, and `GET /api/v1/usage`).
- Next pass-through routes: `app/api/v1/rewrite/route.ts`, `app/api/v1/rewrite/[id]/route.ts`, `app/api/v1/usage/route.ts`.

## Changes required
1. On **every** v1 response (200/202 and 429), set rate-limit headers computed from the resolved key's window:
   - `X-RateLimit-Limit`: the key's `RateLimitPerMinute`.
   - `X-RateLimit-Remaining`: max(0, limit − calls in the current minute window).
   - `X-RateLimit-Reset`: unix epoch seconds when the current window resets.
   - On `429` additionally set `Retry-After` (seconds until reset).
   Compute from the SAME window data the limiter already uses — do not introduce a second counter.
2. Make the Next pass-through routes **forward** these response headers from the backend to the caller (copy `X-RateLimit-*` and `Retry-After` if present).

## Acceptance (machine-checkable)
- [ ] xUnit/integration: a successful v1 call returns `X-RateLimit-Limit/-Remaining/-Reset`; exceeding RPM returns `429` with `Retry-After` and `X-RateLimit-Remaining: 0`.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` green.

## Do NOT
- Do NOT change the limit value, the quota/billing math, or add same-origin to v1 routes.
- Do NOT add a second rate-limit store; reuse the existing window source.
