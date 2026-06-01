# PAY-31: Purchase-side fraud / abuse controls

**Priority:** P2 · **Owner:** Codex (code) + Owner (Stripe Radar rules) · **Skill:** resilience-test-generation · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- The pricing-spec covers free-trial multi-account abuse + campaign-code abuse, but there are **no purchase-side controls**: card-testing, checkout velocity, refund abuse. Stripe Radar is available (owner-configurable). Checkout: `BillingHttpFunctions.CreateCheckoutSession`; admin already tracks refunds (`AdminAuditLog`).

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge). Use neutral wording for security controls (no banned terms).

## Changes required
1. **Checkout velocity limit:** rate-limit checkout-session creation per user/identity (e.g. N sessions per window) to blunt card-testing; return a clear 429. Reuse the existing auth rate-limit pattern if present.
2. **Refund-abuse monitoring:** flag users exceeding a refund count/amount threshold (from `AdminAuditLog` refund entries) and surface in admin stats; do not auto-act.
3. **Stripe Radar:** `docs/fraud-controls.md` documenting recommended Radar rules + how our metadata (externalAuthUserId) aids review (owner enables Radar in the dashboard).

## Acceptance (machine-checkable)
- [ ] Unit/integration test: exceeding the checkout velocity limit returns 429; under the limit succeeds.
- [ ] Refund-abuse flag computed from audit logs is covered by a test.
- [ ] `docs/fraud-controls.md` exists. `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT auto-suspend/auto-refund from these signals (surface for owner action). Do NOT touch `main`.
