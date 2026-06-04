# Commercialization Roadmap — Reply In My Voice

Date: 2026-05-21
End state: `replyinmyvoice.com` serves the live app, accepts rewrite-pack and Pro/API payments, quality gate enforced, owner rewrite-quality analysis working, API keys available, and MCP distribution verified.
Commercial north star: `docs/commercialization-north-star.md`.
See `plans/commercialization-recon.md` for current state snapshot.

## Milestone summary

Updated 2026-05-21 after expansion to Version B (Consumer + API + MCP simultaneous launch).

| ID | Name | Goal | Issues |
|---|---|---|---|
| M0 | Stabilize working tree | Clean baseline before new work | 6 |
| M1 | Entra migration completion | Replace Clerk fully | 14 |
| M2 | Rewrite quality gate | Stop worse-than-draft rewrites | 9 |
| **M2.5** | **Learning loop** | **Self-improving quality via LearningOps V1** | **10** |
| M3 | Workspace V2 + char limits | Ship the simplified UX | 8 |
| M4 | Landing & legal polish | Production-grade marketing site | 10 |
| M5 | Cost telemetry + rewrite analysis | Owner visibility into quality, failures, cost, and strategy versions | 6 |
| M6 | Production verification | Confirm everything live | 8 |
| M7 | Launch day & growth | Smoke, analytics, support | 8 |
| **M8** | **B2B API + Keys** | **Developer API with tiered subscriptions** | **16** |
| **M9** | **MCP + Skill distribution** | **LLM-tool integrations** | **10** |
| **Total** | | | **~105** |

Three new milestones (M2.5 / M8 / M9) added 2026-05-21 per user direction to launch consumer site + B2B API + MCP/Skill simultaneously (Version B sequencing).

Each issue ≤30 min of codex work, single PR-sized scope. Codex briefs follow the six-section template (TASK / CONTEXT / CONSTRAINTS / CHANGES REQUIRED / ACCEPTANCE / DO NOT).

---

## M0 — Stabilize working tree (MUST be first)

**Why first**: 52 modified files uncommitted. If codex starts new work on this tree, every diff review becomes "is this from my brief or pre-existing WIP?" — unworkable.

### Issues
- `M0-001` Audit the 52 dirty files: produce `plans/wip-triage.md` listing each file → keep/discard/stash decision
- `M0-002` Commit the keep-set in 2-4 themed commits (auth migration WIP / rewrite gate WIP / docs / tests)
- `M0-003` Discard or stash the rest
- `M0-004` `npm run lint && npm run typecheck && npm run test` must pass; fix or ticket what doesn't
- `M0-005` Push to main, confirm CI green
- `M0-006` Update AGENTS.md: Stripe is live as of 2026-05-21; sandbox is local-only

**Exit criteria**: `git status` clean, CI green on main, AGENTS.md reflects live mode.

---

## M1 — Entra migration completion

**Why**: Auth is half-migrated; can't ship customer signups while two systems coexist. This is the single biggest blocker.

### Issues
- `M1-001` Inventory Clerk usage: grep every `@clerk/`, `useUser`, `auth()`, `clerkMiddleware` reference → `plans/clerk-removal-map.md`
- `M1-002` Server: replace `clerkMiddleware` in `middleware.ts` with Entra session check
- `M1-003` Server: replace Clerk session helpers in API routes (`app/api/*/route.ts`) with `lib/entra-auth.ts`
- `M1-004` Client: rewrite `app/sign-in/[[...sign-in]]/page.tsx` with Entra MSAL redirect flow
- `M1-005` Client: rewrite `app/sign-up/[[...sign-up]]/page.tsx` with Entra sign-up flow
- `M1-006` Client: `app/auth/callback/route.ts` handles Entra code-exchange
- `M1-007` DB: add `entra_user_id` to `User` Prisma model; migration; backfill plan documented
- `M1-008` Webhook adapter: `lib/stripe-events.ts` maps `userId` claim → DB user
- `M1-009` Tests: `tests/unit/entra-auth.test.ts` covers token validation, expired tokens, wrong audience
- `M1-010` E2E: Playwright test for sign-in → /app → rewrite → sign-out
- `M1-011` Remove `@clerk/nextjs` from `package.json` once green
- `M1-012` Strip `CLERK_*` env vars from `.env.example`, `lib/env.ts`, Cloudflare Worker secrets
- `M1-013` Remove Clerk DNS records (`clerk.replyinmyvoice.com` etc) from Cloudflare — documented manual step
- `M1-014` Update `docs/manual-setup.md` to reflect Entra-only setup

