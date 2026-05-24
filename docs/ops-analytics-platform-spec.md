# Ops & Analytics Platform — Decision Record + Implementation Spec

- **Date:** 2026-05-24
- **Owner:** TimeAwake Ltd (ChuanQiao1128)
- **Status:** Accepted (planning) — implementation not started
- **Relationship to ADR-0001:** This realizes the deferred analytics direction in `docs/architecture-decision-record.md` §4.2 + §10. It **diverges on transport** (Kafka/K8s instead of Service Bus/Function App) and records why below. It does **not** change the transactional backend decision in ADR-0001.
- **Skills applied:** `cloud-architecture-cost-review` (option + cost gate), `system-spec-synthesis` (this spec). Logged in `docs/skill-run-log.md` (2026-05-24).

---

## 0. TL;DR

Build a **decoupled internal ops/analytics plane** that consumes business events and powers a personal admin dashboard (cost, quality, users, API sales, alerting). It is **separate from the customer-facing revenue path** — if it fails, payments and rewrites are unaffected (at-most analytics lag).

Two distinct goals, two tracks:

| Goal | Track | Cost |
|---|---|---|
| **A. A usable ops dashboard** (cost / quality / users / API sales) | Extend the existing Next.js `/admin` + Neon Postgres (Phase 0) | ~$0 |
| **B. Demonstrable Kafka / K8s / Docker / Python** | A local-first event-streaming ops sidecar (Phases 1–3) | ~$0 local; optional ephemeral VM $15–40/mo |

**Rejected:** an always-on managed AKS + Confluent/Event Hubs stack (~$150–600+/mo) for a pre-revenue, low-traffic workload whose data already lands in Postgres. Managed always-on infra stays behind the `AZURE_ALLOW_PAID_RESOURCES` / `AZURE_BUDGET_LIMIT` gate.

---

## 1. Context & goals

### 1.1 Why this is two goals, not one

The owner wants (A) a real personal system to watch the business, and (B) a portfolio artifact demonstrating Kafka, Kubernetes, Docker, and Python. These have different optimal solutions. Conflating them produces the worst outcome: a complex *and* expensive system for a workload that doesn't need it.

- **Goal A is ~80% already built** (see §4). The remaining work is a handful of SQL-backed admin views — no streaming infra required.
- **Goal B is worth building**, but the cost-honest way is **local-first** (Docker Compose + a Kafka-compatible broker + a local Kubernetes), not a 24/7 managed cluster.

### 1.2 Relationship to ADR-0001 (the honest divergence)

ADR-0001 §10 already specifies a future Python analytics plane, but via **Azure-native serverless**: a Python Function App (Timer / Service Bus triggers), events exported as Parquet to ADLS Gen2, pandas/DuckDB compute, results surfaced in the admin. It explicitly preserves a CQRS-style async seam and the rule *"never query the OLTP store for analytics."*

This spec keeps **every principle** from ADR-0001 §4.2 / §10 / §11:

- Analytics is a separate bounded context behind an **async seam**.
- Python is **never on the transactional hot path** (ADR §4.2 calls that an anti-pattern).
- Analytics reads its **own store**, not the OLTP DB.

It **diverges only on transport and host**: Kafka-compatible streaming + Python consumers + Kubernetes, chosen because the explicit goal includes demonstrating those technologies, and because the same logical design is transport-agnostic. The Azure-native serverless path from ADR §10 remains the **cheapest cloud-production option** and is retained as such in §12.

---

## 2. Decision

1. **Phase 0 first:** extend the existing `/admin` on Neon Postgres with the four missing views (API sales, margin, quality-not-charged, abuse/anomaly). Pure SQL + Next.js, ~$0, immediately useful. This fully satisfies Goal A.
2. **Phases 1–3 (local-first):** build the event-streaming ops sidecar — outbox → relay → Kafka-compatible broker → Python consumers → analytics Postgres → dashboard — running under Docker Compose locally, then Kubernetes locally (kind/k3d) with KEDA lag-based autoscaling. This satisfies Goal B without recurring cloud cost.
3. **Managed always-on cloud (AKS / Confluent / Event Hubs / extra hosted Postgres) is NOT provisioned** without explicit owner approval honoring the budget flags. For cheap cloud production, prefer the ADR §10 Azure-serverless path (§12).

