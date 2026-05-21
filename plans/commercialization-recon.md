# Commercialization Recon — Reply In My Voice

Date: 2026-05-21
Supervisor: Claude (read-only) — implementation goes through Codex MCP.
Goal: get from current code state to **`replyinmyvoice.com` live, taking real NZ$9/month subscriptions**.

## Authoritative state (as of 2026-05-21)

### Repo & git
- Remote: `git@github.com:ChuanQiao1128/replyinmyvoice.git`
- Branch: `main`
- **52 modified files uncommitted** (auth migration + rewrite-gate WIP)
- Recent commits centered on Clerk → Entra cutover, fact ledger, rewrite gate calibration

### Local environment — user has already gone live
| Var | State | Notes |
|---|---|---|
| `LAUNCH_CONFIRMED` | `true` | Domain cutover authorized |
| `NEXT_PUBLIC_APP_URL` | `https://replyinmyvoice.com` | Live URL configured |
| `STRIPE_SECRET_KEY` | `sk_live_***` | Live mode |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | `pk_live_***` | Live mode |
| `STRIPE_PRICE_ID` | `price_***` (length 30) | Assumed live (since keys are live) |
| `STRIPE_WEBHOOK_SECRET` | `whsec_***` | Live webhook secret |
| `STRIPE_LIVE_CUTOVER_APPROVED` | `true` | User's explicit go-ahead |
| `ADMIN_EMAILS` | 1 email present | Likely `chuanqiao1128@gmail.com` |

**Policy conflict to fix**: AGENTS.md still says "Keep Stripe in sandbox mode." Reality is live. AGENTS.md needs a codex update to reflect.

### Production status (needs verification)
- Worker URL: `https://replyinmyvoice-app.qc1128qc.workers.dev` — last deployed `c4b14fa9-...`
- Domain: `https://replyinmyvoice.com` — unknown whether it's showing app or holding page
- Cloudflare Worker prod secrets: unknown whether they match `.env.local` live values
- Stripe live webhook: unknown whether endpoint reaches the Worker

### What's built
- Next.js 15.5.18 + React 19 + OpenNext Cloudflare adapter
- Prisma + Neon Postgres for runtime data
- **Auth: in transition** — Clerk being replaced by Microsoft Entra External ID + Google federation. Both code paths currently coexist.
- Stripe integration: checkout + customer portal + webhook handlers for checkout/subscription/invoice events
- Free quota: 3 lifetime rewrites → paywall → NZ$9/month subscription = 40 rewrites/billing month
- Rewrite engine: fact-extract → scenario card → 3 candidates → Sapling Naturalness Check → quality gate
- Admin scaffolding: `/admin`, `/admin/rewrites` exist
- 30+ unit tests in `tests/unit/`, Playwright e2e setup
- Privacy + Terms pages exist (`app/privacy/page.tsx`, `app/terms/page.tsx`)
- DNS configured (Clerk verification records — to be cleaned up once Entra fully replaces Clerk)
- GitHub Actions: `.github/workflows/cloudflare-worker.yml`, `.github/workflows/dotnet-azure.yml`

### What's broken or incomplete
1. **Rewrite quality regression**: long customer-support drafts measured 89% AI-like → rewrite returned 99-100%. Quality gate not enforcing reject-if-rewrite>=draft yet (per docs/next-development-brief.md "Next Rewrite Quality Fix").
2. **Workspace V2 not shipped**: Current workspace still uses Quick context (audience/purpose/must-keep). Brief calls for 5 scenarios + 4 tones, draft-only support, optional context.
3. **Character limits**: backend still allows 10000-char combined; needs reduction to 5000.
4. **Landing samples have fabrications**: Maya/Jordan names invented; needs replacement with documented samples from `docs/sample-cases.md`.
5. **FAQ uses 2-column card grid**: brief calls for accordion / single-column list.
6. **Cost telemetry incomplete**: `RewriteCostLog` / `RewriteProviderCall` tables not yet in schema; OpenAI token capture + Sapling char capture not wired.
7. **Admin dashboard scaffold only**: cards/tables not populated against real telemetry.
8. **Entra migration not finished**: Clerk middleware + sign-in/up pages still present.

### What's external / dashboard / user-only
- Stripe KYC, NZ GST registration, dispute policy — user's responsibility (assumed done since live keys exist)
- TimeAwake Ltd Stripe account activation — user-verified
- Cloudflare custom-domain attach for replyinmyvoice.com → Worker — needs verification or execution
- Live Stripe webhook endpoint dashboard config — assumed done since `STRIPE_WEBHOOK_SECRET` is set live

## Banned terms guard (per AGENTS.md)
CI scans `app`, `components`, `public`, `lib/**` for: `humanizer / bypass / undetect / detector / evade`. Every codex brief must repeat this constraint.

## Supervisor flow per issue
1. Claude reads issue + relevant code → writes plan
2. Claude delegates: `mcp__codex__codex(cwd=/Users/qc/Desktop/CloudFlare, sandbox=workspace-write, approval-policy=never, prompt=<self-contained brief>)`
3. Brief always includes: TASK, CONTEXT (abs paths), CONSTRAINTS (banned terms, secrets, ≤30min scope cap), CHANGES REQUIRED, ACCEPTANCE, DO NOT
4. Codex returns → Claude reads diff via Read/Grep, runs `npm test` / `dotnet test` via bash
5. Pass → codex closes the GitHub issue with comment; Fail → codex-reply with corrections
6. Claude never runs `git commit`, `git push`, `wrangler deploy`, or edits source files directly

## Known sandbox limitations
- Claude's bash sandbox cannot reach `api.github.com` (403). All `gh` operations are delegated to codex (which runs on the user's Mac with `gh` already authenticated).
- Claude's bash sandbox cannot reach `replyinmyvoice.com` or `workers.dev` for smoke testing. Smoke tests are delegated to codex or run by the user.
