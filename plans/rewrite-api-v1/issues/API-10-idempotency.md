# API-10: Idempotency-Key on submit (no duplicate reserve)

**Tier:** 2 · **Owner:** Codex · **Depends on:** API-03

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §State and Error Handling (Idempotency).
- `QuotaService.ReserveAsync` already dedupes by `(userId, idempotencyKey)`: same key + same request hash → returns the EXISTING attempt; same key + different hash → `Conflict` (`QuotaService.cs:89-108`).
- Submit endpoint (API-03) already passes an `Idempotency-Key` header or a generated one.

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets. No push/touch `main`.

## Changes required
1. Verify + harden the submit endpoint so a client-supplied `Idempotency-Key`:
   - same key + identical body → returns the SAME attempt `id` (`202`), creating only ONE reservation.
   - same key + different body → `409 idempotency_conflict`.
2. Add end-to-end tests for this behavior. This issue is primarily test-hardening on top of API-03's wiring; fix the endpoint if a gap is found.

## Acceptance (machine-checkable)
- [ ] xUnit/integration: two submits with the same `Idempotency-Key` + same draft → same `id`, exactly ONE `RewriteAttempt`/reservation. Same key + different draft → `409`.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT change `QuotaService.ReserveAsync` semantics. Do NOT weaken the metering invariant.
