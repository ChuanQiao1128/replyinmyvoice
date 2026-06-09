# Rewrite API — Phase 2 Specification (Commercialization Foundation)

- **Status:** Design for owner review — **NOT started. Do not build yet.**
- **Date:** 2026-06-05
- **Author:** Claude (supervisor), via `system-spec-synthesis`
- **Builds on:** [`SPEC.md`](./SPEC.md) (Phase 1, shipped), [`DELIVERY-PLAN.md`](./DELIVERY-PLAN.md) (refines/corrects Phase 2 §, BILL-/OBS-)
- **Scope of this doc:** the owner's Phase 2 ask — (1) redesign `/developers` + the Developer API surface to a market standard, (2) usage statistics & visualization (remaining calls, frequency/time/count, on-site usage history, today/yesterday/this-month bar charts), (3) billing/charge history + the rest of the commercialization base modules, (4) the billing closed loop incl. `invoice.payment_failed` downgrade, (5) the `signal`-field governance decision — plus a complete gap/TODO backlog.

> This **refines and in one place corrects** the existing `DELIVERY-PLAN.md` Phase 2 (`BILL-01..04`, `OBS-01`). Where it sharpens an existing issue it keeps the same id; new work gets new ids (`USE-`, `BILLH-`, `DEVX-`, `OPS-`). It does **not** re-plan Phase 1 (shipped) or Phase 3/4 hardening/GA beyond what these requirements touch.

---

## Context

Phase 1 closed the loop: the rewrite engine is a paid, key-authed, async public API (`POST /api/v1/rewrite` → `202 {id}` → poll `GET /api/v1/rewrite/{id}`; `GET /api/v1/usage`), shipped to prod on `main @ 0c1d112` (2026-06-05). Key lifecycle (create/list/revoke), per-key RPM, per-call audit, idempotency, terminal-state guarantee, and the shared-pool quota are live. `API_KEY_PEPPER` was set on the Functions app on 2026-06-05 (this turn).

What Phase 1 deliberately left thin is **everything around the call** — the commercial surface a developer evaluates and an operator runs the business on: discoverable, runnable docs; a usage dashboard with history and charts; visible charge history; and a renewal-failure path that actually downgrades. This spec designs that layer.

### A surprising amount of the *data* already exists — the gap is mostly *surfacing*, *scheduling*, and *normalizing*

Verified in code (see Current System). The recurring theme: Phase 1 wrote the rows; Phase 2 must expose, aggregate, and act on them.

---

## Goals

1. **Developer page & docs at a market standard** (Stripe / OpenAI / Resend shape): a cold developer goes signup → pay → key → first successful rewrite using only the page; the async submit+poll protocol, auth, errors, rate limits, idempotency, pricing/quota, and data-retention are all documented and accurate.
2. **Usage statistics & visualization** in the signed-in portal:
   - (a) remaining call count (exists programmatically; surface it prominently),
   - (b) call frequency / time / count statistics,
   - (c) on-site usage history (the per-call log),
   - (d) a bar-chart usage view: **today, yesterday, and month-to-date**, plus a rolling 30-day daily chart.
3. **Billing / charge history**: a unified, user-visible record of every Stripe charge (subscription renewals **and** one-time packs), refunds, and disputes — with receipts/invoices.
4. **Close the billing loop**: `invoice.payment_failed` must actually downgrade a non-paying account after grace (today it does not — see the correction below), with dunning reminders and a clear in-app PastDue state.
5. **Resolve `signal`-field governance**: keep it informational and non-guaranteed; stop advertising an endpoint that does not exist.
6. **Fill the remaining commercialization base modules** (rate-limit headers, key rotation, cost attribution, scheduled jobs, runtime currency) — enumerated as a complete backlog.

## Non-Goals (Phase 2)

- A separate B2B meter / new Stripe price / per-token billing (still shared pool by decision — `SPEC.md` decision 4/6). Higher-volume **API packs** are a post-Phase-2 pricing question.
- `POST /api/v1/analyze-signal` as a standalone billable endpoint (governance decision: **drop from the public surface for now** — §E).
- Official SDK, webhooks, MCP, OpenAPI (Phase 4 `DX-02/DX-05`).
- Real charges from automation (AGENTS.md hard limit — first live charge is owner-performed).
- Heavy charting dependencies — use a dependency-free SVG/CSS bar component consistent with the "Warm Writing Desk" system.