**Rejected alternatives:** see §12 cost table. The dominant rejection is the managed always-on Kafka/K8s stack on cost grounds for the current workload.

---

## 3. Invariants & non-goals

**Invariants (must always hold):**

- **I1 — Revenue-path isolation.** The customer rewrite + payment path (today: Next.js `app/api/rewrite/route.ts` + Stripe webhook → Neon) must not gain a synchronous dependency on the broker, consumers, analytics DB, or dashboard. Event emission is fire-and-forget or transactional-outbox; a broker outage degrades to *analytics lag only*.
- **I2 — No-charge-on-quality-failure integrity preserved.** Nothing in this plane may alter quota/credit/charge behavior. It only **observes**.
- **I3 — Analytics reads its own store.** Consumers write to a **separate analytics database**, never adding analytical query load to the OLTP store (Neon today, Azure SQL post-migration). (ADR-0001 §10/§11.)
- **I4 — Secrets policy.** No secret values printed/committed; env validated at runtime in the handler that uses it. Broker/DB credentials for the sidecar live in its own env, not in source.
- **I5 — Restricted-term ban.** The CI grep guard (the restricted substrings listed in `AGENTS.md` / `CLAUDE.md`) must not appear in code, prompts, comments, names, or copy. Use "anomaly detection" / "abuse detection", "skip", etc.

**Non-goals:**

- Not a replacement for the `/admin` transactional reads that must be live and exact (e.g. a support agent looking up a user) — those stay direct reads on the OLTP store.
- Not a real-time SLA system. Analytics may lag seconds-to-minutes.
- Not a customer-facing product surface. Operator-only, behind `requireAdminUser`.
- No managed always-on cloud infra in this spec's default path.

---

## 4. Current-state inventory (what already exists)

**Data (Neon Postgres via `prisma/schema.prisma`):**

| Model | Carries | Maps to wish-list item |
|---|---|---|
| `RewriteCostLog` | per-request cost, DeepSeek/Sapling tokens+cost+latency, status, scenario, strategyVersion, AI-like %, escalation | per-rewrite cost; provider counts; failures; strategy cost |
| `RewriteProviderCall` | per-call provider/role/model/tokens/chars/cost/latency/success | DeepSeek vs Sapling call counts |
| `RewriteLearningSample` | diagnosis, plan, candidates, status | samples for learning/eval |
| `RewriteCanaryRollback` | strategy regression state; **already emits admin-email + GitHub-issue alerts** | strategy "expensive but low quality" alerting |
| `StripeEvent` | webhook type/status/attemptCount/failedAt/lastError | "did the Stripe webhook fail?" (direct query) |
| `ApiKey` + `ApiKeyUsage` | planTier, quota, period usage; per-request endpoint/status/latency/`costUsdEstimate` | **B2B API sales tracking** |
| `User` | `stripePriceId`, `subscriptionStatus`, `planTier`, `currentPeriodEnd` | who pays, plan |
| `RewriteCredit` | `amountGranted`/`amountConsumed`, `source`, `stripeEventId` | pack-grant ledger |
| `RewriteUsage` | per-period quota counter | usage finalize |

**Admin surface (already live):** `app/admin/page.tsx` (overview) + `app/admin/rewrites` + `app/admin/rewrites/[id]` (per-call drill-down) + `app/admin/learning`, all reading Neon via `lib/admin/metrics.ts`. The overview already shows requests, success/quality-fail/server-fail, signal drop, avg/p95 cost, DeepSeek+Sapling split (field name `openAiCostUsd` is legacy; provider is DeepSeek), escalation, top-cost scenarios.

