# Stripe Live Mode Cutover Notes

## PAY-01 Webhook Subscription Requirement

The live Stripe webhook endpoint must subscribe to `invoice.payment_failed` in addition to the existing checkout and subscription lifecycle events.

Required live endpoint events:

- `checkout.session.completed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`
- `invoice.payment_failed`

PAY-01 uses `invoice.payment_failed` as the explicit failed-renewal hook. A matched subscription user is downgraded to the free quota plan immediately and the handler emits a structured `CorrelationId` log field for PAY-02 alerting. Do not send customer email from this handler; notifications are a separate payment issue.

`invoice.payment_succeeded` remains out of scope for PAY-01. Renewal success still relies on `customer.subscription.updated`.
