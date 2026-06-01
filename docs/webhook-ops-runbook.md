# Stripe Webhook Operations Runbook

Date: 2026-06-01
Owner: TimeAwake Ltd
Scope: Monitor Stripe webhook delivery, inspect failed deliveries, and safely replay missed events.

## Signals

The Azure Functions readiness endpoint is:

```text
/api/health/ready
```

The payment webhook checks appear under:

```json
{
  "checks": {
    "failedStripeEvents": {
      "ok": false,
      "count": 1,
      "threshold": 0
    },
    "lastProcessedStripeEvent": {
      "ok": true,
      "lastProcessedAt": "2026-06-01T00:00:00+00:00",
      "ageSeconds": 240,
      "maxAgeMinutes": 1440
    }
  }
}
```

Configure the PAY-02 alert channel to alert when either condition is true:

- `checks.failedStripeEvents.ok == false`
- `checks.lastProcessedStripeEvent.ok == false`

The relevant runtime settings are:

| Setting | Meaning |
|---|---|
| `Health:FailedStripeEventsThreshold` | Number of unresolved `StripeEvent.Status = Failed` rows allowed before readiness degrades. Default is `0`. |
| `Health:StripeLastProcessedMaxAgeMinutes` | Maximum age of the most recent processed Stripe event before readiness degrades. Default is `0`, which reports the metric but does not fail readiness for an empty or quiet account. |

Use a traffic-appropriate age window. During a low-volume launch, start with a conservative value such as 24-48 hours after the first successful production webhook, then tighten it only when payment volume makes a shorter window meaningful.

## Inspect Failed Deliveries

Use Stripe Test mode for drills. For a production incident, inspect existing events only; do not create a new real charge as part of this procedure.

1. Open the Stripe Dashboard and select the correct mode: Test mode for sandbox checks, live mode only for a real production incident.
2. Go to Workbench or Developers, then Webhooks.
3. Open the Reply In My Voice webhook endpoint.
4. Open the event deliveries list. Stripe shows delivery state such as delivered, pending, or failed for each event destination.
5. Filter to failed deliveries if available.
6. Open the failed event and record only safe identifiers in the incident note:
   - Stripe event id, for example `evt_...`
   - event type, for example `checkout.session.completed`
   - delivery status code
   - delivery timestamp
   - the internal `StripeEvent.Status`, `AttemptCount`, and `LastError` if present
7. Cross-check application readiness:
   - `failedStripeEvents.count` should include unresolved failed rows.
   - `lastProcessedStripeEvent.ageSeconds` shows how stale the last successful webhook processing is.

Do not copy raw webhook payloads, customer email addresses, payment method details, API keys, or webhook signing secrets into docs, tickets, or chat.

## Resend Or Replay

Stripe supports manual resend from the Dashboard while also continuing its automatic retry schedule for failed deliveries.

1. Fix the root cause first if the endpoint is still returning non-2xx responses. Common causes are backend outage, signature-secret drift, database outage, or a bad deploy.
2. In Stripe Dashboard, open the specific failed event.
3. Click the resend action for the event and choose the Reply In My Voice webhook endpoint.
4. Wait for the delivery attempt to complete.
5. Confirm Stripe shows a 2xx response for the resend attempt.
6. Confirm `/api/health/ready`:
   - `checks.failedStripeEvents.count` decreases after the replay processes.
   - `checks.lastProcessedStripeEvent.lastProcessedAt` advances for a successfully processed Stripe event.
7. Confirm the user-facing outcome for the relevant event type:
   - pack purchase: one `RewriteCredit` grant exists
   - refund or dispute: credit clawback applied
   - subscription event: user subscription status and period end are current
8. Record the incident outcome in `plans/decisions-log.md` with redacted identifiers.

If a resend still fails, do not disable signature verification or idempotency. Keep the failed event id, inspect backend logs by correlation id, fix forward, and resend again after the endpoint is healthy.

## Why Replay Is Safe

Webhook replay is safe for this codebase because processing is idempotent:

- `StripeEvent.EventId` is the primary key. A duplicate Stripe event cannot create a second independent processing row for the same event id.
- `StripeEvent.Status` tracks `Processing`, `Processed`, or `Failed`, with attempt count and last error for retry visibility.
- `RewriteCredit.StripeEventId` has a unique index for credit grants, so a duplicate `checkout.session.completed` event cannot grant the same pack twice.
- The webhook service treats duplicate credit-grant unique-constraint collisions as idempotent success.

The replay procedure must preserve these protections. Never change the webhook secret, skip signature validation, delete `StripeEvent` rows, or remove the unique `RewriteCredit.StripeEventId` constraint to force a replay.

## References

- Stripe webhook receiving and manual resend docs: https://docs.stripe.com/webhooks
- Stripe Workbench webhook event delivery docs: https://docs.stripe.com/workbench/webhooks
