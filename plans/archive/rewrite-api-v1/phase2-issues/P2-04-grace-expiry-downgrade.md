# P2-04: Scheduled payment-grace-expiry downgrade timer + subscription terminalize

**Tier:** 1 (prereq, merged into base) · **Owner:** Codex · **Depends on:** P2-01

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §D (BILL-04a/b corrected).
- **The bug:** `invoice.payment_failed` sets `SubscriptionStatus=PastDue` + a 7-day grace and KEEPS paid quota; the only thing that downgrades is `StripeEventService.ProcessExpiredPaymentGraceAsync()` (`StripeEventService.cs:172-207`) which flips `PastDue→Inactive` past grace — **but NOTHING schedules it**, so failed renewals keep paid quota forever.
- Existing timer style: see `StripeReconciliationService` / any `[TimerTrigger]` Azure Function under `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/`. Subscription cancel helper: `StripeBillingService` (`Infrastructure/Services/StripeBillingService.cs`) — add a cancel method if none exists.

## Changes required
1. **New Timer Function** `PaymentGraceExpiryFunction` (daily fixed cron, e.g. `"0 0 14 * * *"` UTC) that calls `ProcessExpiredPaymentGraceAsync()`. Keep the function thin; all logic stays in the service so it is unit-testable.
2. **On downgrade** (inside `ProcessExpiredPaymentGraceAsync`, when a user is flipped to `Inactive`): also **cancel the user's Stripe subscription** via `StripeBillingService` so Stripe stops retrying the failed renewal. Cancel is NOT a charge — sandbox-safe. Guard with a fake-able Stripe client so tests don't hit the network.
3. **Terminal dunning:** in the `customer.subscription.updated` handler, treat Stripe status `unpaid` (and `canceled`) as terminal → `Inactive` + clear grace (if not already handled).

## Acceptance (machine-checkable)
- [ ] xUnit: a `PastDue` user whose `PaymentGraceEndsAt` is in the past → after `ProcessExpiredPaymentGraceAsync`, status `Inactive` and the account summary quota falls to the free baseline; an `Active` user and a `PastDue`-still-in-grace user are NOT changed; the fake Stripe client's cancel was invoked exactly for the downgraded user.
- [ ] `cd backend-dotnet && dotnet test` green; `dotnet build` green.

## Do NOT
- Do NOT initiate any charge. Do NOT touch `LAUNCH_CONFIRMED`, live price IDs, or `*_WEBHOOK_SECRET`.
- Do NOT change the grace WINDOW length or the `invoice.payment_failed`/`_succeeded` handlers' existing behavior — only add the scheduler + cancel-on-downgrade + unpaid-terminal.
