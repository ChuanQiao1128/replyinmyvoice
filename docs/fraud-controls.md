# Purchase-Side Fraud Controls

This project uses Stripe Checkout for purchases and subscription sign-up. The application should surface risk signals for owner review, while Stripe Radar remains the owner-managed control plane in the Stripe Dashboard.

## Application Controls

- Checkout session creation is rate-limited per authenticated external user id before a Stripe session is created. The current backend limit is 5 checkout sessions per 10-minute window. Requests over the limit return HTTP 429 with `Retry-After`.
- Admin stats include refund-review aggregates from `AdminAuditLog` entries with action `refund`. Users are counted for review when their recorded refunds reach either 3 refunds or 2,500 minor currency units in total. This is informational only.
- The app does not auto-suspend users, auto-refund payments, or block purchases based on these refund-review stats.

## Stripe Radar Recommendations

Enable and tune these rules in the Stripe Dashboard using test mode first:

- Review or block high-velocity payment attempts from the same card, IP address, email, or customer.
- Review transactions with elevated Radar risk scores or suspicious card verification results.
- Review multiple failed payment attempts before a successful checkout for the same customer or card fingerprint.
- Review repeated refunds, disputes, or failed payments associated with the same customer.

Keep rules in review mode until the owner has checked real test-mode events and confirmed that valid customers are not being interrupted.

## Metadata For Review

Checkout metadata includes `externalAuthUserId`, `sku`, `checkoutMode`, and `rewrites`. For subscription sessions, the same metadata is also attached to the subscription data. The `externalAuthUserId` value lets the owner connect a Stripe event back to the app user during Radar review, support triage, and audit-log checks without storing secret values in Stripe metadata.

Do not put API keys, webhook secrets, private notes, or raw credentials into Stripe metadata, application logs, or review notes.
