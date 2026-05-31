# PAY-26: Dispute / chargeback operations runbook (+ evidence context)

**Priority:** P2 · **Owner:** Codex (doc + optional code) + Owner (submits evidence) · **Skill:** state-machine-modeling · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Code already reacts to `charge.dispute.created/closed` (revokes unused credits in `StripeEventService.RevokeDisputedChargeCreditsAsync`), but there is **no operational runbook**: how to submit evidence, response SLA, win/loss handling, repeat-disputer policy.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge).

## Changes required
1. `docs/dispute-chargeback-runbook.md`: step-by-step for the owner — where disputes appear in Stripe, the response deadline, what evidence to submit (purchase record, usage logs, terms acceptance), win/loss outcomes, and a repeat-disputer policy (e.g. suspend via the existing admin suspension).
2. Optional (only if cheap): an admin read view that, given a `payment_intent`/charge, returns the evidence context (user purchase record + usage history) to paste into Stripe — reuse `AdminService.GetUserDetailAsync`.

## Acceptance (machine-checkable)
- [ ] `docs/dispute-chargeback-runbook.md` exists with deadline + evidence checklist + repeat-disputer policy.
- [ ] If the optional admin view is built: a test returns the evidence context for a seeded disputed payment.
- [ ] Any code change: `dotnet test` / `npm run test` green.

## Do NOT
- Do NOT auto-submit dispute evidence. Do NOT touch `main`.
