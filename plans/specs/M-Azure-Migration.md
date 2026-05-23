# M-Azure-Migration — Spec

Status: **ACTIVE — drives Phase 5 planner and L3b human-spec workflow**
Authored: 2026-05-23T16:35+12:00 by Claude (supervisor mode) at user instruction "Azure-spec-go"
Authority: This file is the binding contract for the Azure backend cutover. Children produced by Phase 5 planner under epic_id=`M-Azure-Migration` MUST satisfy the acceptance criteria here. If conflict with `docs/dotnet-azure-full-run-target.md`, that doc is the architecture truth; this file is the **cutover-completion** plan layered on top of it.

---

## 1. Reframe: build is largely done, cutover is what's missing

Per `docs/dotnet-azure-full-run-result.md` (2026-05-19), the parallel .NET/Azure backend already exists:

- `backend-dotnet/ReplyInMyVoice.sln` — ASP.NET Core 8 Web API + Worker + tests
- Azure resources live: SQL Database, Service Bus + rewrite queue, Application Insights, Functions App at `https://replyinmyvoice-func-dev.azurewebsites.net`
- EF Core migrations applied to Azure SQL
- All 5 domain entities (AppUser, UsagePeriod, RewriteAttempt, UsageReservation, StripeEvent) persisted
- Service Bus publisher + WebJob worker idempotent
- Stripe webhook with signature verification + idempotent event storage
- `.github/workflows/dotnet-azure.yml` CI/CD with OIDC

The user instruction "放弃 Neon" (abandon Neon) and "迁移后端到 Azure" (migrate backend to Azure) maps to **cutover**, NOT to a fresh build. Build is ~80% done.

This spec is therefore organized around **what remains** to flip the live consumer traffic from Cloudflare Worker + Neon to Azure Functions + Azure SQL.

---

## 2. End-state architecture (post-cutover)

```text
                           ┌──────────────────────────────┐
                           │ replyinmyvoice.com           │
                           │ Cloudflare Pages (Next.js)   │
                           │ - marketing + /app + /pricing│
                           │ - sign-in/sign-up            │
                           └─────────────┬────────────────┘
                                         │ /api/* (fetch)
                                         ▼
                           ┌──────────────────────────────┐
                           │ api.replyinmyvoice.com       │
                           │ (NEW DNS — A/CNAME to Azure) │
                           │ Azure Functions              │
                           │ ASP.NET Core 8 Web API       │
                           │ - /api/v1/rewrite            │
                           │ - /api/v1/stripe/webhook     │
                           │ - /api/v1/auth/* (later)     │
                           │ - /health                    │
                           └─────────────┬────────────────┘
                                         │
                       ┌─────────────────┼─────────────────┐
                       ▼                 ▼                 ▼
                  ┌──────────┐    ┌──────────────┐    ┌─────────────┐
                  │ Azure SQL│    │ Service Bus  │    │ App Insights│
                  │ (Neon →) │    │ + WebJob     │    │ telemetry   │
                  └──────────┘    │ Worker       │    └─────────────┘
                                  └──────────────┘
```

Cloudflare Pages keeps the Next.js frontend.
Cloudflare Worker (current `/api/rewrite/*` host) is DECOMMISSIONED post-cutover.
Neon Postgres goes READ-ONLY for 14 days as rollback insurance, then DELETED.

---

## 3. Decisions taken in this spec (LOCKED unless amended)

