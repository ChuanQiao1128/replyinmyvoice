# Production-Readiness Audit — Architecture / B2B API / Billing

> Source: multi-agent audit (33 agents, adversarially verified against the actual code), 2026-06-18.
> Verdict: **well above demo quality.** Most hard parts are genuinely production-grade.

## Hands-on verification outcome (2026-06-18) — READ THIS FIRST

The three "P0" items were each implemented by hand and run against the full 800-test suite. Two of
the three turned out NOT to be defects on close reading. Net real change shipped: **P0-3 only.**

| Item | Audit said | Hands-on truth | Action |
|------|-----------|----------------|--------|
| **P0-1** outbox at-least-once / SB dedup | duplicate rewrite job → duplicate charge | **Not a money bug.** The rewrite-job *consumer* (`ProcessRewriteJobHandler`) is already idempotent by attempt status (no-op if Succeeded/Failed/Expired; conditional `MarkProcessing` claim blocks concurrent double-process) → the engine runs **at most once per attempt**. Real residual = duplicate *notification emails* (Resend is sent directly, not DB-enqueued) on a crash between send and MarkSent → **annoyance, not money → P1.** SB duplicate-detection is belt-and-suspenders infra (immutable queue property; needs Azure CLI/bicep, not app code). | **Reclassified P1.** No code change. |
| **P0-2** PastDue gets paid quota on web path | failed-payment user keeps ~90 free rewrites | **False positive — intentional.** Test `ProcessWebhookEventAsync_subscription_past_due_keeps_paid_grace_state` explicitly asserts PastDue → `"paid"`; there is a deliberate dunning **grace window** (`PaymentFailedAt`/`PaymentGraceEndsAt` + `ProcessExpiredPaymentGrace` job that transitions users out of past_due when grace ends). The V1 API being stricter is a defensible separate policy, not proof of a bug. My change broke the grace feature; the full-suite run caught it; **reverted.** | **Reverted.** Open question for owner below. |
| **P0-3** partial-refund `Math.Ceiling` over-claws | over-grants / no refund-object dedup | **Rounding half is real** (33% refund of 10 credits revoked 4, leaving 6 instead of fair 7) → fixed with `Math.Round(AwayFromZero)`. The **dedup half is moot**: clawback is recomputed from Stripe's *cumulative* `amount_refunded` + preserved `OriginalAmountGranted`, so repeated/partial refunds are idempotent (no compounding) — no per-refund-object table added. | **Shipped** (`StripeEventPayloadSynchronizer` + test). |

**Open question for the owner (P0-2 follow-up):** consumer web access keeps a paid grace window when a
subscription goes `past_due`, but the public B2B API (`HasPaidApiEntitlementHandler`) excludes `past_due`.
Is that web-vs-API divergence intended (API stricter), or should both honor the same grace policy? This is
a product decision, not an auto-fixable bug.

**Genuine residual hardening promoted out of "P0":**
- (a) ✅ **DONE** (`e301edc`) — idempotent notification send: the outbox message id is threaded down the
  notification chain as a Resend `Idempotency-Key`, so a crash-redispatch can't double-email. Each link is
  test-pinned.
- (b) enable Service Bus duplicate detection on the rewrite-jobs queue (infra; belt-and-suspenders since the
  consumer is already idempotent). Still P1.
- (c) **NEW follow-up** (surfaced by adversarial review of (a)) — notification sends are not retried when
  Resend returns a *transient* error: `ResendNotificationEmailProvider` returns `Skipped` (not throw), so the
  outbox marks the message Sent and the email is lost rather than retried. This is pre-existing and separate
  from the dedup fix, but (a) is the prerequisite that makes adding retry-on-transient-error duplicate-safe.
  Scope carefully: retry only transient (5xx/429), not permanent (4xx/invalid recipient). P1.

## P0 (original audit framing — superseded by the table above)

### P0-1 · Outbox at-least-once window + Service Bus dedup never enabled  · `architecture` · effort M
- **What today:** handler publishes the side effect, THEN `MarkSent` + `SaveChanges` run separately
  under a ~30s lease. `MessageId` is set but `RequiresDuplicateDetection` is configured nowhere, so
  the dedup key is inert. Stripe notification handlers have no dedup either.
- **Failure:** a worker crash between publish and mark-sent leaves the row `Processing`; lease expires;
  another worker re-runs the handler → duplicate rewrite jobs (→ duplicate processing cost) and
  duplicate payment emails.
- **Fix:** enable Service Bus duplicate detection on the rewrite-job queue; for Stripe notification
  handlers commit `MarkSent` BEFORE the side effect (or add a dedup table keyed on `MessageId`);
  add a fail-after-success test.
- **Files:** `DispatchDueOutboxHandler.cs`, `AzureServiceBusRewriteJobPublisher.cs`,
  `StripeNotificationOutboxMessageHandlers.cs`

### P0-2 · PastDue users get full paid quota on the web rewrite path  · `billing` · effort S
- **What today:** `AccountUsagePlanProvider` groups `PastDue` with `Active` and returns
  `QuotaLimit = 90`. The **V1 API path already excludes PastDue correctly** — the web path diverges.
