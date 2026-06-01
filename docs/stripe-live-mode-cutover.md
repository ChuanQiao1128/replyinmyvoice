# Stripe Live Mode Cutover

## PAY-01 Webhook Subscription Requirement

The LIVE Stripe webhook endpoint for Reply In My Voice must subscribe to `invoice.payment_failed` in addition to the existing checkout, subscription, refund, and dispute events used by the C# backend.

Owner dashboard action: confirm the live endpoint includes `invoice.payment_failed` before cutover or before relying on PAY-02 renewal-failure alerts.

Automation must not initiate a real Stripe charge during verification.
