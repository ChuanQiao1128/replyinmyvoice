# PAY-20: GST / tax — Stripe automatic_tax wiring + turnover tracker toward NZ$60k

**Priority:** P1 · **Owner:** Codex (code) + Owner (IRD registration, enable Stripe Tax) · **Skill:** data-module-review + cloud-architecture-cost-review · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- No tax code exists (no `automatic_tax`/Stripe Tax). Pricing-spec sets prices GST-inclusive in anticipation; not GST-registered (pre-NZ$60k); no turnover tracking toward the IRD NZ$60,000 threshold.
- Checkout: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs` (`CreateCheckoutSessionUrlAsync`). Revenue is derivable from `RewriteCredit` (Source=PURCHASE, `StripeAmountTotal`/`Currency`) and `AdminService.GetStatsAsync`.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, additive/nullable migrations, never push to main, never initiate a real charge). Enabling Stripe Tax in the dashboard + IRD GST registration are **owner-only** dashboard/legal actions — code must be ready behind a flag, not assume it is on.

## Changes required
1. Add an env-gated `automatic_tax` option to checkout session creation (e.g. `STRIPE_AUTOMATIC_TAX_ENABLED`, default **off**); when on, set `AutomaticTax { Enabled = true }` and the required customer address/tax-id collection. Default-off must not change current behavior.
2. **Turnover tracker:** a service/report that sums gross paid revenue over a rolling 12-month window and raises a structured warning (and a notification via PAY-19 if available) when approaching NZ$60k (e.g. at 80%). Surface in admin stats.
3. Docs: `docs/gst-tax-playbook.md` — owner steps to register for GST + enable Stripe Tax + switch the flag on; note that prices are already GST-inclusive.

## Acceptance (machine-checkable)
- [ ] Checkout supports `automatic_tax` behind an env flag; with the flag off, existing checkout tests still pass unchanged.
- [ ] Unit test: turnover tracker computes the 12-month gross and fires the threshold warning at the configured fraction.
- [ ] `docs/gst-tax-playbook.md` exists with the owner checklist.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT enable Stripe Tax by default or assume registration. Do NOT change pack prices. Do NOT touch `main`.
