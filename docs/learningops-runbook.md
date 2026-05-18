# LearningOps Runbook

Date: 2026-05-18

## Purpose

LearningOps turns real rewrite outcomes into controlled strategy improvements.

It is not a live prompt hot-update system. Production behavior changes only after code/test changes are verified, pushed to GitHub, and deployed to Cloudflare.

## Source Of Learning

The source of truth is the production database table:

```text
RewriteLearningSample
```

The app writes rows when:

- a rewrite succeeds,
- a quality-gate failure occurs.

The scheduled job reads those rows and creates:

```text
LearningRun
LearningFinding
StrategyCandidate
docs/rewrite-memory-digest.md
```

## Daily Policy

```text
Run every 24 hours automatically.
Push/deploy automatically only when a qualified strategy promotion passes all gates.
Never deploy just because the scheduled job ran.
```

## Current Command

```bash
npm run learningops:run
```

The command:

1. loads local environment values without printing them,
2. reads recent `RewriteLearningSample` rows,
3. analyzes repeated failures and repair successes,
4. writes one `LearningRun`,
5. writes `LearningFinding` rows,
6. writes `StrategyCandidate` rows,
7. updates `docs/rewrite-memory-digest.md`,
8. prints safe summary counts only.

## Promotion Decisions

### `digest_only`

No strong learning signal exists.

Action:

- update digest/report only
- do not push/deploy production strategy changes

### `docs_only`

There is useful learning, usually repeated repair success, but no code change is justified yet.

Action:

- update strategy memory docs if useful
- do not deploy production strategy changes

### `promoted_candidate`

There is a strong repeated pattern or severe reproducible regression.

Action:

- add or update an eval/regression case,
- update rewrite/repair code or prompt guardrails,
- run validation,
- push and deploy only if all gates pass.

### `blocked`

The job could not make a safe promotion.

Action:

- write the blocker,
- do not deploy.

## Validation Gate

Before any LearningOps strategy promotion deploys:

```bash
npm run typecheck
npm run lint
npm run test
npm run build
npm run cf:build
```

For rewrite-quality changes, also run the relevant scenario evaluation command.

## Safety Rules

- Do not print secrets.
- Do not expose raw user samples publicly.
- Do not use user samples in marketing.
- Do not modify Stripe, Clerk, DNS, pricing, payment behavior, or unrelated product behavior.
- Do not promote one weak sample.
- Severe one-sample regressions need a reproducible eval/regression case before deployment.
- Do not add a database table that production reads as live prompt rules unless a future reviewed release mechanism is designed.