**Exit criteria**: zero Clerk references in source; e2e sign-in flow green; production Worker deployed with Entra; AGENTS.md updated.

---

## M2 — Rewrite quality gate

**Why**: Per `docs/next-development-brief.md` "Next Rewrite Quality Fix", customer-support drafts went 89%→99% AI-like. Cannot ship to paying customers in this state.

### Issues
- `M2-001` Implement hard reject rules in `lib/rewrite-quality-gate.ts`: `rewriteSignal >= draftSignal` OR (`rewriteSignal > 50` AND `reduction < 30`) → reject candidate
- `M2-002` Wire quality gate into `lib/rewrite-pipeline/pipeline.ts` selection step
- `M2-003` On all-candidates-rejected: return safe failure response; **do not charge usage** (quota service)
- `M2-004` Targeted repair pass receives diagnosis tags + scenario guardrails + facts
- `M2-005` `tests/unit/rewrite-quality-gate.test.ts` covers each rejection rule
- `M2-006` Add Priya billing/proration regression case to `tests/unit/rewrite-email-eval-cases.test.ts`
- `M2-007` Build 25-case evaluation harness in `scripts/eval-scenarios.ts` (10 long, 5 customer-support)
- `M2-008` Run harness; record in `docs/scenario-evaluation-results.md`
- `M2-009` Gate deploy: ≥30 avg signal reduction, ≥70% below 50%, no rewrite-worse-than-draft

**Exit criteria**: documented eval log, deploy unblocked, quality regression test in CI.

---

## M3 — Workspace V2 + character limits

**Why**: Current workspace UX still has the heavy Quick context panel. Brief mandates 5 scenarios + 4 tones + draft-only + 5000-char cap.

### Issues
- `M3-001` Add 5 scenarios to `lib/rewrite-presets.ts`: Blank / Email / Customer support / Cover letter / Work update
- `M3-002` Reduce visible tones to 4: Warm / Professional / Friendly / Concise
- `M3-003` Add scenario-specific prompt guardrails in `lib/openai.ts` or new `lib/rewrite-scenarios.ts`
- `M3-004` Rewrite `components/app/rewrite-workspace.tsx`: scenario chips → optional context → required draft → tone → submit → output → naturalness → recent (collapsed)
- `M3-005` Validation in `lib/validation.ts`: per-field 3000 max, combined 5000 max, draft min 10
- `M3-006` Helper copy under combined counter: "For long threads, paste only the part you need to answer and the facts that matter."
- `M3-007` Add `scenario` to API request schema + propagate to prompt
- `M3-008` Tests: `tests/unit/rewrite-presets.test.ts`, `tests/unit/validation.test.ts`, `tests/unit/workspace-copy.test.ts`

**Exit criteria**: Visible UI matches brief; backend enforces 5000-char cap; tests green.

---

## M4 — Landing & legal polish

**Why**: Site is going LIVE with real money. Marketing copy and legal pages must be production-grade.

