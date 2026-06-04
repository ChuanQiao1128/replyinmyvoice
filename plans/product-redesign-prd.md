# Reply In My Voice — Product Redesign PRD & System Spec

- **Status:** Draft **v0.2** (owner feedback integrated)
- **Date:** 2026-05-24
- **Author:** Claude Code (supervisor/planner) — synthesized from owner PM strategy + owner design feedback + live codebase
- **Branch context:** authored in `feat/students-v2` worktree
- **Source inputs:** owner PM strategy + owner design-feedback (2026-05-24 chat); repo facts (`lib/quota.ts`, `lib/stripe.ts`, `lib/subscription.ts`, Prisma schema, `app/`, `components/`, `backend-dotnet/src/`); `AGENTS.md`; `plans/decisions-log.md`.
- **Convention:** **FACT** = verified in repo. **PROPOSED** = owner-supplied, pending sign-off. **ASSUMPTION** = my fill-in default, needs confirmation. **DECISION** = locked by owner.

### Changelog v0.1 → v0.2
- **DECISION locked:** billing source of truth = **Next.js** (#19.1); **Stripe live changes authorized** (#19.2) — exact numbers re-confirmed at the Price-creation step.
- Reframed core experience from "rewrite tool" → **reply-decision assistant** (§5 principles; §8.4 output).
- Output redesigned: **Ready to send + Facts preserved** (primary) + **Why this works · Tone check · Before-you-send** (secondary).
- Input: **progressive disclosure** (2 fields default, facts collapsed).
- Scenarios become **guided micro-flows**; light **task-first onboarding**.
- **Situational** post-copy paywall; **value-anchored** free quota.
- "Naturalness Check" → user-facing **"Tone check"** (multi-dimension, no detector framing).
- Pricing page **split**: monthly plans vs one-time; **Exam Week Pass** scenario packaging; top-up surfaced at exhaustion only.
- Referral **triggers + anti-abuse**; SEO templates become **semi-productized** (mini interactive box).
- Quota meter shows **per-source breakdown + expiry**.
- Added owner **P0/P1/P2 UX-refinement priority** (§17.1).

> ⚠️ Supersedes `plans/commercialization-roadmap.md`'s "NZ$9 / 40 rewrites / one clear plan" pricing (stale).

---

## 1. Context

Reply In My Voice is a live Next.js app (Cloudflare Worker via OpenNext, Neon Postgres, Entra auth, Stripe, DeepSeek + Sapling) that rewrites a rough draft reply into a clearer, more natural version **while preserving the facts the user supplied**. It now presents trial-code access plus rewrite packs: Trial 3 · Quick NZ$2.50/10 · Value NZ$6.90/30 · Pro/API NZ$19.90/90 monthly.

**The pivot:** reposition from a generic "AI rewrite" tool to a **reply-decision assistant — the last step before you send a message that matters.** Students are the low-cost **growth wedge**; Pro/API is the **profit pool**. Explicitly **not** an "AI humanizer / bypass-detector / undetectable" product — which also matches the CI guardrail banning `humanizer|bypass|undetect|detector|evade`.

**Four product pillars (owner):** ① scenario-driven · ② fact-credible · ③ copy = value · ④ low-anxiety conversion.

---

## 2. Goals

1. Reposition the surface around real, high-anxiety reply moments.
2. Make the product a **reply *decision* helper**, not just a sentence polisher — reduce *communication risk*, not just AI-likeness.
3. Restructure pricing into a 5-SKU model that protects margin and opens student-friendly entry points.
4. Move conversion to the **moment of value** (after a successful rewrite is **copied**).
5. Stand up **Pro/API as a true second product** (developer workflow).
6. Build three growth loops (student video, referral, SEO templates).
7. Instrument the funnel for unit-economics management.

---

## 3. Non-Goals

- ❌ No "unlimited" plan. ❌ No detector-pass/"undetectable" promises (keep "tone is a reference, not a guarantee").
- ❌ No permanent student discount tier (use Exam Pass + referral + first-month bonus).
- ❌ **No essay/assignment generation.** `/students` states: *"Built for real messages, not writing assignments for you."* Reinforced by the **before-you-send checklist** (§8.4) which nudges the user to own the content.
- ❌ No annual plan this phase.
- ❌ **P2 deferrals:** Chrome extension, Gmail integration, team workspace, custom voice profile, mobile app, advanced tone studio.

---

## 4. Personas

| Persona | Trigger | Motive | Entry |
|---|---|---|---|
| **Student** (wedge) | Lecturer email, extension, internship follow-up, group-project | Fear of sounding rude / too AI / too stiff | `/students` |
| **Young professional** | Boss, colleague, client, HR replies | Look clear, professional, natural | `/` |
| **Teacher / sales / service** | High-frequency replies | Save time, keep tone | `/` |
| **Developer / automation** | Wire rewrite into MCP / Claude Code / Cursor / internal tools | Automation, volume, pays for API | `/developers` |

---

## 5. Core Use Cases & Product Principles

**6 reply situations:** extension request · lecturer email · internship follow-up · group-project contribution · client delay · "make this less rude." (**FACT**: `/students` already ships ~these.)

**Principles:**
1. **Reply-decision over rewrite** — output tells the user *why* this works and *what to check*, not just a nicer sentence.
2. **Fact preservation is the moat — and it must be visible** (§8.4 "Facts preserved").
3. **Tone is a reference, never a promise.**
4. **Value before paywall** — never block the first successful rewrite; the **copy** is the value event.
5. **Scenario over blank box.**
6. **Cost discipline** — cap input size front+back; never uncap cost.

---

## 6. Pricing & Packaging

### 6.1 SKUs (live packs model)

| SKU | Price | Allowance | API | Positioning |
|---|---|---|---|---|
| **Trial code access** | NZ$0 | **3 rewrites** | ✗ | Try one real message |
| **Quick Pack** | **NZ$2.50** | **10 rewrites, valid 90 days** | ✗ | Lowest-cost paid pack |
| **Value Pack** | **NZ$6.90** | **30 rewrites, valid 90 days** | ✗ | Most popular pack |
| **Pro/API** | **NZ$19.90/mo** | **90 rewrites/mo** | ✓ | Heavy users & developer workflows |

Margin rationale (owner): Stripe NZ ≈ 2.65%+NZ$0.30 local / 3.5%+NZ$0.30 intl; GST at NZ$60k turnover; worst-case ≈ NZ$0.10/rewrite. Pack sizes keep rewrite costs bounded while preserving a low entry price.

### 6.2 Entitlements

| Capability | Free | Starter | Pro/API | Exam Pass | Top-up |
|---|---|---|---|---|---|
| Web workspace + Tone check + scenarios | ✓ | ✓ | ✓ | ✓ | ✓ |
| Facts-preserved view | ✓ | ✓ | ✓ | ✓ | ✓ |
| API keys + MCP + REST | — | — | ✓ | — | — |
| Usage dashboard | basic meter | meter | full + API usage | meter | — |

**Rules:** API = **Pro only**; **web + API share one quota pool** (DECISION-aligned; M8 per-key `monthlyQuota` becomes a rate-safety cap, not the budget); overage = Top-up only; no metered surprise billing.

---

## 7. Pricing Page (split layout — owner §14)

**Monthly plans**

| Plan | Best for |
|---|---|
| Starter | Regular personal replies |
| Pro/API | Heavy users & developer workflows |

**One-time options**

| Option | Best for |
|---|---|
| Exam Week Pass | Deadline week, no subscription |
| Top-up | A few extra rewrites |

Recommendation hints: *"Most students start with Exam Week Pass or Starter" · "Developers choose Pro/API" · Top-up appears at `quota_exhausted`, not on the main page.*

---

## 8. Core Funnel & Workspace UX

```
Scenario pick → (micro-flow) → progressive input → rewrite → output → COPY → situational nudge → quota meter
                                                                       └── conversion moment
```

### 8.1 Light onboarding (task-first; owner §13)
First `/app` visit: one line — *"Pick a real message you need to send. We'll help you make it clearer while keeping your facts unchanged."* + 3 task buttons (*Ask for more time · Follow up politely · Make this less rude*). Task before features. No full tutorial.

### 8.2 Step 1 — Scenario-first entry + micro-flows
6 buttons (§5) + "Something else." Selecting a scenario opens an optional **guided micro-flow** (P1) of 3–5 light prompts, e.g.:
- *Ask for extension:* original deadline? desired new date? partial progress? reason? tone (formal/sincere/brief)?
- *Make this less rude:* what's the real frustration? desired action from them? keep relationship friendly? need to be firmer?

Micro-flow answers seed the input + facts; reduces blank-box pressure, raises output quality, strengthens fact differentiation.

### 8.3 Step 2 — Progressive input (owner §3)
Default **2 fields**:
1. **What message are you replying to?** → `RewriteLearningSample.messageToReplyTo` (**FACT exists**)
2. **What do you want to say?** → `roughDraftReply` (**FACT exists**)

Collapsed advanced (one tap "**Add facts that must stay true**"):
3. **Facts that must stay true** → `fact-ledger` (**FACT exists**). Hint: *"Optional: dates, names, deadlines, promises the AI must not change."*

Per-field + combined char caps enforced **front and back** (§16).

### 8.4 Step 3 — Output (reply-decision layer)
**Primary (always visible):**
- **Ready to send** — the rewrite + prominent **Copy**.
- **Facts preserved** — explicit list pulled from the fact-ledger, e.g. *Deadline: Fri 5pm · Reason: family emergency · Request: extension to Mon · No new promises added.* If a supplied fact may have been altered → **Potential issue:** *"The rewrite softened the reason — check it still feels accurate."* (This is the visible moat — owner §4.)

**Secondary (compact / expandable, to avoid first-screen overload):**
- **Why this works** — benefit bullets (warmer · removed over-apology · kept key facts · clear request · lower conflict). (Absorbs old "what changed.")
- **Tone check** — multi-dimension, *reference not guarantee*: *Tone: polite & clear · Warmth: medium · Directness: high · Risk: may sound slightly formal.* (User-facing rename of "Naturalness Check"; internal `writing-signal` unchanged; removes any detector framing — owner §12.)
- **Before you send** — short checklist: *check the deadline is correct · make sure the reason is true · edit anything too formal.* (Trust + brand-protective; owner §15.)

### 8.5 Step 4 — Situational post-copy paywall (owner §5)
Fires after **copy** of a successful rewrite; non-blocking; copy varies by scenario:
- *Student:* "Nice — this one's ready to send. 2 free rewrites left. Starter gives you 55/month for lecturer emails, extension requests, and internship follow-ups."
- *Work:* "Nice — a cleaner reply. Starter helps you handle client, manager, and colleague messages without overthinking every reply."
- *Pro intent:* "Need this inside your workflow? Pro includes API access and shared web/API quota."

Sells *reduced anxiety / saved time*, not rewrite count.

### 8.6 Quota meter (per-source; owner §6, §11)
Collapsed total: `21 rewrites left`. Expanded detail:
- Monthly plan: 18 left
- Exam Pass: 3 left · expires in 2 days
- Top-up: 10 left

Free users see value-anchor: *"3 free rewrites — best used on real messages you actually need to send, not test text."*

### 8.7 User/quota state model
```
anonymous ─signup→ free(3 lifetime)
free ─quota>0→ can_rewrite ─success→ output ─copy→ situational nudge
free ─quota=0→ blocked_soft (Starter / Exam Pass; Top-up shown here)
any ─checkout(sub)→ planTier set by Stripe price
any ─checkout(one-time)→ +25 (exam, 7d) / +10 (top-up) credits
starter/pro ─period rollover→ allowance reset; credits untouched
pro ─→ can create API keys
```
Consumption on a **quality-passed** rewrite: plan allowance → soonest-expiring credit → next.

---

## 9. `/students` Wedge Page

- **H1 (PROPOSED):** "Write the message you're nervous to send." (current: "Sound like yourself when the message matters.")
- 6 before/after situations (mostly present).
- CTA: **"Try 3 free rewrites — no card."** + value-anchor framing.
- **Boundary line (required):** "Built for real messages, not writing assignments for you."
- Dedicated **Exam Week Pass** block: *7 days · 25 rewrites · no subscription · built for deadline weeks* (reduces subscription resistance — owner §7).

---

## 10. `/developers` + Pro/API (workflow product — owner §8)

- Headline: *"Bring reply rewriting into your workflow."* (not "Get 110 rewrites/month").
- Surface: API keys · usage logs · shared quota · rate limits · top-ups · developer docs · MCP examples · copy-paste integration snippets.
- **3 example use cases:** ① rewrite a customer-support reply · ② improve an email draft from Claude Code / Cursor · ③ batch-improve internal response templates.
- **API-key dashboard (P0 for Pro):** create / reveal-once / revoke / last-used / current-period usage. (**FACT**: `ApiKey`/`ApiKeyUsage` schema exists.)
- Rules: keys = Pro only; web+API shared quota; overage = top-up; no unlimited; request length capped front+back.

---

## 11. Growth Loops

- **Loop A — student video** (marketing, no code).
- **Loop B — referral (gated + anti-abuse):** not on first screen; trigger after the user has **copied ≥1 result** OR free remaining ≤1 OR first `quota_exhausted`. Copy: *"Help a friend send a better message. When they complete their first rewrite, you both get 3 extra rewrites."* **Reward only after the referee completes their first rewrite** (not on signup). Rules: +3 each, **+15/person/month cap**, no self-referral, device/IP/payment-method anomaly check, **revocable credits**.
- **Loop C — SEO `/templates/*` (~10), semi-productized:** each page = scenario blurb → one before/after → copyable template → **"Make this fit my situation"** mini input → result → CTA (signup/copy/upgrade). Goal: convert "this template isn't quite mine" into a product entry, not just SEO content.
- **Share card:** ⚠️ **must not use `next/og`** — `@vercel/og` WASM fails on Cloudflare/OpenNext (**FACT**: M4-009). Use client canvas / pre-rendered template.

---

## 12. Analytics & Metrics

- **North Star:** **copied rewrites per active user per week** (copy ⇒ reply likely actually sent — pillar ③).
- Activation / Conversion / Retention / Unit-economics per v0.1.
- **Events:** `landing_view, signup, scenario_selected, microflow_completed, rewrite_started, rewrite_succeeded, rewrite_failed, output_copied, fact_issue_flagged, quota_exhausted, upgrade_clicked, checkout_started, checkout_succeeded, exam_pass_purchased, topup_purchased, referral_shared, referral_converted, api_key_created, api_request`. No PII.
- **BLOCKER (FACT):** `POSTHOG_API_KEY` pending — build instrumentation provider-agnostic now.

---

## 13. Current System (FACTS to build on / change)

Unchanged from v0.1 — key points: live = Next.js/Worker/Neon (**billing lands here — DECISION**); `lib/quota.ts` binary; `RewriteUsage{periodKey,count}`; `lib/stripe.ts` single `STRIPE_PRICE_ID` subscription-only; `ApiKey`/`ApiKeyUsage` exist; `messageToReplyTo`+`roughDraftReply` exist; `fact-extraction`+`fact-ledger` exist; `StripeEvent` idempotency exists; landing uses `PricingV2` (`pricing.tsx` dead); `.NET` reservation model exists but **not** the launch target; `next/og` WASM broken on CF.

---

## 14. Proposed Architecture & Data Model (Next.js / Neon — DECISION)

### 14.1 New / changed Prisma models
```prisma
model User {
  // ...existing...
  planTier         String   @default("free")  // 'free'|'starter'|'pro' — set by webhook from price map
  referralCode     String?  @unique
  referredByUserId String?
  credits          RewriteCredit[]
  referralsMade    Referral[] @relation("referrer")
}

model RewriteCredit {                          // NEW — non-periodic grants
  id             String   @id @default(cuid())
  userId         String
  source         String                        // 'exam_pass'|'top_up'|'referral'|'first_month_bonus'|'manual'
  amountGranted  Int
  amountConsumed Int      @default(0)
  grantedAt      DateTime @default(now())
  expiresAt      DateTime?                      // exam_pass=+7d; top_up/referral=null|long
  stripeEventId  String?                        // idempotency for purchase grants
  user           User     @relation(fields: [userId], references: [id], onDelete: Cascade)
  @@index([userId, expiresAt]); @@index([stripeEventId])
}

model Referral {                                // NEW — attribution + monthly cap + abuse audit
  id            String   @id @default(cuid())
  referrerId    String
  refereeId     String   @unique
  status        String   @default("pending")    // 'pending'|'credited'|'revoked'
  creditedAt    DateTime?
  signupIpHash  String?                          // anomaly check (hashed, no raw PII)
  referrer      User     @relation("referrer", fields: [referrerId], references: [id], onDelete: Cascade)
  @@index([referrerId, creditedAt])
}
```
- Keep `RewriteUsage` as periodic allowance (monthly for Starter/Pro; `periodKey="lifetime"` for Free's 3 — **ASSUMPTION**).
- **`RewriteReservation` (ASSUMPTION, recommended):** TTL'd reserve→finalize→release to stop concurrent over-spend (mirrors `.NET` `ExpiredReservationCleanupService`).

### 14.2 Effective quota
```
remaining = max(0, planAllowance(tier) - currentPeriodUsage) + Σ(credit.granted - credit.consumed) for unexpired credits
canUseApi = (tier == 'pro')
```

### 14.3 Stripe shape (live authorized; confirm numbers at creation)
| SKU | Object | Mode | Env |
|---|---|---|---|
| Starter | Price recurring | subscription | `STRIPE_PRICE_STARTER` |
| Pro | Price recurring | subscription | `STRIPE_PRICE_PRO` |
| Exam Pass | Price one-time | payment | `STRIPE_PRICE_EXAM_PASS` |
| Top-up | Price one-time | payment | `STRIPE_PRICE_TOPUP` |

Tier from `priceId→tier` map; checkout supports both modes; retire `STRIPE_PRICE_ID` from new checkout (no real users to migrate).

---

## 15. API & Job Contracts
- **REST (Pro):** `POST /api/v1/rewrite` — API-key (hashed) auth, idempotency-key header, size limit, decrements **shared** quota, logs `ApiKeyUsage`+`RewriteCostLog`, per-key rate limit.
- **MCP:** `rewrite_email`, `analyze_signal`, `list_scenarios` (**FACT**) → same core.
- **Webhook (extend `/api/stripe/webhook`):** `customer.subscription.*`→ set `planTier`+status from price; `checkout.session.completed`(payment)→ grant credits **idempotently** (event-id keyed); `invoice.paid`(first)→ first-month bonus once.
- **Job:** reservation-expiry cleanup (cron or lazy-on-read).

---

## 16. State / Error / Non-Functional
- **Quota race:** reserve→finalize→release; only **quality-passed** rewrites consume; all-rejected ⇒ safe failure, **no consumption**.
- **Webhook idempotency:** `StripeEvent` + `stripeEventId` on credits (replay-safe).
- **Referral abuse:** no self-referral; +15/mo cap; reward post-first-rewrite only; device/IP/payment anomaly → hold/revoke.
- **Pre-send checklist** present on every output (brand-protective, anti-"do my homework").
- **Cost ceiling:** per-field + combined char caps front+back; worst-case ≈ NZ$0.10/rewrite (validate via `RewriteCostLog`).
- **Scale-to-zero:** Cloudflare Worker (FACT).
- **API-key security:** hashed (`keyHash`), shown once, revocable, rate-limited, logged.
- **Banned terms** absent from copy AND code/`/templates/*`; grep guard each phase. **Secrets** never printed; runtime env validation.

---

## 17. Rollout Plan

- **Phase 0 — Copy-only repositioning (no Stripe/schema; ship now):** new positioning across surfaces; delete dead `pricing.tsx`; `/students` H1 + boundary; "Naturalness"→"Tone check" label; free-quota value-anchor copy. *(Pricing copy: see Open Decision #19.8.)*
- **Phase 1 — Revenue core:** credit-ledger + tier + reservation; Stripe products/prices (confirm numbers); checkout/webhook (sub + one-time + bonus); split `/pricing`; situational post-copy paywall + per-source quota meter.
- **Phase 2 — Workspace funnel & decision layer:** scenario-first + micro-flows; progressive 2+1 input; output redesign (Ready-to-send + Facts-preserved primary; Why/Tone/Before-you-send secondary); task-first onboarding.
- **Phase 3 — Growth:** referral (gated + anti-abuse); semi-productized `/templates/*`; share cards; analytics events (provider when key lands).

### 17.1 Owner UX-refinement priority (overlay)
- **P0 (do first):** scenario-first default · simplify to 2+1 input · **Facts preserved** in output · situational paywall copy · free-quota value-anchor. → mostly Phase 2 + copy bits in Phase 0/1.
- **P1:** scenario micro-flows · Exam Week Pass packaging · template mini-widget · per-source quota meter · Pro/API productized page. → Phase 1/2/3.
- **P2:** multi-dimension tone explanation · before-you-send checklist depth · referral risk/revocation · reply-style memory · scenario favorites. → Phase 3+.

Skills per phase: `data-module-review` + `state-machine-modeling` (P1 schema/lifecycle), `resilience-test-generation` (quota race + webhook replay), `ui-browser-testing` (P0/P2 verify), `cloud-architecture-cost-review` (margin).

---

## 18. Verification Plan
- **Unit:** quota math (allowance+credits+expiry+order); tier-from-price; webhook idempotency/replay; first-month-bonus once; referral +15 cap & post-first-rewrite gating; **facts-preserved extraction + fact-issue flag**; char-cap; tone-check output shape.
- **Resilience:** concurrent requests don't over-spend; webhook replay grants once; provider failure after reserve releases quota.
- **E2E:** signup → free rewrite → copy → situational nudge → exhaust → checkout (test) → tier reflected; Exam Pass/Top-up grant credits; API key create→call→usage logged; referral reward only after referee's first rewrite.
- **Guards:** banned-term grep clean; cost-cap; no secret leak.
- **Visual:** desktop/mobile for `/`, `/students`, `/pricing`, `/developers`, `/app`.

---

## 19. Open Decisions / Assumptions
1. ✅ **DECISION — billing backend = Next.js.**
2. ✅ **DECISION — Stripe live changes authorized.** (Re-confirm exact numbers/quotas/bonuses at Price-creation.)
3. **MVP slice:** Phase 0 + Phase 1 first, Phase 2 funnel second? (My recommendation.) Or pull scenario-first funnel into the first cut?
4. **Exam Pass & Top-up = one-time payments** (owner §7 implies yes; treat as CONFIRMED unless told otherwise).
5. **Shared web+API quota pool** (assumption; reconciles M8 per-key quota → rate cap).
6. **Free = "3 lifetime"** mechanics (assumption: never-reset period key).
7. **Top-up / referral credit expiry** (assumption: none/long; Exam Pass = 7d).
8. ✅ **DECISION — pricing copy:** use trial-code access plus Quick, Value, and Pro/API rewrite packs.

---

## 20. Launch Gates
Banned-term grep clean · full suite + e2e green · cost-cap verified · live Stripe prices created+mapped · webhook (sub+one-time+bonus) verified via replayed test events · one owner real-transaction test before public launch · quota correctness under concurrency · analytics firing (provider optional) · key pages verified desktop+mobile.

---

## 21. Out of Scope (P2)
Chrome extension · Gmail integration · team workspace · custom voice profile · mobile app · advanced tone studio · annual billing.
