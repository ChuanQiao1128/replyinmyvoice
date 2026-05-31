# PAY-04: Test Stripe API failure on checkout-create and refund (no partial state)

**Priority:** P0 · **Owner:** Codex · **Skill:** resilience-test-generation + dotnet-backend-testing · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Checkout-create: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs` (`CreateCheckoutSessionUrlAsync`) via `BillingHttpFunctions.CreateCheckoutSession`.
- Admin refund: `AdminService.IssueRefundAsync` → `StripeBillingService.RefundPaymentAsync` (`IStripeRefundClient`), writes an `AdminAuditLog`.
- Test doubles today (`FakeStripeBillingService` / `FakeStripeRefundClient`) only ever **succeed** (or throw a mapped error for portal-missing-customer). The real failure modes — Stripe returns 5xx / network timeout / `StripeException` — are untested at the two places real money/sessions are created.
- **Gap:** no test proves a Stripe failure leaves NO partial DB state (no orphaned audit log on a failed refund; no DB write on a failed checkout-create).

## Constraints (AGENTS.md)
- Banned terms: `humanizer|bypass|undetect|detector|evade`.
- Automation must NEVER initiate a real Stripe charge — these are fakes/mocks, not live calls.
- Do NOT push to `main`.

## Changes required
1. Extend the fake/mocked Stripe client(s) to allow injecting a failure (throw `StripeException` / simulated 5xx / `TaskCanceledException`).
2. **Checkout-create failure test:** Stripe throws on session create → the API returns 5xx, and there is **no** new DB state (no user mutation that implies a successful checkout, no credit). Assert the error is surfaced, not swallowed.
3. **Refund failure test:** `RefundPaymentAsync` throws → `AdminService.IssueRefundAsync` does **not** write an `AdminAuditLog` row (or writes a clearly-marked failure row, per the existing design — assert whichever the code intends, and that no `refund`-success audit row is persisted) and no credit clawback is applied.
4. (If applicable) assert the deterministic Stripe idempotency key is still used so a retry after a transient failure does not double-refund.

## Acceptance (machine-checkable)
- [ ] 2+ new tests (checkout-create failure, refund failure) added and passing.
- [ ] Tests prove no partial/orphaned DB state on failure.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT make real Stripe network calls in tests.
- Do NOT touch `main` / deploy.
