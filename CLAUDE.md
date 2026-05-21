# CLAUDE.md — Claude Code instructions for /Users/qc/Desktop/CloudFlare

## Source Of Truth

`AGENTS.md` in this same directory is the single source of truth for this project's policies, product decisions, deployment rules, and skill triggers.

```text
/Users/qc/Desktop/CloudFlare/AGENTS.md
```

Claude Code must read `AGENTS.md` before doing any non-trivial planning, implementation, review, or deployment work in this repository, and must follow every rule in it. This `CLAUDE.md` exists only to:

1. Tell Claude Code which file is authoritative.
2. Repeat the required skill triggers verbatim so Claude Code can route work to the correct skill even before `AGENTS.md` is fully loaded.
3. Mirror the most load-bearing operational rules that Claude Code must not violate on a first turn.

If anything in this file ever appears to disagree with `AGENTS.md`, `AGENTS.md` wins. Update `CLAUDE.md` to match, do not act on the stale copy.

## Skill Policy

This project uses real, reusable agent skills whose project source lives in `agent-skills/` and may also be mirrored in Codex or Claude global skill directories:

```text
/Users/qc/Desktop/CloudFlare/agent-skills
/Users/qc/.codex/skills
/Users/qc/.claude/skills
```

The eight required skills are:

```text
system-spec-synthesis
resilience-test-generation
state-machine-modeling
data-module-review
dotnet-backend-testing
ui-browser-testing
cloud-architecture-cost-review
claude-heavy-planning-handoff
```

If the current session has not indexed a newly created skill yet, read the project source at `agent-skills/<skill-name>/SKILL.md` and follow it as the fallback. Both Codex and Claude Code are expected to use these skills — `claude-heavy-planning-handoff` is not a Codex-only handoff path; Claude Code may invoke it directly when the task fits.

### Required Skill Triggers

Use `system-spec-synthesis` when:

```text
The task asks for a system spec, architecture summary, implementation-ready requirements, API/data/job contract design, Azure backend planning, or converting loose product notes into an executable engineering plan.
```

Use `resilience-test-generation` when:

```text
The task changes or tests retries, timeouts, rate limits, provider failures, Stripe webhook replay, Azure Service Bus redelivery, OpenAI/Sapling failures, quota races, idempotency, or recovery behavior.
```

Use `state-machine-modeling` when:

```text
The task changes or reviews subscription states, free/paid quota states, usage reservations, rewrite attempts, queue jobs, webhook lifecycle, deployment lifecycle, or any multi-step workflow with statuses and transitions.
```

Use `data-module-review` when:

```text
The task changes or reviews Prisma schema, Entity Framework models, migrations, data access services, usage counters, idempotency tables, transactions, indexes, or persistence invariants.
```

Use `dotnet-backend-testing` when:

```text
The task adds, changes, reviews, or explains C#/.NET backend tests, xUnit tests, ASP.NET Core API integration tests, WebApplicationFactory tests, EF Core transaction tests, provider fakes, webhook tests, queue/worker tests, or CI dotnet test coverage.
```

Use `ui-browser-testing` when:

```text
The task adds, changes, reviews, or debugs frontend UI, Playwright tests, browser flows, screenshots, responsive layout, visual regressions, auth redirects, forms, navigation, console/network errors, or local webpage verification.
```

Use `cloud-architecture-cost-review` when:

```text
The task chooses, changes, reviews, or deploys cloud architecture, Azure/Cloudflare hosting, databases, queues, workers, CI/CD deployment targets, monthly run-rate cost, scale-to-zero behavior, or any plan that may create paid infrastructure or increase AI/provider costs.
```

Use `claude-heavy-planning-handoff` when:

```text
The task is broad enough to span more than three modules or services, requires architecture planning before implementation, changes Azure/backend/auth/billing/queue architecture, or should be routed from Codex to Claude Code for heavy planning.
```

### Interview Demo Prompts

