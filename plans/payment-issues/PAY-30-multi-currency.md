# PAY-30: Multi-currency support (design + optional first non-NZD currency)

**Priority:** P2 (optional — owner decides scope) · **Owner:** Codex (design) + Owner (decide currencies, create Stripe prices) · **Skill:** cloud-architecture-cost-review · **Depends on:** PAY-25 (price versioning) recommended

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Everything is hard-NZD (`STRIPE_PRICE_*_NZD` env vars; SKU map is NZD-only). Non-NZ buyers can only pay in NZD. The pricing-spec treats this as non-goal-adjacent, but the owner asked for full commercial coverage.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge). Adding currencies requires NEW Stripe prices (owner action) — do not edit existing ones.

## Changes required
1. **Design doc** `docs/multi-currency-plan.md`: how to add a currency — per-currency Stripe price ids, SKU→(currency→priceId) mapping, currency selection (by user choice or geo), how `RewriteCredit.StripeCurrency` already records the charged currency, and reporting/reconciliation implications (PAY-22/PAY-29 group by currency).
2. **Optional implementation** (only if owner opts in within the issue): generalize the SKU→price resolution to take a currency, add one additional currency end-to-end behind config, with the price ids supplied as env (key names only).

## Acceptance (machine-checkable)
- [ ] `docs/multi-currency-plan.md` exists with the mapping + selection + reporting design.
- [ ] If implemented: SKU→price resolution accepts a currency and falls back to NZD; a unit test covers resolution for two currencies. Existing NZD behavior unchanged.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT change existing NZD prices. Do NOT hardcode FX rates (let Stripe price per currency). Do NOT touch `main`.
