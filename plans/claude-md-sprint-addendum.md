## Active Commercialization Sprint (added 2026-05-21)

Authorization: The project owner (ChuanQiao1128, operator of TimeAwake Ltd) granted the supervisor (Claude in Cowork mode) an autonomous run mandate on 2026-05-21 to take `replyinmyvoice.com` to revenue-ready state across all 11 planned milestones. No real users on the live site yet, so UX changes do not need preservation guarantees.

End state: `replyinmyvoice.com` accepts real NZ$ subscriptions from consumers AND offers a B2B API with tiered subscriptions AND ships an MCP server + Claude Code Skill for LLM-tool integration.

Roadmap: `/Users/qc/Desktop/CloudFlare/plans/commercialization-roadmap.md` and the manifest pair `plans/issue-manifest.md` + `plans/issue-manifest-additions.md` and `plans/issues/M0-*.md`. As of 2026-05-21 the scope expanded to include `.NET + Azure SQL + Azure Functions + Service Bus` deployment (was scoped out, now in).

### Sprint-specific posture (overrides default supervisor caution)

- UX changes to `/app` workspace and landing pages are authorized — there are no real users to preserve compatibility for
- Cloudflare Worker config + custom domain attach are authorized via `wrangler` from codex (uses `CLOUDFLARE_API_TOKEN` already in `.env.local`)
- Live Stripe Products/Prices may be created via the Stripe API by codex using the user's `STRIPE_SECRET_KEY` already present
- Prisma schema migrations may be run by codex in `workspace-write` mode (never `--force-reset`)
- Azure resources (SQL, Functions, Service Bus, Application Insights) may be provisioned via `az` CLI by codex; user has `az login` set
- DeepSeek Pro is the current rewrite-orchestra provider (`OPENAI_BASE_URL` points to DeepSeek); all OpenAI-named env vars are now DeepSeek-routed
- Codex may push to `main` directly during this sprint when the change passes its own validation (lint+typecheck+test). PR-then-merge is preferred for risky changes; small fixes may go straight to main

### Sprint hard limits (autonomy cannot cross)

- Never initiate real Stripe charges from automation. The first live transaction (M7-001) is the user's hands-on test.
- Never run `npm publish` — user provides `NPM_TOKEN` at M9-006 and runs publish themselves
- Never print, log, commit, or summarize secret values from `.env.local`, `.dev.vars`, `globalapikey/`
- Banned-term scan still blocks: `humanizer | bypass | undetect | detector | evade` — halt on any match
- OpenAI+Sapling+DeepSeek cumulative eval spend ≤ NZ$20 per supervisor turn (track in `plans/sleep-run-budget.md`). DeepSeek is much cheaper than original OpenAI estimate so this is more than enough for 100-case eval
- Never modify `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID` (existing consumer price) without the user's explicit instruction in the brief
- Azure resources that ARE NEW must respect `AZURE_BUDGET_LIMIT` and `AZURE_ALLOW_PAID_RESOURCES` flags in `.env.local`

### Decision policy

- Codex makes architectural / library / naming calls autonomously when the brief doesn't specify
- Codex documents every non-obvious decision in the commit message AND appends a line to `plans/decisions-log.md` (create if missing)
- Format: `<ISO date> | <issue-id> | <decision> | <one-line rationale>`

### Failure handling

- Codex retries up to 2× with corrected briefs from supervisor
- On 3rd failure, codex marks the issue as `blocked` in `plans/issue-board.md` with the failure summary and moves to next pending issue
- Supervisor reviews blocked issues and either provides corrected brief or escalates to user

### Service / external dependency status (as of 2026-05-21)

Already configured in `.env.local`: Stripe live, Entra External ID + Google federation, Azure subscription + tenant, Cloudflare account + API token, Neon Postgres, DeepSeek Pro, Sapling, admin allowlist.

Pending user-provided (blocks specific milestones, not the overall run):
- `POSTHOG_API_KEY` — blocks M7-002
- `SENTRY_DSN` — blocks M7-003
- `GITHUB_PAT` (fine-grained for ChuanQiao1128/replyinmyvoice) — enables GitHub MCP, otherwise gh CLI works
- `NPM_TOKEN` — blocks M9-006 only
- `AZURE_SQL_ADMIN_USER` + `AZURE_SQL_ADMIN_PASSWORD` — blocks Azure SQL provisioning steps in M-Azure
- `AZURE_SERVICE_BUS_NAMESPACE` — blocks Service Bus provisioning, unless codex creates from scratch via `az` CLI

User confirms at start of each session: which dependencies are now available so supervisor can resume blocked work.