**Emission point (today):** the live producer is the **Next.js path** — `app/api/rewrite/route.ts` calls `tryPersistRewriteCostLog()` (`lib/observability/rewrite-telemetry.ts`) which does a raw `INSERT INTO "RewriteCostLog"` into Neon at 3 sites (success / quality-fail / server-fail). The .NET `OutboxMessage` (`backend-dotnet/.../OutboxMessage.cs`) is the **migration target**, not the current live producer.

**Gaps for Goal A:** no API-sales view, no per-user margin view, no quality-not-charged reconciliation view, no abuse/anomaly view. All are SQL over existing tables.

---

## 5. Target architecture

```
            CUSTOMER-FACING (unchanged, ADR-0001)                 OPS / ANALYTICS PLANE (this spec)
 ┌────────────────────────────────────────────┐
 │ Next.js /app  ──►  /api/rewrite (Worker)     │   I1: async seam (fire-and-forget / outbox)
 │  rewrite + Stripe webhook                    │                │
 │      │ writes RewriteCostLog etc.            │                ▼
 │      ▼                                       │      ┌───────────────────┐
 │  Neon Postgres (OLTP)  ◄─────────────────────┼──────│  outbox rows /CDC  │
 └────────────────────────────────────────────┘      └─────────┬─────────┘
                                                                 │ relay publishes
                                                                 ▼
                                                       ┌───────────────────┐
                                                       │ Kafka-compatible   │  topics: rewrite.*, usage.*,
                                                       │ broker (Redpanda)  │  stripe.*, cost.*, learning.*
                                                       └─────────┬─────────┘
                                          ┌──────────────┬───────┴───────┬──────────────┐
                                          ▼              ▼               ▼              ▼
                                     cost-consumer  quality-consumer  billing-audit  alert-consumer  learning-consumer
                                          └──────────────┴───────┬───────┴──────────────┘
                                                                 ▼
                                                       ┌───────────────────┐
                                                       │ Analytics Postgres │  (separate DB — I3)
                                                       └─────────┬─────────┘
                                                                 ▼
                                                  FastAPI analytics API + Streamlit / Next admin
```

Run target: **local Docker Compose** (Phases 1–2) → **local Kubernetes (kind/k3d) + KEDA** (Phase 3). Optional ephemeral VM for a live demo (§12).

---

## 6. Event contract

**Topics (Kafka-compatible):**

```
rewrite.requested            rewrite.succeeded            rewrite.quality_failed
rewrite.provider_call        usage.finalized              stripe.webhook_processed
cost.threshold_exceeded      learning.sample_created
```

**Common envelope (all events):**

```jsonc
{
  "eventId": "uuid",            // producer-generated, unique → consumer idempotency key
  "eventType": "rewrite.succeeded",
  "occurredAt": "ISO-8601",
  "schemaVersion": 1,
  "correlationId": "requestId", // = RewriteCostLog.requestId where applicable
  "userId": "cuid|null",
  "payload": { /* per-type, below */ }
}
```

**Payloads (carry IDs + metrics, never secrets or full draft text):**

- `rewrite.succeeded` / `rewrite.quality_failed`: `requestId, scenario, tonePreset, strategyVersion, status, durationMs, totalEstimatedCostUsd, draftAiLikePercent, rewriteAiLikePercent, changePoints, usedEscalation, charged(bool)`.
- `rewrite.provider_call`: `requestId, provider, role, model, inputTokens, outputTokens, characters, estimatedCostUsd, latencyMs, success, errorCode`.
- `usage.finalized`: `userId, periodKey, count, planTier`.
- `stripe.webhook_processed`: `stripeEventId, type, status, attemptCount, failedAt`.
- `cost.threshold_exceeded`: `window, observedCostUsd, thresholdUsd, dimension` (emitted by alert-consumer, not the app).
- `learning.sample_created`: `learningSampleId, scenario, status, label`.

**Keying & semantics:**