### Issues
- `M4-001` Run rewrite engine against 4 documented samples (teacher / sales / workplace / client); record measured signals in `docs/sample-cases.md`
- `M4-002` Replace `components/landing/interactive-demo.tsx` samples with measured ones; **no invented names** (Maya/Jordan removed)
- `M4-003` Rewrite `components/landing/how-it-works.tsx` to match brief's 4 simpler steps
- `M4-004` Convert `components/landing/faq.tsx` to single-column accordion
- `M4-005` Pricing page: Trial 3 · Quick NZ$2.50/10 · Value NZ$6.90/30 · Pro/API NZ$19.90/90 monthly; NZ-currency display; no annual
- `M4-006` Footer: "Operated by TimeAwake Ltd. · info@timeawake.co.nz · Privacy · Terms"
- `M4-007` Update `app/privacy/page.tsx`: explain stored content (drafts, rewrites, signals, metadata) for quality improvement
- `M4-008` Update `app/terms/page.tsx`: refund / dispute policy for live rewrite-pack and Pro/API purchases
- `M4-009` Add OG image + meta description per route
- `M4-010` Add `app/sitemap.ts` + `app/robots.ts`

**Exit criteria**: lighthouse SEO ≥90; no fabricated facts in samples; Privacy/Terms reflect live storage truth.

---

## M5 — Cost telemetry + Rewrite Quality Analysis

**Why**: Without per-request quality and cost analysis, the owner cannot tell whether the rewrite system is actually improving, whether quality gates are protecting users, which failure modes matter, or whether the live packs model can run profitably.

First version: offline report, not admin UI. Source of truth: `docs/rewrite-quality-analysis-spec.md`.

### Issues
- `M5-001` Prisma migration: `RewriteCostLog` table per `docs/next-development-brief.md` schema
- `M5-002` Capture telemetry across pipeline: LLM tokens, Sapling calls/chars, estimator, `RewriteCostLog`, and `RewriteProviderCall`
- `M5-003` `scripts/analyze_rewrite_quality.py`: read-only SQL → Pandas aggregation → Markdown + CSV + PNG charts
- `M5-004` Failure reason normalization: ensure `RewriteCostLog.errorCode` carries primary quality/provider/server reason
- `M5-005` Fixture-backed report tests: success, quality failure, server failure, unavailable signal, worse-than-draft, escalation, and multiple strategies
- `M5-006` Owner runbook: how to generate, read, and avoid publishing sensitive report artifacts

**Exit criteria**: Every rewrite logs cost and signal metrics; the owner can generate `docs/rewrite-quality-analysis-report.md`, `exports/rewrite-quality-summary.csv`, and six charts without raw user text; report shows success rate, quality failures, failure reasons, average signal drop, no-regression rate, cost per success, p50/p95 duration, escalation rate, and strategy-version comparison.

---

## M6 — Production verification

**Why**: Live secrets exist locally, but deployed Worker may still run with sandbox values.

