# Payment Module Audit & Commercialization Plan

- **Branch:** `audit/payment-module`
- **Date:** 2026-06-01
- **Author:** Claude (supervisor) — read-only audit, cross-checked against source
- **Model in production:** **credit-packs** (not subscription-first)
  - Free 3 (lifetime) · Quick NZ$2.50 = 10 · Value NZ$6.90 = 30 · Pro·API NZ$19.90/mo = 90 · (hidden Focus NZ$4.90 = 20)
- **Stack:** Next.js (Cloudflare Worker, thin proxy) → C# Azure Functions (`replyinmyvoice-func-dev`) → Azure SQL (EF Core)

> Scope: this document inventories what the payment module already has, what is defined-but-undone, what is missing entirely, the test posture, and a prioritized roadmap to *real commercial use* (accepting real NZ$ payments reliably). No secret values are recorded here — key names only.

---

## 0. Verdict (TL;DR)

The payment **code path is materially complete and unusually well-tested for its stage**: hosted Stripe Checkout for 4 SKUs, signature-verified + idempotent + transactional webhook ingestion, pack credit-grant with 90-day expiry, two-tier quota with transparent credit-overflow, refund/dispute clawback (full + prorated), admin refund/credit/suspension with audit log, billing portal, and leaked-reservation reclamation. Live flags are already flipped (`LAUNCH_CONFIRMED=true`, `STRIPE_LIVE_CUTOVER_APPROVED=true`), pack price vars are set on both the Worker and the Functions app.

**What stands between "code complete" and "real revenue" is mostly operational + three engineering hardening items, not a rewrite:**

1. **No real NZ$ transaction has ever run end-to-end** (M7-001 is an owner-only manual checkpoint, never executed). This is the single biggest gap.
2. **Subscription renewal-failure path is incomplete** — no `invoice.payment_failed` handler, no grace-period decision, and `MapSubscriptionStatus` behavior for `past_due`/`unpaid`/`incomplete` is unverified. Pro/API *happy path* works (status-driven quota); the *failure* path does not reliably revoke access.
3. **Production payment observability is off** — `SENTRY_DSN` / `POSTHOG_API_KEY` exist in `.env.local` but are not wired into the Worker/Functions runtime, so failed payments and webhook failures are detected only by customer reports.

Everything else is enhancement (receipts, reconciliation, GST, admin UI, e2e tests, campaign codes).

### Corrected finding (important)

A prior reading claimed *"Pro/API subscribers are charged but never granted 90 rewrites."* **This is false.** Verified in code: `pro_api` checkout is `mode=subscription` (no credit grant), but `customer.subscription.created/updated` sets `SubscriptionStatus=Active`, and `GetUsagePlan` grants 90/period purely by status ([AccountService.cs:292](../backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs)). Pro/API works on the happy path. The real defect is the **renewal-failure** path (item 2 above), not the initial grant.

---

## 1. Architecture snapshot

| Layer | Where | Role |
|---|---|---|
| Pricing UI | `app/pricing/page.tsx`, `components/landing/buy-button.tsx`, `components/landing/pricing-v2.tsx` | Renders packs; `BuyButton` POSTs `{sku}`; disables button if price env unset |
| Proxy | `app/api/stripe/{checkout,portal,webhook}/route.ts`, `app/api/me/route.ts` | Same-origin + Entra bearer; forwards to C#; no billing logic locally |
| Billing service | `…/Infrastructure/Services/StripeBillingService.cs` | Checkout/portal session creation, refund client, API-version pin, SKU map |
| Webhook + entitlement | `…/Functions/Functions/StripeWebhookFunction.cs`, `…/Infrastructure/Services/StripeEventService.cs` | Signature verify, dedup, transactional grant/clawback/subscription-sync |
| Quota | `…/Infrastructure/Services/QuotaService.cs`, `…/AccountService.cs` | Reserve→finalize/release; credit-overflow; status-driven paid plan |
| Admin | `…/Infrastructure/Services/AdminService.cs`, `…/Functions/Functions/AdminHttpFunctions.cs` | Read endpoints + refund/credit/suspend (audited) — **API only, no UI** |
| Cleanup | `ExpiredReservationCleanup{Service,TimerFunction,Worker}` | Reclaims leaked reservations every 5 min |