These exact prompts demonstrate the skills without touching production systems:

```text
Use cloud-architecture-cost-review to compare Azure App Service, Azure Functions, and Container Apps for the .NET rewrite backend before implementation.
Use system-spec-synthesis to turn the current Azure backend migration notes into an implementation-ready system spec.
Use resilience-test-generation to design tests for the rewrite request flow when OpenAI fails after quota reservation.
Use state-machine-modeling to model subscription status, free quota, paid quota, and rewrite reservation lifecycle.
Use data-module-review to review the quota, usage reservation, and Stripe event persistence model in this repo.
Use dotnet-backend-testing to design xUnit and ASP.NET Core integration tests for quota reservation, Stripe webhook replay, and worker job finalization.
Use ui-browser-testing to verify the /app workspace with Playwright, desktop/mobile screenshots, and console/network review.
Use claude-heavy-planning-handoff to prepare a Claude Code planning brief for migrating the rewrite backend to Azure App Service, Azure Service Bus, and a .NET worker.
```

## Operational Rules Claude Code Must Not Violate

These are repeated from `AGENTS.md` because violating them on the first turn is unrecoverable. The full versions live in `AGENTS.md`.

### Working Directory

Work only inside this folder unless the user explicitly says otherwise:

```text
/Users/qc/Desktop/CloudFlare
```

Do not create a second unrelated app folder. Do not move the project elsewhere.

### Secrets

Never print, quote, summarize, commit, or expose secret values from:

```text
/Users/qc/Desktop/CloudFlare/.env.local
/Users/qc/Desktop/CloudFlare/.dev.vars
/Users/qc/Desktop/CloudFlare/globalapikey/
```

Validate required secrets at runtime in the handler that uses them, not at module import.

### Banned Terms

Never use these substrings in user-facing copy, metadata, route labels, marketing sections, UI text, or — because the grep also scans `lib/**` — in internal prompts, comments, helper names, filenames, or constants:

```text
humanizer
bypass
undetect
detector
evade
```

Run before completion:

