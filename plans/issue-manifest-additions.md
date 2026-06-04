# Issue Manifest — Additions (M2.5 / M8 / M9)

These three milestones were added after the original 7-milestone roadmap. They will be merged into the main `issue-manifest.md` parse by `create-issues.py`.

Total new issues: 36 (M2.5: 10, M8: 16, M9: 10).

---

## Milestone: M2.5-Learning

The continuous rewrite-quality improvement loop. M2 fixes the immediate quality bug; M2.5 makes quality improvement a self-sustaining engine that runs every 24h, learns from real customer failures, and proposes (gated) code changes. Based on `docs/next-development-brief.md` "LearningOps V1" section.

### M2.5-001
title: M2.5-001 Define 100-case baseline corpus across 5 scenarios
body:
> Build `docs/learning-baseline-corpus.md`: 100 representative drafts, 20 per scenario (Blank/Email/Customer support/Cover letter/Work update). Each case has: draft text, scenario, tone, expected facts to preserve, expected draft AI-like signal range. Source: 50% from `RewriteLearningSample` real failures + 50% hand-crafted edge cases. NO real user PII in committed text — fictional or stripped.

### M2.5-002
title: M2.5-002 Run 100-case baseline; record results to docs/learning-baseline.md
body:
> Extend `scripts/eval-scenarios.ts` to take the corpus from M2.5-001 and run the full pipeline. Record for each: scenario, tone, draft signal, candidate signals, final signal, signal change, quality-gate decision, facts preserved, rejection reasons. Sandbox OpenAI key. Budget cap: NZ$10 per full run.

### M2.5-003
title: M2.5-003 Failure-mode clustering by diagnosis tags
body:
> New `lib/learningops/cluster.ts`: group failed cases by primary diagnosis tag (`stock_opening`, `corporate_polish`, `uniform_rhythm`, etc per the AI-like cause taxonomy). For each cluster: count, exemplar case ids, common scenario, common tone. Output: `LearningFinding` table rows (new Prisma migration).

### M2.5-004
title: M2.5-004 Strategy candidate generator: cluster → prompt patch
body:
> `lib/learningops/candidates.ts`: takes a `LearningFinding` and proposes a targeted prompt/strategy patch. Patches are STRUCTURED (e.g. "add to repair prompt for customer_support scenario: 'avoid balanced 4-paragraph structure'"). Each candidate has risk level, required regression test, evidence count. New `StrategyCandidate` table.

### M2.5-005
title: M2.5-005 Auto-draft PR from promotable StrategyCandidate
body:
> Scheduled job (M2.5-007) calls a codex MCP session with the StrategyCandidate brief to draft a PR. Codex modifies the relevant prompt/scenario file, adds a regression test, updates `docs/rewrite-strategy-memory.md`. Opens PR, NEVER auto-merges.

### M2.5-006
title: M2.5-006 CI gate: scenario-evaluation regression check
body:
> GitHub Actions step that reads `docs/scenario-evaluation-results.md` from the PR branch + the latest main branch. Fails if any of: avg signal reduction drops by ≥5 points; %below-50 drops by ≥5 points; any case regresses from pass→fail. Blocks merge.

### M2.5-007
title: M2.5-007 Scheduled LearningOps run (Cloudflare Cron Trigger)
body:
> Cron trigger every 24h: pulls last 7 days of `RewriteLearningSample` rows → clusters (M2.5-003) → generates candidates (M2.5-004) → if strong candidate exists, drafts PR (M2.5-005). Writes `LearningRun` row with status: `digest_only` | `docs_only` | `promoted` | `blocked`. Per `docs/next-development-brief.md` automation policy: never auto-deploys, only opens PRs.

### M2.5-008
title: M2.5-008 Promotion approval UX in /admin/learning
body:
> `app/admin/learning/page.tsx`: list of recent `LearningRun` rows with status, finding counts, PR links if any. Per-finding view: cluster details, evidence cases, proposed candidate. Admin can mark candidate as `approved` / `needs_revision` / `rejected`. Updates `StrategyCandidate.status` in DB.