---

## Current System (what already exists — do **not** rebuild)

| Capability | Status | Evidence |
|---|---|---|
| Async v1 API (submit/poll/usage), key auth, RPM, idempotency, terminal state | ✅ live | `backend-dotnet/.../Functions/V1RewriteHttpFunctions.cs`; `app/api/v1/*` |
| API-key CRUD + portal UI (create-once, masked `Last4`, revoke) | ✅ live | `components/developers/api-keys-panel.tsx`; `app/developers/keys/page.tsx`; `app/api/keys/*` |
| **Per-call usage events with timestamps** (`ApiKeyUsage`: `ApiKeyId, Endpoint, StatusCode, LatencyMs, CostUsdEstimate, CreatedAt`; indexes `(ApiKeyId,CreatedAt)`, `(Endpoint)`, `(CreatedAt)`) | ✅ rows written, ❌ never surfaced | `ReplyInMyVoice.Domain/Entities/ApiKeyUsage.cs`; written in `V1RewriteHttpFunctions` per call |
| Remaining-quota read (`GET /api/v1/usage`) + portal `SubscriptionStatus` (X of Y remaining) | ✅ live | `app/api/v1/usage/route.ts`; `components/app/subscription-status.tsx` |
| **One-time pack** purchase history (sku, amount, currency, receipt, date, expiry, remaining) | ✅ live | `GET /api/me/payments` → `AccountService.GetPurchaseHistoryAsync`; `components/account/account-panel.tsx` `PurchaseHistorySection` |
| Stripe webhooks: `checkout.session.completed`, `customer.subscription.*`, `invoice.payment_failed`, `invoice.payment_succeeded`, `charge.refunded`, `charge.dispute.*` | ✅ handled | `StripeEventService.cs:388-434` |
| Webhook idempotency (`StripeEvents` table, lock+retry) | ✅ live | `StripeEvent.cs`; `StripeEventService.cs` |
| `invoice.payment_failed` → `PastDue` + 7-day grace + FailedPayment notification | ✅ handled | `StripeEventService.SyncInvoicePaymentFailedAsync` |
| `invoice.payment_succeeded` → recover `PastDue`→`Active` | ✅ handled | `StripeEventService.SyncInvoicePaymentSucceededAsync` |
| Grace-expiry → downgrade logic (`ProcessExpiredPaymentGraceAsync`) | 🟡 **code exists, never scheduled** | `StripeEventService.cs:172-207` — **no timer calls it** |
| Stripe reconciliation audit (paid-vs-granted) | ✅ live (timer) | `StripeReconciliationService.cs` |
| `signal` (draft/rewrite naturalness) on succeeded result | ✅ live, informational | `V1RewriteSucceededResponse.Signal`; `SPEC.md` decision 3 |
| Billing support form (refund/billing question) + history | ✅ live | `account-panel.tsx`; `/api/billing-support-requests` |

**Net:** charts need an aggregation read over `ApiKeyUsage` + a UI — **no new usage table required**. The downgrade needs a **scheduler**, not new logic. Charge history needs subscription **invoices normalized** (packs are already done). The dev page needs a **rewrite to match the live async contract** (it currently advertises a synchronous shape and a non-existent endpoint).

### Correction to `DELIVERY-PLAN.md` `BILL-04`

The plan's acceptance — *"simulated failed-payment webhook → quota falls back to free baseline"* — is **wrong against the code**. `invoice.payment_failed` does **not** downgrade; it sets `PastDue` and **keeps the paid 90 quota for a 7-day grace** (`AccountService.cs:360-374` treats `PastDue` as paid). Downgrade only happens when `ProcessExpiredPaymentGraceAsync` flips `PastDue`→`Inactive` after grace — and **nothing schedules that method**. So today a failed renewal lets a user keep paid quota **indefinitely**. The real Phase 2 fix is the **scheduled grace-expiry sweep** (§D), not new webhook code.

---

## Proposed Architecture (by workstream)

### A. Developer page & API docs redesign (`DEVX-*`) — refines `DX-01`

