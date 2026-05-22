# M6 Validation Report

Updated: 2026-05-23T05:08:19+12:00

## M6-007 Full Validation Suite

Source issue: `M6-007 Full validation suite green`.

Issue command set:

```bash
npm run lint
npm run typecheck
npm run test
npm run test:e2e
npm run build
npm run cf:build
```

## Current Evidence

| Command | Status | Evidence |
| --- | --- | --- |
| `npm run lint` | pass | ESLint exited 0 at 2026-05-23T04:51+12:00. |
| `npm run typecheck` | pass | `tsc --noEmit` exited 0 at 2026-05-23T04:51+12:00. |
| `npm run test` | pass | Vitest passed with 47 test files and 281 tests at 2026-05-23T04:52+12:00. |
| `npm run test:e2e` | blocked | Playwright's web server could not start because this sandbox rejects local listen on `0.0.0.0:3000` with `EPERM`. A minimal Node HTTP server check at 2026-05-23T04:50:52+12:00 also failed to bind `127.0.0.1` with `EPERM`, confirming a runner restriction before route or browser behavior executes. |
| `npm run build` | pass | Next.js production build exited 0 and generated 11 static pages at 2026-05-23T04:52+12:00. |
| `npm run cf:build` | pass | OpenNext Cloudflare build exited 0 and saved `.open-next/worker.js` at 2026-05-23T04:53+12:00. |

## Scope Decision

The earlier `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo` socket failure is recorded as a separate environment limitation, not as the remaining M6-007 prerequisite. The M6-007 issue brief names the Node/Next validation commands above, and this repair does not touch `backend-dotnet/`; per `plans/codex-implementation-prompt.md`, .NET tests are required when an issue touches `backend-dotnet/`.

## Remaining Prerequisite

Rerun `npm run test:e2e` in a local or CI runner that permits loopback server binding. No live money action, dashboard mutation, npm publish, secret change, or `.env.local` edit is required.

## M6-008 Stripe Live Webhook Delivery

Source issue: `M6-008 Verify Stripe live webhook delivery`.

### Status

Blocked on operator-run live Stripe evidence. Codex cannot complete this issue inside the unattended implementation protocol because the required event must be sent from an operator-controlled live Stripe environment and then checked against production data. No live Stripe trigger, dashboard action, provider mutation, secret edit, `.env.local` edit, or real-money action was performed during the repair.

### State Model

| State | Meaning | Persisted evidence | Terminal? |
| --- | --- | --- | --- |
| `pending_operator_event` | No current live delivery evidence is recorded for M6-008. | M6-008 is `BLOCKED-WAITING-USER` in `plans/issue-board.md`. | No |
| `event_delivered` | Stripe shows an HTTP 2xx delivery to the production webhook URL. | Stripe delivery attempt id, event id, event type, and timestamp recorded here. | No |
| `event_processed` | The app accepted the event and recorded it in production DB. | `StripeEvent.id`, `stripeMode='live'`, `status='processed'`, and `attemptCount >= 1`. | No |
| `subscription_synced` | The event mapped to an existing production user/customer/subscription and updated entitlement data. | Matching `User.stripeCustomerId`, `stripeSubscriptionId`, `subscriptionStatus`, and `currentPeriodEnd`. | Yes |
| `endpoint_only_verified` | Delivery and `StripeEvent` persistence passed, but the event was synthetic or did not map to a production user. | Stripe delivery plus `StripeEvent` row; no `User` update claimed. | Yes, partial |

| Event | Source | To | Side effects | Reject when |
| --- | --- | --- | --- | --- |
| Operator sends live webhook event to `https://replyinmyvoice.com/api/stripe/webhook` | Stripe live environment | `event_delivered` | Stripe records delivery attempt and HTTP status. | Event targets a non-production URL or lacks live-mode context. |
| App records webhook event | `app/api/stripe/webhook/route.ts` | `event_processed` | Inserts or updates `StripeEvent`; duplicate processed events are skipped. | Signature verification fails, DB is unavailable, or handler returns 4xx/5xx. |
| Event maps to an existing user/customer/subscription | Webhook handler and DB | `subscription_synced` | Updates `User` subscription fields. | Event customer/subscription has no matching local user. |
| Event does not map to an existing user/customer/subscription | Webhook handler and DB | `endpoint_only_verified` | Confirms route delivery and idempotency only. | This must not be claimed as subscription entitlement verification. |

