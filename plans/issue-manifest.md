# Issue Manifest — M1 through M7

For M0 issues, see the detailed briefs in `plans/issues/M0-*.md`.
For M1+, this manifest is the source. Detailed briefs are written when each milestone starts (see `plans/issues/M*-*.md` once expanded).

Each entry below is the body of a GitHub issue. When `gh issue create` is called by codex, it should use the entry's `title:` as `--title` and the `body:` (the indented block) as `--body`. Milestone assignment per the heading.

---

## Milestone: M1-Entra

### M1-001
title: M1-001 Inventory all Clerk usage in repo
body:
> Produce `plans/clerk-removal-map.md` listing every file that imports `@clerk/*`, uses `auth()`, `useUser`, `clerkMiddleware`, or references Clerk env vars. Group by: middleware / API routes / client components / tests / config / docs. No code changes. Read-only.

### M1-002
title: M1-002 Replace clerkMiddleware in middleware.ts with Entra session check
body:
> Rewrite `middleware.ts` to use `lib/entra-auth.ts` for session validation. Preserve all current route protection (signed-out `/app` → redirect to `/sign-in`; unauthenticated `/api/rewrite` → 401). Update `tests/unit/entra-auth.test.ts` if needed. Open PR.

### M1-003
title: M1-003 Migrate API route auth from Clerk to Entra
body:
> For every file under `app/api/*/route.ts` that imports Clerk: replace with Entra token validation via `lib/entra-auth.ts`. Files to touch: `app/api/rewrite/route.ts`, `app/api/stripe/webhook/route.ts` (if Clerk used), any other route. Preserve current 401 contract. Add tests.

### M1-004
title: M1-004 Replace sign-in page with Entra MSAL redirect flow
body:
> Rewrite `app/sign-in/[[...sign-in]]/page.tsx` to initiate Entra External ID auth via redirect. Use `NEXT_PUBLIC_ENTRA_AUTHORITY`, `NEXT_PUBLIC_ENTRA_CLIENT_ID`, `NEXT_PUBLIC_ENTRA_API_SCOPE` from `lib/env.ts`. Preserve sign-up link, error handling, and analytics tagging.

### M1-005
title: M1-005 Replace sign-up page with Entra flow
body:
> Same as M1-004 for `app/sign-up/[[...sign-up]]/page.tsx`. Trigger Entra sign-up policy (`AZURE_EXTERNAL_ID_SIGN_IN_FLOW_NAME`). Preserve any Stripe-specific post-signup hooks.

### M1-006
title: M1-006 Implement /auth/callback for Entra code exchange
body:
> `app/auth/callback/route.ts` exchanges authorization code for tokens via Entra well-known endpoint. Validate `state`, set HTTP-only session cookie, redirect to `/app`. Errors → `/sign-in?error=<code>`. Cover with route test.

### M1-007
title: M1-007 Add entra_user_id to User Prisma model + migration
body:
> Update `prisma/schema.prisma` to add `entraUserId String? @unique` on `User`. Generate migration. Document backfill plan in `plans/clerk-to-entra-user-backfill.md` (zero-downtime: dual-write during migration, then switch primary lookup to entraUserId). Do NOT run destructive prisma commands.

### M1-008
title: M1-008 Update Stripe webhook handler to use entra user lookup
body:
> `lib/stripe-events.ts`: map Stripe `client_reference_id` or `customer.metadata.userId` to DB user via `entraUserId` first, then fall back to `clerkUserId` during migration window. Tests in `tests/unit/stripe-webhook-events.test.ts`.

### M1-009
title: M1-009 Add Entra token validation tests
body:
> `tests/unit/entra-auth.test.ts` covers: valid token → user; expired token → 401; wrong audience → 401; wrong issuer → 401; missing scope → 403; malformed JWT → 401. Mock Entra JWKS endpoint.

