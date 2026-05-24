# Single C# + Azure SQL Backend Migration

**Status:** Planning — awaiting owner decisions (see §4)
**Date:** 2026-05-24
**Owner decision (this session):** Collapse the dual stack onto **one backend = C# + Azure SQL**.
**Planning skill:** `claude-heavy-planning-handoff` (invoked directly by Claude Code).

---

## 1. Locked End State

| Layer | End state |
|---|---|
| **Frontend** | Next.js / React on Cloudflare, **thin**. Owns: Entra login/session cookie (BFF), UI, localStorage history, and a same-origin proxy to the C# API. Owns **no database**. |
| **Backend** | **100% C#** on Azure. ASP.NET Core API / Azure Functions + Worker. **Azure SQL is the sole datastore** via EF Core. The **full rewrite quality engine runs in C#**. |
| **Auth** | **Entra** (Azure-native) stays. Canonical user key = the Entra **`oid`** claim (stable across ID + access tokens; audience-independent). |
| **Deleted** | **Neon**, **Prisma**, every Prisma model, `lib/generated/prisma`, and all **Clerk** remnants (`clerkUserId` column, `ResolveClerkIssuer` dead code, dormant Clerk env vars). |

**Hard constraint:** the live site runs on Neon **today**. This is a **port-then-delete** migration — Neon is deleted *last*, only after each capability is live on C#. Production stays green at every slice.

---

## 2. Why this is the right move

The current dual stack has a latent identity bug (confirmed this session): Neon `User.clerkUserId` stores the **ID-token `sub`** (audience = frontend client), while the C# backend keys `AppUser.ExternalAuthUserId` on the **access-token `sub`** (audience = `api://<API client id>`). Because Entra `sub` is **pairwise per audience**, those are *different strings for the same human* — there is no reliable bridge between the two user tables. Collapsing to one backend (and keying on `oid`) eliminates the bridge problem entirely instead of papering over it.

---

## 3. Inventory: what exists vs what must be built

### 3a. Already in C# / EF Core (deployed in E, live or live-ready)
- **Entities (6):** `AppUser`, `UsagePeriod` (≈ Prisma `RewriteUsage`), `RewriteAttempt`, `UsageReservation`, `StripeEvent`, `OutboxMessage`.
- **Services:** `AccountService`, `QuotaService` (reservation + idempotency), `RewriteRequestService` + `RewriteJobProcessor` (thin transactional glue), `StripeBillingService`, `StripeEventService` (webhook idempotency + entitlement sync), `OutboxDispatcherService`, `ExpiredReservationCleanupService`.
- **Functions:** Account (`/api/me`), Billing (checkout/portal), Stripe webhook, Rewrite HTTP + RewriteJob, Health, timer triggers.
- **Worker:** Service Bus rewrite worker, outbox dispatcher, reservation cleanup.
- **Auth:** `FunctionAuthResolver` (Entra JWKS, issuer, audience, scope validation).

### 3b. Must port TS → C# (the work)
| # | Surface | TS source (approx LOC) | Notes |
|---|---|---|---|
| **A. Rewrite quality engine** | input-analyzer, fact-extraction + fact-ledger, candidate generation, structured reviewer, deterministic + LLM fact gates, structural send-ready gates, Sapling naturalness gate, **Rewrite Quality Strategist Agent** + diagnosis + repair playbook, budget manager, bounded escalation, canary + canary-rollback | `lib/rewrite-pipeline/*` (~5k), `lib/openai.ts` (1.8k), `lib/fact-extraction.ts` (0.7k), `lib/writing-signal.ts` (0.34k), `lib/rewrite-diagnosis.ts` (0.24k) ≈ **~8k LOC** | **Dominant effort + riskiest cutover.** C# today only has a 112-LOC single-shot provider. Provider: DeepSeek (OpenAI-compatible) + Sapling. |
| **B. Data models (no EF equivalent)** | `RewriteCostLog` + `RewriteProviderCall`, `RewriteLearningSample`, `LearningRun` + `LearningFinding` + `StrategyCandidate`, `RewriteCanaryRollback`, `RewriteCredit`, `Referral`, `ApiKey` + `ApiKeyUsage` | Prisma schema (~12 models) | Each = EF entity + config + migration. Additive/dormant (like E) until the engine writes them. |
| **C. Learning / admin / eval tooling** | learningops run + promotion-brief, admin/learning + admin/metrics, scenario-evaluation-regression, eval corpus | `lib/learningops*` (~1k), `lib/admin/*` (0.6k), eval (~0.4k) | Internal, **port last**. Eval corpus becomes the C# parity gate. |
| **D. B2B API keys** | ApiKey issuance + API-key auth middleware + per-key rate limit + monthly quota + usage logging | `ApiKey`/`ApiKeyUsage` (Prisma) | Now cleanly keyed on `AppUser.Id` — no Neon bridge needed. |

### 3c. Stays on the frontend (NOT ported)
- `lib/entra-auth.ts` (653) + `lib/entra-native-auth.ts` (302): login redirect, email-OTP sign-up, **session cookie minting**. This is the BFF auth layer. C# only *validates* the forwarded bearer token. (Both sides canonicalize on `oid`.)
- React UI, client validation mirror, localStorage history (`rimv.rewrite.history.v1`).

---

## 4. Architecture / cost decisions needing owner input

