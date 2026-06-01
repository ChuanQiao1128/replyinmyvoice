# PAY-27: Customer self-serve refund / billing-support request channel

**Priority:** P2 · **Owner:** Codex · **Skill:** ui-browser-testing + data-module-review · **Depends on:** PAY-07 (#384, admin UI), PAY-19 (notifications)

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Refunds are owner-API-only (PAY-07 adds an admin UI). Customers have no structured channel to say "I was charged twice / wrong amount / I want a refund" — only the `info@timeawake.co.nz` email (support-runbook M7-004).

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, additive/nullable migrations, never push to main, never initiate a real charge). This channel must NOT auto-refund — the owner decides in the admin UI.

## Changes required
1. A new entity (e.g. `BillingSupportRequest`: userId, type [refund/billing-question], relatedPaymentIntentId nullable, message, status [open/resolved], timestamps) + additive migration.
2. An authenticated, same-origin proxy endpoint to create a request from the account area (a simple form: pick a purchase, reason, message).
3. The request appears in the PAY-07 admin UI queue (list + mark resolved + jump to the refund action). On submit, send a "we received your request" confirmation (PAY-19).
4. Caller-scoped reads (a user sees only their own requests).

## Acceptance (machine-checkable)
- [ ] Submitting a request creates a caller-scoped `BillingSupportRequest`; cross-user read returns empty.
- [ ] The request is visible in the admin queue; admin can mark resolved.
- [ ] Confirmation notification sent on submit (fake provider).
- [ ] `dotnet test` + `npm run test` green; banned-term grep clean.

## Do NOT
- Do NOT auto-refund from this channel. Do NOT send real email in tests. Do NOT touch `main`.
