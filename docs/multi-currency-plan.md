# Multi-Currency Support Specification

## Context

`PAY-30` covers the design for taking the payment module from NZD-only checkout to configured multi-currency checkout. The current production payment model is credit packs, with `pro_api` as the only subscription SKU.

Source inputs:

- `plans/payment-issues/PAY-30-multi-currency.md`
- `plans/payment-module-audit.md`
- `plans/payment-issues/PAY-22-reconciliation.md`
- `plans/payment-issues/PAY-29-accounting-export.md`
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs`
- `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs`
- `app/pricing/page.tsx`

## Goals

- Keep existing NZD checkout behavior unchanged.
- Support future currencies by adding new Stripe Prices per SKU and currency.
- Resolve checkout price IDs from `sku` plus selected currency, with NZD as the compatibility fallback.
- Preserve one product entitlement model: pack SKUs grant their fixed rewrite count, and `pro_api` remains a subscription that grants status-driven period quota.
- Make payment reporting and reconciliation currency-aware.

## Non-Goals

- Do not create or edit Stripe Prices from application code.
- Do not change existing NZD price IDs or environment variable names.
- Do not hardcode exchange rates. Stripe Price objects define the charge amount for each currency.
- Do not combine mixed-currency revenue into one converted total unless a later accounting task adds an approved conversion source.
- Do not initiate a real or live charge as part of implementation or verification.

## Current System

Backend checkout resolution is currently hardcoded to NZD environment keys in `StripeBillingService.SkuDefinitions`:

| SKU | Mode | Rewrites | Current price env key |
| --- | --- | ---: | --- |
| `quick_pack` | `payment` | 10 | `STRIPE_PRICE_QUICK_PACK_NZD` |
| `value_pack` | `payment` | 30 | `STRIPE_PRICE_VALUE_PACK_NZD` |
| `pro_api` | `subscription` | 90 | `STRIPE_PRICE_PRO_API_MONTHLY_NZD` |
| `focus_pack` | `payment` | 20 | `STRIPE_PRICE_FOCUS_PACK_NZD` |

The frontend pricing page uses the same NZD-only key names to decide whether paid calls to action are configured.

Payment persistence already has the important currency column:

- `RewriteCredit.StripeAmountTotal` stores the charged amount in Stripe minor units.
- `RewriteCredit.StripeCurrency` stores the charged currency reported by Stripe.
- `RewriteCredit.StripeSku` and `RewriteCredit.StripePaymentIntentId` link the payment back to the internal SKU and Stripe payment.

That means historical credits are already value-stable: entitlement is based on stored granted rewrites, while revenue reporting can use the stored amount and currency from the actual charge.

## Proposed Architecture

### Stripe Price Setup

Each supported currency needs a separate Stripe Price for each paid SKU. Existing NZD prices stay untouched. For a first additional currency, the owner creates new Stripe Prices in the Stripe dashboard or Stripe API outside the application, then supplies only the resulting price IDs as runtime secrets or settings.

Use environment key names that make currency explicit:

| SKU | NZD key | Example additional-currency key pattern |
| --- | --- | --- |
| `quick_pack` | `STRIPE_PRICE_QUICK_PACK_NZD` | `STRIPE_PRICE_QUICK_PACK_USD` |
| `value_pack` | `STRIPE_PRICE_VALUE_PACK_NZD` | `STRIPE_PRICE_VALUE_PACK_USD` |
| `pro_api` | `STRIPE_PRICE_PRO_API_MONTHLY_NZD` | `STRIPE_PRICE_PRO_API_MONTHLY_USD` |
| `focus_pack` | `STRIPE_PRICE_FOCUS_PACK_NZD` | `STRIPE_PRICE_FOCUS_PACK_USD` |

The application should treat these as key names only. No tracked file should contain real price ID values.

### SKU To Currency Price Mapping

The backend should evolve from this shape:

```csharp
sku -> CheckoutSkuDefinition(PriceEnvVar, Mode, Rewrites)
```

to this shape:

```csharp
sku -> CheckoutSkuDefinition(Mode, Rewrites, PriceEnvByCurrency)
```

where `PriceEnvByCurrency` is keyed by normalized ISO currency code such as `nzd` or `usd`.

Example logical mapping:

```text
quick_pack:
  mode: payment
  rewrites: 10
  prices:
    nzd: STRIPE_PRICE_QUICK_PACK_NZD
    usd: STRIPE_PRICE_QUICK_PACK_USD