### M1-010
title: M1-010 Add Playwright e2e for Entra sign-in → /app → rewrite → sign-out
body:
> `tests/e2e/auth-rewrite-flow.spec.ts`. Use a test Entra user. Walk: landing → sign-in → consent (mocked or stubbed) → callback → /app → fill draft → submit → see output → sign-out → /app redirects to /sign-in. Document any test account env vars in `local-env.md`.

### M1-011
title: M1-011 Remove @clerk/nextjs and related deps from package.json
body:
> After M1-001 through M1-010 land and CI is green: remove `@clerk/nextjs` and any clerk-only deps. Run `npm install`. Run full validation suite. Open PR.

### M1-012
title: M1-012 Strip CLERK_* env references from code + .env.example + lib/env.ts
body:
> Remove every `CLERK_*` env var declaration and runtime reference. `.env.example` should no longer mention Clerk. Update `lib/env.ts` schema. Preserve historical context in a section of `docs/manual-setup.md` titled "Clerk removal — rollback notes".

### M1-013
title: M1-013 Document Clerk Cloudflare DNS record removal
body:
> The Clerk DNS records (`clerk.replyinmyvoice.com`, `accounts.replyinmyvoice.com`, `clkmail.*`, `clk._domainkey.*`, `clk2._domainkey.*`) become orphaned after Entra cutover. Codex CANNOT delete DNS in this issue. Write `plans/clerk-dns-cleanup.md` with the exact `wrangler` / Cloudflare API commands user runs manually post-verification.

### M1-014
title: M1-014 Update docs/manual-setup.md to Entra-only flow
body:
> Rewrite the Clerk section as "Removed — see plans/clerk-dns-cleanup.md". Promote the Entra External ID section to primary. Preserve all secret-handling rules. No `.env.local` writes.

---

## Milestone: M2-Quality

### M2-001
title: M2-001 Implement hard quality gate rules in rewrite pipeline
body:
> `lib/rewrite-quality-gate.ts`: reject candidate if `rewriteSignal >= draftSignal` OR (`rewriteSignal > 50` AND `(draftSignal - rewriteSignal) < 30`). Return structured rejection reason. Update `lib/rewrite-pipeline/pipeline.ts` selection to consult gate. Tests in `tests/unit/rewrite-quality-gate.test.ts`.

### M2-002
title: M2-002 Safe-failure response when all candidates rejected
body:
> When every candidate is rejected by the quality gate, API returns 200 with `{ status: "quality_failed", reason, draftSignal, attemptedCandidates }`. DO NOT charge usage. Update `lib/quota.ts` to skip increment on `quality_failed`. Tests: `tests/unit/rewrite-api-quality.test.ts`, `tests/unit/quota.test.ts`.

### M2-003
title: M2-003 Targeted repair pipeline receives diagnosis tags + facts
body:
> `lib/rewrite-pipeline/`: when first candidate is rejected, repair pass gets `{ draft, candidate, draftScore, candidateScore, diagnosisTags, failureReason, requiredFacts, scenarioGuardrails }`. The repair prompt must reference the specific failure pattern (stock_opening, corporate_polish, etc). Tests in `tests/unit/rewrite-targeted-repair.test.ts`.

### M2-004
title: M2-004 Add Priya billing/proration regression case
body:
> Add the customer-support draft (89% → 99% AI-like failure) as a fixture in `tests/unit/rewrite-email-eval-cases.test.ts`. Assert the new pipeline either successfully repairs it OR returns `quality_failed` — never returns the worse-than-draft text.

### M2-005
title: M2-005 Build 25-case evaluation harness
body:
> Extend `scripts/eval-scenarios.ts` to run 25 cases (10 long 300-900 words, 5 customer-support, distributed across 5 scenarios). For each case record: scenario, tone, word count, diagnosis tags, draft/candidate/repaired/final signal, score change, facts preserved, decision. Output: `docs/scenario-evaluation-results.md`.

