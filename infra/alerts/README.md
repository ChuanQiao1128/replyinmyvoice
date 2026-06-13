# Business Metric Alerts

This folder contains an operator-run Azure CLI alert pack for the .NET/Azure backend. It is not referenced by CI and should not be run during local tests.

## Prerequisites

- `az login` has selected the intended subscription.
- `AZURE_ALLOW_PAID_RESOURCES=true` is set for the operator shell.
- `AZURE_RESOURCE_GROUP`, `APPINSIGHTS_RESOURCE_NAME`, and `SERVICEBUS_NAMESPACE_ID` are set by name.
- Optional: `SERVICEBUS_QUEUE_NAME` defaults to `rewrite-jobs`.
- Optional: `ALERT_ACTION_GROUP_ID` attaches an existing action group to every rule.
- The Function app has `APPLICATIONINSIGHTS_CONNECTION_STRING` configured. Verify the setting by name with `az functionapp config appsettings list`; do not print the value.

## Rules

- `rimv-outbox-backlog-age`: `outbox_backlog_age_seconds` max above 120 seconds over 10 minutes. The dispatcher target cadence is seconds, so a two-minute head-of-queue age suggests a stuck dispatcher or poison row.
- `rimv-outbox-failed`: `outbox_failed_total` sum above 0 over 15 minutes. Handler failures are retried, but any occurrence deserves review because rewrite jobs are latency-sensitive.
- `rimv-stripe-event-failed`: `stripe_event_failed_total` sum above 0 over 15 minutes. Billing sync failures can be retried by Stripe, but repeated failures risk entitlement drift.
- `rimv-quota-release-spike`: `quota_released_total` sum above 10 over 30 minutes. Releases are expected on failed rewrite attempts; a spike points to a provider outage or quality regression.
- `rimv-provider-circuit-open`: `provider_breaker_open_total` sum at least 3 over 15 minutes. Single opens can occur during transient provider trouble; sustained opens need attention.
- `rimv-servicebus-dlq`: native `DeadletteredMessages` max above 0 for the rewrite queue over 5 minutes. Any dead-lettered rewrite job needs replay or investigation.

## DLQ Metric Choice

The Service Bus DLQ alert intentionally uses Azure Monitor's native `DeadletteredMessages` metric rather than an app timer probe.

Service Bus runtime-property reads require management rights. The Function app connection should stay least-privilege for send/listen operations instead of widening runtime app permissions for a metric probe.

Azure Monitor emits `DeadletteredMessages` for `Microsoft.ServiceBus/namespaces` with an `EntityName` dimension at one-minute granularity. That gives zero app code, zero polling cost, and no new probe failure path. It also keeps alerting during a Functions outage, which is when the DLQ signal matters most.

A timer probe that fails silently would need its own watchdog. DLQ depth greater than zero needs no custom logic.

## Cost Note

The pack creates five scheduled-query alerts plus one native metric alert. At current low-volume expectations this is roughly US$8 per month, mostly from scheduled-query rules. The script exits unless `AZURE_ALLOW_PAID_RESOURCES=true` is present.
