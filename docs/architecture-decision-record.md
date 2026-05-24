# ADR-0001 — All-C# Backend on Azure (Python analytics deferred, Next.js frontend retained)

- **Status:** Accepted — 2026-05-24
- **Deciders:** Project owner (ChuanQiao1128, TimeAwake Ltd); Claude Code (advisory / architecture review)
- **Extends:** `docs/dotnet-azure-full-run-target.md` (prior parallel-backend target). This ADR upgrades that target from *"build a parallel C# backend"* to *"make C# the single backend of record and migrate the remaining TS backend onto Azure."*
- **Scope of this record:** the **backend** only. The frontend stays Next.js. Python is a documented **future** direction, explicitly **out of scope** for the next run.
- **Review method:** `cloud-architecture-cost-review` skill (logged in `docs/skill-run-log.md`).

---

## 1. Context

### 1.1 Current state (the "mess")

The backend is split by accident of history, not by design:

| Capability | Lives in | Runtime / language |
|---|---|---|
| Quota reservation / idempotency / usage | C# | Azure Functions + Azure SQL (dev) |
| Stripe checkout / portal / webhook | C# | Azure Functions (dev) |
| Queue / worker / outbox / retry / dead-letter | C# | Service Bus + Functions (dev) |
| Account / subscription status (`/api/me`) | C# | Azure Functions (dev) |
| **Rewrite quality engine (core IP, ~10.4k LOC)** | **TS** | **Next.js route on Cloudflare Workers** |
| Learning system (`learningops`) | TS | Cloudflare / Node |
| Observability, cost logs, canary rollout | TS | Cloudflare / Node |
| B2B API keys (`feat/api-keys` branch) | TS | Cloudflare / Node |
| **Operational database** | **Two stores** | **Azure SQL (C# side) + Neon Postgres (TS side)** |
| Frontend | Next.js | TS / React (Cloudflare) |

Evidence: `docs/dotnet-azure-full-run-result.md` (C# backend deployed to a dev Azure environment — Functions, Azure SQL with migrations applied, Service Bus, Application Insights, OIDC CI/CD); `app/api/rewrite/route.ts` (the user-facing rewrite still runs the TS pipeline in-process); `lib/rewrite-pipeline/` (~6.1k LOC orchestrator) + top-level `lib/rewrite-*.ts` / `lib/fact-extraction.ts` / `lib/writing-signal.ts` (~4.3k LOC); `prisma/schema.prisma` (12 Postgres models) vs the 6 EF Core entities in `backend-dotnet/`.

**Root cause of the mess:** a split *runtime* (TS-on-Cloudflare engine + C#-on-Azure transactional core) and a split *datastore* (Neon + Azure SQL) — not language collisions inside one service.

### 1.2 Drivers

- One backend language (C#) to reduce cognitive load and stop maintaining two runtimes.
- Deep, production-grade Azure integration (not just "hosted on Azure").
- A defensible **.NET + Azure** narrative for interviews targeting C# roles, with a credible **Python** story for analytics.
- Single operational source of truth for data.

### 1.3 Constraints (non-negotiable)

- Frontend remains Next.js; do not rewrite the UI.
- Do not break the live product; the existing TS engine stays running until C# reaches parity.
- Cost discipline: prefer scale-to-zero; any fixed-cost resource is approval-gated.
- Server-side quota/billing invariants from `docs/dotnet-azure-full-run-target.md` continue to hold.
- Secrets policy: no secret values in source, docs, or logs; validate at runtime.
- User-facing positioning stays "natural, personal, fact-preserving replies." Restricted terms remain banned.

---

## 2. Decision

1. **C# is the single backend of record.** The rewrite quality engine, learning system, observability/cost/canary, and B2B API keys are ported from TS to C#/.NET and run on Azure. The TS Next.js API routes become thin authenticated proxies (the `/api/me` pattern in `lib/azure-api.ts` is the template).
2. **Architecture is organized by bounded context / workload, then the best-fit runtime is chosen per context** — not by language preference. The contexts are decoupled by explicit seams (sync HTTP for presentation→core; async events/data for core→analytics).
3. **Python is reserved for the asynchronous analytics / data-science context only**, and is **deferred** — not built in the next run. It must never sit on the latency-critical transactional path.
4. **Next.js remains the presentation + auth/session + thin BFF layer.** It is not the system of record; business logic lives in C#.
5. **Consolidate to one operational store: Azure SQL** (EF Core). Retire Neon Postgres + Prisma from the runtime path. A separate **analytical** store is introduced later, with the Python phase.
6. **Target deep Azure-native integration:** Managed Identity everywhere, Key Vault, Functions declarative bindings, Service Bus, Application Insights, API Management for the B2B API, OIDC CI/CD, and Bicep IaC.

---

## 3. Target architecture

```
[Browser]
   │ HTTPS
[Next.js (React / App Router)]            presentation + auth/session (Entra External ID) + thin BFF proxy
   │ HTTPS + Bearer  (today: /api/me)
[C# backend @ Azure]                       SYSTEM OF RECORD
   │   Functions (HTTP API) · Domain · EF Core / Azure SQL · Stripe · quota · outbox · Service Bus worker
   │ emits events / data (Service Bus / Event Grid / Outbox / cost logs)
   ▼ async seam (no request-path coupling)
[Python analytics @ Azure]  (FUTURE)       Functions / Container Apps Jobs: pandas / DuckDB / scikit-learn
   ▼                                        over an analytical store (ADLS Gen2 Parquet → Fabric/ADX)
[Dashboards: Next.js admin or Power BI / Fabric]
```

Two seams, deliberately different:
- **Sync HTTP (presentation → core):** Next.js calls the C# API with a bearer token. Cheap, request-scoped, already proven by `/api/me`.
- **Async events/data (core → analytics):** the transactional path never blocks on analytics. This is a CQRS-style read/write separation: **do not run analytics on the OLTP database.**

---

## 4. Rationale

### 4.1 Why C# + Azure (the backend of record)

- The backend's core job is **transactional correctness under concurrency**: quota races, idempotency (Stripe webhook replay, queue redelivery), outbox, near-exactly-once. A strongly-typed managed runtime + mature ORM (EF Core) + real transactions catches a class of money/quota bugs at compile time.
- **One typed language across HTTP API, queue worker, and domain model.** The `ReplyInMyVoice.Domain` project is shared by the HTTP and queue processors — no duplicated models across runtimes.
- **.NET is Azure's first-class citizen:** Functions bindings, `DefaultAzureCredential` / Managed Identity, App Insights auto-instrumentation, EF Core + Azure SQL — least glue code, best docs. This is what makes the integration *deep* rather than *hosted*.
- **Skeptic rebuttals (interview-ready):**
  - vs **Node:** stronger compile-time guarantees on a money/quota domain; a better first-class long-running worker story.
  - vs **Java:** faster cold starts on Functions consumption, lighter, more modern ergonomics, tighter Azure tooling.
  - vs **Go:** richer ORM/EF ecosystem and more mature Azure SDKs.
  - Honest close: *all of them could do it; for an Azure-native, correctness-heavy domain (and the target roles), .NET is the best overall fit.*

### 4.2 Why Python — and only for analytics (deferred)

- Analytics/data-science is a **different workload**: batch, exploratory, statistical/ML. Python's ecosystem (`pandas`, `numpy`, `scikit-learn`, `statsmodels`, `DuckDB`, `plotly`) is the industry standard and well ahead of .NET here.
- **Precedent already exists** in the repo: `scripts/analyze_rewrite_quality.py` — analysis/eval already leans Python.
- The async seam means polyglot costs ≈0 on the request path, and isolation protects transactional latency/reliability from heavy batch/ML jobs.
- **Discipline:** Python is **never** placed on the transactional hot path. Putting it there "to show Python" would be an anti-pattern. Conversely, Python is not used for the transactional core (GIL/concurrency, weaker compile-time guarantees, Azure tooling favors .NET).
- **Skeptic rebuttal ("then keep it all C# with ML.NET"):** pure aggregation could be C#; but the moment we need statsmodels/scikit-learn/notebook exploration/forecasting/LLM-as-judge evaluation, Python wins decisively, and it is where analysts/data scientists work. The split is principled, not fashionable.

### 4.3 Why Next.js (note: *Next.js*, not NestJS)

- **NestJS ≠ Next.js.** NestJS is a server-side Node framework; Next.js is the React meta-framework this repo actually uses (App Router, OpenNext on Cloudflare). Avoid conflating them.
- Next.js is the **presentation + auth/session + thin BFF** layer. It owns rendering (marketing site + `/app` workspace) and session, and proxies to the C# API. It is **not** the system of record.
- Clean three-way responsibility split: **Next.js = presentation/BFF · C# = business truth source · Python = offline analytics.**
- Frontend host (Cloudflare) is an edge/CDN concern independent of backend cloud. If "all-Azure" optics are wanted later, Next.js can move to Azure Static Web Apps / Container Apps — optional, not required.

---

## 5. Considered alternatives (rejected)

- **Option B — keep the TS rewrite engine, host it as a Node container on Azure Container Apps.** Cheapest path (~2–4 weeks): removes the two-cloud / two-DB mess without re-implementing the eval-validated engine. **Rejected** because the owner wants a single-language (C#) backend, the deep .NET/Azure integration narrative, and the interview value of an all-.NET backend — accepting the higher cost and the rewrite-engine quality-revalidation risk as a deliberate trade-off. *(Recorded as the strongest rejected option; it remains the fallback if rewrite-engine parity proves intractable.)*
- **All-C# analytics (ML.NET / LINQ) when analytics arrives.** Rejected for the data-science ecosystem reasons in §4.2; revisit only for embedded inference.
- **Move the frontend to Azure now.** Rejected as unnecessary scope; Cloudflare hosting is fine and the frontend is explicitly out of scope.

---

## 6. Target Azure service map

| Concern | Service | Status | Cost shape |
|---|---|---|---|
| Transactional API | Azure Functions (.NET isolated) | live (dev) | scale-to-zero |
| Background processing | Functions/Worker + Service Bus | live (dev) | scale-to-zero / low |
| Operational data (OLTP) | Azure SQL | live (dev) | low (serverless tier auto-pause) |
| Messaging / fan-out | Service Bus (+ Event Grid later) | live (dev) | low |
| Secrets / identity | Key Vault + Managed Identity | planned | low |
| Consumer auth | Entra External ID (+ Google) | configured | low |
| Observability | Application Insights + Log Analytics | live (dev) | usage-based |
| **B2B API gateway** | **Azure API Management** | planned | ⚠️ fixed floor — use **Consumption tier** for low traffic |
| CI/CD | GitHub Actions + OIDC | live | ~free |
| Infrastructure as code | Bicep | planned | n/a |
| Rate-limit / hot counters | Azure Cache for Redis | optional / later | ⚠️ fixed cost |
| Analytical store | ADLS Gen2 (Parquet) → Fabric / Azure Data Explorer | future (Python phase) | low → ⚠️ |
| ML lifecycle | Azure Machine Learning | future (if modeling) | medium |
| Frontend host | Cloudflare (unchanged); SWA optional | unchanged | n/a |

**Cost rule:** default to scale-to-zero compute. APIM, Fabric/ADX, Redis, and Front Door carry fixed monthly cost and require explicit owner approval before provisioning, honoring `AZURE_BUDGET_LIMIT` / `AZURE_ALLOW_PAID_RESOURCES`.

---

## 7. Migration scope & sequencing (for the upcoming long run)

> The long autonomous run modifies the backend to match this ADR. The rewrite engine is the long pole; everything else is more mechanical.

**In scope (this run):**

| # | Workstream | Notes | Est. (eng-days) | Risk |
|---|---|---|---|---|
| A | Port rewrite quality engine → C# | input analyzer, fact ledger, strategy router, candidate gen, fact/structural/policy gates, Sapling naturalness gate, targeted repair, budget manager, quality strategist; keep DeepSeek + Sapling adapters | 25–45 | 🔴 dominant |
| B | Learning system → C# | `learningops` + learning EF entities + capture path | 5–12 | 🟡 |
| C | Observability / cost / canary → C# | telemetry + cost logs + canary rollback entities; lean on App Insights | 5–8 | 🟡 |
| D | B2B API keys → C# | ApiKey/Usage + hashing + rate limit + admin; front with API Management | 4–8 | 🟡 |
| E | Consolidate data Neon→Azure SQL | add EF entities + migrations for the 6 Postgres-only models; migrate data; repoint; retire Prisma/Neon | 4–8 | 🟡 |
| F | Frontend rewiring | `/api/rewrite`, `/api/stripe/*`, health, admin → thin proxies to C# (per `/api/me`); update Playwright e2e | 3–6 | 🟢 |
| G | Productionize Azure | Key Vault + Managed Identity, prod tiers, API Management, alerts/dashboards, Bicep, prod CI/CD with slots | 6–12 | 🟡 |
| H | Cutover & hardening | run C# + TS in parallel → shadow-compare → cut → monitor → decommission TS engine; update docs | 5–10 | 🟡 |

**Total ≈ 57–109 eng-days. Wall-clock (solo + heavy AI assist, primary focus): optimistic ~1 month · realistic ~2–3 months · cautious ~3–5 months.** The swing factor is rewrite-engine quality parity (A + H).

**Rewrite-engine migration approach (de-risking A/H):** port the pipeline to C#; keep the TS engine live; **shadow-run both** against the eval corpus; gate cutover on *no quality regression vs the TS baseline* and *zero failing known samples* (per `AGENTS.md`); preserve the no-charge-on-quality-failure rule and the restricted-term ban.

**Out of scope (this run):**
- Python analytics build (documented as future; see §9).
- Any frontend rewrite (Next.js untouched beyond proxy rewiring).
- Live Stripe charges / production-domain cutover unless `LAUNCH_CONFIRMED` / `STRIPE_LIVE_CUTOVER_APPROVED` are set in the brief.
- Provisioning fixed-cost Azure resources without approval.

---

## 8. Completion criteria

- `dotnet build` + `dotnet test` green; resilience/idempotency invariants from `docs/dotnet-azure-full-run-target.md` covered by tests.
- The user-facing rewrite is served by the C# engine with **no quality regression vs the TS baseline** on the eval corpus, and no known sample failing.
- All operational reads/writes go to Azure SQL; Neon/Prisma removed from the runtime path.
- Next.js API routes are thin proxies; e2e (Playwright) passes.
- Deep-integration proof points present: Managed Identity (no static secrets), Key Vault, App Insights distributed tracing across Functions + worker, API Management fronting the B2B API, OIDC CI/CD, Bicep.
- Restricted-term scan over `app components public lib scripts tests` is clean.

## 8.1 Stop conditions (per AGENTS.md)

Continue through ordinary build/test/migration/Azure CLI errors. Stop only for: dashboard-only actions impossible locally; denied SSH/API permissions; invalid credentials; a real paid/live financial action; or work that would require exposing secrets.

---

## 9. Risks & mitigations

| Risk | Mitigation |
|---|---|
| **Rewrite-engine quality parity** (dominant) | Shadow-run C# vs TS on the eval corpus; cutover gated on no regression; TS engine stays live as rollback. |
| Data migration Neon→Azure SQL (Postgres→SQL Server semantics, data movement) | EF migrations + a one-time backfill job; verify row counts and invariants before repoint. |
| Fixed-cost creep (APIM/Fabric/Redis) | Approval gates; prefer Consumption/serverless tiers; honor budget flags. |
| Long pole stalls the whole run | Workstreams B–G can proceed in parallel with A; H only after A reaches parity. |

---

## 10. Future direction — Python analytics (when we build it)

- **Deploy target:** a **separate Python Function App** (Timer + HTTP/Service Bus triggers) for scheduled cost/user aggregation and lightweight analytics APIs — scale-to-zero, matches the Functions-first posture. Graduate heavy/long/ML jobs to **Azure Container Apps Jobs**. If it grows into real BI, **Microsoft Fabric** / **Azure Data Explorer (Kusto)** + Power BI; for modeling, **Azure Machine Learning**.
- **Data flow:** C# writes operational data to Azure SQL → async export (Service Bus/Event Grid or scheduled) lands events as **Parquet in ADLS Gen2** → Python computes with pandas/DuckDB → results to an analytics table or Blob → surfaced in the Next.js admin or Power BI. **Never query the OLTP store for analytics.**

---

## 11. Interview talking points (condensed)

- **Thesis:** "I split the system by workload, then chose the best-fit runtime per bounded context, decoupled by an async seam — boundaries first, languages second."
- **C#/Azure:** correctness-critical OLTP (money/quota/idempotency) → strong typing + EF Core + transactions + Azure-native integration.
- **Python (deferred):** analytics is batch/exploratory/ML; Python's ecosystem is the standard; isolated behind an async seam; never on the hot path.
- **Next.js (not NestJS):** presentation + session + thin BFF; business logic stays in C#.
- **CQRS:** separate analytical reads from transactional writes; feed analytics via events/export, not the OLTP DB.
- **Cost:** default scale-to-zero; fixed-cost services (APIM/Fabric/Redis) only when traffic/BI justifies.

---

## 12. References

- `docs/dotnet-azure-full-run-target.md` — backend invariants + prior run target (extended by this ADR).
- `docs/dotnet-azure-full-run-result.md` — what the C# backend already proves on Azure dev.
- `AGENTS.md` — product, skill, deployment, and autonomy rules; stop conditions.
- `app/api/rewrite/route.ts`, `lib/rewrite-pipeline/` — the TS engine to be ported.
- `lib/azure-api.ts` — the thin-proxy pattern to replicate.
- `prisma/schema.prisma` vs `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/` — the data-model gap to close.

---

## 13. Billing & Rewrite-Pack Ledger — first migration slice (added 2026-05-24)

A pricing redesign (one-time rewrite packs + Pro/API subscription, replacing the single NZ$9/40 plan) was accepted from the owner. Detailed build spec: **`docs/rewrite-packs-pricing-spec.md`**.

Architecture decisions recorded here:

- **It is a bounded context inside the C# backend**, not new TS. Billing/packs is money + quota + idempotency — exactly what §4.1 assigns to C#. It reuses primitives that already exist on Azure dev (`QuotaService`, `UsageReservation`, `StripeEventService`, `StripeBillingService`, `OutboxMessage`, `ExpiredReservationCleanup`). Building it in TS/Prisma (as the source brief was written) would grow the very migration debt this ADR removes.
- **It is the recommended first vertical slice of the migration** (overlaps Workstreams D/E in §7): it ships revenue features *and* advances the C#/Azure cutover at the same time. Only the presentation pieces (pricing page, char counter, balance UI, campaign capture) stay in Next.js.
- **Governance:** the new single ~2500-char total input cap (owner decision 2026-05-24; ≈420 English words, char-based) changes `AGENTS.md` (currently combined 5000 + per-field caps) and `replyinmyvoice_requirements.md`; those must be updated when the limit lands. `AGENTS.md` remains source of truth.
- **Economics caveat (cloud-architecture-cost-review):** margin must absorb Stripe fees (2.65%+NZ$0.30 domestic / 3.5%+NZ$0.30 intl + ~2% FX) and GST 15% (at the IRD NZ$60k threshold; consumer prices GST-inclusive). True worst-case gross margins are ~21–26%, not the headline ~40–85%. Validate the NZ$0.12/rewrite assumption against real `RewriteCostLog`/`RewriteProviderCall` data — and remember one user rewrite fans out to multiple DeepSeek + Sapling calls.
- **Prerequisite:** Phase 0 repo hygiene (the working tree is `feat/api-keys` with 94 dirty files + 57 stashes — the overnight-supervisor collision). Start from a clean baseline in an isolated worktree before this slice.
