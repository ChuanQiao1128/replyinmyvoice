# PAY-25: Price change / versioning strategy (protect historical grants + in-flight checkouts)

**Priority:** P2 · **Owner:** Codex · **Skill:** data-module-review · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- SKU→price mapping: `StripeBillingService.SkuDefinitions` → `STRIPE_PRICE_*` env vars. Granted credits store the absolute count + `StripeSku`/`StripeAmountTotal` on `RewriteCredit`, so historical grants are already value-stable.
- There is **no documented strategy** for changing a pack's price or size without breaking historical grants, in-flight checkouts, or grandfathering existing buyers.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, additive/nullable migrations, never push to main, never initiate a real charge). Per AGENTS.md, do NOT change the owner's existing prices.

## Changes required
1. `docs/price-change-playbook.md`: the safe procedure — new price = a NEW Stripe price id (never edit/delete an old one); update the SKU→price env mapping to the new id; old grants are unaffected (count is persisted); in-flight checkouts use whatever price they were created with. How to grandfather (keep old price id reachable for a window) and how to change pack SIZE (rewrites granted) without touching historical credits.
2. Lightweight code support: ensure the granted-rewrites count is always resolved from the checkout metadata at purchase time (already is) and persisted — add a regression test proving a later SKU/size change does NOT alter the remaining balance of an old credit.

## Acceptance (machine-checkable)
- [ ] `docs/price-change-playbook.md` exists with the new-price-id procedure + grandfather + size-change guidance.
- [ ] Regression test: change the SKU definition's rewrite count, re-read an existing credit granted under the old size → its balance is unchanged.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT mutate or delete existing Stripe prices. Do NOT touch `main`.
