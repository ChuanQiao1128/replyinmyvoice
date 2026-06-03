# Observability Runbook — `replyinmyvoice.com`

Date: 2026-06-01
Owner: TimeAwake Ltd (ChuanQiao1128)
Scope: How we know the live site is up, where payment/webhook failures are surfaced, where alerts go, and what to do when an alert fires. This is the day-one minimum; deeper cost telemetry and KPI reporting layer on top.

This document satisfies M7-006 (uptime monitoring).

---

## 1. What we monitor

| Signal | Endpoint / source | Why |
|---|---|---|
| Site liveness | `https://replyinmyvoice.com/` | Catches DNS, Cloudflare Worker, and edge routing breakage. |
| DB health | `https://replyinmyvoice.com/api/health/db` | Catches Azure SQL connectivity loss through the Azure Functions backend. |
| Rewrite path | `https://replyinmyvoice.com/api/health/rewrite` (planned — falls back to landing-page check if absent) | Catches provider (DeepSeek + Sapling) outages. |
| Stripe webhook | Stripe dashboard → Developers → Webhooks → delivery success rate | Catches signature drift and webhook URL misconfiguration. |
| Worker errors | Cloudflare dashboard → Workers → `replyinmyvoice-app` → Logs | Catches runtime exceptions not surfaced to the customer. |
| Payment funnel events | PostHog events from Worker/browser instrumentation | Catches checkout, payment, and webhook failure trends. |
| Backend payment errors | Application Insights traces from Azure Functions | Catches checkout, portal, and webhook processing failures with `CorrelationId`. |

The site is single-region (Cloudflare edge — global by default); we do not need multi-region probes for launch.

## 2. UptimeRobot configuration

UptimeRobot is the day-one external probe. It is free for ≤50 monitors at 5-minute intervals and emails on failure — sufficient for the early commercial launch. Operator (ChuanQiao1128) creates these in `https://uptimerobot.com` under the TimeAwake Ltd account.

| Monitor name | URL | Type | Interval | Alert contacts |
|---|---|---|---|---|
| `rimv-prod-home` | `https://replyinmyvoice.com/` | HTTP(s) | 5 min | `info@timeawake.co.nz` |
| `rimv-prod-db-health` | `https://replyinmyvoice.com/api/health/db` | Keyword (expects `"ok"`) | 5 min | `info@timeawake.co.nz` |
| `rimv-prod-www` | `https://www.replyinmyvoice.com/` | HTTP(s) | 15 min | `info@timeawake.co.nz` |

Alert rules:

- Alert when down for **2 consecutive checks** (so a single transient 5xx does not page).
- Send email on down AND on recovery.
- SSL expiry monitoring on the apex monitor (UptimeRobot does this for free) → alerts 30 days before cert expiry. Cloudflare auto-renews, but the alert is a safety net.

Status page: a public status page can be added later (UptimeRobot free tier supports one). Defer until customer requests it; the operator-only inbox is sufficient at launch.

## 3. Cloudflare-native monitoring (parallel layer)

In addition to the external probe, configure the following in the Cloudflare dashboard:

1. **Worker analytics alerts** → Workers & Pages → `replyinmyvoice-app` → Settings → Alerts → enable "Worker error rate > 5% for 5 minutes" → email `info@timeawake.co.nz`.
2. **DNS health** → Cloudflare Notifications → "DNS Firewall" or "Origin Monitoring" alerts for the `replyinmyvoice.com` zone → email `info@timeawake.co.nz`.
3. **Custom domain SSL** → Custom domains tab on the Worker should already show "Active" — Cloudflare alerts if cert provisioning fails.

These are zero-cost on the existing Cloudflare plan.

## 4. Stripe webhook delivery

Stripe is the most expensive thing to get wrong (silent revenue loss). The Stripe dashboard's webhook view shows delivery success rate per endpoint.

- Operator opens the dashboard once per week during launch month: Stripe Dashboard → Developers → Webhooks → click the production endpoint → check "Recent events" for delivery failures.
- Stripe automatically retries failed webhooks with exponential backoff for up to 3 days. PAY-02 adds PostHog/Application Insights alerts so the operator sees failures before customers report missing credits.
- If failure rate >5% for a sustained period: treat as an incident per `docs/rollback-plan.md` triggers; investigate signature secret drift and recent deploy changes.

## 5. Payment observability alert rules

Runtime wiring:

- Cloudflare Worker secrets: `SENTRY_DSN`, `POSTHOG_API_KEY`.
- Azure Functions app settings: `SENTRY_DSN`, `POSTHOG_API_KEY`.
- GitHub Actions secrets for runtime-setting automation: `SENTRY_DSN`, `POSTHOG_API_KEY`.
- Do not commit DSN/key values. `wrangler.jsonc` should not contain these values.

Frontend/Worker events emitted:

| Event | Source | Alert use |
|---|---|---|
| `checkout_started` | Buy button click and `/api/observability/payment` | Funnel denominator. |
| `checkout_redirected` | Checkout proxy success | Confirms Stripe-hosted checkout handoff. |
| `payment_succeeded` | Stripe webhook proxy for `checkout.session.completed` | Confirms credit-pack payment completion path. |
| `payment_failed` | Checkout/portal failures and `invoice.payment_failed` webhook proxy | Alerts payment path failures or subscription renewal failure. |
| `webhook_failed` | Stripe webhook proxy/backend failure paths | Alerts failed webhook processing. |