### M2-006
title: M2-006 Run evaluation; gate deploy
body:
> Run `npm run eval:scenarios`. Verify: avg signal reduction ≥30; ≥70% below 50% final; zero rewrites worse than draft; Priya case passes. Append results to `docs/scenario-evaluation-results.md`. Add a CI check that reads the results file and fails if these thresholds regress.

### M2-007
title: M2-007 Document signal calibration in docs/optimization-notes.md
body:
> Record what prompt/strategy changes were made to achieve the M2 thresholds, which scenarios were hardest, and what residual risks remain. Cite specific eval cases.

### M2-008
title: M2-008 Add quality-gate UI for safe-failure state
body:
> When API returns `quality_failed`, `components/app/rewrite-workspace.tsx` shows: "We couldn't produce a confident rewrite. Try shortening or pasting only the parts that matter." Do NOT show the rejected candidate text. Do NOT decrement quota visually. Test in `tests/unit/workspace-copy.test.ts` or new test.

### M2-009
title: M2-009 Update Naturalness Check display for repaired candidates
body:
> When repair was used, show both first-candidate and final-candidate signals with labels. Preserve existing third-party disclaimer copy.

---

## Milestone: M3-V2

### M3-001
title: M3-001 Add 5 scenarios to lib/rewrite-presets.ts
body:
> Define `scenarioOptions: ScenarioOption[]` with exactly: Blank / Email / Customer support / Cover letter / Work update. Each has id, label, helper text. Tests in `tests/unit/rewrite-presets.test.ts`.

### M3-002
title: M3-002 Reduce visible tone presets to 4
body:
> `tonePresetOptions` becomes: Warm / Professional / Friendly / Concise. Preserve `tonePresetToTone` compatibility mapping. Update tests.

### M3-003
title: M3-003 Add scenario-specific prompt guardrails
body:
> New file `lib/rewrite-scenarios.ts` exports `getScenarioGuardrails(scenario): { rewrites, preserves, avoids }` per the spec in `docs/next-development-brief.md` "Scenario-Specific Backend Prompt Guardrails" section. Wire into `lib/openai.ts` rewrite prompt assembly.

### M3-004
title: M3-004 Rewrite components/app/rewrite-workspace.tsx (V2 layout)
body:
> One-page vertical: scenario chips → optional context → required draft → tone buttons → submit → output → naturalness → recent (collapsed). Remove Quick context panel. Draft-only requests must work. Preserve `Try again` button.

### M3-005
title: M3-005 Enforce 5000-char combined cap in lib/validation.ts
body:
> Per-field max: messageToReplyTo 3000, roughDraftReply 3000 (min 10). Combined cap: 5000 across all submitted fields including legacy. Update zod schema. Tests in `tests/unit/validation.test.ts`.

### M3-006
title: M3-006 Add character helper copy + counter
body:
> Visible counter `/5000` near submit button. Helper text: "For long threads, paste only the part you need to answer and the facts that matter." Position below combined counter. Test in `tests/unit/workspace-copy.test.ts`.

### M3-007
title: M3-007 Add scenario to API request schema
body:
> `scenario: 'blank' | 'email' | 'customer_support' | 'cover_letter' | 'work_update'` required. Propagate to prompt assembly. Backward-compat: API accepts requests without `scenario` and defaults to 'blank' for one release. Tests.

### M3-008
title: M3-008 Remove or hide legacy Quick context UI
body:
> Audience / Purpose / Must keep chips no longer rendered in `rewrite-workspace.tsx`. Legacy API fields remain accepted for one release but ignored if empty. Visual snapshot test or e2e to confirm removal.

---

## Milestone: M4-Landing

### M4-001
title: M4-001 Run rewrite engine against 4 documented sample cases
body:
> Per `docs/next-development-brief.md` "Sample Selection Process": teacher / sales / workplace / client. Use realistic 150-300 word inputs. Record measured signals + preserved-facts checklist in `docs/sample-cases.md`. NO fabricated names.

