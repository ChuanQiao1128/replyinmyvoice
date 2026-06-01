# Reply In My Voice — Rewrite Packs & Pricing Specification

- **Status:** Draft for implementation — 2026-05-24
- **Owner decision source (product/pricing):** GPT Pro design brief (pasted by the project owner 2026-05-24). The business/pricing design is **accepted as-is and must not be redesigned.**
- **Architecture source:** `docs/architecture-decision-record.md` (ADR-0001 — all-C# backend on Azure).
- **Policy source of truth:** `AGENTS.md`.
- **Skills applied:** `system-spec-synthesis` (structure), `cloud-architecture-cost-review` (economics). Ledger atomicity, state lifecycle, and refund/idempotency should get dedicated `data-module-review`, `state-machine-modeling`, and `resilience-test-generation` passes during implementation.

> **Legend:** `[GPT Pro]` = accepted from the owner's brief, do not change · `[RECONCILED]` = same intent, retargeted to C#/Azure · `[ADDED]` = gap Claude is supplementing · `[ASSUMPTION]` = needs confirmation · `[OPEN]` = decision required from owner.

---

## Owner Decisions — 2026-05-24

Resolves Open Questions #2 and #3 below:

- **Input limit (was 3000):** the owner considers 3000 too large. **Clarification:** 3000 *characters* ≈ 500 English *words*, not 3000 words — so the owner's "under ~400–500 words" target maps to ~2400–2500 chars. **Locked to ~2500 characters total / ~420 English words** (one constant; owner may dial). **Enforce by characters** (deterministic — word counting is ambiguous); the combined total is authoritative and per-field caps become sub-limits under it. Do not go far below ~2000, or the core workflow "paste a short email to reply to + write a draft" stops fitting. **Wherever this spec says 3000 (or 5000) for the input cap, read 2500.** Implementation must update `lib/rewrite-limits.ts` (`combined: 2500`), `lib/validation.ts`, `AGENTS.md`, and `replyinmyvoice_requirements.md`.
- **Campaign codes:** **one redemption per user, total** (not one-per-code). A user may claim a bonus from at most one campaign code ever — closes the +23 stacking hole.

---

## Context

This spec turns the owner's rewrite-packs pricing brief into an implementation-ready plan, reconciled to ADR-0001 (the backend is moving entirely to C#/.NET on Azure) and to the repository's real state.

Source inputs (no secret values quoted):
- GPT Pro brief — SKUs, prices, rewrite counts, UI copy, ledger concepts, webhook rules, campaign codes, tests, acceptance.
- `docs/architecture-decision-record.md` — C# is the single backend of record; Next.js stays as presentation/BFF; Python analytics deferred.
- `AGENTS.md` — confirmed product decisions (current: free = 3 lifetime, paid = NZ$9/mo / 40 rewrites; combined input cap 5000 chars), banned marketing terms, deployment gates, secrets policy.
- Repo reality — a deployed C# billing core on Azure dev already exists (`backend-dotnet/`: `QuotaService`, `UsageReservation`, `StripeBillingService`, `StripeEventService`, `OutboxMessage`, `ExpiredReservationCleanup`); the user-facing rewrite still runs the TS pipeline; data is split Azure SQL + Neon; the working tree is dirty (94 files, 57 stashes on `feat/api-keys`).

**Core reconciliation:** GPT Pro's brief is written in TS/Prisma/Next-API terms (`lib/rewrite-balance.ts`, Prisma models, `/api/campaign/redeem`). Building it that way would add a new pile of TS that ADR-0001 then has to port — directly increasing the "mess" the owner wants gone. Therefore the **billing/packs/ledger/webhook/balance/API logic is implemented in C#/EF Core/Azure SQL**, extending the primitives that already exist. Only the **presentation pieces stay in TS/Next.js**.

---

## Goals

1. Replace the single NZ$9/40-rewrite subscription model with: Free Trial → one-time **rewrite packs** → **Pro/API** subscription. `[GPT Pro]`
2. Show users **"rewrites"** everywhere; keep "credit" terminology internal-only. `[GPT Pro]`
3. Enforce **one total input limit per rewrite** (target 3000 chars) on both client and server. `[GPT Pro]`
4. Implement an idempotent **credit-grant ledger** with reserve → consume → refund semantics, in C#. `[RECONCILED]`
5. Gate **API access** to active Pro/API subscribers only. `[GPT Pro]`
6. Support **application-layer campaign bonus rewrites** (not Stripe coupons) for IG/TikTok/Xiaohongshu/forum launch. `[GPT Pro]`
7. Do all of the above as the **first vertical slice of the C#/Azure migration**, not as new TS. `[ADDED]`
8. Preserve existing paying users and all existing data (additive only). `[GPT Pro]`

---

## Non-Goals (this effort)

`[GPT Pro]` Student email verification · student-only pricing · "Student/Exam" SKU names · per-length 2×/3× charging · 5000-char inputs · unlimited plans · annual plans · auto-creating live Stripe Prices · Chrome extension · Gmail integration · custom voice profiles · team workspaces.

`[ADDED]` Python analytics build (deferred per ADR-0001) · frontend rewrite (Next.js stays) · live Stripe charges / production-domain cutover unless `LAUNCH_CONFIRMED` + `STRIPE_LIVE_CUTOVER_APPROVED` are set in the run brief.

---

## Current System (repo reality)

| Concern | Today | Implication for this spec |
|---|---|---|
| User-facing rewrite | TS pipeline in `app/api/rewrite/route.ts` on Cloudflare | Web rewrite endpoint will call the C# balance service; quota check moves server-side in C# |
| Quota / reservation | **Already C#** — `QuotaService`, `UsageReservation` (RESERVED→CONSUMED/RELEASED), idempotency by `(UserId, IdempotencyKey)` | **Extend**, don't rebuild. The grant ledger generalizes this. |
| Stripe webhook | C# `StripeEventService` (idempotent storage), `StripeBillingService` | **Extend** with pack-grant + subscription-grant mapping |
| Expiry sweeper | C# `ExpiredReservationCleanup` timer | **Extend** to handle grant expiry housekeeping |
| Data store | Split: Azure SQL (C#) + Neon Postgres (TS, 12 Prisma models) | New tables go in **Azure SQL via EF Core**; existing Neon usage data must be backfilled |
| Input cap | `lib/rewrite-limits.ts` `combined: 5000`; `AGENTS.md` says 5000 | Change to 3000 total **and** update `AGENTS.md` (governance) |
| Working tree | `feat/api-keys`, **94 dirty files, 57 stashes** | **Phase 0 cleanup is a hard prerequisite** (see Rollout) |

---

## Proposed Architecture

**Bounded context:** *Billing & Rewrite-Pack Ledger* is owned by the C# backend (it is money + quota + idempotency — exactly what C# owns per ADR-0001).

```
[Next.js]  pricing page · char counter · balance display · campaign-code capture   (TS — stays)
   │ HTTPS + bearer (thin proxy; same pattern as lib/azure-api.ts /api/me)
[C# Azure Functions]  checkout · web-rewrite · /api/v1/rewrite · campaign/redeem · Stripe webhook
   │
[C# services]  RewriteBalanceService (extends QuotaService) · StripeBillingService · StripeEventService
   │
[Azure SQL via EF Core]  RewriteCreditGrant · RewriteCreditUsage · CampaignCode · CampaignRedemption
   │
[Timer Function]  grant-expiry housekeeping (extends ExpiredReservationCleanup)
```

`[RECONCILED]` Map of GPT Pro artifacts → C#:
| GPT Pro (TS) | This spec (C#) |
|---|---|
| `lib/rewrite-balance.ts` | `RewriteBalanceService` in `ReplyInMyVoice.Infrastructure/Services` (extends `QuotaService`) |
| Prisma `RewriteCreditGrant` etc. | EF Core entities in `ReplyInMyVoice.Domain/Entities` + additive `AppDbContext` migration |
| `POST /api/campaign/redeem`, `POST /api/v1/rewrite`, checkout | C# Functions in `ReplyInMyVoice.Functions/Functions`; Next.js routes become thin proxies |
| Shared `MAX_TOTAL_REWRITE_INPUT_CHARS` | **Authoritative in C#**; `lib/rewrite-limits.ts` keeps a mirrored constant for client-side UX only |

---

## Data Model

`[RECONCILED]` EF Core entities (additive migration; **never reset**). Field names follow GPT Pro's brief; types are C#/EF equivalents.

- **RewriteCreditGrant** — `Id, UserId, Amount, Remaining, Source(enum), StripePriceId?, StripeCheckoutSessionId? (unique), StripeInvoiceId? (unique), StripeSubscriptionId?, CampaignCode?, Metadata(json)?, ExpiresAt?, CreatedAt, UpdatedAt`. Indexes: `(UserId)`, `(UserId, ExpiresAt)`, `(UserId, Remaining)`.
- **RewriteCreditUsage** — `Id, UserId, GrantId?, ApiKeyId?, Channel(enum), Status(enum), IdempotencyKey?, RequestHash?, ErrorCode?, CreatedAt, ConsumedAt?, RefundedAt?`. Indexes: `(UserId)`, `(ApiKeyId)`, `(Status)`, `(IdempotencyKey)`.
- **CampaignCode** — `Id, Code(unique), BonusRewrites, ExpiresInDays(=14), MaxRedemptions?, RedeemedCount, Active, StartsAt?, EndsAt?, Source?, CreatedAt, UpdatedAt`.
- **CampaignRedemption** — `Id, UserId, CampaignCodeId, GrantId, CreatedAt`. Unique `(UserId, CampaignCodeId)`.
- Enums: `RewriteCreditSource { SIGNUP, CAMPAIGN, PURCHASE, SUBSCRIPTION, REFERRAL, ADMIN, REFUND }`, `RewriteUsageChannel { WEB, API, ADMIN }`, `RewriteUsageStatus { RESERVED, CONSUMED, REFUNDED }`. `[GPT Pro]`

`[ADDED]` **Backfill of existing users (do not strand balances):** during the additive migration, seed grants for current users — remaining free quota → a `SIGNUP` grant; active legacy NZ$9 subscribers → a `SUBSCRIPTION` grant for the current period. This intersects ADR-0001 Workstream E (Neon→Azure SQL consolidation); migrate the Neon `RewriteUsage`/`User` subscription data into Azure SQL as part of this.

`[ADDED]` **User entity fields** (reuse existing schema where present): `planTier (FREE|PRO)`, `apiAccess (bool)`, plus existing `subscriptionStatus`. Do not duplicate an existing subscription table.

---

## API and Job Contracts

`[RECONCILED]` All endpoints are C# Functions; Next.js proxies forward the bearer token.

**Checkout** — input `{ "sku": "quick_pack" | "value_pack" | "focus_pack" | "pro_api" }`. Maps SKU → env price id + mode (`payment` for packs, `subscription` for pro_api). Sets `metadata { userId, sku, rewrites }`. `[GPT Pro]`

**Web rewrite** and **API rewrite** share one flow `[GPT Pro]` `[ADDED: explicit idempotency]`:
1. Validate input (≤ 3000 total chars). 2. (API) validate key + active Pro. 3. Check available balance ≥ 1. 4. **Reserve 1** (atomic). 5. Call rewrite pipeline. 6. Success → **consume**; failure → **refund**. 7. Record `RewriteCreditUsage`. 8. Return new balance.
- `[ADDED]` WEB channel supplies an idempotency key per click (client-generated or server-derived from `RequestHash`) to dedupe double-clicks, mapping onto the existing `(UserId, IdempotencyKey)` mechanism.

**`POST /api/v1/rewrite`** (API) — auth header is the API key; errors: `API_REQUIRES_PRO` (403), `QUOTA_EXHAUSTED` (402/403), `INPUT_TOO_LONG` (400, `limit: 3000`). `[GPT Pro]`

**`POST /api/campaign/redeem`** — `{ "code": "XHS" }` → `{ ok, bonusRewrites, balance }`; errors `ALREADY_REDEEMED`, `INVALID_CODE`. `[GPT Pro]`

**Stripe webhook** (idempotent via `StripeEventService`) `[GPT Pro]` `[RECONCILED]`:
- One-time packs: on `checkout.session.completed` with `mode=payment` & `payment_status=paid` → grant by SKU (`PURCHASE`, expires +90d, `StripeCheckoutSessionId` unique-guards double-grant).
- Pro/API: current C#/Azure entitlement is **status-driven**, not a `RewriteCredit` grant. `active`/`trialing` subscriptions receive the paid usage plan of 90 rewrites per Stripe period.
- Renewal failure grace policy: **no grace period** for Pro/API renewal failures. `past_due`, `unpaid`, `incomplete`, `incomplete_expired`, and `paused` immediately map to the free plan (`free:lifetime`, limit 3). `canceled`/`customer.subscription.deleted` also map to a non-paid plan. Already-purchased pack rewrites are not revoked by subscription status alone and still expire or get clawed back through the refund/dispute paths.
- `invoice.payment_failed`: process idempotently, locate the user by Stripe customer and/or subscription, and emit a structured operations log with the Stripe event correlation id for PAY-02 alerting. Do not send email from this handler.
- `[ADDED]` Grant source-of-truth for one-time packs = **checkout `metadata`** (sku + rewrites), with line-item price id as a cross-check (Stripe events don't include line items unless the session is retrieved). `[ADDED]` Ack and no-op unhandled event types. If a later issue changes Pro/API from status-driven quota to subscription credit grants, grant only on invoice `billing_reason ∈ { subscription_create, subscription_cycle }` and ignore others to avoid double-grant.

**Grant-expiry job** — `[ADDED]` balance is computed on read (ignore `ExpiresAt ≤ now` and `Remaining ≤ 0`); a timer Function (extend `ExpiredReservationCleanup`) does housekeeping only.

---

## State and Error Handling

**Grant lifecycle:** `granted → (partially) consumed → exhausted (Remaining=0) | expired (ExpiresAt≤now)`. Consumption order = **earliest-expiring grant first (FIFO by ExpiresAt)** so campaign bonuses (14d) burn before packs (90d). `[GPT Pro]`

**Usage state machine:** `RESERVED → CONSUMED` (success) or `RESERVED → REFUNDED` (pipeline failure). Never charge after a successful pipeline call only — reserve **before** the model call. `[GPT Pro]`

`[ADDED]` **Concurrency / double-spend:** reservation across multiple FIFO grants must be atomic (DB transaction with row locking / serializable isolation / optimistic concurrency). The C# `QuotaService` already proves "concurrent requests cannot exceed the slot" — reuse that pattern. Required test: N concurrent reserves on a balance of 1 yield exactly one success.

`[ADDED]` **Displayed balance** = `Remaining (valid grants) − in-flight RESERVED`, so concurrent/in-flight rewrites are held.

`[ADDED]` **Refunds / chargebacks / disputes:** handle `charge.refunded` and `charge.dispute.created`/`closed`. Policy `[OPEN]`: claw back unused granted rewrites on refund/chargeback; define whether balance may go negative (recommend floor at 0 and record an `ADMIN`/`REFUND` negative-adjustment grant for audit).

Error envelope `[GPT Pro]`: `{ "error": { "code", "message", "limit"? } }` with codes `INPUT_TOO_LONG`, `QUOTA_EXHAUSTED`, `API_REQUIRES_PRO`, plus campaign `ALREADY_REDEEMED` / `INVALID_CODE`.

---

## Security and Privacy

`[GPT Pro]` Server-authoritative everywhere; client limits are UX only. API keys creatable/usable **only** by active Pro/API; one-time packs never unlock API. Webhooks idempotent. Campaign bonuses are app-layer grants, never Stripe coupons. Never auto-create live Stripe Prices (owner creates them and sets env/Worker secrets). No public unpaid rewrite endpoint.

`[ADDED]` **Campaign abuse cap (RESOLVED):** GPT Pro prevents re-redeeming the *same* code, but a user could redeem **every** seed code (XHS+TIKTOK+IG+FORUM+LAUNCH = +23 rewrites). **Owner decision: one campaign redemption per user, total** — enforce a per-user `CampaignRedemption` count of 1 (not just unique per code). Implement before launch.

`[ADDED]` **Free-trial multi-account abuse:** 3 free per verified identity (Entra/Clerk); accept residual MVP risk; mitigations later (email verification already exists; device/IP signals later).

`[ADDED]` **API rate limiting** beyond balance: per-key request rate cap (RPS/RPM) to bound cost spikes — via Azure API Management (ADR-0001) or an app-level token bucket.

`[ADDED]` **Restricted marketing terms:** obey the `AGENTS.md` banned-term list. Do not use the project's restricted substrings or make claims about defeating content-detection systems or academic-misconduct use; keep copy to "natural, clear, sounds-like-you replies." (Note: the CI grep guard scans `app components public lib scripts tests`.)

Secrets: validate at runtime; never commit/log secret values.

---

## Economics & Cost Guardrails `[ADDED]`

GPT Pro's margin table counts only the **NZ$0.12/rewrite** provider cost. Real margin must also absorb **Stripe fees** (domestic 2.65% + NZ$0.30; international 3.5% + NZ$0.30; +~2% on currency conversion) and **GST 15%** (once turnover hits the IRD NZ$60,000 threshold; prices shown to NZ consumers must be GST-**inclusive**).

Corrected gross margin (provider NZ$0.12/rewrite assumed):

| SKU | Price | Rewrites | Provider cost | Margin now (domestic, pre-GST) | Margin worst case (intl + GST) |
|---|---|---|---|---|---|
| Quick Pack | NZ$2.50 | 10 | NZ$1.20 | ~NZ$0.93 (~37%) | ~NZ$0.54 (~21%) |
| Value Pack | NZ$6.90 | 30 | NZ$3.60 | ~NZ$2.82 (~41%) | ~NZ$1.72 (~25%) |
| Pro/API | NZ$19.90/mo | 90 | NZ$10.80 | ~NZ$8.27 (~42%) | ~NZ$5.11 (~26%) |
| Focus Pack | NZ$4.90 | 20 | NZ$2.40 | ~NZ$2.07 (~42%) | ~NZ$1.29 (~26%) |

Findings (do **not** change the owner's prices; flag for awareness):
- The NZ$0.30 fixed Stripe fee hits **Quick Pack** hardest — treat NZ$2.50/10 as an **acquisition product** (worst-case ~21% gross margin, below the ~30% target).
- **"Cost per rewrite" must be the full bounded pipeline**, not one model call. One user rewrite can fan out to up to 3 candidates + targeted repairs (hard cap 10 attempts) of DeepSeek calls **plus** Sapling naturalness calls. Measure actual cost from the existing `RewriteCostLog` / `RewriteProviderCall` before trusting NZ$0.12 — DeepSeek is cheaper than the old OpenAI estimate, so true margins are likely **better** than the worst case above.
- Keep the conservative rewrite counts. `[GPT Pro]` Do not raise counts (e.g., Value→35, Pro→110) until measured worst-case cost is stably < NZ$0.10/rewrite.
- GST: not registered yet (pre-NZ$60k); set prices GST-inclusive now so registration doesn't force a reprice. Track cumulative turnover toward the threshold.

---

## Rollout Plan

### Phase 0 — Repo hygiene (hard prerequisite) `[ADDED]`
The tree is `feat/api-keys` with **94 dirty files + 57 stashes** (overnight-supervisor collision; see memory note). Before any feature work:
1. **Snapshot** the current dirty tree to a safety branch/tag (don't lose in-flight C# work).
2. **Triage the 94 dirty files** deliberately — commit the good C# work, discard cruft. Do **not** bulk-discard.
3. **Audit the 57 stashes** — most are overnight noise, but some may hold real work. Export anything uncertain to patch files before dropping. Do **not** blanket `stash clear`.
4. Cut a **clean baseline branch** (`feat/rewrite-packs-pricing`) from a known-good commit, ideally in an **isolated git worktree** so the overnight supervisor can't collide.
5. Confirm `lib/admin-visible.ts` and `lib/rewrite-completeness.ts` remain present (they do now). If ever shown `deleted` and not by this task, restore or ask — never commit the deletion. `[GPT Pro]`

### Phase 1 — C# ledger + webhook + balance (the core)
EF entities + additive migration; `RewriteBalanceService` (`getBalance/grant/reserve/consume/refund`, FIFO-by-expiry); extend `StripeEventService` mapping for packs + subscription grants; expiry housekeeping. New env (no hardcoded price ids): `STRIPE_PRICE_QUICK_PACK_NZD`, `STRIPE_PRICE_VALUE_PACK_NZD`, `STRIPE_PRICE_PRO_API_MONTHLY_NZD`, `STRIPE_PRICE_FOCUS_PACK_NZD` (optional; hidden unless set + flag on). Preserve legacy `STRIPE_PRICE_ID` mapping. `[GPT Pro]`

### Phase 2 — Frontend rewire (TS, stays)
Pricing page → generic packs (Free Trial / Quick / Value / Pro/API; Focus hidden); live char counter `2,340 / 3,000` + "Uses 1 rewrite"; balance display "8 rewrites left"; out-of-rewrites → "Buy rewrites"; update balance without reload; don't decrement on refunded failures. Copy per GPT Pro §13–14 (UI says "rewrites"). Set `combined: 3000` in `lib/rewrite-limits.ts` and **update `AGENTS.md`** (see Open Questions on per-field reconciliation).

### Phase 3 — Campaign codes
Capture `?code=` → localStorage → redeem after auth; seed XHS/TIKTOK/IG/LAUNCH (+5, 14d), FORUM (+3, 14d); enforce the abuse cap from Security.

### Phase 4 — Data migration & legacy preservation
Backfill existing users into the ledger; keep legacy NZ$9/40 subscribers working; don't auto-migrate them (owner decides later). `[GPT Pro]`

Flags/gates: Focus Pack behind env+flag; live prices owner-created; no live charges/cutover unless launch flags set.

---

## Verification Plan

`[GPT Pro]` unit coverage: signup grants 3; campaign +5 once; balance ignores expired; FIFO earliest-expiry consumed first; 3000 chars accepted / 3001 rejected; success decrements 1; failure refunds; Quick/Value/Pro webhook grants 10/30/90; webhook replay doesn't double-grant; non-Pro can't create/use API key; Pro out-of-balance → `QUOTA_EXHAUSTED`.

`[ADDED]` also: **campaign abuse cap** (can't redeem all seed codes past the cap); **concurrency** (N parallel reserves on balance 1 → exactly one success); **refund/chargeback** claws back correctly and never goes below floor; webhook grants **only** on `subscription_create`/`subscription_cycle`; **backfill** preserves existing balances; idempotent web double-click charges once.

`[GPT Pro]` manual QA: signup shows 3; 3000 ok / 3001 disabled; success 3→2; exhaustion shows buy prompt; Quick/Value/Pro checkouts grant +10/+30/+90 and Pro unlocks API; pack-only users can't reach API-key creation; campaign XHS +5 then second attempt fails; webhook replay no double-grant; legacy subscriber unaffected.

`[ADDED]` Use `dotnet-backend-testing` (xUnit + WebApplicationFactory + EF SQLite) for backend; `resilience-test-generation` for reserve/refund/webhook-replay/concurrency; `ui-browser-testing` (Playwright) for the pricing/counter/balance UI.

Deliverables to owner on completion `[GPT Pro]`: changed-file list; migration name; new env list; **Stripe live Prices to create manually**; local test commands; manual QA results; legacy-subscription risk; outstanding TODOs.

---

## Open Questions (decisions needed from owner)

1. `[OPEN]` **Build order:** confirm the ledger/webhook/balance/API is built in **C#** now (recommended — it advances ADR-0001 and reuses existing C# billing primitives), not in new TS. Owner priority already states "C#/Azure migration first," so this is the assumed default.
2. ✅ **RESOLVED** (see Owner Decisions 2026-05-24): input cap = **~2500 chars total**, char-based, combined authoritative, per-field caps demoted to sub-limits; update `AGENTS.md` + `replyinmyvoice_requirements.md`.
3. ✅ **RESOLVED** (see Owner Decisions 2026-05-24): **one campaign redemption per user, total.**
4. `[OPEN]` **Quick Pack margin:** accept NZ$2.50/10 as a thin-margin acquisition product, or revisit after measuring real pipeline cost from `RewriteCostLog`?
5. ✅ **RESOLVED** (PAY-01 2026-06-01): Pro/API renewal failures have **no grace period**. `past_due`, `unpaid`, `incomplete`, `incomplete_expired`, and `paused` immediately fall back to the free status-driven quota. Monthly rollover and mid-cycle upgrade/downgrade/proration remain deferred unless a later payment issue changes them.
6. `[OPEN]` **Refund/chargeback policy:** claw-back unused rewrites + floor balance at 0?
7. `[ASSUMPTION]` Pre-GST (turnover < NZ$60k); prices set GST-inclusive in anticipation.
