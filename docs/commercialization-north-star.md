# Commercialization North Star Specification

Date: 2026-05-22

## Context

Reply In My Voice is being pushed through an autonomous commercialization run. The current system has three cooperating execution roles:

- The shell overnight loop is the primary executor. It processes repair inbox items first, then product issues; it runs Codex for scoped edits, validation, PRs, CI polling, and merges.
- Claude's scheduled task `replyinmyvoice-loop-monitor` monitors the loop, records checkpoints, and alerts on blockers.
- Codex's scheduled automation is a dead-man watchdog only. It should restart a dead or stale loop when safe, not consume normal repair work while the shell loop is healthy.

This document is the durable source of truth for the commercial goal. It exists so a fresh Codex, Claude, or human session can recover the same target without relying on chat history.

## Goals

The commercial north star is not "all issues closed." The product is ready when it can be used, demonstrated, and sold as a real product:

- A public visitor can understand Reply In My Voice without banned positioning or misleading claims.
- A real user can create an account with Google or an email-based sign-up flow.
- A signed-in user can use `/app` to rewrite an email or reply while preserving facts and staying within quota.
- Free users get the confirmed free quota, hit a clear paywall, and can upgrade.
- Paid users can complete Stripe checkout, receive the paid quota, manage billing, and keep using the product.
- Operators can verify failures, costs, quality, support contact paths, deployment status, and rollback readiness.
- The rewrite system has a clean measured evaluation result before advertising quality claims.
- The owner can generate an internal Rewrite Quality Analysis report that proves rewrite quality, failure reasons, cost, latency, and strategy-version performance from production telemetry.
- Developers can create API keys, call a documented rewrite API, and see quota or billing behavior.
- MCP users can install and run the Reply In My Voice MCP server from Codex, Claude Code, Cursor, or similar clients.

## Non-Goals

- Claude should not implement product issues during the monitor task.
- Codex should not perform user-only actions such as live card charges, Stripe refunds, or npm publication without explicit user action.
- No agent should print secrets, tokens, raw `.env.local` values, private keys, or provider credentials.
- No launch claim should depend on unavailable rewrite-signal measurements.
- No user-facing copy should frame the product as detector evasion or guaranteed hidden AI writing.

## Current System

Primary execution files:

- `plans/overnight-supervisor.sh` runs the shell-driven loop and consumes `plans/codex-worker-inbox.md` before selecting new issue-board work.
- `plans/codex-implementation-prompt.md` tells Codex what it may do inside each issue branch.
- `plans/issue-board.md` tracks issue state.
- `plans/supervisor-handoff.md` tells Claude how to monitor without racing Codex.
- `plans/overnight-progress.md`, `plans/decisions-log.md`, and `plans/blockers-log.md` record the run history.

Current product target areas:

- Consumer app and billing: M1 through M7 in `plans/issue-board.md`.
- API key and B2B platform: M8 in `plans/issue-board.md`.
- MCP and skill distribution: M9 in `plans/issue-board.md`.
- Rewrite quality and learning loop: M2 and M2.5, backed by `docs/fact-reconstruct-rewrite-target.md`, `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`, and `docs/rewrite-email-eval-cases-100.md`.
- Rewrite quality and cost analysis: M5, backed by `docs/rewrite-quality-analysis-spec.md`, `RewriteCostLog`, and `RewriteProviderCall`.

## Proposed Architecture

Use a three-layer operating model.

1. Durable goal layer

   `docs/commercialization-north-star.md` is the top-level target. It defines what "commercially ready" means and how Claude/Codex should judge progress.

2. Monitor layer

   Claude's `replyinmyvoice-loop-monitor` scheduled task reads this document and `plans/supervisor-handoff.md`, then performs health checks, safe loop restarts, progress summaries, blocker detection, and stop-signal alerts. It does not implement product work and does not call Codex.

3. Executor layer

   Codex runs through the shell loop. The shell supervisor first consumes one pending item from `plans/codex-worker-inbox.md`; if no repair is pending, it selects the next pending issue from `plans/issue-board.md`. Codex implements the scoped task, validates locally, writes `plans/task-status.json`, and leaves git/GitHub operations to the shell supervisor.

   Claude should not spend monitor budget fixing issues or asking the owner to copy/paste logs into Codex. When it sees a non-user engineering blocker, it writes a sanitized item to `plans/codex-worker-inbox.md`. The running shell loop consumes that inbox directly, so repair latency is the next loop iteration rather than a separate hourly worker cycle.

   `plans/overnight-progress.md` is the human-readable report. `plans/codex-worker-inbox.md` is the machine-readable repair queue. No agent should use ordinary progress prose as its normal repair trigger.

Claude is the monitor. The shell loop is the dispatcher. Codex is the scoped implementation worker. The repo documents are the shared contract.