**Data model (EF Core, `…/Infrastructure/Data/AppDbContext.cs`):**
- `RewriteCredit` — credit ledger **and** payment record in one table (`Source` PURCHASE/ADMIN/ERASED, amounts, `ExpiresAt`, `StripeEventId` unique, `StripePaymentIntentId`/`Sku`/`AmountTotal`/`Currency`). **No `receipt_url` captured.**
- `StripeEvent` — dedup table (PK `EventId`, status machine Processing/Processed/Failed, lock-until, attempt count).
- `AppUser` — `StripeCustomerId`, `StripeSubscriptionId`, `SubscriptionStatus`, `CurrentPeriodEnd`, `SuspendedAt`.
- `UsagePeriod` / `UsageReservation` — quota counters + in-flight holds (reservation can be credit-backed via `RewriteCreditId`).
- `AdminAuditLog` — refund/grant/suspend trail.

---

## 2. DONE — implemented & merged (by capability)

All under the `SO-*` site-overhaul wave + `SO-R*` resilience wave, merged 2026-05-30. Earlier `M*` items merged 2026-05-21.

| Capability | Issue/PR | Note |
|---|---|---|
| Hosted Checkout, 4 SKUs, per-SKU price-env map | (core) | `payment` for packs, `subscription` for pro_api |
| Get-or-create Stripe customer keyed to Entra `oid` | (core) | persisted on `AppUser` |
| Signature-verified webhook, fail-closed on missing secret | #288 (SO-022) | unsigned only in `Testing` |
| Pack credit grant on `checkout.session.completed` | #285 (SO-020) | +90d expiry; captures PI/sku/amount/currency; unique `StripeEventId` |
| Subscription status sync | #14 (M1-008) + core | `customer.subscription.*` → status/period |
| Two-tier quota + credit overflow (FIFO-by-expiry) | (core) | `QuotaService.ReserveAsync` |
| Refund clawback (full + prorated, floored at consumed) | #286 (SO-021) | `charge.refunded` |
| Dispute clawback | #286 (SO-021) | `charge.dispute.created/closed` |
| Idempotent partial-refund across multiple events | #317 (SO-R04) | cumulative-original math |
| Atomic grant + mark-processed (one txn) | #316 (SO-R03) | serializable |
| Duplicate-grant unique-violation = idempotent success | #327 (SO-R14) | |
| Reconcile orphan paid-checkout (before user row) | #329 (SO-R16) | retry-after-user-exists |
| Reclaim leaked quota reservations | #314 (SO-R01) | 5-min sweep |
| Pin Stripe API version `2025-08-27.basil` | #325 (SO-R12) | asserted, not set |
| EF transient-fault retry | #315 (SO-R02) | Azure SQL |
| Structured logging + correlation IDs | #323 (SO-R10) | rewrite/payment pipeline |
| Deep readiness/health endpoint | #324 (SO-R11) | SB / outbox / stuck reservations |
| Purchase history API `GET /api/me/payments` | #287 (SO-023) | caller-scoped |
| Per-source usage breakdown | #283 (SO-034) | free vs pack |
| Admin read endpoints (users/usage/payments/cost/sub) | #291 (SO-041) | admin-gated |
| Admin issue refund (Stripe, audited, idempotency-keyed) | #294 (SO-042) | |
| Admin grant/adjust credits (audited) | #297 (SO-047) | |
| User suspension | #295 (SO-048) | enforced in rewrite path |
| AdminAuditLog | #292 (SO-046) | |
| Same-origin guards on checkout/portal/access-token | #282 (SO-013) | |
| Account deletion: cancel sub + erase payment data | #281 (SO-014) | GDPR-style |
| Terms page: billing/cancellation/refund/disputes (NZ law) | #172 (M4-008) | 14-day good-faith + CGA 1993 |
| Support runbook + observability runbook | (docs) | `docs/support-runbook.md`, `docs/observability.md` |

---

## 3. Critical findings & risks (verified against source)

### F1 — Subscription renewal-failure path is incomplete (HIGH)
- No `invoice.payment_failed` / `invoice.payment_succeeded` handler anywhere (grep confirmed empty).
- Quota is **status-driven**: `GetUsagePlan` returns paid 90 for `Active/Trialing/Testing`. Renewal works only because Stripe later emits `customer.subscription.updated` with a new `current_period_end` (rotating the `PeriodKey` → quota resets).
- **Risk:** a failed renewal relies entirely on Stripe emitting `customer.subscription.updated` with a non-active status, **and** on `MapSubscriptionStatus` mapping `past_due`/`unpaid`/`incomplete` to a non-active value. That mapping is **unverified**. No grace-period was ever decided (spec open question). Worst case: a customer whose card fails keeps paid access, or loses it abruptly with no dunning.
- **Fix:** verify `MapSubscriptionStatus`; add `invoice.payment_failed` handler (downgrade/notify) + a `past_due` grace-period decision; optionally adopt the spec's `invoice.payment_succeeded` grant model for cleaner billing-period accounting.

