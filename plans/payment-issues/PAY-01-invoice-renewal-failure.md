# PAY-01: Handle subscription renewal failure (invoice.payment_failed) + verify status mapping + grace policy

**Priority:** P0 · **Owner:** Codex · **Skill:** state-machine-modeling + resilience-test-generation · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Model: credit-packs; Pro·API is the only `mode=subscription` SKU (90 rewrites/billing period).
- Paid quota today is **status-driven**: `AccountService.GetUsagePlan` returns the 90-rewrite paid plan whenever `SubscriptionStatus ∈ {Active, Trialing, Testing}` — it does NOT depend on a credit grant.
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs` (`GetUsagePlan`, ~line 292)
- Subscription status is set only from `customer.subscription.*` events via `SyncSubscriptionObjectAsync` → `MapSubscriptionStatus`.
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs` (`SyncEntitlementAsync` ~314, `SyncSubscriptionObjectAsync` ~353, `MapSubscriptionStatus`)
- **Defect:** there is NO `invoice.*` handler anywhere (verified by grep). A failed renewal relies entirely on Stripe later emitting `customer.subscription.updated` with a non-active status, AND on `MapSubscriptionStatus` mapping `past_due`/`unpaid`/`incomplete`/`incomplete_expired`/`paused` to a non-active value. That mapping is unverified, and there is no explicit grace-period decision.

## Constraints (AGENTS.md)
- Banned terms (CI grep on app/components/public/lib): `humanizer|bypass|undetect|detector|evade`. Do not introduce them anywhere.
- No secret values in tracked files; validate env at runtime in the handler.
- Automation must NEVER initiate a real Stripe charge.
- Do NOT push to `main` (a merge to main auto-deploys prod AND runs `dotnet ef database update` on LIVE Azure SQL). Work on a feature branch; open a PR.
- This touches subscription/quota lifecycle — model the states before coding (state-machine-modeling).

## Changes required
1. **Audit + fix `MapSubscriptionStatus`** so every non-paying Stripe status maps to a status for which `GetUsagePlan` returns the free plan. Required mappings:
   - `active`, `trialing` → `Active`/`Trialing` (paid)
   - `past_due`, `unpaid`, `incomplete`, `incomplete_expired`, `canceled`, `paused`, `incomplete` → a non-paid status (so `GetUsagePlan` returns `free:lifetime`, limit 3). Default policy: **`past_due`/`unpaid` immediately downgrade to free** (packs are the primary product; do not lend paid quota on a failed charge). Document the chosen policy in a code comment + in `docs/rewrite-packs-pricing-spec.md`.
2. **Add an `invoice.payment_failed` handler** in `SyncEntitlementAsync`: locate the user by `customer` (and/or `subscription`), idempotently record the failure (reuse the `StripeEvent` dedup machinery), and emit a structured log line with the correlation id (this is the hook PAY-02 alerts on). Do not send email here (notifications are out of scope / PAY-09).
3. (Optional, only if cheap) Add an `invoice.payment_succeeded` no-op/confirm handler that is idempotent, so the renewal happy-path is explicit rather than implicit-via-subscription.updated. If it complicates the change, skip and rely on `customer.subscription.updated` (note the decision in the PR).
4. Update `docs/rewrite-packs-pricing-spec.md` (resolve the open grace-period question) and note in `docs/stripe-live-mode-cutover.md` that the LIVE webhook endpoint must subscribe to `invoice.payment_failed` (owner dashboard action).

## Acceptance (machine-checkable)
- [ ] New xUnit test: `customer.subscription.updated` with `status=past_due` (and one with `unpaid`) → `user.SubscriptionStatus` is non-paid AND `AccountService.GetUsagePlan(user)` returns `("free","free:lifetime",3)`.
- [ ] New xUnit test: `invoice.payment_failed` event is processed idempotently (second delivery → no duplicate side effect; `StripeEvent` row count stays 1).
- [ ] Existing payment tests still pass: `cd backend-dotnet && dotnet test` is green.
- [ ] `docs/rewrite-packs-pricing-spec.md` states the grace-period policy explicitly.

## Do NOT
- Do NOT change pack pricing, the credit-grant path (`SyncCheckoutSessionAsync`), or refund logic.
- Do NOT add real email/SMS sending (that's PAY-09).
- Do NOT touch `main`, deploy, or run live Stripe calls.