## Data Model

No new database table is introduced by this document. The durable operational state lives in repository files:

| File | Owner | Purpose |
| --- | --- | --- |
| `docs/commercialization-north-star.md` | Human/Codex | Top-level commercial target and readiness gates |
| `plans/issue-board.md` | Shell loop/Codex | Executable product issue queue and status board |
| `plans/current-task.md` | Shell loop | Current issue handoff to Codex |
| `plans/task-status.json` | Codex | Machine-readable result for the current issue |
| `plans/overnight-progress.md` | Shell loop/Claude | Checkpoint history |
| `plans/decisions-log.md` | Shell loop/Codex/Claude | Durable decisions |
| `plans/blockers-log.md` | Shell loop/Claude | User or engineering blockers |
| `plans/codex-worker-inbox.md` | Claude/shell loop/Codex | Sanitized repair queue for non-user blockers, consumed by the shell loop before product issues |
| `plans/sleep-run-budget.md` | Shell loop/Codex | DeepSeek/Sapling run budget notes |
| `plans/STOP-OVERNIGHT.txt` | User/Claude in emergency | Stop unattended execution |
| `plans/MONEY-MADE.txt` | User | Signal that real revenue was confirmed and the current unattended loop should stop for review |

`plans/MONEY-MADE.txt` is a stop signal for the current unattended loop. It is not a substitute for the API/MCP readiness gates if those remain open.

## API and Job Contracts

Claude monitor contract:

- Read `docs/commercialization-north-star.md`.
- Read `plans/supervisor-handoff.md`.
- Check loop liveness with process state and `plans/overnight.log` modification time.
- Alert if `plans/overnight.log` has not updated for more than 20 minutes while no stop signal exists.
- Count issue statuses from `plans/issue-board.md`.
- Scan recent `plans/blockers-log.md` entries.
- Read recent `origin/main` commits.
- Check for `plans/MONEY-MADE.txt` and `plans/STOP-OVERNIGHT.txt`.
- Append a concise checkpoint to `plans/overnight-progress.md`.
- Append a structured pending item to `plans/codex-worker-inbox.md` for each new actionable non-user blocker, after checking for duplicates.
- Restart the shell loop only when it is dead or stale, no stop signal exists, and pending work remains.
- Exit within 3 to 5 minutes.

Codex executor contract:

- Run a permission gate before assigning or executing work. Classify the task as autonomous, provider/user-permission, paid-resource, secret/dashboard, or workspace-race sensitive. User-only actions stay with the owner; provider or sandbox failures become documented blockers or repair items; source-code and docs defects can proceed autonomously.
- Run a work-allocation gate before splitting work. A strong model with enough context should receive one coherent end-to-end task when the task shares product intent, data contracts, UI behavior, or deployment state. Do not split a coherent task merely to create parallel agents; the repeated context transfer costs tokens, loses prompt/context-cache efficiency, and increases integration risk.
- Split or parallelize only when tasks are truly independent, have separate files or worktrees, have clear interfaces, and can be merged without one agent needing another agent's private reasoning. Good split examples are isolated frontend polish and backend telemetry work with no shared files. Bad split examples are one feature's API, schema, UI, and tests when the acceptance criteria need one consistent design.
- Before selecting a new issue-board item, check `plans/codex-worker-inbox.md` and process one pending non-user repair item if present.
- Read `plans/current-task.md` for the scoped issue.
- Keep the current issue scope unless a safety or correctness dependency is required.
- Use this document to avoid optimizing for closed issues while missing commercial readiness.
- Do not use git or `gh` inside the Codex implementation step; the shell loop owns git/GitHub.
- Write `plans/task-status.json` with the agreed schema.

Repair inbox contract:

- Claude and the shell supervisor append structured non-user blockers to `plans/codex-worker-inbox.md`.
- The shell supervisor processes one pending item at a time before product issue work.
- Do not infer normal repair tasks from `plans/overnight-progress.md`; that file is for people.
- Use `plans/issue-board.md`, `plans/blockers-log.md`, and `plans/overnight.log` only as evidence for a queued item, except for the special case of restarting a dead loop when no stop signal exists and pending work remains.
- Fix only non-user blockers: provider retry/routing, prerequisite cleanup, CI failures, dirty-repo loop bugs, documentation gaps, or scoped engineering defects.
- Do not process real-money tests, npm publication, provider dashboard changes, missing secrets, legal decisions, or product decisions as autonomous fixes.
- Record the PR, commit, verification, or not-actionable reason back in the inbox item.

Codex scheduled watchdog contract:

- Do not process ordinary repair inbox items while the shell loop is alive and `plans/overnight.log` is fresh.
- If the loop is dead or stale, no stop signal exists, and pending issue-board or inbox work remains, restart it with the documented `screen` command and record the restart.
- If repeated restart attempts fail because the loop itself is broken, open a scoped repair PR from a clean worktree or mark the inbox item waiting on a true user action. Do not race a live shell loop.

