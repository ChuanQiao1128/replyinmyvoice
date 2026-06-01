# Price Change Playbook

Use this playbook when changing a one-time rewrite pack price, changing the number of rewrites in a pack, or grandfathering buyers during a transition. It is a procedure only; it does not approve any price change.

## Rules

- Never edit, reuse for a different offer, archive, or delete a Stripe price that has been used by Checkout.
- A new public price or pack size means creating a new Stripe price id in Stripe test mode first, then live mode only when the owner approves live setup.
- Keep secret values out of source control. Store only env var names in docs and code.
- Keep `STRIPE_PRICE_ID`, `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, and webhook secret wiring unchanged unless a separate approved issue requires it.

## Changing A Pack Price

1. Create a new Stripe price for the existing product, with the new amount and currency.
2. Leave the old Stripe price active until all in-flight Checkout Sessions have expired and any grandfather window has closed.
3. Update the SKU price env mapping to the new price id:
   - `quick_pack` -> `STRIPE_PRICE_QUICK_PACK_NZD`
   - `value_pack` -> `STRIPE_PRICE_VALUE_PACK_NZD`
   - `focus_pack` -> `STRIPE_PRICE_FOCUS_PACK_NZD`
   - `pro_api` -> `STRIPE_PRICE_PRO_API_MONTHLY_NZD`
4. Deploy the env setting change through the normal release path. Do not commit the price id value.
5. Verify a new Checkout Session points at the new Stripe price id in Stripe test mode.

Historical grants are unaffected because `RewriteCredit` stores the purchased rewrite count and Stripe amount at grant time. In-flight Checkout Sessions are also unaffected: Stripe charges the price attached to the session when it was created, and the webhook grants from that session metadata.

## Grandfathering

Grandfathering means keeping the old Stripe price id available for a defined window. Do not alternate the public SKU env var between old and new ids.

Safe options:

- Keep the old Checkout Session links alive until their normal Stripe expiry.
- For support-led grandfathering, create a separate, explicit old-offer path or support procedure that uses the old price id for eligible users during the window.
- Record the start date, end date, eligible audience, old price id name, and new price id name in an internal operations note without secret values.

At the end of the window, stop creating new sessions for the old price id. Do not delete historical prices from Stripe.

## Changing Pack Size

Pack size is the number of rewrites granted by the SKU, not the Stripe amount.

1. Create a new Stripe price id if the size change is tied to a public offer change.
2. Update `StripeBillingService.SkuDefinitions` so new Checkout Sessions write the new `metadata["rewrites"]` value.
3. Update the matching `STRIPE_PRICE_*` env var to the new price id when the amount changes.
4. Verify a test Checkout Session contains the expected `sku` and `rewrites` metadata.
5. Verify `checkout.session.completed` persists:
   - `RewriteCredit.AmountGranted`
   - `RewriteCredit.OriginalAmountGranted`
   - `RewriteCredit.StripeSku`
   - `RewriteCredit.StripeAmountTotal`

Do not rewrite existing `RewriteCredit` rows when changing pack size. Old credits keep their existing remaining balance. Partial refunds use the persisted original grant count, so later SKU-size changes do not recalculate old purchases from the current SKU map.

## Operational Checks

- Run `cd backend-dotnet && dotnet test`.
- Confirm no tracked file contains Stripe secret values.
- Confirm no live charge is initiated by automation.
- Keep old Stripe prices visible in Stripe long enough for support, refunds, and audit history.