- **Partition key:** `userId` (preserves per-user ordering; null → round-robin).
- **Delivery:** at-least-once. Consumers must be **idempotent on `eventId`** (upsert / dedupe table). This is the resilience contract — see `resilience-test-generation` when implementing.
- **Versioning:** additive only within `schemaVersion`; breaking change bumps the version and consumers handle both during rollover.

---

## 7. Producer / emission seam

**Principle (I1):** emitting an event must never block or fail a rewrite/payment.

**Option 7A — Application outbox + relay (recommended; most honest, reuses existing pattern).**
- In the **same transaction** that writes `RewriteCostLog` (and on Stripe webhook processing), insert an `OutboxEvent` row. On the Next/Neon side this is a new lightweight table mirroring the .NET `OutboxMessage` shape (`id, eventType, payloadJson, status, attemptCount, nextAttemptAt, lockedUntil, sentAt, lastError`). No dual-write to the broker on the hot path.
- A **relay** (a Cloudflare Worker cron, or a small Python `relay` service in the sidecar) polls unsent outbox rows, publishes to the broker, marks `sent`. Crash-safe, ordered, retryable.
- Post-migration, the **.NET `OutboxMessage`** becomes the unified producer — this spec's topics/envelope are designed to be emitted from either side unchanged.

**Option 7B — CDC off Postgres (most "impressive", heavier ops).**
- Debezium (Kafka Connect) tails the Postgres WAL → topics, **zero app changes**. Requires Neon **logical replication** (paid-plan feature — verify) and running Kafka Connect. Listed as an advanced alternative; not the default because of the extra moving part and the Neon dependency.

**Option 7C — Periodic ETL (simplest, no hot-path change).**
- A scheduled Python job reads rows newer than a stored watermark and produces both events and aggregates. Acceptable for Phase 0→1 bring-up; lacks the "real event stream" story for the portfolio.

**Decision:** ship **7A** (outbox+relay) as the production-grade seam; keep **7B** documented as the advanced demo variant. Never **7** in a way that adds a synchronous broker call to `app/api/rewrite/route.ts`.

---

## 8. Consumer contracts

Each consumer is an independent, horizontally-scalable Python service (own consumer group), idempotent on `eventId`, writing only to the analytics DB.

| Consumer | Subscribes | Computes / writes |
|---|---|---|
| `cost-consumer` | `rewrite.*`, `rewrite.provider_call` | per-request, daily, per-model, per-user cost rollups → `fact_rewrite_cost`, `agg_cost_daily` |
| `quality-consumer` | `rewrite.succeeded`, `rewrite.quality_failed` | quality-fail rate, signal-drop distribution, per-strategyVersion effectiveness → `agg_quality_daily`, `agg_strategy` |
| `billing-audit-consumer` | `stripe.webhook_processed`, `usage.finalized` | reconcile Stripe status vs usage vs charged-flag; flag drift → `audit_billing` |
| `alert-consumer` | all | thresholds on cost/failure-rate/timeout; emits `cost.threshold_exceeded`; notifies (email/Slack) → `alert_log` |
| `learning-consumer` | `learning.sample_created`, `rewrite.quality_failed` | select samples worth human review / eval promotion → `learning_queue` |

**Idempotency:** each consumer keeps a `processed_event(eventId, consumer, processedAt)` unique row; replays are no-ops. (Mirrors the existing `StripeEvent` idempotency discipline.)

---

## 9. Analytics data model (separate DB — I3)

Start with **Postgres** (a separate local DB in Compose; in cloud, a separate DB/branch — *not* the OLTP store). Tables:

- `fact_rewrite_cost` (one row per `rewrite.succeeded|quality_failed`, denormalized cost + quality)
- `fact_provider_call` (one row per `rewrite.provider_call`)
- `agg_cost_daily`, `agg_quality_daily`, `agg_strategy` (consumer-maintained rollups)
- `audit_billing`, `alert_log`, `learning_queue`, `processed_event`

**ClickHouse is explicitly out of scope** at current volume (hundreds–thousands of rows/day); Postgres handles this for years. ClickHouse is reconsidered only if/when a columnar OLAP demonstration becomes its own goal.