Shell supervisor contract:

- Own branch creation, git status checks, commit, push, PR creation, CI polling, merge, issue close, and board updates.
- Check `plans/codex-worker-inbox.md` before `plans/issue-board.md`.
- Append repair inbox items for non-user failures such as missing `plans/task-status.json`, Codex aborts, no-change results, Git/dirty-repo failures, PR creation/merge failures, and CI failures.
- Use `GH_TOKEN` or `GITHUB_TOKEN` safely without sourcing arbitrary env files.
- Stop on `plans/STOP-OVERNIGHT.txt`, `plans/MONEY-MADE.txt`, or no pending work.

## State and Error Handling

The run states are:

- `running`: shell loop is active and `overnight.log` is updating.
- `stalled`: no log update for more than 20 minutes or no supervisor process is present.
- `repair_queued`: Claude or the shell supervisor has written a pending non-user blocker to `plans/codex-worker-inbox.md`.
- `repairing`: the shell supervisor is handling exactly one inbox item through the normal Codex implementation protocol.
- `blocked`: issue board or blockers log contains a user-only or engineering blocker.
- `needs_user`: live payment, npm token, provider dashboard action, or secret update is required.
- `money_made`: `plans/MONEY-MADE.txt` exists; stop unattended work and alert the user.
- `stopped`: `plans/STOP-OVERNIGHT.txt` exists or no pending work remains.

Error policy:

- Repeated CI failures should be summarized in `plans/blockers-log.md`.
- Dirty worktree or checkout failures should be treated as loop health issues, not as product progress.
- Signal-unavailable rewrite eval results cannot be used as launch-quality proof.
- Real-money tests, refunds, npm publishing, and production secret changes require user involvement.

## Security and Privacy

- Do not log secret values.
- Do not paste `.env.local`, `.dev.vars`, API keys, private keys, Stripe secrets, GitHub tokens, Entra secrets, OpenAI/DeepSeek keys, or Sapling keys into docs, PR bodies, comments, or chat.
- Do not run live Stripe charges automatically.
- Do not publish npm packages automatically.
- Keep raw user rewrite text hidden from admin views unless the relevant admin flag and user-approved debugging context allow it.

## Rollout Plan

1. Stabilize the autonomous loop and monitor handoff.
2. Finish live consumer readiness: auth, `/app`, rewrite, quota, billing, webhook, monitoring, support, and rollback.
3. Produce a clean measured rewrite-quality evaluation with no unavailable-signal launch claim.
4. Confirm real account flow with explicit user involvement for live payment/refund.
5. Complete API key and B2B endpoint work.
6. Complete MCP server tools and install docs after the API is callable.
7. Prepare advertising/demo assets only after the consumer flow and rewrite claims have current verification evidence.

## Verification Plan

Minimum consumer launch checks:

- Public routes return healthy responses on `replyinmyvoice.com`.
- Sign-up/sign-in works for Google and the chosen email flow.
- Authenticated `/app` rewrite succeeds and charges quota only after quality success.
- Free quota, paywall, Stripe checkout, webhook, paid quota, and billing portal work in the intended live/sandbox mode.
- `npm run lint`, `npm run typecheck`, `npm run test`, build, CI, and deployment are green.
- Banned-term scan over user-facing source and `lib/**` is clean.
- Sentry/PostHog or chosen observability path is configured enough to detect launch failures.

Minimum rewrite-quality checks:

- A current measured eval report exists.
- A current internal Rewrite Quality Analysis report exists, generated from `RewriteCostLog` / `RewriteProviderCall` or a documented fixture if production data is not available.
- Average signal drop and below-threshold counts are based on measured cases, not unavailable scores.
- Known sample failures are either fixed or explicitly marked as non-launch blockers with rationale.
- Facts, money, dates, names, deadlines, and no-change-without-confirmation constraints are preserved.
- Cost per successful rewrite, quality-failure rate, provider failure rate, duration p50/p95, escalation rate, and strategy-version comparison are visible in the owner report.

Minimum developer/API/MCP checks:

- Users can create, revoke, and view API keys.
- `POST /api/v1/rewrite` authenticates with API keys and enforces per-key quota/rate behavior.
- OpenAPI docs or equivalent developer docs exist.
- MCP server exposes working tools backed by the API.
- MCP install instructions are verified locally before public distribution.

## Open Questions

- Whether email sign-up should be implemented through Entra External ID user flows or a separate first-party email-code flow.
- Whether API/B2B launch must be simultaneous with first paid consumer launch, or can follow after the first money-made checkpoint.
- Whether the first public MCP package should be published to npm immediately or distributed from GitHub until API billing is proven.
