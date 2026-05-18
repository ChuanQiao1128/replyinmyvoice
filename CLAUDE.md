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

This project uses real, reusable agent skills mirrored in three locations with identical content:

```text
/Users/qc/Desktop/CloudFlare/agent-skills
/Users/qc/.codex/skills
/Users/qc/.claude/skills
```

The five required skills are:

```text
system-spec-synthesis
resilience-test-generation
state-machine-modeling
data-module-review
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

Use `claude-heavy-planning-handoff` when:

```text
The task is broad enough to span more than three modules or services, requires architecture planning before implementation, changes Azure/backend/auth/billing/queue architecture, or should be routed from Codex to Claude Code for heavy planning.
```

### Interview Demo Prompts

These exact prompts demonstrate the skills without touching production systems:

```text
Use system-spec-synthesis to turn the current Azure backend migration notes into an implementation-ready system spec.
Use resilience-test-generation to design tests for the rewrite request flow when OpenAI fails after quota reservation.
Use state-machine-modeling to model subscription status, free quota, paid quota, and rewrite reservation lifecycle.
Use data-module-review to review the quota, usage reservation, and Stripe event persistence model in this repo.
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
