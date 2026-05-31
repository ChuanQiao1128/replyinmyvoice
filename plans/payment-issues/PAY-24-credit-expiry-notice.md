# PAY-24: Credit-expiry reminder notifications

**Priority:** P1 · **Owner:** Codex · **Skill:** resilience-test-generation · **Depends on:** PAY-19 (notification infra)

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Pack credits expire +90d (campaign +14d, FIFO-by-expiry). Expiry is computed on read (`AccountService.CalculateExpiresInDays`), but users are **never warned** before credits lapse. `RewriteCredit` has `ExpiresAt`, `AmountGranted`, `AmountConsumed`.
- Timer pattern: `ExpiredReservationCleanupTimerFunction.cs`. Notifications via PAY-19.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge).

## Changes required
1. A scheduled job that finds credits with remaining balance (`AmountGranted > AmountConsumed`) expiring within a threshold (e.g. 7 days) and sends a "your credits expire soon" notification (PAY-19).
2. **Idempotent:** each credit is reminded at most once per threshold — persist a "reminder sent" marker (e.g. a nullable `ExpiryReminderSentAt` on `RewriteCredit`, additive nullable migration) so reruns don't re-notify.

## Acceptance (machine-checkable)
- [ ] Unit test: a credit with remaining balance expiring within the window → one notification (fake provider); rerun → no second notification; an already-consumed/expired credit → no notification.
- [ ] Migration is additive + nullable only.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT notify on zero-remaining or already-expired credits. Do NOT send real email in tests. Do NOT touch `main`.