PostHog alert: create an insight filtered to `event = webhook_failed`, rolling 10-minute window, threshold **count >= 1**, email `info@timeawake.co.nz`. For the first launch week, any webhook failure is actionable because Stripe retries can hide the customer impact for up to 3 days.

PostHog funnel watch: `checkout_started -> checkout_redirected -> payment_succeeded`, grouped by `sku`, 24-hour window. Alert when `checkout_started >= 3` and `checkout_redirected = 0`, or when `checkout_redirected >= 3` and `payment_succeeded = 0`; route to `info@timeawake.co.nz`.

Application Insights alert for Functions payment errors:

```kusto
traces
| where timestamp > ago(10m)
| where tostring(customDimensions.PaymentObservabilityEvent) in ("payment_failed", "webhook_failed")
| summarize failure_count = count() by tostring(customDimensions.PaymentObservabilityEvent)
```

Create an Azure Monitor scheduled-query alert on this query with threshold **failure_count >= 1** over 10 minutes, severity 2, action group email `info@timeawake.co.nz`. Include `customDimensions.CorrelationId` in the alert details or linked query so Worker, Stripe, and Functions logs can be joined.

Application Insights alert for sustained rate: same filter over 30 minutes, threshold **failure_count >= 3**, severity 1. Use this for escalation if the first alert is missed or multiple customer payments are affected.

Owner action: create the PostHog insights/alerts, Azure Monitor scheduled-query alerts, and the UptimeRobot monitors in §2 after secrets are supplied and production deployment is complete.

## 6. Alert routing

All day-one alerts route to `info@timeawake.co.nz` (the same inbox documented in `docs/support-runbook.md`). The operator runbook for receiving an alert:

1. **Confirm the alert is real** — open the URL in a browser, run the curl command in §8 below.
2. **Check Cloudflare Workers logs** for the same time window — Workers & Pages → `replyinmyvoice-app` → Logs.
3. **Cross-reference Stripe webhook delivery** if the alert is on `/api/health/db` (DB outage will manifest as Stripe webhook failures too).
4. **Decide**: hot-fix forward (deploy a Worker patch via `wrangler deploy`) or roll back per `docs/rollback-plan.md`.
5. **Communicate** per the §6 outage template in `docs/support-runbook.md` if downtime exceeds 30 minutes.
6. **Record** the incident in `plans/decisions-log.md` with timestamp, trigger, action, outcome.

## 7. Baseline metrics to track manually

Until M5-* cost telemetry and M7-008 KPI script land, the operator records weekly snapshots in `plans/launch-metrics.md` (create on first entry):

| Metric | Source | Target / threshold |
|---|---|---|
| Uptime % | UptimeRobot dashboard | ≥99.5% for week 1, ≥99.9% from week 2 |
| Median Worker latency | Cloudflare Workers analytics | <500 ms p50 |
| 5xx rate | Cloudflare Workers analytics | <0.5% sustained |
| Webhook delivery rate | Stripe dashboard + PostHog `webhook_failed` | ≥99% |
| Functions payment-error count | Application Insights scheduled-query alert | 0 per 10 minutes |
| Rewrite error rate | (manual: `/api/health/rewrite` once it lands; otherwise customer reports) | <2% |

A poor week-one number is not necessarily a rollback trigger — it is a "look closer" trigger. See `docs/rollback-plan.md` for hard rollback criteria.

## 8. Manual probe commands

For ad-hoc checks (the operator runs these from a laptop, not from automation):

```bash
# Site is up
curl -sS -o /dev/null -w "%{http_code} %{time_total}s\n" https://replyinmyvoice.com/

# DB health
curl -sS https://replyinmyvoice.com/api/health/db

# Rewrite endpoint exists (auth-gated — expect 401)
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/api/rewrite
```

Document any unexpected response in `plans/decisions-log.md`. Do not paste response bodies that might contain personal data.

## 9. Out of scope (deferred)

- **Broad product analytics** — PAY-02 captures payment funnel events only; fuller signup/rewrite analytics remains a later M7/KPI task.
- **Source-map upload automation** — PAY-02 captures runtime payment errors; source-map upload can be added separately if needed.
- **Cost telemetry per rewrite** — M5-001 / M5-002 will replace manual Stripe + DeepSeek spreadsheet tracking.
- **Auto-scaling alerts** — Cloudflare Workers scales to zero / autoscales transparently; no operator action.
- **Database backups** — Azure SQL backup/restore policy is managed in Azure; no day-one payment alert action.

## 10. Where this runbook lives

- This file: `docs/observability.md` (canonical).
- Linked from `docs/rollback-plan.md` §"Trigger conditions" so the operator sees both at decision time.
- Paired with `docs/support-runbook.md` (alerts → inbox → triage).

## 11. Verification before launch

Before the cutover that flips `replyinmyvoice.com` to the live Worker (gated by `LAUNCH_CONFIRMED=true`):

1. Confirm UptimeRobot monitors from §2 exist and are in "Up" state against the workers.dev preview hostname.
2. Update each monitor's URL to the production hostname during the cutover window.
3. Confirm Cloudflare Worker error-rate alert from §3 is configured.
4. Send a test webhook from Stripe (Developers → Webhooks → "Send test webhook") and confirm 200 OK.
5. Confirm PostHog receives a test `checkout_started` event and Application Insights receives a synthetic `PaymentObservabilityEvent` trace from the Functions payment path.

Tick these off in `plans/decisions-log.md` at the cutover entry.

---

Last verified: 2026-06-01 (PAY-02 documentation pass — PostHog, Azure Monitor, and UptimeRobot alert creation remain owner dashboard actions).