value_pack:
  mode: payment
  rewrites: 30
  prices:
    nzd: STRIPE_PRICE_VALUE_PACK_NZD
    usd: STRIPE_PRICE_VALUE_PACK_USD

pro_api:
  mode: subscription
  rewrites: 90
  prices:
    nzd: STRIPE_PRICE_PRO_API_MONTHLY_NZD
    usd: STRIPE_PRICE_PRO_API_MONTHLY_USD

focus_pack:
  mode: payment
  rewrites: 20
  prices:
    nzd: STRIPE_PRICE_FOCUS_PACK_NZD
    usd: STRIPE_PRICE_FOCUS_PACK_USD
```

Price resolution should normalize the requested currency to lowercase, look up that currency under the SKU, and fall back to `nzd` when the requested currency is missing, unsupported, or not configured.

The fallback rule keeps old clients working because a request with no currency still resolves the existing NZD price.

### Currency Selection

Recommended product behavior:

1. Default to NZD.
2. If multi-currency is enabled, show a small explicit currency selector near pricing and checkout actions.
3. Optionally preselect a currency from Cloudflare geo headers, but treat geo only as a suggestion.
4. Send the selected currency with the checkout request, for example `{ sku, currency }`.
5. The backend validates the currency against configured currency keys for that SKU and falls back to NZD when needed.

Explicit user choice should win over geo. Geo-based preselection can be wrong for travelers, VPN users, expatriates, or buyers who want a different billing currency.

## Data Model

No schema change is required for the first multi-currency implementation.

`RewriteCredit` already records the data needed for currency-aware ledgers:

| Field | Multi-currency role |
| --- | --- |
| `StripeSku` | Groups revenue and credits by internal SKU. |
| `StripeAmountTotal` | Stores the charged amount in the currency minor unit. |
| `StripeCurrency` | Stores the actual Stripe charge currency. |
| `AmountGranted` | Keeps entitlement independent from price and currency. |
| `StripePaymentIntentId` | Lets reconciliation match Stripe payments to internal grants. |

Future reporting code should normalize null or blank `StripeCurrency` carefully. Legacy rows without a currency should be reported as `unknown`, not silently counted as NZD.

## API and Job Contracts

### Checkout Request

Current request:

```json
{ "sku": "quick_pack" }
```

Future request:

```json
{ "sku": "quick_pack", "currency": "usd" }
```

Compatibility:

- `currency` is optional.
- Missing, blank, malformed, unsupported, or unconfigured currency resolves to NZD.
- Unknown SKU behavior remains unchanged: the HTTP boundary rejects unknown SKUs before checkout creation.

### Checkout Metadata

Existing metadata should remain:

- `externalAuthUserId`
- `sku`
- `rewrites`

The selected currency may be added as non-authoritative metadata for diagnostics, but Stripe's session and payment objects remain the source of truth for the charged currency. Webhook processing should continue persisting the currency Stripe returns.

### Frontend Pricing Configuration

The frontend should eventually check configuration per SKU and selected currency. A SKU can be shown as available for a currency only when its selected-currency price key is configured, or when the product decision is to fall back and clearly charge in NZD.

If a currency selector is visible, the UI copy should make the actual checkout currency clear before the user enters Stripe Checkout.

## State and Error Handling

- Price IDs are runtime configuration and should be read inside the checkout handler path, not validated at module import time.
- If the requested currency key is absent, use the NZD key for that SKU.
- If both the requested currency key and the NZD fallback key are missing, fail checkout with the existing missing-configuration error.
- Do not retry with a different currency after Stripe Checkout session creation fails. A failed Stripe API call should remain a failed checkout attempt.
- Refunds should continue using the stored payment currency. Existing admin refund logic already rejects mismatched requested currency.

## Reporting and Reconciliation

PAY-22 reconciliation must compare both amount and currency:

- Match Stripe payment or charge to `RewriteCredit.StripePaymentIntentId`.
- Flag paid-without-grant.
- Flag grant-without-payment.
- Flag amount mismatch.
- Flag currency mismatch.

PAY-29 accounting export must include `currency` on every revenue row and subtotal by currency:

```text
date,user_ref,sku,amount_minor,currency,payment_intent,receipt_url
```

Do not add mixed-currency revenue totals together. If a future export needs a home-currency total, use Stripe balance transaction exchange data or an approved accounting source, not application hardcoded rates.

Admin stats and dashboards should group payment revenue by `(currency, sku)` or show one table per currency. Credit consumption can remain rewrite-count based because credits represent entitlement, not accounting currency.

## Security and Privacy

- Do not store Stripe price ID values in tracked docs.
- Do not log secret values, full payment identifiers beyond the existing operational IDs, or customer card data.
- Keep Stripe Checkout as the payment collection surface.
- Keep live-charge actions owner-only and outside automated verification.
- Keep `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_WEBHOOK_SECRET`, and legacy `STRIPE_PRICE_ID` wiring unchanged.

## Rollout Plan

1. Owner chooses the first additional currency and creates a new Stripe Price per SKU in that currency.
2. Add runtime configuration keys for the new prices in local, Worker, Azure Functions, and CI secret stores.
3. Generalize backend SKU price resolution to accept optional currency and fall back to NZD.
4. Add backend unit tests for two currencies and fallback behavior.
5. Add frontend currency selector or a server-selected default only after backend fallback tests pass.
6. Run sandbox checkout smoke tests only. Do not run a real charge.
7. Update PAY-22 and PAY-29 implementations to group and reconcile by currency before relying on multi-currency financial reports.

## Verification Plan

Design-only acceptance for PAY-30:

- `docs/multi-currency-plan.md` exists and documents mapping, selection, and reporting design.
- `cd backend-dotnet && dotnet test` passes.

If optional implementation is later approved:

- Unit test: `quick_pack` resolves NZD when no currency is supplied.
- Unit test: `quick_pack` resolves the additional currency when configured.
- Unit test: unsupported or unconfigured currency falls back to NZD.
- Unit test: existing NZD behavior and metadata are unchanged.
- Frontend unit test: selected currency is included in checkout request when the selector is enabled.

## Architecture Cost Review

- Goal: Add commercial coverage for buyers outside New Zealand without changing the core payment architecture.
- Usage assumption: Low-volume MVP checkout traffic; Stripe-hosted Checkout remains the payment collection path.
- Runtime requirements: Runtime configuration lookup, Stripe Checkout session creation, webhook persistence, and currency-aware reports.
- Options compared: Keep NZD-only; add separate Stripe Prices per SKU and currency; build application-side exchange-rate conversion.
- Fixed monthly cost risks: The recommended path adds no new always-on compute, database, queue, or cloud service.
- Variable usage cost risks: Additional currencies may change Stripe processing, settlement, tax, and accounting costs. Exact fees should be checked in Stripe for the selected currency and buyer geography before launch.
- Recommended option: Separate Stripe Price per SKU per currency, selected by user choice with optional geo preselection, and NZD fallback.
- Rejected options: Application-side exchange-rate conversion and automatic price creation from the app, because both add financial correctness and operations risk without improving checkout reliability.
- Approval gates: Owner must decide currencies and create/configure new Stripe Prices before code enables a non-NZD checkout path.
- Verification needed: Sandbox checkout, webhook persistence of `StripeCurrency`, PAY-22 reconciliation grouped by currency, and PAY-29 export grouped by currency.
- Limitations: No exact Stripe fee comparison was performed because PAY-30 does not select a concrete currency or create paid resources.

## Open Questions

- Which currency should be first after NZD?
- Should the UI show a persistent manual selector, geo-preselect plus selector, or only account-level currency selection?
- Should `focus_pack` remain hidden in all currencies unless `SHOW_FOCUS_PACK` is enabled?
- Should price display copy show tax-inclusive notes by buyer region, or wait for the PAY-20 tax implementation?