### F2 — No production payment observability (HIGH)
- `SENTRY_DSN` and `POSTHOG_API_KEY` key names exist and are non-empty in `.env.local`, but are **not** in `wrangler.jsonc` vars nor wired into the Functions runtime. `docs/observability.md` + `support-runbook.md` treat M7-002/M7-003 as not-yet-wired.
- **Risk:** failed payments, webhook delivery failures, and checkout errors are invisible until a customer complains. For real money this is not acceptable.
- **Fix:** wire Sentry + PostHog into Worker vars + Functions settings; emit `checkout_started`, `payment_succeeded`, `payment_failed`, `webhook_failed` events; alert on webhook-failure + payment-error rate.

### F3 — Full purchase→grant→refund never run end-to-end (HIGH)
- M7-001 (first real purchase) is a perpetual owner-only checkpoint, never executed. No e2e test (SO-060 never created). `business-qa-and-deploy-result.md` confirms sandbox-only.
- **Risk:** the live checkout → signed webhook → grant → balance-increment → refund-clawback chain has never been exercised against real Stripe live-mode data. Unknown unknowns (live price IDs, live webhook secret, receipt emails, portal config) surface only on first real charge.
- **Fix:** build the e2e test (Stripe test-mode), then owner executes one real NZ$ purchase + small refund and records it.

### F4 — No receipt / invoice capture (MEDIUM)
- `receipt_url` is never persisted (SO-020 captured PI/sku/amount/currency but not the receipt URL the spec lists). No tax-invoice generation.
- **Risk:** customer-support and reconciliation have no canonical receipt link; accounting has no document trail.
- **Fix:** capture `receipt_url` (from the charge/payment-intent) onto `RewriteCredit`; surface it in `/api/me/payments` and the (future) admin UI; rely on Stripe-emailed receipts short-term.