### M4-002
title: M4-002 Replace interactive-demo samples with measured ones
body:
> `components/landing/interactive-demo.tsx` uses fixtures from `docs/sample-cases.md`. Static AI-like signal values from measured runs (no live Sapling calls on homepage). Detail-consistency rules enforced.

### M4-003
title: M4-003 Rewrite how-it-works.tsx with simpler 4 steps
body:
> Per brief: Paste thread / Pick quick context / Choose tone preset / Review the signal. Do NOT say "Lock the facts." Visual snapshot if applicable.

### M4-004
title: M4-004 Convert FAQ to single-column accordion
body:
> `components/landing/faq.tsx` becomes max-w-3xl single column with dividers (or accordion). No two-column card grid. Mobile-friendly. Use existing color tokens.

### M4-005
title: M4-005 Pricing page polish
body:
> `app/pricing/page.tsx`: NZ$9/month, 40 rewrites, "Cancel anytime", link to ToS + Privacy. No annual plan. No Sapling Pro feature copy.

### M4-006
title: M4-006 Footer with TimeAwake Ltd + support email
body:
> Footer: "Operated by TimeAwake Ltd. · info@timeawake.co.nz · Privacy · Terms". Visible on every page. No tracking pixels.

### M4-007
title: M4-007 Privacy page reflects data storage truth
body:
> `app/privacy/page.tsx`: explain stored content (drafts, rewrites, Naturalness signals, rewrite metadata) for quality improvement; NOT exposed publicly or used in marketing. Plain-English MVP copy. Date the policy.

### M4-008
title: M4-008 Terms page for live NZ$9 charges
body:
> `app/terms/page.tsx`: subscription terms, refund policy, dispute process, governing law (NZ), TimeAwake Ltd as operator. Mention quota (40/month), no refund for partial month. Date it.

### M4-009
title: M4-009 Add OG image + per-route metadata
body:
> Create 1200x630 OG image. Set `metadata` export in `app/layout.tsx` defaults + per-route overrides (landing / pricing / sign-in). Title pattern: `Reply In My Voice — <page>`. Description grounded in the actual product.

### M4-010
title: M4-010 Add sitemap.ts and robots.ts
body:
> `app/sitemap.ts` lists landing / pricing / sign-in / sign-up / privacy / terms. `app/robots.ts` allows all + sitemap reference. `/admin*` MUST be disallowed. Test that build emits both routes correctly.

---

## Milestone: M5-Telemetry

### M5-001
title: M5-001 Cost telemetry DB schema (RewriteCostLog + RewriteProviderCall)
body:
> Single Prisma migration adding both tables per `docs/next-development-brief.md` "Admin Cost Observability" section. RewriteCostLog: id, userId, requestId, scenario, tonePreset, status, durationMs, draft/rewrite signals, internalStrategies/repairs/rejected counts, openAi tokens, sapling chars, totalEstimatedCostUsd, modelsUsedJson. RewriteProviderCall: id, costLogId, provider, role, tokens|chars, estimatedCostUsd, latencyMs, success, errorCode. Indexes on (userId, createdAt), (status, createdAt). Document rollback.

### M5-002
title: M5-002 Capture telemetry across pipeline (OpenAI tokens + Sapling chars + estimator + persist)
body:
> Three coupled changes in one issue (small enough to ship together): (a) `lib/openai.ts` returns `{ tokens: { prompt, completion }, model, role }` per call; (b) `lib/writing-signal.ts` returns `{ chars, callCount, latencyMs }` per Sapling call; (c) new `lib/observability/cost-estimator.ts` maps to USD using env-configured pricing; (d) `lib/rewrite-pipeline/pipeline.ts` writes `RewriteCostLog` + N×`RewriteProviderCall` in a transaction at end of every request (success, quality_failed, provider_failed). Failure to log MUST NOT fail the request. Tests: `tests/unit/rewrite-cost.test.ts`.

