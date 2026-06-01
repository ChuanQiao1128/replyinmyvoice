# GST / Stripe Tax Playbook

Reply In My Voice prices are already set as GST-inclusive. Do not change pack prices as part of GST registration or Stripe Tax enablement.

This playbook is operational guidance, not tax advice. The owner should confirm timing and filing setup with IRD or a qualified tax adviser.

## Owner Checklist

1. Monitor the admin GST turnover report before each monthly finance review.
2. Register for GST with IRD before the business reaches the NZ$60,000 requirement, or when it expects to reach that amount in the next 12 months.
3. Choose the GST accounting basis and filing frequency during IRD registration.
4. In the Stripe Dashboard, enable Stripe Tax and complete the business origin, registration, default tax code, and tax behaviour settings.
5. Confirm Stripe products/prices remain GST-inclusive for New Zealand customers.
6. Run one Stripe test-mode checkout after enabling Stripe Tax in the dashboard.
7. Set `STRIPE_AUTOMATIC_TAX_ENABLED=true` only after IRD registration and Stripe Tax dashboard setup are complete.
8. Verify a test checkout collects the billing address and shows the expected tax treatment.
9. Keep `STRIPE_AUTOMATIC_TAX_ENABLED` unset or `false` until all owner steps are complete.

## Runtime Flags

- `STRIPE_AUTOMATIC_TAX_ENABLED`: default off. When `true`, checkout sends `automatic_tax[enabled]=true`, requires billing address collection, lets Checkout save the collected address to the Stripe customer, and enables tax ID collection.
- `GST_TURNOVER_THRESHOLD_NZD`: optional. Defaults to `60000`.
- `GST_TURNOVER_WARNING_FRACTION`: optional. Defaults to `0.80`.
- `GST_TURNOVER_NOTIFICATION_EMAIL`: optional. If set and PAY-19 notifications are configured, the backend sends a turnover warning notification when the configured warning level is reached.

## Turnover Report

The admin stats response includes `gstTurnover`.

The report sums gross paid NZD purchase revenue from `RewriteCredit` rows where `Source = PURCHASE`, `StripeAmountTotal` is present, and `StripeCurrency = nzd`, within the rolling 12-month window ending at the report time.

This is a gross revenue tracker, not a reconciliation ledger. It does not subtract refunds or convert non-NZD payments.

## References

- IRD GST registration: https://www.ird.govt.nz/registering-for-gst
- IRD GST basis/frequency: https://www.ird.govt.nz/gst/registering-for-gst/which-gst-accounting-basis-and-filing-frequency-should-i-use
- Stripe automatic tax in Checkout: https://docs.stripe.com/payments/checkout/automatic_taxes
- Stripe tax ID collection in Checkout: https://docs.stripe.com/tax/checkout/tax-ids