---

## 10. Phase 0 spec — admin extensions (do first, ~$0)

All additive, pure SQL + Next server components in the existing admin. No new infra.

1. **`lib/admin/api-sales.ts` + `/admin/api-keys`** — read `ApiKey` + `ApiKeyUsage`: per-key planTier, period usage / quota, last used, request count + `SUM(costUsdEstimate)`; per-endpoint breakdown. First B2B sales dashboard.
2. **`lib/admin/margin.ts` + overview card** — per-user `SUM(RewriteCostLog.totalEstimatedCostUsd)` (cost is real, USD). **Revenue caveat (verified, do not fake):** dollar revenue is *not* a single column — `RewriteCredit.amountGranted` is credits, and `User.stripePriceId` identifies the plan. **Build-time task:** map `stripePriceId` → price (from the pricing config / `docs/rewrite-packs-pricing-spec.md`) for revenue, or pull invoice totals from Stripe. Until that mapping is wired, show **cost per user** and **credits granted vs consumed**, and label revenue "pending price-map".
3. **`lib/admin/quality-not-charged.ts` + view** — `status='quality_failed'` volume + cost, confirming these did not consume quota/credit (cross-check `RewriteUsage`/`RewriteCredit`). Protects invariant I2 visibility.
4. **`lib/admin/anomaly.ts` + view** — per-user request count & failure rate over rolling windows (SQL `GROUP BY` + window funcs); surface outliers for abuse review. (Term: "anomaly/abuse detection" — never the banned substring.)

**Acceptance:** each view renders behind `requireAdminUser`; queries are indexed (existing `@@index` on `createdAt`/`userId`/`status` cover them); no change to any write path; restricted-term grep clean.

---

## 11. Phases 1–3 spec — local-first event sidecar (Goal B)

**Phase 1 — event skeleton (Docker + Redpanda):**
- New folder `ops-analytics/` (separate from the Next app; own `docker-compose.yml`).
- Services: `redpanda` (Kafka-compatible, single binary) + `redpanda-console` (UI).
- Define the §6 topics; implement the §7A outbox table + a `relay` producer.
- Acceptance: a rewrite in local dev produces an outbox row → relay publishes → event visible in redpanda-console.

**Phase 2 — Python consumers + analytics DB + API/dashboard:**
- Add to Compose: `analytics-postgres`, `fastapi` (admin/analytics API), the five §8 consumers, and `streamlit` (fast operator dashboard) — or surface via the existing Next admin.
- Acceptance: `docker compose up` brings the whole plane up; events flow end-to-end; Streamlit shows live cost/quality; killing the broker leaves the customer rewrite path (run separately) unaffected (proves I1).

**Phase 3 — Kubernetes (local-first):**
- `ops-analytics/k8s/` manifests or a Helm chart for all services; run on **kind / k3d / minikube** ($0).
- Add **KEDA** scaling `quality-consumer` / `cost-consumer` on Kafka consumer-group lag — the core K8s+Kafka demonstration.
- Acceptance: `kubectl apply` (or `helm install`) stands up the plane on kind; a synthetic event burst visibly scales a consumer deployment via KEDA.

---

## 12. Cloud-graduation options & cost (if/when it must run in cloud)

| Option | Shape | Fixed monthly | When to choose |
|---|---|---|---|
| **Local-first (default)** | Docker Compose / kind on the operator's machine | **$0** | Now — personal dashboard + portfolio |
| **Ephemeral VM demo** | One small VM running k3s + Redpanda, **turned off when idle** | ~$15–40/mo *while on* | Live interview demo; tear down after |
| **ADR-0001 §10 Azure-serverless** | Python Function App + existing Service Bus + Postgres/ADLS | **~$0 idle** (scale-to-zero) | Cheapest real cloud production for the ops dashboard |
| **Managed Kafka + AKS** (rejected default) | AKS (nodes always billed) + Confluent/Event Hubs | **~$150–600+/mo** | Only with explicit approval + real scale justification |