**Problem with today's `app/developers/page.tsx`:** it is marketing-stale and partly fictional:
- advertises `POST /api/v1/analyze-signal` — **not implemented**;
- shows a **synchronous** `200 OK { rewrittenText, signal }` response — the real API is **async `202 {id}` + poll**;
- badge says *"In active development · not yet public"* — it **is** public now;
- no authentication, error, rate-limit, idempotency, quota, or retention docs;
- no discovery path: nothing links to `/developers/keys`, no nav "Developers" entry, no quickstart.

**Target information architecture** (market standard — Stripe/OpenAI/Resend):

```
/developers                      ← overview + commercial pitch + "Get your API key" CTA
  ├─ Overview          what it is, who it's for (CRM/help-desk/support tooling), pricing model, link to keys
  ├─ Quickstart        create key → first POST → poll GET → read result (copy-paste curl, 60-sec path)
  ├─ Authentication    Bearer rmv_live_…; key lifecycle; rotation; never log keys
  ├─ API reference
  │     POST /api/v1/rewrite      request {draft}, 202 {id,status}, Location header
  │     GET  /api/v1/rewrite/{id} processing | succeeded(+rewrittenText,signal) | failed(error)
  │     GET  /api/v1/usage        {scope,quota,used,remaining,periodEnd}
  │     Errors           full table: 400/401/402/409/429 + codes + "uncharged" column
  │     Rate limits      per-key RPM (default 60); X-RateLimit-* headers (see OPS-01)
  │     Idempotency      Idempotency-Key semantics
  ├─ Guides            async polling + backoff; handling failed/timeout; quota & buying more
  ├─ Pricing & quota   shared pool, per-succeeded-call=1, no free tier, packs
  ├─ Data & privacy    bounded 30-day retention; deletion; no over-promising
  └─ Changelog/version v1 stability statement; signal = informational, may evolve
```

- **Discovery path:** add a "Developers" entry to the signed-in nav; `/developers` CTA "Get your API key" → `/developers/keys`; from `/developers/keys`, tabs to **Usage** and **Billing** (§B/§C) so the keys page becomes a real developer dashboard.
- **Commercial-application copy:** lead with concrete use cases (drop fact-preserving, in-voice rewrites into a CRM/help-desk/support-inbox) and the metering model in plain terms ("you pay per successful rewrite; an API call and a website rewrite draw from the same balance").
- **Accuracy gate:** every endpoint/shape on the page must match the live contract; remove `analyze-signal`; correct the response to async; drop the "not public" badge. Banned-term grep stays clean.
- **Constraint:** all copy in English; "Warm Writing Desk" visual system; no heavy doc framework — server-rendered sections + the existing `api-code` block style.

### B. Usage statistics & visualization (`USE-*`) — refines `BILL-02`

**Data source:** `ApiKeyUsages` (already per-call, already indexed by `(ApiKeyId, CreatedAt)`). No new table for v1 (a daily rollup table is a *deferred* perf optimization — only if volume makes on-the-fly grouping slow).

**New read endpoints (Entra-authed, portal):**