```text
D1. Cutover model            single-flip-with-rollback (not parallel-run forever)
                              Reason: dual-write is fragile + we have no real users
                              yet. Single-flip on a scheduled maintenance window.

D2. Frontend → Azure routing  separate hostname (api.replyinmyvoice.com) not
                              path-based reverse-proxy. Reason: keeps Cloudflare
                              and Azure traffic genuinely separate; clean
                              rollback by flipping DNS.

D3. Auth at cutover           keep Clerk-compatible JWT for at-launch. Entra
                              (M1-Entra epic) lands AFTER Azure cutover in a
                              separate epic. Reason: stacking auth migration on
                              backend migration is too much risk at once.

D4. Data migration            schema-fresh (Azure SQL has 0 rows). Reason:
                              there are no real users yet (per CLAUDE.md sprint
                              addendum 2026-05-21). At first live transaction
                              (M7-001) the new backend handles it natively.

D5. Stripe webhook URL        operator changes in Stripe dashboard from
                              Cloudflare endpoint to api.replyinmyvoice.com.
                              Pre-cutover: webhook to BOTH for 24h to validate.
                              Post-cutover: webhook to Azure only.

D6. Rollback window           14 days after cutover. Cloudflare Worker stays
                              deployed but not pointed-to by DNS; Neon stays
                              read-online but not written-to. Day 14:
                              decommission Worker, set Neon to read-only-forever.
                              Day 90: delete Neon project.

D7. Cost gate                 Azure dev resources cost ≤ NZ$15/month per
                              .env.local AZURE_BUDGET_LIMIT. Cutover does NOT
                              add new paid resources beyond what's already
                              provisioned. If new resource needed (e.g.,
                              prod-tier SQL), it's a separate operator gate.
```

---

## 4. Cutover phases (LOCKED order)

```text
Phase A. Failure-mode parity (the bar before any cutover)
Phase B. Frontend routing change (Next.js calls api.replyinmyvoice.com)
Phase C. Pre-cutover smoke (dual-write Stripe webhook to both for 24h)
Phase D. Cutover window (DNS flip + monitor 4h)
Phase E. Stabilization (14 days at single-flip)
Phase F. Decommission (Worker off, Neon read-only)
```

Phase A is the longest. If A is incomplete, NO cutover. Phase A is composed of the 13 failure-mode invariants listed in `docs/dotnet-azure-full-run-target.md` §"Required Tests" — each must pass on Azure SQL + Azure Service Bus + production-shape config.

---

## 5. Item decomposition for the registry (13 items)

These appear as M-Azure-* in `plans/loop-registry.json`. Each is small enough for L1 or L2 except the cutover gates which are evidence-lane.

### Phase A — parity

```text
M-Azure-001  Confirm all 13 failure-mode xUnit tests exist
             worker_class=scoped, runtime=codex-cli, coupling=low
             owned_paths: backend-dotnet/tests/**
             acceptance: dotnet test passes ALL of the 13 named tests
                         from docs/dotnet-azure-full-run-target.md §Required Tests
             min_level=1

M-Azure-002  Run failure-mode xUnit suite against Azure SQL Express
             locally + against live Azure SQL via integration test mode
             worker_class=scoped, runtime=codex-cli, coupling=medium
             owned_paths: backend-dotnet/tests/Integration/**
             acceptance: 13/13 pass against live dev Azure SQL
             min_level=1

M-Azure-003  Verify queue redelivery test passes on real Azure Service Bus
             worker_class=scoped, runtime=codex-cli, coupling=medium
             owned_paths: backend-dotnet/tests/Worker/**
             acceptance: induce redelivery via Service Bus dead-letter,
                         verify no double-charge
             min_level=1

M-Azure-004  Verify Stripe webhook replay test against Azure endpoint
             worker_class=scoped, runtime=codex-cli, coupling=medium
             owned_paths: backend-dotnet/tests/**
             acceptance: 5 duplicate events stored once; subscription state
                         reaches active without double-mutation
             min_level=1
```

### Phase B — frontend routing

```text
M-Azure-005  Add BACKEND_BASE_URL env var to Next.js
             worker_class=scoped, runtime=codex-cli, coupling=low
             owned_paths: .env.example, lib/config.ts (or equivalent),
                          app/api/rewrite/route.ts
             forbidden_paths: .env.local, .dev.vars, globalapikey/**
             acceptance: app/api/rewrite/route.ts proxies to
                         process.env.BACKEND_BASE_URL when set;
                         tests/unit/api-rewrite-proxy.test.ts passes
             min_level=1

M-Azure-006  Wire client-side rewrite to use new BACKEND_BASE_URL
             worker_class=scoped, runtime=codex-cli, coupling=medium
             owned_paths: components/app/rewrite-workspace.tsx,
                          lib/api/rewrite-client.ts
             acceptance: with BACKEND_BASE_URL unset, workspace works
                         against existing /api/rewrite; with it set
                         to https://replyinmyvoice-func-dev.azurewebsites.net,
                         workspace works against Azure backend
             min_level=1

M-Azure-007  E2E Playwright test against Azure backend (when sandbox unblocked)
             worker_class=coordinator-poll (evidence-lane), evidence_type=manual-only
             acceptance: operator runs Playwright against staging w/ Azure backend,
                         posts results to plans/evidence/M-Azure-007.md
             min_level=1 (no auto-execution; coordinator-poll)
```

