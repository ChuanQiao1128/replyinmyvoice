# Issue Board — Commercialization Roadmap

Last updated: 2026-05-21T23:48Z (supervisor unwedge — banned-term scope fixed in overnight-supervisor.sh, STOP signal now gitignored; M2.5-002 | M2.5-Learning | M2.5-002 Run 100-case baseline; record results to docs/learning-baseline.md | https://github.com/ChuanQiao1128/replyinmyvoice/issues/84 | pending pending eval-scenarios.ts incremental refactor) (PR #171 merged bacba15 → M4-007 done; PR #172 merged 54dd119 → M4-008 done; M5-001 verified-already-complies via existing migration 20260520221000 + schema.prisma models → done no-PR; M2-007 verified-already-complies via existing docs/optimization-notes.md → done no-PR; M6-006 verified-banned-term-scan-clean on main → done no-PR)
Created this run: 45 | Skipped (dup): 60 | Errored: 0

## Supervisor loop
Pick next `pending` with lowest M-number, lowest id. Update status: `pending` → `in_progress` → `review` → `done` or a blocked category. Use `BLOCKED-WAITING-USER` only for real external user actions such as live-money tests, publish tokens, provider dashboard changes, missing secrets, or explicit product/legal decisions. Use `BLOCKED-PROVIDER`, `BLOCKED-PREREQ`, or `BLOCKED-AUTONOMY` for retryable provider failures, missing automation prerequisites, or broad coupled work that Codex/engineering should handle.

| ID | Milestone | Title | GitHub | Status |
|---|---|---|---|---|
| M0-001 | M0-Stabilize | M0-001: Audit dirty working tree → triage report | (dup) | done |
| M0-002 | M0-Stabilize | M0-002: Commit kept WIP in themed branches | (dup) | done |
| M0-003 | M0-Stabilize | M0-003: Discard or stash remaining dirty files | (dup) | done |
| M0-004 | M0-Stabilize | M0-004: Green baseline — lint, typecheck, tests pass | (dup) | done |
| M0-005 | M0-Stabilize | M0-005: Verify GitHub Actions CI is green on main | (dup) | done |
| M0-006 | M0-Stabilize | M0-006: Update AGENTS.md to reflect Stripe live state | https://github.com/ChuanQiao1128/replyinmyvoice/pull/151 | done (squash-merged 75c773d) |
| M1-001 | M1-Entra | M1-001 Inventory all Clerk usage in repo | https://github.com/ChuanQiao1128/replyinmyvoice/pull/152 | done (squash-merged 5d77f4a) |
| M1-002 | M1-Entra | M1-002 Replace clerkMiddleware in middleware.ts with Entra session check | (dup) | BLOCKED-AUTONOMY |
| M1-003 | M1-Entra | M1-003 Migrate API route auth from Clerk to Entra | (dup) | BLOCKED-AUTONOMY |
| M1-004 | M1-Entra | M1-004 Replace sign-in page with Entra MSAL redirect flow | (dup) | BLOCKED-AUTONOMY |
| M1-005 | M1-Entra | M1-005 Replace sign-up page with Entra flow | (dup) | BLOCKED-AUTONOMY |
| M1-006 | M1-Entra | M1-006 Implement /auth/callback for Entra code exchange | (dup) | BLOCKED-AUTONOMY |
| M1-007 | M1-Entra | M1-007 Add entra_user_id to User Prisma model + migration | (dup) | BLOCKED-AUTONOMY |
| M1-008 | M1-Entra | M1-008 Update Stripe webhook handler to use entra user lookup | (dup) | BLOCKED-AUTONOMY |
| M1-009 | M1-Entra | M1-009 Add Entra token validation tests | (dup) | BLOCKED-AUTONOMY |
| M1-010 | M1-Entra | M1-010 Add Playwright e2e for Entra sign-in → /app → rewrite → sign-out | (dup) | BLOCKED-AUTONOMY |
| M1-011 | M1-Entra | M1-011 Remove @clerk/nextjs and related deps from package.json | https://github.com/ChuanQiao1128/replyinmyvoice/pull/154 | done (squash-merged 25beac8) |
| M1-012 | M1-Entra | M1-012 Strip CLERK_* env references from code + .env.example + lib/env.ts | https://github.com/ChuanQiao1128/replyinmyvoice/pull/155 | done (squash-merged b0b6c99) |
| M1-013 | M1-Entra | M1-013 Document Clerk Cloudflare DNS record removal | https://github.com/ChuanQiao1128/replyinmyvoice/pull/153 | done (squash-merged ba8127a) |
| M1-014 | M1-Entra | M1-014 Update docs/manual-setup.md to Entra-only flow | https://github.com/ChuanQiao1128/replyinmyvoice/pull/153 | done (squash-merged ba8127a) |
| M2-001 | M2-Quality | M2-001 Implement hard quality gate rules in rewrite pipeline | (dup) | BLOCKED-PREREQ |
| M2-002 | M2-Quality | M2-002 Safe-failure response when all candidates rejected | (dup) | BLOCKED-PREREQ |
| M2-003 | M2-Quality | M2-003 Targeted repair pipeline receives diagnosis tags + facts | (dup) | BLOCKED-PREREQ |
| M2-004 | M2-Quality | M2-004 Add Priya billing/proration regression case | (dup) | BLOCKED-PREREQ |
| M2-005 | M2-Quality | M2-005 Build 25-case evaluation harness | (dup) | BLOCKED-PREREQ |
| M2-006 | M2-Quality | M2-006 Run evaluation; gate deploy | (dup) | BLOCKED-PREREQ |
| M2-007 | M2-Quality | M2-007 Document signal calibration in docs/optimization-notes.md | (dup) | done (already complies — file exists on main from commit fbc42f1 with iteration log + diagnosis tags + strategy memory link) |
| M2-008 | M2-Quality | M2-008 Add quality-gate UI for safe-failure state | (dup) | BLOCKED-PREREQ |
| M2-009 | M2-Quality | M2-009 Update Naturalness Check display for repaired candidates | (dup) | BLOCKED-PREREQ |
| M2.5-001 | M2.5-Learning | M2.5-001 Define 100-case baseline corpus across 5 scenarios | https://github.com/ChuanQiao1128/replyinmyvoice/issues/82 | done (already closed #82; corpus exists on main from PR #174) |
| M2.5-002 | M2.5-Learning | M2.5-002 Run 100-case baseline; record results to docs/learning-baseline.md | https://github.com/ChuanQiao1128/replyinmyvoice/issues/84 | BLOCKED-AUTONOMY |
| M2.5-003 | M2.5-Learning | M2.5-003 Failure-mode clustering by diagnosis tags | https://github.com/ChuanQiao1128/replyinmyvoice/issues/86 | done |
| M2.5-004 | M2.5-Learning | M2.5-004 Strategy candidate generator: cluster → prompt patch | https://github.com/ChuanQiao1128/replyinmyvoice/issues/88 | done |
| M2.5-005 | M2.5-Learning | M2.5-005 Auto-draft PR from promotable StrategyCandidate | https://github.com/ChuanQiao1128/replyinmyvoice/issues/90 | done |
| M2.5-006 | M2.5-Learning | M2.5-006 CI gate: scenario-evaluation regression check | https://github.com/ChuanQiao1128/replyinmyvoice/issues/92 | done |
| M2.5-007 | M2.5-Learning | M2.5-007 Scheduled LearningOps run (Cloudflare Cron Trigger) | https://github.com/ChuanQiao1128/replyinmyvoice/issues/94 | BLOCKED |
| M2.5-008 | M2.5-Learning | M2.5-008 Promotion approval UX in /admin/learning | https://github.com/ChuanQiao1128/replyinmyvoice/issues/96 | done |
| M2.5-009 | M2.5-Learning | M2.5-009 Canary deploy for new strategy | https://github.com/ChuanQiao1128/replyinmyvoice/issues/98 | done (squash-merged #181 52b0eee) |
| M2.5-010 | M2.5-Learning | M2.5-010 Strategy rollback on regression | https://github.com/ChuanQiao1128/replyinmyvoice/issues/100 | done |
| M3-001 | M3-V2 | M3-001 Add 5 scenarios to lib/rewrite-presets.ts | (dup) | BLOCKED-AUTONOMY |
| M3-002 | M3-V2 | M3-002 Reduce visible tone presets to 4 | (dup) | BLOCKED-AUTONOMY |
| M3-003 | M3-V2 | M3-003 Add scenario-specific prompt guardrails | (dup) | BLOCKED-AUTONOMY |
| M3-004 | M3-V2 | M3-004 Rewrite components/app/rewrite-workspace.tsx (V2 layout) | (dup) | BLOCKED-AUTONOMY |
| M3-005 | M3-V2 | M3-005 Enforce 5000-char combined cap in lib/validation.ts | (dup) | BLOCKED-AUTONOMY |
| M3-006 | M3-V2 | M3-006 Add character helper copy + counter | (dup) | BLOCKED-AUTONOMY |
| M3-007 | M3-V2 | M3-007 Add scenario to API request schema | (dup) | BLOCKED-AUTONOMY |
| M3-008 | M3-V2 | M3-008 Remove or hide legacy Quick context UI | (dup) | BLOCKED-AUTONOMY |
| M4-001 | M4-Landing | M4-001 Run rewrite engine against 4 documented sample cases | (dup) | BLOCKED-PROVIDER |
| M4-002 | M4-Landing | M4-002 Replace interactive-demo samples with measured ones | (dup) | done |
| M4-003 | M4-Landing | M4-003 Rewrite how-it-works.tsx with simpler 4 steps | (dup) | done (already complies — no PR needed) |
| M4-004 | M4-Landing | M4-004 Convert FAQ to single-column accordion | https://github.com/ChuanQiao1128/replyinmyvoice/pull/158 | done (squash-merged e25fb12) |
| M4-005 | M4-Landing | M4-005 Pricing page polish | https://github.com/ChuanQiao1128/replyinmyvoice/pull/158 | done (squash-merged e25fb12) |
| M4-006 | M4-Landing | M4-006 Footer with TimeAwake Ltd + support email | https://github.com/ChuanQiao1128/replyinmyvoice/pull/159 | done (squash-merged 357bfbd) |
| M4-007 | M4-Landing | M4-007 Privacy page reflects data storage truth | https://github.com/ChuanQiao1128/replyinmyvoice/pull/171 | done (squash-merged bacba15) |
| M4-008 | M4-Landing | M4-008 Terms page for live NZ$9 charges | https://github.com/ChuanQiao1128/replyinmyvoice/pull/172 | done (squash-merged 54dd119) |
| M4-009 | M4-Landing | M4-009 Add OG image + per-route metadata | https://github.com/ChuanQiao1128/replyinmyvoice/pull/157 | done (squash-merged 4031b84) |
| M4-010 | M4-Landing | M4-010 Add sitemap.ts and robots.ts | https://github.com/ChuanQiao1128/replyinmyvoice/pull/156 | done (squash-merged 4f7b435) |
| M5-001 | M5-Telemetry | M5-001 Cost telemetry DB schema (RewriteCostLog + RewriteProviderCall) | (dup) | done (already complies — both models in schema.prisma + migration 20260520221000 creates tables with required fields + indexes) |
| M5-002 | M5-Telemetry | M5-002 Capture telemetry across pipeline (OpenAI tokens + Sapling chars + estimator + persist) | (dup) | done |
| M5-003 | M5-Telemetry | M5-003 Offline Rewrite Quality Analysis report script | (dup) | BLOCKED |
| M5-004 | M5-Telemetry | M5-004 Normalize rewrite failure reasons for analysis | (dup) | in_progress |
| M5-005 | M5-Telemetry | M5-005 Fixture-backed Rewrite Quality Analysis tests | (dup) | pending |
| M5-006 | M5-Telemetry | M5-006 Owner runbook for rewrite-quality reports | (dup) | pending |
| M6-001 | M6-Verify | M6-001 Diff Cloudflare Worker prod secrets vs .env.local live | (dup) | pending |
| M6-002 | M6-Verify | M6-002 Push missing live secrets to Worker | (dup) | pending |
| M6-003 | M6-Verify | M6-003 Smoke test workers.dev preview | (dup) | pending |
| M6-004 | M6-Verify | M6-004 Confirm replyinmyvoice.com → Worker custom domain attach | (dup) | pending |
| M6-005 | M6-Verify | M6-005 Smoke test replyinmyvoice.com | (dup) | pending |
| M6-006 | M6-Verify | M6-006 Banned-term scan clean on main | (dup) | done (verified 2026-05-21T21:36Z — grep -RniE 'humanizer\|bypass\|undetect\|detector\|evade' app components public lib returned empty on main HEAD 54dd119) |
| M6-007 | M6-Verify | M6-007 Full validation suite green | (dup) | pending |
| M6-008 | M6-Verify | M6-008 Verify Stripe live webhook delivery | https://github.com/ChuanQiao1128/replyinmyvoice/issues/63 | pending |
| M7-001 | M7-Launch | M7-001 Real-account live test (with refund) | https://github.com/ChuanQiao1128/replyinmyvoice/issues/65 | BLOCKED-WAITING-USER (real money — user only) |
| M7-002 | M7-Launch | M7-002 PostHog analytics minimal events | https://github.com/ChuanQiao1128/replyinmyvoice/issues/67 | pending |
| M7-003 | M7-Launch | M7-003 Sentry error monitoring | https://github.com/ChuanQiao1128/replyinmyvoice/issues/69 | pending |
| M7-004 | M7-Launch | M7-004 Confirm support email pipeline | https://github.com/ChuanQiao1128/replyinmyvoice/pull/169 | done (squash-merged 79af939) |
| M7-005 | M7-Launch | M7-005 SEO baseline — Google Search Console verification | https://github.com/ChuanQiao1128/replyinmyvoice/pull/168 | done (squash-merged faf9d29) |
| M7-006 | M7-Launch | M7-006 Uptime monitoring | https://github.com/ChuanQiao1128/replyinmyvoice/pull/170 | done (squash-merged be32e60) |
| M7-007 | M7-Launch | M7-007 Rollback procedure documented + dry-run | https://github.com/ChuanQiao1128/replyinmyvoice/pull/165 | done (squash-merged c660148) |
| M7-008 | M7-Launch | M7-008 Post-launch 24h + 7d KPI review | https://github.com/ChuanQiao1128/replyinmyvoice/issues/80 | pending |
| M8-001 | M8-API | M8-001 ApiKey + ApiKeyUsage Prisma schema | https://github.com/ChuanQiao1128/replyinmyvoice/pull/173 | in_progress (PR open b21ea9b, awaiting CI) |
| M8-002 | M8-API | M8-002 API key generate + revoke UI at /app/api-keys | https://github.com/ChuanQiao1128/replyinmyvoice/issues/104 | pending |
| M8-003 | M8-API | M8-003 API key authentication middleware | https://github.com/ChuanQiao1128/replyinmyvoice/issues/106 | pending |
| M8-004 | M8-API | M8-004 POST /api/v1/rewrite endpoint (API-key auth) | https://github.com/ChuanQiao1128/replyinmyvoice/issues/108 | pending |
| M8-005 | M8-API | M8-005 OpenAPI 3.0 spec at /api/v1/openapi.json | https://github.com/ChuanQiao1128/replyinmyvoice/issues/110 | pending |
| M8-006 | M8-API | M8-006 Per-key rate limiting via Cloudflare KV | https://github.com/ChuanQiao1128/replyinmyvoice/issues/112 | pending |
| M8-007 | M8-API | M8-007 Per-key monthly quota enforcement | https://github.com/ChuanQiao1128/replyinmyvoice/issues/114 | pending |
| M8-008 | M8-API | M8-008 Stripe products for B2B tiers | https://github.com/ChuanQiao1128/replyinmyvoice/issues/116 | pending |
| M8-009 | M8-API | M8-009 B2B subscription state machine | https://github.com/ChuanQiao1128/replyinmyvoice/issues/118 | pending |
| M8-010 | M8-API | M8-010 B2B Stripe webhook handlers (shared endpoint) | https://github.com/ChuanQiao1128/replyinmyvoice/issues/120 | pending |
| M8-011 | M8-API | M8-011 B2B customer portal link from /app/api-keys | https://github.com/ChuanQiao1128/replyinmyvoice/issues/122 | pending |
| M8-012 | M8-API | M8-012 API docs site at /developers | https://github.com/ChuanQiao1128/replyinmyvoice/issues/125 | pending |
| M8-013 | M8-API | M8-013 Standardized API error JSON format | https://github.com/ChuanQiao1128/replyinmyvoice/issues/127 | pending |
| M8-014 | M8-API | M8-014 Idempotency-Key header support | https://github.com/ChuanQiao1128/replyinmyvoice/issues/129 | pending |
| M8-015 | M8-API | M8-015 Webhook subscriptions for API customers | https://github.com/ChuanQiao1128/replyinmyvoice/issues/131 | pending |
| M8-016 | M8-API | M8-016 B2B onboarding email + Stripe customer creation flow | https://github.com/ChuanQiao1128/replyinmyvoice/issues/133 | pending |
| M9-001 | M9-Distribution | M9-001 npm package skeleton @replyinmyvoice/mcp-server | https://github.com/ChuanQiao1128/replyinmyvoice/pull/160 | done (squash-merged 93f295c) |
| M9-002 | M9-Distribution | M9-002 Implement MCP tools (rewrite_email, analyze_signal, list_scenarios) | https://github.com/ChuanQiao1128/replyinmyvoice/issues/137 | pending |
| M9-003 | M9-Distribution | M9-003 MCP server config: REPLY_IN_MY_VOICE_API_KEY env | https://github.com/ChuanQiao1128/replyinmyvoice/issues/139 | pending |
| M9-004 | M9-Distribution | M9-004 README with install for Codex / Claude Code / Cursor / Continue.dev | https://github.com/ChuanQiao1128/replyinmyvoice/pull/163 | done (squash-merged b8669ec) |
| M9-005 | M9-Distribution | M9-005 Example workflows in docs/mcp-examples.md | https://github.com/ChuanQiao1128/replyinmyvoice/pull/164 | done (squash-merged 71a02a1) |
| M9-006 | M9-Distribution | M9-006 Publish @replyinmyvoice/mcp-server to npm | https://github.com/ChuanQiao1128/replyinmyvoice/issues/145 | BLOCKED-WAITING-USER (needs NPM_TOKEN) |
| M9-007 | M9-Distribution | M9-007 Claude Code Skill template at agent-skills/replyinmyvoice-rewrite/SKILL.md | https://github.com/ChuanQiao1128/replyinmyvoice/pull/161 | done (squash-merged 2c09dbb) |
| M9-008 | M9-Distribution | M9-008 Skill .skill packaging script for distribution | https://github.com/ChuanQiao1128/replyinmyvoice/pull/162 | done (squash-merged 388afd3) |
| M9-009 | M9-Distribution | M9-009 Marketing page /developers for MCP + Skill | https://github.com/ChuanQiao1128/replyinmyvoice/pull/166 | done (squash-merged 7770159) |
| M9-010 | M9-Distribution | M9-010 Launch announcement page + draft posts | https://github.com/ChuanQiao1128/replyinmyvoice/pull/167 | done (squash-merged 83555f6) |