- **Failure:** a failed-payment user keeps full access for the grace window (~90 rewrites, ≈NZ$45 of
  unrecovered access) on the web path.
- **Fix:** return a grace tier for `PastDue` in `AccountUsagePlanProvider`; add a status check in
  `CreateRewriteAttemptHandler` returning `subscription_past_due`; add a test mirroring the API path.
- **Files:** `AccountUsagePlanProvider.cs`, `CreateRewriteAttemptHandler.cs`, `RewriteHttpFunctions.cs`

### P0-3 · Partial-refund `Math.Ceiling` over-grants + no refund-object dedup  · `billing` · effort M
- **What today:** `Math.Ceiling` over-grants (a 33% refund of 10 credits returns 4 → customer keeps 6,
  not 7, compounding). Same-event replay is guarded, but two distinct refund objects in separate
  events are indistinguishable (no refund-object ID tracking).
- **Fix:** use `Math.Round(AwayFromZero)` or floor per the chosen rule; track cumulative refunded
  amount + processed refund-object IDs on `RewriteCredit`; add a multi-refund-object test.
- **Files:** `StripeEventPayloadSynchronizer.cs`, `RewriteCredit.cs`

## P1 — should fix (hardening a competent team expects)

| # | Subsystem | Item | Effort |
|---|-----------|------|--------|
| P1-1 | architecture | Dead-letter table + authenticated admin requeue (serves outbox-recovery, Stripe-poison, webhook-delivery) | M |
| P1-2 | architecture | Distributed tracing — no OpenTelemetry/`ActivitySource`/`traceparent`; CorrelationId logged but not propagated across Api→outbox→worker | M |
| P1-3 | architecture | Worker has no `StopAsync` drain → SIGTERM mid-handler can re-dispatch (subsumed once P0-1 lands, but make explicit) | S |
| P1-4 | architecture | No post-startup secret re-validation/rotation — a rotated Stripe key silently fails until restart | M |
| P1-5 | b2b-api | Failed webhook deliveries: no per-customer metric, no customer status endpoint, no admin retry (selling webhooks = blocking) | M |
| P1-6 | b2b-api | OpenAPI spec hand-maintained, confirmed drift (omits `rate_limit_unavailable`/`not_found`/`rewrite_failed`, 500/503) → adopt code-gen or CI contract test | M |
| P1-7 | b2b-api | API-key pepper has no version/rotation → a compromise forces regenerating every customer key | M |
| P1-8 | b2b-api | `ApiKey.Scope` stored but never set/returned/enforced → false least-privilege; enforce-or-remove before B2B launch | M |
| P1-9 | billing | Expired-reservation Timer has no try-catch/alert → fails silently, locks credits | S |
| P1-10 | billing | Expired-credits returns generic `quota_exhausted`; handler's `ErrorCode` never surfaced by the HTTP function | S |

## P2 — polish
- p99 handler-duration metric + alert (subsumed once P0-1 + StopAsync land).
- Global cross-key/system rate limit; consider token-bucket per tier.
- Document webhook timestamp ±5-min validation with example code (docs only).
- `invoice.payment_action_required` (3DS) not marked PastDue — low practical risk given checkout UX.
- Private setters + domain methods on quota entities — low-value clean-code upgrade (SQL already enforces invariants).

## Already production-grade — DO NOT churn
Transactional outbox + competing-consumer lease (Serializable); quota reservation lifecycle (15-min TTL
+ cleanup); Stripe inbound idempotency (EventId PK + RowVersion + Serializable retry + poison detection);
RowVersion on all money-affecting entities; API-key crypto (secure RNG, Base62, SHA-256+pepper, last-4
separate, plaintext-once, masked on list) + atomic rotate-then-revoke; rate limiter (Serializable +
backoff + fail-closed 503, concurrency-tested); webhook security (HMAC-SHA256 over timestamp+body,
set-time SSRF prevention); HTTP hardening (request size limits, correlation-ID middleware); daily
reconciliation timer + alert; observability baseline (App Insights, terminal-failure telemetry,
health-ready failed-count checks, sev-2 alert); migration-discipline scanner; correct DI lifetimes.

## Recommended sequence
1. **This week, by hand (money-critical — keep OUT of the autonomous wave; one issue each → integration branch, never main):**
   P0-2 (S, mirrors the API path) → P0-1 SB-dedup flag + Stripe-notification window (cheap half) →
   P0-3 refund rounding (M, careful test review).
2. **Batch 2 → Codex delivery wave, each gated by a failing test:** P1-1 dead-letter+requeue, P1-2
   tracing, P1-3 StopAsync drain, P1-9 Timer alert, P1-10 ErrorCode plumbing, P1-6 OpenAPI drift check.
3. **Batch 3, pre-B2B-launch:** P1-7 pepper rotation, P1-8 Scope enforce-or-remove, P1-4 secret
   re-validation, P1-5 webhook delivery alerting/retry, global rate limit, webhook replay docs.