### Phase C — pre-cutover validation

```text
M-Azure-008  Add Stripe webhook URL to point at Azure endpoint (DUAL-WRITE)
             worker_class=coordinator-poll (evidence-lane), evidence_type=stripe-event
             acceptance: Stripe dashboard shows webhooks delivered to BOTH
                         Cloudflare and Azure endpoints; verifier checks
                         StripeEvent table in Azure SQL has events for
                         last 24h matching Stripe Dashboard count
             min_level=1 (operator does the dashboard click)

M-Azure-009  24-hour soak: shadow traffic + telemetry compare
             worker_class=coordinator-poll, evidence_type=file-present
             expected_artifact: plans/evidence/M-Azure-009-soak.md
             acceptance: report shows error rate Azure ≤ Cloudflare
                         AND latency p95 within 50% of Cloudflare
             min_level=1
```

### Phase D — cutover

```text
M-Azure-010  DNS swap api.replyinmyvoice.com → Azure
             worker_class=coordinator-poll, evidence_type=manual-only
             acceptance: dig api.replyinmyvoice.com resolves to Azure
                         IP; /health returns 200 within 5 min of dig success
             forbidden_paths: ALL (operator-only Cloudflare dashboard action)
             min_level=5 (human-only per §6 hard stops on DNS)

M-Azure-011  Stripe webhook URL switch to Azure-only
             worker_class=coordinator-poll, evidence_type=manual-only
             acceptance: Cloudflare endpoint receives no webhook for 1h
                         after switch; Azure endpoint receives the
                         webhook for the test event triggered at switch
             min_level=5 (Stripe dashboard action)
```

### Phase E — stabilization (14 days)

```text
M-Azure-012  14-day stability monitor (Azure as primary)
             worker_class=coordinator-poll, evidence_type=manual-only
             acceptance: KPI checkpoint at days 1, 3, 7, 14;
                         no incident requiring rollback to Cloudflare
             min_level=5 (passive operator check)
```

### Phase F — decommission

```text
M-Azure-013  Cloudflare Worker decommission + Neon read-only
             worker_class=scoped, runtime=codex-cli, coupling=medium
             owned_paths: wrangler.toml, plans/cloudflare-decommission.md
             acceptance: wrangler deploy --dry-run shows no production
                         worker; Neon Postgres dashboard shows DB role
                         demoted to read-only
             min_level=1 (with operator gate at end for actual `wrangler delete`)
```

---

## 6. Dependencies (DAG)

```text
M-Azure-001 ──┬──> M-Azure-002 ──┬──> M-Azure-009 ──> M-Azure-010 ──> M-Azure-011 ──> M-Azure-012 ──> M-Azure-013
              │                  │
              └──> M-Azure-003 ──┤
              │                  │
              └──> M-Azure-004 ──┘

M-Azure-005 ──> M-Azure-006 ──> M-Azure-007 ──> M-Azure-009
```

M-Azure-007 + M-Azure-008 + M-Azure-009 gate M-Azure-010.
M-Azure-010 gates M-Azure-011.
M-Azure-011 gates M-Azure-012.
M-Azure-012 gates M-Azure-013.

---

## 7. Acceptance for "Azure migration done"

The entire M-Azure-Migration epic is done when ALL of:

1. `dig api.replyinmyvoice.com` resolves to Azure ✓ (M-Azure-010)
2. Last 100 Stripe webhook events all delivered to Azure, 0 to Cloudflare ✓ (M-Azure-011)
3. 14 days uptime ≥ 99.5% on Azure ✓ (M-Azure-012)
4. `wrangler` shows no production Worker for replyinmyvoice ✓ (M-Azure-013)
5. Neon dashboard shows DB in read-only mode ✓ (M-Azure-013)
6. All 13 failure-mode xUnit tests pass on the last 7 days of CI runs (M-Azure-001 — M-Azure-004 evidenced repeatedly)

---