### Issues
- `M6-001` `wrangler secret list` against `replyinmyvoice-app` Worker — diff vs `.env.local` live values; document in `plans/worker-secret-diff.md`
- `M6-002` Push any missing live secrets via `wrangler secret put` (codex does this with user's wrangler auth)
- `M6-003` Smoke test all routes on `https://replyinmyvoice-app.qc1128qc.workers.dev` (`/`, `/pricing`, `/sign-in`, `/app` redirect, `/api/rewrite` 401, `/api/stripe/webhook` 200, `/api/health/db` 200)
- `M6-004` Confirm `replyinmyvoice.com` is attached to the Worker (Cloudflare custom domain). If still on Pages holding page, document attach steps.
- `M6-005` Smoke test all routes on `https://replyinmyvoice.com`
- `M6-006` Banned-term scan: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` must return empty
- `M6-007` Full suite green: `npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build && npm run cf:build`
- `M6-008` Verify Stripe live webhook endpoint reachable: Stripe dashboard → send test event → confirm 200 from `/api/stripe/webhook`

**Exit criteria**: Live domain serves app; all smokes green; CI green; banned terms clean.

---

## M7 — Launch day & growth

**Why**: Going live with real money requires observability + a real-customer smoke + a path to growth.

### Issues
- `M7-001` Real-account live test: register → redeem trial code → exhaust trial rewrites → paywall → live checkout for the current lowest-cost paid option → confirm matching rewrite balance → refund test charge → document in `docs/launch-day-report.md`
- `M7-002` Add PostHog (or GA4) — minimal: page views, signup, rewrite_completed, paywall_hit, checkout_started, subscription_active. No PII in event props.
- `M7-003` Add Sentry (or Cloudflare logpush) for error monitoring
- `M7-004` Customer support: confirm `info@timeawake.co.nz` is monitored; add to footer + Stripe receipts
- `M7-005` SEO baseline: submit sitemap to Google Search Console; verify domain in GSC
- `M7-006` Status page or simple uptime check (UptimeRobot free tier or Cloudflare-native)
- `M7-007` Backup plan: document rollback to holding page (DNS swap or Pages re-route)
- `M7-008` Post-launch dashboard: 24h / 7d KPI review in `/admin/overview`

**Exit criteria**: At least one real paid customer completes the full flow; analytics + errors flowing; rollback procedure tested.

---

## Cross-cutting constraints (always in every codex brief)

```
Banned terms (CI grep guard): humanizer, bypass, undetect, detector, evade
- in user-facing copy, AND in lib/**, AND in internal prompts/comments/filenames

Secrets policy:
- Never print, summarize, or commit values from .env.local, .dev.vars, globalapikey/
- Validate required env at runtime in the handler, not at module import

Scope cap:
- Single issue ≈ 30 min of work. If scope exceeds, STOP and report — do not silently expand.

Forbidden actions:
- No `git commit` / `git push` from codex unless the brief explicitly says so for this issue
- No `wrangler deploy` unless the brief says so
- No modification of LAUNCH_CONFIRMED (already true; do not touch)
- No modification of Stripe live mode (already true; do not touch)
```

---

## Sequencing & dependencies — Version B (Consumer + API + MCP simultaneous)

```
M0 (stabilize)
  │
  ├──> M1 (Entra)      ┐
  │                    │
  ├──> M2 (quality)    │ parallel after M0
  │                    │
  └──> M8 (API+keys)   ┘
       │
       ├──> M2.5 (learning loop)
       │
       ├──> M3 (workspace V2)
       │
       ├──> M4 (landing + legal)
       │
       ├──> M5 (telemetry — needed by both consumer + API tier)
       │
       └──> M9 (MCP + Skill — depends on M8 API existing)
            │
            └──> M6 (verify both products)
                 │
                 └──> M7 (launch both)
```

- **M0 strictly first**: 52-file dirty tree must be baselined.
- **M1 / M2 / M8 can run in parallel** after M0 — different file sets (auth vs rewrite pipeline vs new API surface). Codex sessions kept on separate branches.
- **M2.5 starts after M2** (need quality gate first), runs concurrent with M3/M4/M5/M9.
- **M3 / M4 / M5 can interleave** after M1+M2 land.
- **M9 depends on M8** (MCP server calls the API; API must exist first).
- **M6 strictly last before M7**: verification depends on everything merged.
- **M7 launches both products** — consumer site at `replyinmyvoice.com/app` + API at `replyinmyvoice.com/developers`.

### Critical-path estimate

Assuming 4-5 codex sessions/day, 30 min each, supervised:
- M0: 1 day
- M1 + M2 + M8 in parallel: ~3 weeks (M8 is the longest at 16 issues)
- M2.5 + M3 + M4 + M5 + M9: ~3 weeks
- M6: 2-3 days
- M7: 1-2 days

Total: **6-8 weeks to dual launch**. M8 is the schedule risk — if API tier proves too complex, fall back to Version A (consumer launch first, API later).

---

## Issue creation plan

1. User signs off on this roadmap
2. Claude writes each issue's full codex brief as `plans/issues/<id>.md` (e.g., `plans/issues/M0-001.md`)
3. Single codex delegation: "for every file under `plans/issues/`, run `gh issue create --milestone <M*> --label codex-task --body-file <file>` against `ChuanQiao1128/replyinmyvoice`; write resulting issue URLs back to `plans/issue-board.md`"
4. Claude maintains `plans/issue-board.md` as supervision state — claimed / in-progress / blocked / done
5. Supervision loop: read next open issue → plan → delegate codex → review diff → run tests → close or iterate