```bash
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

### Deployment Cutover

Production cutover of `replyinmyvoice.com` is gated by `LAUNCH_CONFIRMED` in `.env.local`. Do not modify DNS, the `replyinmyvoice` Cloudflare Pages project's custom domain, or live routing unless `LAUNCH_CONFIRMED=true` and Worker preview verification has passed. Keep Stripe in sandbox mode. Worker name is `replyinmyvoice-app`.

### Autonomous Long-Run Target

If the user explicitly starts the long autonomous Azure backend run, the source of truth is:

```text
/Users/qc/Desktop/CloudFlare/docs/dotnet-azure-full-run-target.md
```

Do not split that run into staged Phase 1 / Phase 2 work. Ordinary build, test, package, Azure CLI, migration, App Service, Service Bus, and worker packaging errors are not stop conditions — investigate, fix, and continue. Stop only for the explicit conditions listed in `AGENTS.md` (dashboard-only actions impossible from local, denied SSH/API permissions, invalid credentials, real paid/live financial action required, or work that would require exposing secrets).

## When To Re-Read AGENTS.md

Re-read `AGENTS.md` whenever:

- The task touches deployment, Stripe, Clerk, Neon, Prisma, Sapling, Cloudflare Workers, OpenNext, or the rewrite engine.
- The user references a phase (preflight, launch cutover, workspace V2, diagnosis-driven rewrite, no-bad-result gate, etc.).
- A skill trigger fires — the corresponding `AGENTS.md` section likely has additional concrete decisions for that area.
- You are about to commit, push, or deploy.

`AGENTS.md` is long but stable. Treat it like a contract, not optional reading.

## Supervisor Mode (Codex MCP)

When the `codex` MCP server is available in this session (check via available tools — look for `mcp__codex__*`), Claude Code operates in **Supervisor Mode**. The role is tech lead / planner. Claude Code does NOT write or modify source code directly. All code changes are delegated to Codex via the `codex` MCP tools.

This mode is enforced at the permission layer (`~/.claude/settings.json` denies `Edit`, `Write`, `NotebookEdit`, and dangerous shell rewrites). If those tools are denied, this section is in effect. If they are allowed (no codex MCP, or user explicitly disabled), ignore this section.

Full details live in `/Users/qc/Desktop/CloudFlare/codex-supervisor/SUPERVISOR.md`. The load-bearing rules are mirrored below so Claude Code cannot violate them on a first turn.

### Hard Rules

1. **Never use `Edit`, `Write`, or `NotebookEdit` on source files.** Permission layer will block these anyway, but do not attempt them. The only files Claude Code may write directly are planning / spec markdown under `docs/`, `plans/`, or `codex-supervisor/`, and throwaway scratch notes in the session outputs folder. Claude Code may freely **read** any file (Read, Grep, Glob) to understand the codebase before delegating.

2. **All code changes go through `mcp__codex__*` tools.** When a task requires editing a `.ts`, `.tsx`, `.js`, `.cs`, `.py`, `.json`, Prisma schema, SQL migration, Dockerfile, GitHub Actions workflow, or any other source artifact, call the `codex` MCP tool with a self-contained task brief.

3. **No shell-based edits.** Do not use `bash` with `sed`, `awk`, `tee`, heredoc redirects (`>`, `>>`), `git apply`, or similar to indirectly modify source. Bash is allowed for read-only inspection (`ls`, `cat`, `grep`, `git log`, `git diff`, `git status`, `npm test`, `dotnet test`).

4. **No `git commit` or `git push`.** Permission layer denies them. The human reviews diffs and pushes.

### Workflow Per Request

1. **Understand.** Read relevant files. State back what was understood and which files are involved.
2. **Plan.** Write a short plan (3–8 bullets) describing the change. For non-trivial work, save it as `plans/<slug>.md`.
3. **Delegate.** Call the `codex` MCP tool with a brief containing: goal, files to touch (absolute paths), `AGENTS.md` constraints that apply (especially banned terms `humanizer/bypass/undetect/detector/evade`), acceptance criteria, and any required interface signatures.
4. **Review.** When Codex returns, read the diff. Check against the plan, banned terms, secrets policy, and test coverage.
5. **Iterate or accept.** If wrong, re-delegate with corrections. Do not "just fix it yourself" — that breaks the supervisor contract and the permission layer will block it anyway.
6. **Verify.** Run tests / linters / builds via `bash` (read-only commands). Report results to the user.

### Codex Brief Template

Codex starts with zero conversation context. Every delegation must be self-contained:

```text
TASK: <one-line goal>

CONTEXT:
- Repo root: /Users/qc/Desktop/CloudFlare
- Relevant files (read first): <abs paths>
- Project rules: AGENTS.md sections on <X>, <Y>

CONSTRAINTS:
- Banned terms (CI grep guard): humanizer, bypass, undetect, detector, evade
- No secrets in source. Validate env vars at runtime.
- Do not run deploy commands (wrangler deploy, etc.).

CHANGES REQUIRED:
1. <specific change with file + function>
2. ...

ACCEPTANCE:
- <test that must pass>
- <behavior preserved>

DO NOT:
- Touch <files out of scope>
```

### When To Push Back

If the user asks Claude Code to "just edit this one line," remind them supervisor mode is active and offer to either (a) delegate even the one-liner to Codex, or (b) have them temporarily disable supervisor by commenting out the `deny` block in `~/.claude/settings.json`. Do not silently bypass.

### Skills Still Apply

Supervisor mode is orthogonal to the Skill Policy above. Continue invoking the eight required skills when their triggers fire — but the implementation phase of those skills should produce a Codex brief, not direct edits. For example, `data-module-review` reads and analyzes (allowed); if it recommends a schema change, that change is delegated to Codex.
