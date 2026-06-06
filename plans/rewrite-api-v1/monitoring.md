# Rewrite API v1 Monitoring

Scope: server-side observability for the public v1 API proxy routes on the Cloudflare Worker and the Azure Functions backend.

## Runtime Emission

Worker-side API telemetry is emitted only when these server-side Worker environment variables are present at runtime:

- `POSTHOG_API_KEY`
- `SENTRY_DSN`

If either value is absent, that transport is a silent no-op. These keys must stay server-side and must not be exposed through client-visible environment variables. The supervisor will set the production Worker values during deploy.

Azure Functions backend telemetry continues to emit to Application Insights through `APPLICATIONINSIGHTS_CONNECTION_STRING` on the Functions app.

## Signals And Alert Sources

| Signal | Primary Source | Secondary Source | Suggested Alert |
| --- | --- | --- | --- |
| API error rate | PostHog dashboard on `api_error` events grouped by `endpoint` and `status_code` | Application Insights request/failure queries | Alert when v1 `api_error` events exceed 5% of `api_request` + `api_error` events for 5 minutes. |
| 5xx responses | Sentry issues from Worker-side v1 proxy errors and 5xx API outcomes | Application Insights exceptions and failed requests | Alert on any new unresolved 5xx issue in Sentry, and on sustained 5xx count in App Insights. |
| Poll latency | PostHog `api_request` events for `GET /api/v1/rewrite/{id}` using `latency_ms` | Application Insights request duration for the Azure result endpoint | Alert when p95 poll latency is above the API SLO for 10 minutes. |
| `quota_exhausted` spikes | PostHog `api_error` events filtered by `error_code = quota_exhausted` | Application Insights custom dimensions from the backend quota path | Alert when the count doubles versus the previous comparable window or exceeds the launch baseline. |
| `rate_limited` spikes | PostHog `api_error` events filtered by `error_code = rate_limited` | Application Insights request logs and rate-limit custom dimensions | Alert when rate-limit errors rise above the expected per-key or global threshold for 5 minutes. |
| Queue depth | Application Insights metrics/logs from Azure Functions and Service Bus processing | Azure Portal Service Bus metrics | Alert when active messages or oldest-message age remains above the worker processing baseline for 10 minutes. |

## Dashboard Notes

- PostHog should chart `api_request` and `api_error` by `endpoint`, `status_code`, `error_code`, and `latency_ms`.
- Sentry should group Worker-side v1 server errors with `endpoint`, `status_code`, and `request_id` tags when available.
- Application Insights remains the source of truth for backend queue, worker, and Function execution details because those signals originate in Azure.

## Triage Order

1. Start with PostHog to identify which v1 endpoint and status family changed.
2. Open the matching Sentry issue when the change includes 5xx responses or Worker proxy exceptions.
3. Use Application Insights to confirm whether the fault is in Azure Functions, Service Bus queue processing, quota/rate-limit logic, or the Worker-to-Azure proxy path.