### M5-003
title: M5-003 /admin overview page with cost cards
body:
> `app/admin/page.tsx`: cards for today / 7d / 30d windows showing requests, success rate, avg signal drop, %below 50% final, avg cost per successful rewrite, P95 cost, escalation rate, top 5 expensive scenarios. Server-rendered from `RewriteCostLog`.

### M5-004
title: M5-004 /admin/rewrites table + detail page
body:
> Two pages, one issue. `app/admin/rewrites/page.tsx`: paginated table (date, user email, scenario, tone, status, draft/rewrite signal, change, strategies/repairs/rejected counts, escalation, cost, duration). Filters: date range, status, scenario. `app/admin/rewrites/[id]/page.tsx`: request metadata + per-`RewriteProviderCall` cost breakdown + diagnosis tags + rewrite plan + learning sample link. Raw text only if `ADMIN_ALLOW_RAW_REWRITE_TEXT=true`.

### M5-005
title: M5-005 Admin auth + nav gating + pricing decision panel
body:
> Three coupled small changes: (a) `lib/admin-auth.ts` checks signed-in user against `ADMIN_EMAILS` and `ADMIN_ENTRA_USER_IDS`; non-admin gets 404; (b) Admin entry icon in `/app` header renders only for admins, never on public pages; (c) `/admin/costs` panel: plan price / quota / Stripe fee est / avg variable cost / cost at 40/50/100 rewrites / gross margin estimate. Tests: `tests/unit/admin-auth.test.ts`.

### M5-006
title: M5-006 End-to-end test: rewrite → cost log + admin display
body:
> E2E test that does one rewrite as a test user, then signs in as admin (`ADMIN_EMAILS` test value) and verifies the new cost log row appears in `/admin/rewrites` and detail page renders with provider-call breakdown. Catches regression at the integration seam between pipeline → DB → admin UI.

---

## Milestone: M6-Verify

### M6-001
title: M6-001 Diff Cloudflare Worker prod secrets vs .env.local live
body:
> Codex: `wrangler secret list --name replyinmyvoice-app` and compare names (NOT values) to `.env.local`. Write `plans/worker-secret-diff.md` listing: present-in-both, missing-in-worker, missing-in-local. Do NOT print values.

### M6-002
title: M6-002 Push missing live secrets to Worker
body:
> Per the diff from M6-001: for each missing secret, `wrangler secret put <NAME> --name replyinmyvoice-app` reading the value from `.env.local`. Codex MUST NOT print the value. Update `plans/worker-secret-diff.md` with timestamps of pushes.

### M6-003
title: M6-003 Smoke test workers.dev preview
body:
> `curl` each route per `docs/launch-cutover-plan.md` Phase 3: `/`, `/pricing`, `/sign-in`, `/app` (307 when signed out), `POST /api/rewrite` (401 unauthenticated), `GET /api/stripe/webhook` (200), `GET /api/health/db` (200). Record in `docs/preflight-report.md`.

### M6-004
title: M6-004 Confirm replyinmyvoice.com → Worker custom domain attach
body:
> Codex: check Cloudflare custom domains API for `replyinmyvoice.com` attached to `replyinmyvoice-app` Worker. If attached, smoke test the formal domain. If not, write `plans/custom-domain-attach.md` with the exact API/dashboard steps for the user.

### M6-005
title: M6-005 Smoke test replyinmyvoice.com
body:
> Same routes as M6-003 on the formal domain. Plus: `GET /privacy`, `GET /terms`, `GET /robots.txt`, `GET /sitemap.xml`. All 200 (except `/app` which should 307 redirect). Record in `docs/preflight-report.md`.

### M6-006
title: M6-006 Banned-term scan clean on main
body:
> Run `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib`. Expected: empty. If anything matches, ticket each separately and resolve before close.

