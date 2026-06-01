# PAY-23: Failed-payment dunning + renewal-retry notifications

**Priority:** P1 · **Owner:** Codex · **Skill:** state-machine-modeling + resilience-test-generation · **Depends on:** PAY-01 (#378, invoice.payment_failed handler), PAY-19 (notification infra)

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Pro·API is the only subscription. PAY-01 adds the `invoice.payment_failed` handler + status downgrade + grace decision. Stripe has native Smart Retries + its own failed-payment emails; we should lean on those and add only what Stripe can't: our grace-window state + a clear customer notification + optional in-app banner.
- No notification infra today (PAY-19 provides it).

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge).

## Changes required
1. On `invoice.payment_failed` (from PAY-01): record the failure, enter the grace window, and send a "payment failed — update your card" notification (PAY-19) with a link to the Stripe billing portal. Idempotent per event.
2. On grace-window expiry (or terminal `unpaid`/`canceled` via subscription.updated): downgrade per PAY-01 and send a "subscription paused" notification.
3. On `invoice.payment_succeeded` after a failure (recovery): clear the grace state + optional "you're back" notification.
4. Document the dunning timeline + which emails are Stripe-native vs ours in `docs/support-runbook.md`.

## Acceptance (machine-checkable)
- [ ] Unit tests: payment_failed → grace state + notification sent (fake provider); grace-expiry → downgraded + notified; recovery → grace cleared. All idempotent on replay.
- [ ] No real email/charge in tests. `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT duplicate Stripe-native dunning emails if Stripe is configured to send them — coordinate, document. Do NOT touch `main`.