### M2.5-009
title: M2.5-009 Canary deploy for new strategy
body:
> When a StrategyCandidate is promoted to production via merge, use a feature flag (env or KV) to route N% (default 10%) of rewrite traffic to the new strategy. After 24h or 200 rewrites, compare signal-change distributions: if new strategy is worse, auto-disable flag; if better, gradually ramp.

### M2.5-010
title: M2.5-010 Strategy rollback on regression
body:
> If post-promotion measured signal-change for the affected scenario drops by ≥3 points over a 50-rewrite rolling window, automatically toggle the canary flag off, notify admin via email, open a follow-up GitHub issue. Document in `docs/rewrite-strategy-memory.md`.

---

## Milestone: M8-API

B2B API surface for developers. Separate auth path (API key, not Entra session), separate billing tier (Free / Starter NZ$29 / Pro NZ$99 / Enterprise contact), separate rate limits. Hard cap at quota — no overage (user picked tiered subscription without hybrid).

### M8-001
title: M8-001 ApiKey + ApiKeyUsage Prisma schema
body:
> New tables. `ApiKey`: id, userId, keyHash (sha256), name (user label), planTier ('free'|'starter'|'pro'|'enterprise'), scope (json — which endpoints), rateLimitPerMinute, monthlyQuota, currentPeriodUsage, currentPeriodStartedAt, lastUsedAt, expiresAt, revokedAt. `ApiKeyUsage`: id, apiKeyId, requestId, endpoint, statusCode, latencyMs, costUsdEstimate, createdAt. Indexes on keyHash unique, (apiKeyId, createdAt).

### M8-002
title: M8-002 API key generate + revoke UI at /app/api-keys
body:
> `app/app/api-keys/page.tsx`: list user's API keys (name, last 4 chars of plaintext, plan tier, current usage / monthly quota, last used, status). Actions: create new (show plaintext ONCE, never again), revoke. New key shows tier-appropriate quota.

### M8-003
title: M8-003 API key authentication middleware
body:
> `lib/api-key-auth.ts`: validates `Authorization: Bearer rmv_<key>` or `X-API-Key: rmv_<key>` header. Lookup by sha256(key) in `ApiKey` table. Check not revoked, not expired. Returns `{ apiKey, user }` or null. Used by all `/api/v1/*` routes. Tests in `tests/unit/api-key-auth.test.ts`.

### M8-004
title: M8-004 POST /api/v1/rewrite endpoint (API-key auth)
body:
> New route `app/api/v1/rewrite/route.ts`. Auth via M8-003. Same payload schema as `/api/rewrite` but adds `api_version: 'v1'`. Response includes `request_id`, `usage` (tokens, sapling chars, estimated cost), full signal data. Errors in standard format (M8-013). Tests cover: valid key, revoked key, missing key, malformed key, quota exhausted.

### M8-005
title: M8-005 OpenAPI 3.0 spec at /api/v1/openapi.json
body:
> Generate from zod schemas. Endpoint `/api/v1/openapi.json` returns the spec. Includes /v1/rewrite, /v1/analyze-signal, /v1/scenarios. Document auth, errors, rate limits. Test: spec validates against OpenAPI 3.0 schema.

### M8-006
title: M8-006 Per-key rate limiting via Cloudflare KV
body:
> Token-bucket implementation in `lib/rate-limit.ts`. KV namespace `RATE_LIMIT_KV`. Limit per plan tier: Free 5 req/min, Starter 30 req/min, Pro 120 req/min, Enterprise custom. Returns 429 with `Retry-After` and `X-RateLimit-Remaining` headers. Tests with fake clock.

