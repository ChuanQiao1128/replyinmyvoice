# PAY-22: Financial reconciliation job (Stripe vs internal credit ledger)

**Priority:** P1 · **Owner:** Codex · **Skill:** resilience-test-generation + data-module-review · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- No reconciliation exists. `RewriteCredit` (Source=PURCHASE) is both the credit ledger and the payment record; `StripeEvent` is the webhook dedup table. A missed/failed webhook means a real Stripe payment with **no** matching grant — currently undetectable except by customer complaint.
- Existing timer pattern: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ExpiredReservationCleanupTimerFunction.cs` (TimerTrigger). Stripe client: `StripeBillingService`.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge). Reconciliation is READ-ONLY against Stripe (list charges/payment-intents/payouts) — it must never create or refund anything.

## Changes required
1. A reconciliation service + scheduled Functions timer (e.g. daily) that lists Stripe paid charges/payment-intents for a window and compares against `RewriteCredit` grants (match on `StripePaymentIntentId`).
2. Detect and report: (a) **paid-but-no-grant** (missed webhook), (b) **grant-but-no-payment** (anomaly), (c) amount/currency mismatch. Emit a structured report + alert (PAY-02 channel / PAY-19 if available); surface a summary in admin stats.
3. Optionally allow an admin to trigger a manual re-process of a missed event by event id (reusing the existing idempotent `StripeEventService`), but DO NOT auto-grant from the reconciler.

## Acceptance (machine-checkable)
- [ ] Unit tests: seeded fake Stripe data + ledger → reconciler flags paid-but-no-grant and amount-mismatch; a clean dataset reports zero discrepancies.
- [ ] The job is read-only against Stripe (asserted by test using a fake client that fails on any write).
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT auto-create grants or refunds from the reconciler. Do NOT make write calls to Stripe. Do NOT touch `main`.