## 8. Rollback plan (LOCKED — for each cutover gate)

```text
gate           rollback signal                 rollback action (operator)
────────────────────────────────────────────────────────────────────────
M-Azure-010    /health 500 or > 30s p95         flip DNS api.* back to Cloudflare
               within 1h of DNS flip            Worker (5 min TTL on DNS records)

M-Azure-011    Stripe webhook delivery failure  re-add Cloudflare URL alongside
               > 5% within 1h of switch         Azure URL; investigate before
                                                removing Cloudflare again

M-Azure-012    Day-1 SEV-1 incident             flip DNS back AND Stripe URL
               (data loss, real $$ wrong)       back to Cloudflare. Pause
                                                cutover. Engage L4 supervisor.

M-Azure-013    None — gate only flips after     N/A (this is the point of
               14 day stability                 no return)
```

Day-14 (post M-Azure-012) is the no-return point. Day-90 = Neon project deletion.

---

## 9. Cost projection

Already-provisioned (no new spend):

```text
Functions App (Consumption tier)   ~NZ$0-5/month (pay-per-execution)
Azure SQL (S0 or Basic)            ~NZ$8/month
Service Bus (Basic tier)           ~NZ$0.50/month
Application Insights (5GB free)    ~NZ$0/month at current volume
Cloudflare (unchanged until M-Azure-013) ~NZ$0/month (free tier)
Total estimated dev:                ~NZ$8-14/month, well under AZURE_BUDGET_LIMIT
```

If cutover succeeds, post-decommission cost is the same minus the still-paid Neon plan (NZ$19/month). Net SAVING of NZ$19/month after Neon project deletion at day 90.

---

## 10. Stop conditions specific to this epic

In addition to §6 hard stops in `plans/lane-architecture-decisions.md`:

```text
- Azure SQL exceeds 80% of AZURE_BUDGET_LIMIT in any month → halt M-Azure-009
  onwards, escalate to operator
- Any 13-test failure that cannot be fixed within 2 L1 attempts → escalate to
  L3 (claude-checkpoint-review) per §14.6
- Real money path involved BEFORE M-Azure-011 → halt; operator confirms via
  manual ledger inspection in Stripe Dashboard
- Stripe sandbox→live cutover NOT YET DONE → M-Azure-010 + M-Azure-011 still
  apply but the success criteria use sandbox events, not live; flag this
  ambiguity to operator before proceeding
- Cloudflare DNS API not reachable from sandbox → M-Azure-010 forced to L5
  (operator does the dashboard click; no automation)
```

---

## 11. What this spec deliberately does NOT decide

- The exact `dotnet test` filter expression for the 13 failure-mode tests (M-Azure-001 implementation detail)
- The Playwright fixture format for M-Azure-007 (test author picks)
- The KPI dashboard URL for M-Azure-012 14-day monitor (operator's choice — likely Application Insights Live Metrics)
- Whether to keep `api.replyinmyvoice.com` as the hostname or switch to `api.timeawake.co.nz` for the B2B API later (M8-API integration concern, not migration concern)
- Whether to migrate Entra auth (M1-Entra epic) before or after Azure cutover — D3 above says AFTER; if operator wants BEFORE, this spec needs an amendment

---

## 12. Phase implementation mapping

```text
Phase 1 (lane dispatcher)    → reads these items at the selector level only
Phase 2 (scoped runtime)     → executes M-Azure-001, 002, 003, 004, 005, 006, 013 (the codex-cli items)
Phase 3 (evidence-lane)      → operator inbox for M-Azure-007, 008, 010, 011, 012
Phase 5 (epic-planner)       → may shard M-Azure-013 into smaller items if codex
                                exits "needs-planner" on the Cloudflare/Neon
                                decommission step
Phase 7a (completion-bound)  → if M-Azure-002 (full integration suite) exceeds
                                scoped wall ceiling, promotes to completion-bound
                                via §14.6 escalation
```

---

## 13. Amendment procedure

Same as `plans/lane-architecture-decisions.md` §12. Locked sections in this spec are §3 decisions D1-D7, §4 phase order, §5 item decomposition, §7 acceptance, §8 rollback. Amendments add an `## Amendment <date>` section and a one-line entry in `plans/decisions-log.md`.