### M8-007
title: M8-007 Per-key monthly quota enforcement
body:
> Check `ApiKey.currentPeriodUsage < monthlyQuota` before each request. Increment atomically. Reset on Stripe billing period boundary (via webhook in M8-010). Quota exhausted → 402 Payment Required with link to upgrade. Tests.

### M8-008
title: M8-008 Stripe products for B2B tiers
body:
> Codex creates 3 Stripe live-mode products + prices via API: Starter NZ$29/month → 250 rewrites quota, Pro NZ$99/month → 1500 rewrites quota, Enterprise (contact sales — no price object, manual invoice). Records price IDs in `.env.local` as `STRIPE_PRICE_API_STARTER`, etc. (codex MUST NOT print). Adds same in Cloudflare Worker secrets via wrangler.

### M8-009
title: M8-009 B2B subscription state machine
body:
> Extend `lib/subscription.ts` to track consumer entitlement source separately from API tiers. User can have both. `lib/quota.ts` checks the right tier for the request context (session = consumer; API key = B2B tier on the key).

### M8-010
title: M8-010 B2B Stripe webhook handlers (shared endpoint)
body:
> `lib/stripe-events.ts`: on `customer.subscription.created/updated` with a `metadata.plan_type = 'api'`, find or create `ApiKey` row, sync planTier + monthlyQuota + currentPeriodStartedAt. On `customer.subscription.deleted`, revoke keys associated with that subscription. Tests cover both consumer + API webhook flows.

### M8-011
title: M8-011 B2B customer portal link from /app/api-keys
body:
> If user has active B2B subscription, show "Manage billing" button → Stripe Customer Portal session (via `stripe.billingPortal.sessions.create`). Reuse existing helper if present.

### M8-012
title: M8-012 API docs site at /developers
body:
> `app/developers/page.tsx`: developer landing — quickstart (curl example), authentication, endpoints, errors, rate limits, pricing table. Also `app/developers/quickstart/page.tsx`, `app/developers/pricing/page.tsx`. Static for now; OpenAPI Swagger UI deferred.

### M8-013
title: M8-013 Standardized API error JSON format
body:
> All `/api/v1/*` errors return `{ error: { code, message, request_id, docs_url } }`. Codes documented in `docs/api-errors.md`: AUTH_INVALID_KEY, AUTH_REVOKED_KEY, RATE_LIMIT_EXCEEDED, QUOTA_EXHAUSTED, VALIDATION_FAILED, REWRITE_QUALITY_FAILED, PROVIDER_UNAVAILABLE, INTERNAL_ERROR. HTTP status correctly mapped.

### M8-014
title: M8-014 Idempotency-Key header support
body:
> `/api/v1/rewrite` accepts `Idempotency-Key: <user-supplied>` header. Server caches request_id → response for 24h via KV. Repeat call with same key + same payload returns cached response. Different payload + same key → 409 Conflict.

### M8-015
title: M8-015 Webhook subscriptions for API customers
body:
> New `app/app/api-keys/webhooks/page.tsx`: customer registers webhook URL + selects events (rewrite.completed, quota.warned-80pct, quota.exhausted). Backend `lib/api-webhooks.ts` POSTs JSON to URL with HMAC-SHA256 signature. Retries 3× with backoff on 5xx. Tests with fake receiver.

### M8-016
title: M8-016 B2B onboarding email + Stripe customer creation flow
body:
> Sign-up flow at `/developers/sign-up` → checkout for chosen tier → on subscription.created webhook → auto-generate first API key → send onboarding email (info@timeawake.co.nz from address) with key value (one-time display). Email template in `lib/emails/api-onboarding.ts`.

---

## Milestone: M9-Distribution

MCP server + Claude Code Skill so developers can use Reply In My Voice directly inside their LLM tools. All require an API key (decision from supervisor session). Distribution channels: npm, Anthropic skill marketplace if available, GitHub releases.

### M9-001
title: M9-001 npm package skeleton @replyinmyvoice/mcp-server
body:
> New monorepo dir `packages/mcp-server/` (or sibling repo — supervisor decides). package.json, tsconfig, bin entry, MCP SDK dep. Builds to `dist/`. README skeleton. Empty initial release.