Pricing checked 2026-05-24 (approximate — **verify on official calculators before provisioning**): AKS control plane Free $0 / Standard ~$0.10/cluster/hr (~$73/mo) with node VMs always billed on top; managed Kafka small clusters realistically ~$200+/mo (Confluent Basic ~$0.70/hr); Azure Event Hubs throughput-unit billed. Sources: Azure AKS pricing page, Azure Event Hubs pricing page, Confluent pricing.

**Key honesty point:** if the goal is *cheap cloud production*, the ADR §10 Azure-serverless path beats managed Kafka/K8s. If the goal is *demonstrating Kafka/K8s*, local-first proves it at $0. Managed always-on is the worst of both for this workload.

---

## 13. Cost model & approval gates

- Local Docker Compose / kind / k3d: **no approval needed** (no cloud spend).
- Ephemeral VM, managed AKS, Confluent, Event Hubs, extra hosted Postgres, Neon paid-tier logical replication (for 7B CDC): **NEW paid resource → explicit owner approval, honoring `AZURE_ALLOW_PAID_RESOURCES` + `AZURE_BUDGET_LIMIT`** (ADR-0001 §6 cost rule).
- No automation provisions any of the above; the operator decides and approves.

---

## 14. Failure-isolation: how we prove I1

- The broker, consumers, analytics DB, and dashboard are in `ops-analytics/`, deployed independently of the Worker/Functions app.
- The only coupling is the **outbox table write** (same tx, local, cheap) + an **async relay**. If the broker/relay is down, outbox rows accumulate and drain later — rewrites and payments proceed normally.
- **Proof test (resilience-test-generation):** with the broker stopped, run a rewrite end-to-end; assert it succeeds, charges correctly, and the outbox row is `pending`; restart broker; assert the event drains and analytics catches up.

---

## 15. Acceptance criteria (per phase)

- **Phase 0:** four admin views live behind auth; no write-path change; indexed queries; grep clean; margin view honestly labels revenue mapping status.
- **Phase 1:** outbox→relay→Redpanda path proven locally; rewrite path untouched.
- **Phase 2:** full plane via `docker compose up`; end-to-end event flow; broker-down isolation demonstrated.
- **Phase 3:** plane on kind via `kubectl`/Helm; KEDA lag-scaling demonstrated.
- **All phases:** restricted-term grep clean; no secrets in source; analytics never queries OLTP store.

---

## 16. Open questions to resolve at build time

1. **Revenue $ mapping** — confirm canonical source for per-user/MRR dollars: `stripePriceId`→price-table, `RewriteCredit` + price, or Stripe invoice pull. (Blocks the exact margin number, not the cost view.)
2. **Relay host** — Cloudflare Worker cron vs a Python `relay` service in the sidecar. (Worker cron keeps it serverless; Python relay keeps the whole plane self-contained for the demo.)
3. **CDC viability (7B)** — does the current Neon plan expose logical replication? Only relevant if we choose the Debezium variant.
4. **Dashboard surface** — Streamlit (fastest Python story) vs extending Next `/admin` (style consistency). Can do both.

---

## 17. References

- `docs/architecture-decision-record.md` §4.2, §10, §11 — the deferred Python-analytics direction this realizes.
- `prisma/schema.prisma` — event sources (RewriteCostLog, RewriteProviderCall, StripeEvent, ApiKey/ApiKeyUsage, …).
- `lib/admin/metrics.ts`, `app/admin/*` — the existing admin surface Phase 0 extends.
- `lib/observability/rewrite-telemetry.ts`, `app/api/rewrite/route.ts` — the current emission point for the outbox seam.
- `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/OutboxMessage.cs` — the future unified producer; outbox shape to mirror.
- `docs/observability.md` — uptime/alerting baseline this complements (operator-facing analytics, not uptime).
- `AGENTS.md` — restricted terms, secrets, budget flags, deployment gates.