### M6-007
title: M6-007 Full validation suite green
body:
> `npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build && npm run cf:build`. Document in `plans/m6-validation-report.md` with timestamps.

### M6-008
title: M6-008 Verify Stripe live webhook delivery
body:
> Trigger a Stripe live test event (e.g., `customer.subscription.updated` with a fake customer or via Stripe CLI in live mode if available). Verify `/api/stripe/webhook` receives + 200s. Verify DB updated. Document in `plans/m6-validation-report.md`. Do NOT initiate a real test charge.

---

## Milestone: M7-Launch

### M7-001
title: M7-001 Real-account live test (with refund)
body:
> User-led. Register fresh account → exhaust 3 free rewrites → hit paywall → checkout with personal card → real NZ$9 charge → confirm `paid` quota = 40 → do 1 successful rewrite → refund the test charge from Stripe dashboard. Document timestamps + transaction IDs in `docs/launch-day-report.md`. Codex assists with verification scripts only.

### M7-002
title: M7-002 PostHog analytics minimal events
body:
> Add `posthog-js` (or pick lighter alternative). Events: `landing_view`, `signup_started`, `signup_completed`, `rewrite_started`, `rewrite_completed`, `paywall_hit`, `checkout_started`, `subscription_active`. No PII in event props. Document in `docs/analytics.md`.

### M7-003
title: M7-003 Sentry error monitoring
body:
> Add `@sentry/nextjs`. Wire client + server + edge. Capture only error level + above. Mask PII. Source maps uploaded on build. Document in `docs/observability.md`.

### M7-004
title: M7-004 Confirm support email pipeline
body:
> Verify `info@timeawake.co.nz` is monitored. Add it to Stripe receipts and customer portal as support contact. Auto-reply template documented in `docs/support-runbook.md`.

### M7-005
title: M7-005 SEO baseline — Google Search Console verification
body:
> Codex generates verification meta tag (user adds to `app/layout.tsx`). User completes GSC verification + submits sitemap. Document the steps in `plans/seo-baseline.md`.

### M7-006
title: M7-006 Uptime monitoring
body:
> Add UptimeRobot (or Cloudflare-native) monitor for `https://replyinmyvoice.com/api/health/db`. 5-minute interval. Notify `info@timeawake.co.nz` on failure. Document in `docs/observability.md`.

### M7-007
title: M7-007 Rollback procedure documented + dry-run
body:
> Write `docs/rollback-plan.md`: how to revert custom domain to old Pages holding page if launch fails (DNS API steps, expected downtime, customer comms template). Dry-run in a test environment if possible.

### M7-008
title: M7-008 Post-launch 24h + 7d KPI review
body:
> Build a script `scripts/launch-kpi-report.ts` that pulls from DB (RewriteCostLog) + PostHog API for: signups, paid conversions, total revenue, avg cost per rewrite, P95 cost, gross margin, error rate. Run at 24h + 7d post-launch. Output to `docs/launch-day-report.md`.

---

## Codex bulk-creation instructions

When this manifest is delivered to codex via `mcp__codex__codex` for bulk creation:

1. Create milestones on `ChuanQiao1128/replyinmyvoice`:
   ```
   M0-Stabilize, M1-Entra, M2-Quality, M3-V2, M4-Landing, M5-Telemetry, M6-Verify, M7-Launch
   ```
2. For each M0 issue, body = full content of `plans/issues/M0-001.md` etc.
3. For each M1-M7 issue, body = the entry from this manifest (title + body block), plus a footer: "Detailed brief will be added at `plans/issues/<id>.md` when this milestone starts. Source roadmap: `plans/commercialization-roadmap.md`."
4. After creation, write `plans/issue-board.md` with table: id | title | milestone | github_issue_url | status (pending) | codex_session_id (empty) | last_updated.
5. Issue board is supervisor's source of truth for which issue to pick next.