1. **⭐ Compute hosting for the ported rewrite engine (the big one).** The engine is long-running: up to 10 LLM attempts + fact gates + Sapling round-trips → tens of seconds to >2 min per request.
   - **Azure Functions (Flex Consumption)** — scale-to-zero (cheap for spiky MVP), but cold starts + execution-time ceilings. The async pattern already built (HTTP reserves quota → 202 → Service Bus → Worker finalizes) fits this well.
   - **App Service (always-on)** — no cold start, no time ceiling, but fixed monthly cost (AGENTS.md explicitly challenges always-on for MVP).
   - **Container Apps** — scale-to-zero + no time ceiling + more control; slightly more ops.
   - **Recommendation:** keep the existing **HTTP-enqueue → Service Bus → Worker** async pattern; run the heavy rewrite in the Worker; host on scale-to-zero compute. Run a focused `cloud-architecture-cost-review` on this before Slice 3. **Needs owner risk/cost appetite.**
2. **Rewrite cutover quality bar.** AGENTS.md: "do not deploy as final while any known evaluation sample fails." Confirm the C# engine must pass the full 60/100-case eval (fact gates + Sapling signal-reduction target) before the live `/api/rewrite` flips.
3. **Neon decommission timing.** Keep Neon read-only as a fallback for a short window post-cutover, or hard-delete immediately? (No real users → can be aggressive; a few-day safety window is cheap insurance.)
4. **B2B API-keys (D) scope.** ✅ **DECIDED (2026-05-24): in scope for this migration** — Slice 5 stays. API key issuance + per-key auth/rate-limit/quota land on C#/Azure SQL alongside the consumer path.

---

## 5. Staged, bounded-slice sequence (each ≈ one Codex run)

Principle: every slice is independently shippable and leaves prod green. The frontend repoints **endpoint-by-endpoint** behind the existing proxy pattern, so TS and C# coexist until each capability is proven on C#.

| Slice | Goal | Prod-safety |
|---|---|---|
| **0. Auth → `oid`** | C# `ResolveUserIdFromClaims` prefers `oid`; frontend session stores `oid` as canonical + populates `entraUserId`. Kills the sub/oid divergence bug. | Safe: `AppUser` table is fresh/empty (created in E) → no orphaning. Do first. |
| **1. Identity on C#** | Frontend account reads route to C# `/api/me` (proxy exists); stop writing Neon `User` on login. | Identity source of truth = `AppUser`; Neon `User` goes unused. |
| **2. Data models port** | Add EF entities + migrations for §3b-B (cost/telemetry, learning, canary, credits, referrals, API keys). | Additive + dormant (proven pattern from E). |
| **3. Rewrite engine port** (split) | 3a analyzer+facts+ledger+deterministic gates · 3b candidates+reviewer+LLM/structural gates · 3c Sapling gate · 3d Strategist+diagnosis+repair+budget+escalation · 3e canary+cost logging | Built behind a flag; not in live path yet. Each sub-slice validated vs eval corpus. |
| **4. Rewrite cutover** ⚠️ | Prove C# engine eval parity → flip `/api/rewrite` from TS pipeline to C# proxy. Keep TS engine behind a flag for instant rollback. | Riskiest slice. Flag + instant rollback; verify in prod. |
| **5. B2B API keys** (if in scope) | ApiKey issuance UI→C#, API-key auth middleware, per-key rate limit + monthly quota, `ApiKeyUsage` logging. Keyed on `AppUser`. | New surface; additive. |
| **6. Learning/admin/eval tooling** | learningops run + promotion brief + admin dashboards read from C#. | Internal; no user-facing path. |
| **7. Delete Neon + Prisma + Clerk** | Remove `lib/rewrite-pipeline/*`, `lib/openai.ts`, `lib/quota.ts`, `lib/stripe.ts`, `lib/learningops*`, `lib/users.ts`, Prisma schema + `lib/generated/prisma` + prisma deps + DATABASE_URL/DIRECT_URL. Remove Clerk dead code + env vars. Decommission Neon DB (after §4.3 window). | Last. Everything already live on C#. |

---

## 6. Risks & mitigations

- **Rewrite quality regression on cutover** *(highest)* — the engine *is* the product. Mitigate: C# must pass the full eval suite before flipping; keep TS engine behind a flag for instant rollback; verify in prod before deleting TS.
- **Prompt/LLM output drift TS→C#** — porting prompts can shift outputs. Mitigate: port the eval corpus to xUnit/integration first; treat it as the parity gate.
- **Long-running execution on Functions** — execution-time ceiling. Mitigate: the async enqueue + Worker pattern already exists; heavy work runs in the Worker.
- **Cost shift off Cloudflare Workers** — rewrite compute moves to Azure. Mitigate: scale-to-zero hosting (§4.1) + DeepSeek/Sapling spend already tracked.
- **Migration applies to live Azure SQL on merge to main** — each Slice-2 migration must be additive/dormant (proven low-risk in E).
- **Secrets** — DeepSeek/Sapling/Stripe keys live in Azure config, validated at runtime, never in source. Banned-term CI grep still applies to all ported C# (`humanizer|bypass|undetect|detector|evade`).

---

## 7. Open questions (resolve before Slice 3)

1. Compute hosting choice for the engine (§4.1).
2. Cutover quality bar = full eval parity? (§4.2)
3. Neon decommission timing (§4.3).
4. B2B API keys in-scope now or deferred? (§4.4)
5. Does the frontend `/api/rewrite` proxy need to preserve the exact current response contract (`rewrittenText`, `changeSummary`, `riskNotes`, Naturalness Check before/after) so the UI is unchanged? (Assumed **yes**.)
