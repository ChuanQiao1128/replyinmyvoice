# API-08: Per-key rate limit (RPM) + per-call ApiKeyUsage

**Tier:** 2 · **Owner:** Codex · **Depends on:** API-03

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §Decisions (rate limiting) + §Error codes.
- `ApiKey.RateLimitPerMinute` (default 60). `ApiKeyUsage` table = per-call log.
- Submit endpoint: `V1RewriteHttpFunctions.SubmitRewrite` (API-03).

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets. No push/touch `main`.
- **No in-flight concurrency counter** — async submit is millisecond-cheap (SPEC decision). RPM only.

## Changes required
1. In the submit path (API-03), BEFORE reserving, enforce a per-key RPM limit:
   - count this key's `ApiKeyUsage` rows in the last 60 seconds (fixed or sliding window); if `>= key.RateLimitPerMinute` → `429 { error:{ code:"rate_limited" } }` and do NOT reserve (uncharged).
2. Ensure every v1 call writes exactly one `ApiKeyUsage` row (endpoint, status code, latency ms) — INCLUDING rejects, so the window count is accurate. A logging failure must not 500 the request.
3. The check keys off the `ApiKey` row (resolve the key id alongside the user in API-02, or re-resolve here).

## Acceptance (machine-checkable)
- [ ] xUnit/integration: submitting `RateLimitPerMinute + 1` times within a minute → the last call is `429` and creates NO reservation; quota counters reflect only the allowed calls.
- [ ] Each call (including the `429`) writes one `ApiKeyUsage` row.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT add a concurrency/in-flight cap. Do NOT rate-limit the Entra website path.