### F5 — Admin refund/credit is API-only, no UI (MEDIUM)
- All admin **backend** shipped (#291/#294/#297/#295) but SO-044 `/admin` UI was deferred to Wave-2 and never issued.
- **Risk:** the owner cannot issue a refund or adjust credits without hand-crafting an authenticated API call. Operationally fragile under a real support load.
- **Fix:** build `/admin` (users list, per-user detail, refund/credit/suspend actions) — Wave-2 issue SO-044.

### F6 — No financial reconciliation / GST posture (MEDIUM, compliance watch)
- No job reconciles Stripe payouts vs `RewriteCredit` grants. GST is intentionally deferred (prices set inclusive in anticipation, Stripe Tax off, not yet registered, no turnover tracker toward IRD NZ$60k).
- **Risk:** drift between Stripe and internal ledger goes unnoticed; crossing the GST threshold without a tracker is a compliance liability.
- **Fix:** nightly reconciliation report; cumulative-turnover tracker with an alert approaching NZ$60k; enable Stripe Tax + register before crossing.

### F7 — Async/delayed payment events unhandled (LOW–MEDIUM)
- No handler for `checkout.session.expired`, `*_async_payment_succeeded/failed`, or `payment_intent.*`. Only card-immediate `payment_status=paid` grants credits.
- **Risk:** if any non-card / delayed-settlement method is ever enabled, those purchases never grant credits. Low risk while card-only, but a silent trap.

### F8 — API-version pin is asserted, not set (LOW)
- `EnsureStripeApiVersionPinned()` throws if `StripeConfiguration.ApiVersion` ≠ pinned string, but nothing *sets* it — it asserts the SDK's compiled-in default. An SDK bump changing the default throws at runtime rather than pinning explicitly.
- **Fix:** explicitly set `StripeConfiguration.ApiVersion = PinnedStripeApiVersion` (or `RequestOptions.StripeVersion` per call) instead of only asserting.

---

## 4. DEFINED BUT NOT DONE (open work, in plans/specs but no merged issue)

| Item | Source | Status |
|---|---|---|
| Pro/API `invoice.payment_succeeded` grant model + `invoice.payment_failed` | `docs/rewrite-packs-pricing-spec.md` §API Contracts | defined, not implemented (see F1) |
| Campaign codes (`CampaignCode`/`CampaignRedemption`, `POST /api/campaign/redeem`, seed XHS/TIKTOK/IG/LAUNCH/FORUM, 1/user cap) | pricing-spec §Data Model / Phase 3 | zero code |
| Ledger backfill of existing users (SIGNUP/legacy SUBSCRIPTION grants) | pricing-spec §Data Model `[ADDED]` | not done (low risk if no legacy paid users) |
| `/admin` UI | SO-044 (Wave-2) | backend done, UI not issued (F5) |
| In-app receipts UI + pack-buyer portal | SO-023 (UI part, Wave-2) | API done, UI not issued |
| "My rewrites" history UI | SO-032 (Wave-2) | backend done, UI not issued |
| Usage-breakdown UI | SO-034 (UI part, Wave-2) | API done, UI not issued |
| Account UI (manage-billing, delete) | SO-012 (Wave-2) | not issued |
| Playwright e2e `checkout→quota` + `refund→clawback` | SO-060 (Wave-2) | never created (F3) |
| Full gate-run doc/script | SO-061 (Wave-2) | not created |
| Owner runbook for manual purchase + refund checkpoints | SO-062 (Wave-2) | not created |
| First real Stripe purchase + refund verification | M7-001 | owner-only, never executed (F3) |
| PostHog / Sentry instrumentation | M7-002 / M7-003 | runbooks merged, instrumentation not wired (F2) |

---

## 5. GAPS NOT PLANNED (commercial-billing must-haves, absent from all plans)

| Gap | Why it matters |
|---|---|
| GST / tax handling (Stripe Tax, `automatic_tax`, tax invoices) | NZ GST-inclusive pricing + IRD NZ$60k threshold (F6) |
| Tax-compliant receipts/invoices (incl. `receipt_url`) | customer + accounting document trail (F4) |
| Financial reconciliation / ledger audit | catch drift / missed webhooks (F6) |
| Failed-payment dunning + retry notifications | subscription renewal failures (F1) |
| Accounting export (CSV/API) | bookkeeping / tax filing |
| Multi-currency | hard-NZD today; non-NZ buyers |
| Purchase-side fraud/abuse controls (Radar, velocity, refund-abuse cap) | card-testing / chargebacks |
| Credit-expiry user notification | packs expire +90d silently |
| Customer billing-support / self-serve refund-request channel | refunds are owner-API-only |
| PCI / SAQ-A compliance posture doc | standard even with Stripe Checkout |
| Price change / versioning strategy | grandfather buyers, protect in-flight checkouts |
| Dispute / chargeback ops runbook | evidence submission, SLA, repeat-disputer policy |
| Stripe webhook delivery alerting + replay-from-dashboard runbook | idempotency exists in code; ops procedure doesn't |

---

## 6. Test coverage

**Strong (no gap):** idempotency keys + unique indexes consistently enforced and tested (`StripeEventId`, `RequestId`, idempotency-key); duplicate-grant and duplicate-cost-log **races** tested with real interceptor-driven constraint collisions; refund math (prorated, clamp-to-consumed, cumulative multi-event); transactional-outbox + reservation-expiry + worker-redelivery crash-recovery chain. ~57 payment tests across 10 xUnit files (300 total backend tests discovered).

**Resilience matrix:**

| Scenario | Status |
|---|---|
| Webhook replay (same event id) | ✅ COVERED |
| Duplicate event (grant-once) | ✅ COVERED |
| Checkout before user row exists | ✅ COVERED |
| Refund before its grant exists | ⚠️ MISSING |
| Stripe API failure/timeout (checkout-create / refund) | ❌ MISSING (fakes always succeed) |
| Rewrite-provider failure | ✅ COVERED |
| Rewrite-provider timeout/cancellation | ⚠️ PARTIAL |
| Concurrent purchase (duplicate-grant race) | ✅ COVERED |
| True parallel quota reserve-race on last slot | ⚠️ PARTIAL (sequential only) |
| Worker crash mid-rewrite | ✅ COVERED |
| Partial refund applied multiple times | ✅ COVERED |
| Forged/tampered signature (valid-secret, bad v1) | ⚠️ MISSING (only *missing* sig/secret covered) |
| Missing webhook secret | ✅ COVERED |

**Highest-value test gaps for launch:**
1. **Forged/tampered webhook signature** (security-critical): present-but-invalid `Stripe-Signature` (wrong secret, mutated payload, stale-timestamp replay) → 400 + zero credits.
2. **Stripe API call failure** on checkout-create and refund (fakes only ever succeed) → 5xx, no partial DB state, no audit log on refund failure.
3. **Refund arriving before its grant** (out-of-order on the credit side) → handled safely, no negative balance.
4. **API-version pin guard** → flip `StripeConfiguration.ApiVersion`, assert throw.
5. **True concurrent quota reservation race** (`Task.WhenAll`, file-backed SQLite+WAL) → exactly one `Created`, one `QuotaExceeded`.
6. **Worker timeout/cancellation** → reservation released with timeout `ErrorCode`, quota refunded.
7. **Frontend checkout flow** (vitest/Playwright): `BuyButton` 401→sign-in redirect, success→redirect-to-Stripe, error→inline message. Currently only a static string-contract test exists.

---

## 7. Completion roadmap (prioritized)

### P0 — Close before trusting the module with real money
1. **F1 renewal-failure**: verify `MapSubscriptionStatus` mapping; add `invoice.payment_failed` handler + `past_due` grace-period decision. *(Codex; state-machine-modeling + resilience-test-generation)*
2. **F2 observability**: wire Sentry + PostHog into Worker + Functions; alert on `webhook_failed` + payment-error rate. *(Codex + owner for monitor setup)*
3. **Security/robustness tests** (gaps 1 & 2): forged-signature rejection + Stripe-API-failure on checkout/refund. *(Codex; dotnet-backend-testing)*
4. **F4 receipt_url** capture + expose in payments API. *(Codex; data-module-review)*

### P1 — Commercial quality (around / shortly after launch)
5. **F3 e2e** Playwright `checkout→quota` + `refund→clawback` (test mode) — SO-060. *(Codex; ui-browser-testing)*
6. **F5 `/admin` UI** — refund/credit/suspend + per-user detail — SO-044. *(Codex; ui-browser-testing)*
7. **F6 reconciliation** nightly Stripe-vs-ledger report + GST turnover tracker/alert. *(Codex)*
8. **F1 dunning** failed-payment + credit-expiry notifications. *(Codex)*
9. Remaining tests (gaps 3–7): refund-before-grant, API-version pin, concurrent quota race, worker timeout, frontend checkout flow. *(Codex; dotnet-backend-testing + ui-browser-testing)*
10. **M7-001** owner executes one real NZ$ purchase + small refund; record in `decisions-log.md`. *(Owner)*

### P2 — Enhancements / scale
11. Campaign codes (spec Phase 3); ledger backfill; multi-currency; price versioning; dispute ops runbook; customer self-serve refund channel; purchase-side fraud controls; accounting export; PCI/SAQ-A doc; async-payment event handlers (F7); explicit API-version set (F8).

---

## 8. Path to first real NZ$ payment (ordered; **[OWNER]** = dashboard/hands-on, rest automatable via Codex)

1. **[OWNER]** Activate Stripe live account (KYC, payout bank, 2FA, statement descriptor, public name "TimeAwake Ltd / Reply In My Voice", support email).
2. **[OWNER]** Create/confirm LIVE prices for Quick/Value/Pro·API (the old cutover doc still references the retired NZ$9 single price); capture `price_live_…` IDs.
3. **[OWNER]** Create LIVE webhook endpoint `https://replyinmyvoice.com/api/stripe/webhook`; select min events: `checkout.session.completed`, `customer.subscription.created/updated/deleted`, `charge.refunded`, `charge.dispute.created`, `charge.dispute.closed` — **and decide on `invoice.payment_succeeded/failed` (F1)**. Capture live `whsec_…`.
4. **[OWNER]** Configure live Customer Portal (cancel + payment-method + invoices) + customer receipt/refund emails (From/Reply-To = support).
5. **[OWNER]** Put live secret values into `.env.local` + Cloudflare Worker secrets + Azure Functions settings + GitHub Actions secrets (key names only in any tracked file).
6. **(Codex)** Implement the F1 invoice/renewal decision + F2 observability + P0 tests.
7. **(Codex)** Run live preflight (`npm run stripe:live-preflight`) — verify `livemode=true`, currency `nzd`, unit amounts, webhook events, portal-ready (redacted output only); then `cf:deploy`.
8. **(Codex)** Live smoke: checkout-session returns `checkout.stripe.com` URL; signed webhook accepted; unsigned/forged → 400.
9. **[OWNER]** Execute ONE real NZ$ purchase (M7-001 — automation must never initiate a real charge per AGENTS.md hard limits).
10. Verify end-to-end: Stripe shows live payment; `StripeEvent` Processed; `RewriteCredit` PURCHASE +90d granted; `/app` balance +10; a paid rewrite consumes 1. Pro/API: subscription Active, 90 visible, portal cancel works.
11. **[OWNER]** Issue a small real refund; confirm clawback fires; record decision.
12. Post-launch: start GST turnover tracking toward NZ$60k.

---

## 9. Proposed issue breakdown (ready for delivery-pipeline / Codex)

> One issue per row. Acceptance must be machine-checkable. Skill column = which project skill governs the work.

| Proposed ID | Title | Priority | Owner/Codex | Skill |
|---|---|---|---|---|
| PAY-01 | Verify `MapSubscriptionStatus` + add `invoice.payment_failed` handler + `past_due` grace policy | P0 | Codex | state-machine-modeling |
| PAY-02 | Wire Sentry + PostHog into Worker + Functions; emit + alert on payment/webhook failure events | P0 | Codex (+owner monitors) | cloud-architecture-cost-review |
| PAY-03 | Test: forged/tampered webhook signature rejected (bad v1 / mutated body / stale ts) | P0 | Codex | dotnet-backend-testing |
| PAY-04 | Test: Stripe API failure on checkout-create + refund (5xx/timeout) → no partial state | P0 | Codex | resilience-test-generation |
| PAY-05 | Capture `receipt_url` on `RewriteCredit`; expose in `/api/me/payments` | P0 | Codex | data-module-review |
| PAY-06 | Playwright e2e: `checkout→quota` + `refund→clawback` (Stripe test mode) — SO-060 | P1 | Codex | ui-browser-testing |
| PAY-07 | `/admin` UI: users list, per-user detail, refund/credit/suspend — SO-044 | P1 | Codex | ui-browser-testing |
| PAY-08 | Nightly Stripe-vs-ledger reconciliation report + GST turnover tracker/alert | P1 | Codex | cloud-architecture-cost-review |
| PAY-09 | Failed-payment dunning + credit-expiry notifications | P1 | Codex | state-machine-modeling |
| PAY-10 | Tests: refund-before-grant, API-version pin, concurrent quota race, worker timeout, frontend checkout flow | P1 | Codex | dotnet-backend-testing |
| PAY-11 | First real NZ$ purchase + refund verification | P1 | **Owner** | — |
| PAY-12 | Campaign codes (spec Phase 3) | P2 | Codex | data-module-review |
| PAY-13 | Async-payment event handlers + explicit API-version set + ledger backfill | P2 | Codex | resilience-test-generation |

---

## Appendix — key file map

- Checkout/portal/refund client: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs`
- Webhook entrypoint: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeWebhookFunction.cs`
- Event processing / grant / clawback / subscription-sync: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`
- Quota + credit overflow: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`
- Account summary + `GetUsagePlan`: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`
- Admin (refund/credit/suspend/read): `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`, `…/Functions/Functions/AdminHttpFunctions.cs`
- Data model: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`, `…/Domain/Entities/{RewriteCredit,StripeEvent,AppUser,UsagePeriod,UsageReservation,AdminAuditLog}.cs`
- Frontend: `app/pricing/page.tsx`, `components/landing/buy-button.tsx`, `app/api/stripe/*/route.ts`, `app/api/me/route.ts`
- Tests: `backend-dotnet/tests/ReplyInMyVoice.Tests/{StripeEventServiceTests,StripeWebhookApiTests,StripeBillingApiTests,QuotaServiceTests,AdminRefundTests,AdminCreditAdjustTests,RewriteJobProcessorTests,OutboxDispatcherTests,ExpiredReservationCleanupServiceTests,RewriteCostTrackingTests}.cs`
- Specs/docs: `docs/rewrite-packs-pricing-spec.md`, `docs/support-runbook.md`, `docs/observability.md`, `docs/stripe-live-mode-cutover.md`, `plans/site-overhaul/REQUIREMENT.md`
- Config: `wrangler.jsonc` (Worker price vars + `LAUNCH_CONFIRMED`), `.env.local` (key names only)