Invariants:

- A completed M6-008 result must not rely on a real test charge.
- A live-mode result must use a live event id and `StripeEvent.stripeMode='live'`.
- A synthetic Stripe sample event can prove endpoint delivery and `StripeEvent` persistence, but it cannot prove `User` subscription sync unless it carries identifiers for an existing production user/customer/subscription.
- Duplicate live event delivery should not create duplicate business effects; existing local tests cover processed and failed event lifecycle handling.

Illegal transitions:

- `pending_operator_event` -> `subscription_synced` without Stripe delivery evidence and a matching `StripeEvent` row.
- `event_processed` -> `subscription_synced` when the event contains only synthetic sample identifiers.
- `endpoint_only_verified` -> `subscription_synced` without a second event that maps to a production user/customer/subscription.
- Any state -> `done` when the delivery used test mode or an unverified URL.

Persistence implications:

- `StripeEvent.id` is the idempotency key for webhook event processing.
- `StripeEvent.status` must be `processed` for a successful M6-008 claim.
- `User` subscription fields are only expected to change when the webhook event can be matched by metadata or Stripe customer id.
- A missing `User` update is acceptable for endpoint-only verification but must be recorded as partial evidence.

Test checklist:

- Local unit coverage for processed, failed, duplicate, and retryable event states is in `tests/unit/stripe-webhook-events.test.ts`.
- Production verification still requires a live event id, live `StripeEvent` row, and, for full completion, a matching `User` update.

### Data Review Notes

Findings:

- No source-code defect was identified in this repair. The remaining M6-008 blocker is evidence collection, not a migration or webhook-handler patch.
- Full subscription-sync verification requires a live event that maps to an existing production `User`; otherwise the handler can correctly process the event while leaving `User` unchanged.

Open questions:

- Which production user/customer/subscription should the operator use for the non-charging verification event, if full subscription-sync evidence is required before M7-001?

Suggested tests:

- Keep `tests/unit/stripe-webhook-events.test.ts` as the local lifecycle guard.
- If future code changes touch `app/api/stripe/webhook/route.ts`, add a route-level test that proves unmatched subscription events create a processed `StripeEvent` without falsely updating `User`.

### Operator Evidence Checklist

Record these fields before moving M6-008 out of `BLOCKED-WAITING-USER`:

| Evidence | Required value |
| --- | --- |
| Stripe event id | `evt_...` from live mode |
| Stripe delivery attempt | Attempt id or dashboard timestamp |
| Event type | Prefer `customer.subscription.updated` or `checkout.session.completed` that maps to a known production user |
| Delivery result | HTTP 2xx from `https://replyinmyvoice.com/api/stripe/webhook` |
| `StripeEvent` DB row | `id=<event id>`, `stripeMode='live'`, `status='processed'`, `attemptCount >= 1` |
| User subscription DB row | Required only when the event maps to a known production user/customer/subscription |
| Notes | State whether this was full subscription sync verification or endpoint-only verification |

### Safe DB Queries For Operator Run

Use read-only production DB access and replace placeholders locally. Do not paste connection strings or result rows containing private customer data into chat.

```sql
select
  "id",
  "type",
  "status",
  "stripeMode",
  "attemptCount",
  "processedAt",
  "failedAt"
from "StripeEvent"
where "id" = '<stripe_event_id>';
```

```sql
select
  "id",
  "stripeCustomerId",
  "stripeSubscriptionId",
  "subscriptionStatus",
  "currentPeriodEnd",
  "updatedAt"
from "User"
where "stripeCustomerId" = '<stripe_customer_id>'
   or "stripeSubscriptionId" = '<stripe_subscription_id>';
```