### M9-002
title: M9-002 Implement MCP tools (rewrite_email, analyze_signal, list_scenarios)
body:
> `packages/mcp-server/src/tools/`: `rewrite_email({ draft, scenario?, tone?, context? })` → calls /api/v1/rewrite. `analyze_signal({ text })` → calls /api/v1/analyze-signal. `list_scenarios()` → static list. Use `@modelcontextprotocol/sdk`. Tests with mock HTTP.

### M9-003
title: M9-003 MCP server config: REPLY_IN_MY_VOICE_API_KEY env
body:
> Server reads API key from env. Clear error if missing: "Set REPLY_IN_MY_VOICE_API_KEY env var. Get one at https://replyinmyvoice.com/app/api-keys". Optional `REPLY_IN_MY_VOICE_BASE_URL` override for dev.

### M9-004
title: M9-004 README with install for Codex / Claude Code / Cursor / Continue.dev
body:
> `packages/mcp-server/README.md`: install via `npx @replyinmyvoice/mcp-server`. Config snippets for each MCP host (Claude Desktop config.json, Codex .codex/config.toml, Cursor settings, Continue config.yaml). Authentication setup. Example tool calls.

### M9-005
title: M9-005 Example workflows in docs/mcp-examples.md
body:
> `/Users/qc/Desktop/CloudFlare/docs/mcp-examples.md`: 4-6 worked examples. "Lower AI-like signal on this Outlook draft", "Compare two versions of a cover letter", "Rewrite a customer-support reply with scenario guardrails", "Batch-process a folder of drafts". Each shows the prompt to the LLM host, the tool call, the result.

### M9-006
title: M9-006 Publish @replyinmyvoice/mcp-server to npm
body:
> Codex: npm login (user-provided NPM_TOKEN in env), version 0.1.0, publish. Update package.json with repo URL + bugs URL. Tag GitHub release. Verify install works via `npx @replyinmyvoice/mcp-server@0.1.0`.

### M9-007
title: M9-007 Claude Code Skill template at agent-skills/replyinmyvoice-rewrite/SKILL.md
body:
> SKILL.md following the skill format in `/Users/qc/Desktop/CloudFlare/agent-skills/`. Triggers: "lower AI-like signal", "make this sound more natural", "naturalness check on email", "rewrite this draft". Body instructs Claude to call the MCP server. Avoids banned terms.

### M9-008
title: M9-008 Skill .skill packaging script for distribution
body:
> `scripts/package-skill.mjs`: zips `agent-skills/replyinmyvoice-rewrite/` into `dist/replyinmyvoice-rewrite.skill`. Verifies SKILL.md exists. CI publishes the artifact on tag.

### M9-009
title: M9-009 Marketing page /developers for MCP + Skill
body:
> Section on `/developers` (M8-012) for MCP + Skill: install commands, gif demo, "Use in Claude Code / Cursor / Codex". Link to API docs + sign-up.

### M9-010
title: M9-010 Launch announcement page + draft posts
body:
> Static `/launch` page describing both products (consumer + developer API). Draft Twitter thread, Hacker News submission text, Reddit r/SaaS post — all saved to `docs/launch-announcement-drafts.md`. NO actual posting (supervisor decision).

---

## Notes for create-issues.py

The script must:

1. Add M2.5-Learning, M8-API, M9-Distribution to MILESTONES list
2. Parse this file in addition to `issue-manifest.md`
3. Preserve milestone order: M0, M1, M2, M2.5, M3, M4, M5, M6, M7, M8, M9

When creating milestones, the descriptions should be:

- M2.5-Learning: "Continuous rewrite-quality improvement loop"
- M8-API: "B2B API surface with tiered subscriptions"
- M9-Distribution: "MCP server + Claude Code Skill for LLM-tool integration"
