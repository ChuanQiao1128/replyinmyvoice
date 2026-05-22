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
Open draft pull requests only when a qualified strategy promotion passes all gates.
Never deploy from the scheduled LearningOps run.
```

## Cloudflare Scheduled Trigger

The production Worker has a Cloudflare Cron Trigger configured in `wrangler.jsonc`:

```text
0 13 * * *
```

The trigger runs once every 24 hours through `worker.js`, which wraps the OpenNext worker and calls the LearningOps pipeline from the scheduled handler.

The scheduled runtime:

1. reads `RewriteLearningSample` rows from the last 7 days,
2. writes one `LearningRun` row,
3. writes related `LearningFinding` and `StrategyCandidate` rows,
4. records a terminal run status: `digest_only`, `docs_only`, `promoted`, or `blocked`,
5. never deploys production code.

## Current Command

```bash
npm run learningops:run
```

The command:

1. loads local environment values without printing them,
2. reads `RewriteLearningSample` rows from the last 7 days,
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

### `promoted`

There is a strong repeated pattern or severe reproducible regression.

Action:

- create a `StrategyCandidate`,
- prepare draft PR work through the M2.5-005 promotion handoff,
- require eval/regression coverage before production code changes are merged,
- do not deploy from the scheduled run.

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

Deployment is a separate post-review action. The scheduled run itself stops at draft PR preparation.

## Canary Rollout

Production strategy promotions should be released with a canary version label
instead of sending all rewrite traffic to the new path immediately.

Required environment controls:

```text
REWRITE_STRATEGY_CANARY_ENABLED=true
REWRITE_STRATEGY_CANARY_VERSION=<new-strategy-version>
REWRITE_STRATEGY_CANARY_PERCENT=10
```

The runtime assigns traffic deterministically by user/request key. It compares
`RewriteCostLog` signal-change distributions for the control and canary strategy
versions over the recent window. After 24 hours or 200 measured rewrites, the
runtime pauses canary traffic when the canary average signal drop is lower, or
ramps to 25%, 50%, then 100% when the canary average signal drop is higher.

This uses existing database telemetry. Do not create a Cloudflare KV namespace
for this rollout unless a future release needs cross-service state that the
database cannot provide.

## Safety Rules

- Do not print secrets.
- Do not expose raw user samples publicly.
- Do not use user samples in marketing.
- Do not modify Stripe, Clerk, DNS, pricing, payment behavior, or unrelated product behavior.
- Do not promote one weak sample.
- Severe one-sample regressions need a reproducible eval/regression case before deployment.
- Do not add a database table that production reads as live prompt rules unless a future reviewed release mechanism is designed.