| Method | Route | Returns |
|---|---|---|
| `GET` | `/api/me/api-usage/summary` | `{ today:{calls,succeeded,failed}, yesterday:{…}, monthToDate:{…}, last30dCalls, quota, used, remaining, periodEnd }` |
| `GET` | `/api/me/api-usage/series?days=30&bucket=day` | `[{ date:"2026-06-05", calls, succeeded, failed }]` (across all the user's keys; optional `keyId=` filter) |
| `GET` | `/api/me/api-usage/recent?limit=50` | `[{ createdAt, endpoint, statusCode, latencyMs, keyLast4 }]` — the on-site usage history (req 1c) |

**Programmatic (key-authed), optional mirror for API callers:** `GET /api/v1/usage/series?days=30` returning the same buckets scoped to the calling key. Keep `GET /api/v1/usage` unchanged.

**Aggregation:** `GROUP BY CAST(CreatedAt AS date)`, `succeeded = StatusCode in (200,202)`, scoped to the user's `ApiKey` ids and `[from,to)`. Index `(ApiKeyId, CreatedAt)` covers it.

**Time-zone decision (Open Question Q1):** "today/yesterday/this month" are TZ-sensitive. Proposal: bucket in **Pacific/Auckland** (the business TZ) so the owner's "today" matches; document it. Alt: UTC + client-side relabel.

**UI (req 1a–1d):** a **Usage** tab on the developer dashboard (`/developers/keys` → tabs, or `/developers/usage`):
- three summary cards: **Today / Yesterday / Month-to-date** (calls, with succeeded/failed split);
- a **rolling 30-day daily bar chart** (dependency-free SVG; warm palette; hover = exact counts);
- a **remaining-quota** readout (from `/api/v1/usage`) with the buy path on exhaustion;
- a **recent-calls table** (the usage history) with endpoint, status, latency, key.
- Mobile: cards stack, chart scrolls/condenses.

### C. Billing & charge history (`BILLH-*`)

**Conceptual clarity for req 2a:** in this product "**charges**" = Stripe **payments** (subscription renewals + one-time pack purchases + refunds/disputes). Per-API-call **usage** is metered against a count quota and is **not** separately charged — so "charge history" ≠ "usage history" (that's §B). Both belong on the dashboard, clearly separated.

- One-time **pack** charges: ✅ already surfaced (`GET /api/me/payments`).
- **Subscription** charges (Pro/API monthly renewals): ❌ not surfaced. Add a normalized **`StripeInvoice`** entity populated from `invoice.paid` / `invoice.payment_failed` / `invoice.*`:
  `{ Id(stripe invoice id, PK), UserId, SubscriptionId, Status(draft|open|paid|void|uncollectible), AmountDue, AmountPaid, Currency, PeriodStart, PeriodEnd, AttemptCount, NextPaymentAttempt, HostedInvoiceUrl, InvoicePdf, CreatedAt, RowVersion }`.
  - Requires subscribing to `invoice.paid` (currently only `invoice.payment_succeeded`/`_failed` handled) — confirm the Stripe webhook endpoint event set includes the invoice events we read.
- **Unified billing history endpoint** `GET /api/me/billing/history` → merge packs + subscription invoices + refunds/disputes into one date-sorted list: `[{ type:"pack|subscription|refund|dispute", date, description, amount, currency, status, receiptUrl|hostedInvoiceUrl }]`.
- **UI:** a **Billing** tab on the dashboard (and/or extend `account-panel.tsx`): current plan + renewal date, payment method (Stripe portal link), and the unified history with receipts/invoice PDFs.
- **(Optional) API cost attribution:** populate `ApiKeyUsage.CostUsdEstimate` per call (provider cost is already computed for rewrites — `REWRITE_COST_LOG_ENABLED`) so an internal "API margin" view is possible. Internal/admin only; not customer billing. Defer unless wanted.

### D. Billing closed loop & downgrade (`BILL-04` corrected + `OPS-*`)

**The core fix — schedule the grace-expiry sweep.** Add a **Timer-triggered Azure Function** (daily, e.g. `0 0 2 * * *`) that calls `StripeEventService.ProcessExpiredPaymentGraceAsync()`. Without it, failed renewals never downgrade.

Full closed loop:
1. `invoice.payment_failed` → `PastDue` + grace (✅ exists).
2. **Grace reminder** notification at ~day 5 of 7 (❌ add; one-shot today).
3. `invoice.payment_succeeded` → recover → `Active` (✅ exists).
4. **Grace expiry (timer)** → `Inactive`, quota → free baseline, SubscriptionPaused notification (🟡 logic exists, **scheduling missing**).
5. **Stop the retry treadmill (Open Question Q2):** on downgrade, either cancel the Stripe subscription (`StripeBillingService.CancelSubscriptionAsync`) or rely on Stripe dunning settings to mark it `unpaid`/`canceled`. Also handle `customer.subscription.updated status=unpaid` as terminal.
6. **In-app PastDue state (❌ add):** a banner in `/app` + dashboard — "Payment failed — update your card by `{graceEnd}` to keep your plan" → Stripe portal. Surfaces the otherwise-invisible grace window.

**State machine (subscription quota tier):**

```
Active ──invoice.payment_failed──► PastDue(grace, keeps 90)
  ▲                                   │
  └────invoice.payment_succeeded──────┘
PastDue ──grace expires [TIMER] ──► Inactive(quota→free baseline)  ──► (on next pay) Active
PastDue ──subscription unpaid/canceled──► Inactive
```

**Also schedule (likely same gap):** `HARD-05` 30-day purge of API-originated `RewriteAttempt` `RequestJson`/`ResultJson` — confirm whether a timer exists; if not, add one (retention is a public promise — `SPEC.md` §Security).

### E. `signal`-field governance (`DEVX-`/decision)

`signal` is **already live and informational** on a succeeded result (`{draft, rewrite}` naturalness percentages; lower reads more naturally; never detection). The owner's "reserve the signal field" resolves to a **governance** decision, not new code:

- **Keep** `signal` on the `succeeded` response as documented informational reference (per `SPEC.md` decision 3) — but **mark it explicitly non-SLA / may-evolve** in the v1 reference, so callers don't build hard dependencies on the exact numbers.
- **Drop** `POST /api/v1/analyze-signal` from the public surface (it's a Non-Goal and unbuilt). Remove it from `/developers`. If a standalone signal endpoint is ever wanted, it's a separate post-Phase-2 decision (metering, abuse, banned-term framing all reopen).
- **Do not** add `signal` to the **request** (v1 input stays `{draft}` only). Reserve the name in the contract doc as "not accepted in v1."
- Banned-term posture unchanged: market "natural/concise/faithful", never "detector/bypass/undetectable".

### F. API operability leftovers (`OPS-*`) — market-standard expectations

- `OPS-01` **Rate-limit response headers** `X-RateLimit-Limit / -Remaining / -Reset` on v1 responses (standard for any commercial API; today RPM is enforced but invisible).
- `OPS-02` **Key rotation** as a first-class flow (create-new → overlap window → revoke-old) in the keys UI + a documented runbook (today: manual create+revoke; `HARD-02`).
- `OPS-03` **Per-key analytics** in the keys table (last-used ✅ already; add 30-day call count + status mix) — reuses §B aggregation filtered by `keyId`.
- `OPS-04` **Runtime currency:** Functions runtime is `dotnet-isolated` **v8**, EOL **2026-11-10** (Azure warned this turn). Plan the v8→v10 bump as a maintenance item before EOL.
- `OPS-05` **Scheduled-jobs audit:** enumerate every timer the business depends on (grace-expiry, retention purge, reconciliation, expiry reminders) and assert each is actually scheduled + alerting on failure (`OBS-01`/`HARD-04`).

---

## Data Model (new / changed)

| Entity | Change | Notes |
|---|---|---|
| `ApiKeyUsage` | **none** (reuse) | already per-call w/ timestamp + indexes; §B reads it. Optionally populate `CostUsdEstimate` (§C optional). |
| `StripeInvoice` | **new** | subscription invoice normalization for charge history (§C). PK = Stripe invoice id (idempotent upsert from webhooks). |
| `RewriteCredit` | none | pack charge history already derived from it. |
| `AppUser` | none | `SubscriptionStatus`, `PaymentFailedAt`, `PaymentGraceEndsAt` already exist (§D). |
| `ApiKeyUsageDaily` (rollup) | **deferred** | only if on-the-fly daily grouping becomes slow at volume. |

Migrations run on `main` merge against live Azure SQL (project memory `merge-to-main-applies-prod-db-migration`) — `StripeInvoice` is the only new table; review with `data-module-review` before delivery.

---

## API & Job Contracts (new)

**Portal (Entra-authed):** `GET /api/me/api-usage/summary`, `GET /api/me/api-usage/series`, `GET /api/me/api-usage/recent`, `GET /api/me/billing/history` (shapes in §B/§C).
**Programmatic (key-authed, optional):** `GET /api/v1/usage/series`.
**Jobs:** `PaymentGraceExpiryTimerFunction` (daily → `ProcessExpiredPaymentGraceAsync`); confirm/own the retention-purge timer; grace-reminder enqueue.
All new portal routes are thin Next pass-throughs to Functions, **same-origin enforced** (do not loosen the website gate); only `/api/v1/*` stays key-authed and same-origin-free.

---

## State & Error Handling

- Downgrade state machine in §D; the only new transition trigger is the timer.
- Usage/billing reads are GETs: empty windows return zeroed buckets / empty arrays (never 500); a user with no keys → all-zero summary.
- `StripeInvoice` upsert is idempotent by invoice id and reuses the existing `StripeEvents` lock/retry.
- Billing invariant (succeeded=1, else 0) is untouched — Phase 2 only **reads** usage; assert no regression.

## Security & Privacy

- No new secrets. Usage/billing endpoints are owner-scoped (a user sees only their own keys' usage and their own charges) — enforce `userId` filtering server-side; never accept a client-supplied user/key id without ownership check.
- Charge history shows amounts/receipts the user already owns; no card data (Stripe-hosted).
- Banned-term grep clean on all new copy (dev page especially). Retention promise stays "bounded 30-day", never "we don't store your data".
- `API_KEY_PEPPER`: Functions-only secret, **never** in `wrangler.jsonc vars` (committed plaintext); set live 2026-06-05.

## Rollout Plan (maps onto `DELIVERY-PLAN.md`)

Delivered via `dynamic-delivery-workflow` into an integration branch (never `main`); each id below = one issue with machine-checkable acceptance. Suggested order (safe → impactful):

1. **`DEVX-01` dev-page accuracy rewrite** (docs only, no backend risk — can ship first and early).
2. **`USE-01..03` usage endpoints + dashboard charts** (read-only; reuses `ApiKeyUsage`).
3. **`BILL-04*` downgrade timer + PastDue UI** (the real money-loop fix) + `OPS-05` jobs audit.
4. **`BILLH-01..02` `StripeInvoice` + unified billing history**.
5. **`OPS-01/02/03` rate-limit headers, rotation, per-key analytics**; **`OPS-04`** runtime bump scheduled before 2026-11-10.
6. **`E` signal governance** folded into `DEVX-01`.

Phase 2 Gate (refined): test-mode chain subscribe→submit→poll→decrement→exhaust→**fail-renew→grace→timer-downgrade→re-pay-recover** passes; dashboard summary/series/recent equal backend math; billing history shows packs + subscription invoices; dev page accurate + banned-term clean.

## Verification Plan

- **xUnit (`dotnet-backend-testing`, `resilience-test-generation`):** usage aggregation correctness (counts per day/status across multiple keys, TZ boundaries, empty windows); ownership isolation (user A can't read B's usage/keys); `StripeInvoice` idempotent upsert + webhook replay; **grace-expiry timer downgrades exactly the eligible PastDue users and not Active ones**; recovery path; billing invariant no-regression.
- **UI (`ui-browser-testing`):** dashboard renders cards + chart + recent table with seeded data; PastDue banner appears/links to portal; source-string contract tests updated (project memory `workspace-copy-test-pins-ui`).
- **Manual:** curl the new endpoints; simulate `invoice.payment_failed` in Stripe test mode → confirm grace → fast-forward/trigger timer → confirm downgrade → re-pay → recover; banned-term grep.

---

## Complete Gap / TODO backlog (the owner's "list everything still missing")

Legend — Status: ✅ done · 🟡 partial (code exists, not wired/scheduled/surfaced) · ❌ missing. Priority: **P0** money-loop correctness · **P1** core commercial UX · **P2** maturity.

### Billing closed loop (money correctness)
| id | Item | Status | Pri |
|---|---|---|---|
| BILL-04a | **Schedule grace-expiry sweep** (timer → `ProcessExpiredPaymentGraceAsync`) — *without it failed renewals never downgrade* | 🟡 | **P0** |
| BILL-04b | Cancel-or-terminalize Stripe subscription on downgrade; handle `status=unpaid` | ❌ | P0 |
| BILL-04c | In-app **PastDue banner** + grace deadline → Stripe portal | ❌ | P0 |
| BILL-04d | Grace **reminder** notification (~day 5/7) | ❌ | P1 |
| OPS-05 | **Scheduled-jobs audit** (grace, retention purge, reconciliation, reminders all actually scheduled + alert on fail) | 🟡 | P0 |
| HARD-05 | Confirm/own the **30-day retention purge** timer for API attempts | 🟡 | P1 |

### Usage statistics & visualization (req 1)
| id | Item | Status | Pri |
|---|---|---|---|
| USE-01 | `GET /api/me/api-usage/summary` (today/yesterday/MTD + remaining) | ❌ | **P1** |
| USE-02 | `GET /api/me/api-usage/series` (daily buckets) + dependency-free **bar chart** | ❌ | **P1** |
| USE-03 | `GET /api/me/api-usage/recent` + **usage-history table** (req 1c) | ❌ | P1 |
| USE-04 | Surface **remaining calls** prominently on the dev dashboard (data ✅) | 🟡 | P1 |
| USE-05 | TZ decision for "today/yesterday/month" (Q1) | ❌ | P1 |
| USE-06 | (opt) programmatic `GET /api/v1/usage/series` for API callers | ❌ | P2 |

### Billing / charge history (req 2)
| id | Item | Status | Pri |
|---|---|---|---|
| BILLH-01 | **`StripeInvoice`** entity + populate from `invoice.*` (incl. `invoice.paid`) | ❌ | **P1** |
| BILLH-02 | `GET /api/me/billing/history` unified (packs ✅ + subscription invoices + refunds/disputes) + **Billing tab UI** | 🟡 | **P1** |
| BILLH-03 | Confirm Stripe webhook endpoint subscribes to all invoice events we read | ❌ | P1 |
| BILLH-04 | (opt) populate `ApiKeyUsage.CostUsdEstimate` for internal API-margin view | ❌ | P2 |

### Developer page & API discovery (req: market standard)
| id | Item | Status | Pri |
|---|---|---|---|
| DEVX-01 | Rewrite `/developers`: accurate **async** contract, auth, **error table**, rate limits, idempotency, quota, retention; drop `analyze-signal`; remove "not public" badge | 🟡 stale | **P1** |
| DEVX-02 | **Quickstart** (create key → POST → poll → result, copy-paste) | ❌ | P1 |
| DEVX-03 | **Discovery path**: nav "Developers" entry, `/developers`→keys CTA, keys page → Usage/Billing tabs (developer dashboard) | ❌ | P1 |
| DEVX-04 | Async **polling/backoff guide** + handling failed/timeout | ❌ | P2 |
| DEVX-05 | **Pricing & quota** explainer (shared pool, per-succeeded=1, no free tier, packs) | 🟡 | P1 |
| DEVX-06 | `signal` documented **non-SLA/informational**; request stays `{draft}` only (§E) | ❌ | P1 |

### API operability / maturity
| id | Item | Status | Pri |
|---|---|---|---|
| OPS-01 | **Rate-limit headers** `X-RateLimit-*` on v1 responses | ❌ | P1 |
| OPS-02 | **Key rotation** flow + runbook | ❌ | P2 |
| OPS-03 | Per-key analytics (30-day count/status) in keys table | 🟡 | P2 |
| OPS-04 | **Runtime bump** `dotnet-isolated` v8→v10 before EOL **2026-11-10** | ❌ | P1 |
| OBS-01 | PostHog/Sentry wiring **when keys provided** (logs are fallback) | 🟡 | P2 |
| OPS-06 | (post-P2) higher-volume **API packs** pricing | ❌ | P2 |

### Deferred to Phase 4 (DX/GA) — listed for completeness
SDK (`DX-02`), webhooks/OpenAPI/MCP (`DX-05`), API ToS (`DX-04`), public GA cutover + owner quality sign-off (`LAUNCH-01`).

---

## Open Questions (need owner decision before build)

1. **Q1 — Usage TZ:** bucket "today/yesterday/month" in **Pacific/Auckland** (recommended) or UTC?
2. **Q2 — Downgrade & Stripe:** on grace expiry, **cancel the Stripe subscription** (clean, stops retries) or leave it for Stripe dunning to terminalize? (Affects whether a lapsed user auto-resumes on a later successful charge.)
3. **Q3 — Dashboard home:** put Usage/Billing as **tabs on `/developers/keys`** (→ rename to a developer dashboard) or separate routes `/developers/usage` + `/app/account` billing?
4. **Q4 — analyze-signal:** confirm **drop from public surface** for Phase 2 (§E). Re-evaluate post-GA only if there's developer demand.
5. **Q5 — Charts:** confirm **dependency-free SVG** bar chart (vs adding a small chart lib) to honour the minimal-deps + warm-theme constraint.
6. **Q6 — Pre-pepper key:** the test key created before 2026-06-05 is now invalid (pepper turned on). Confirm you'll **regenerate** it (no action needed from me beyond this note).
