# PAY-32: Stripe webhook delivery monitoring + replay runbook

**Priority:** P1 · **Owner:** Codex (code + doc) + Owner (Stripe dashboard alerts) · **Skill:** resilience-test-generation · **Depends on:** PAY-02 (observability) recommended

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Webhook processing is idempotent (`StripeEvent` dedup table with `Status` Processing/Processed/**Failed** + attempt count). SO-R11 added a readiness/health endpoint for Service Bus + outbox + stuck reservations. But there is **no alerting on webhook processing/delivery failures** and **no documented procedure to replay missed events** from the Stripe dashboard.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge).

## Changes required
1. Expose webhook health metrics: count of `StripeEvent` in `Failed` status and the age of the most recent processed event (extend the existing readiness/health endpoint). Alert (PAY-02 channel) when failures exceed a threshold or no events arrive for an unexpected window.
2. `docs/webhook-ops-runbook.md`: how to inspect failed deliveries in the Stripe dashboard, how to resend/replay an event, and confirmation that replay is safe because processing is idempotent (`StripeEvent` PK + unique `RewriteCredit.StripeEventId`).
3. (Optional) admin endpoint to list recent `Failed` StripeEvents with their last error.

## Acceptance (machine-checkable)
- [ ] Health/metrics surface `Failed`-event count + last-processed age; a test seeds a failed event and asserts it is reported.
- [ ] `docs/webhook-ops-runbook.md` exists with the dashboard replay procedure + idempotency note.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT disable signature verification or idempotency to "simplify" replay. Do NOT touch `main`.
