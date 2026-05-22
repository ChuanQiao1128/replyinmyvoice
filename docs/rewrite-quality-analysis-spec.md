# Rewrite Quality Analysis Specification

Date: 2026-05-22
Status: implementation target

## Context

Rewrite Quality Analysis is an internal owner and operations tool. It is not a user-facing feature. Its purpose is to answer whether the rewrite system is improving, where it fails, how much it costs, and which strategy version should be promoted, paused, or repaired.

The first version should be an offline report, not an admin UI. The report is easier to inspect in interviews, cheaper to build, safer for private rewrite text, and sufficient for canary and cost decisions.

## Goals

- Show product quality: total requests, successful rewrites, success rate, quality-failure rate, average signal drop, below-threshold rate, no-regression rate, and fact-failure count.
- Show failure reasons: `signal_unavailable`, `naturalness_gate_failed`, `fact_check_failed`, `reviewer_threshold_failed`, `server_failed`, and provider-specific error codes.
- Show cost and efficiency: average cost per successful rewrite, total estimated provider cost, LLM token cost, Sapling call count and cost, duration p50/p95, escalation rate, and average internal strategies tried.
- Show strategy performance: success rate, signal drop, cost per success, quality-failure rate, and escalation rate grouped by `strategyVersion`.
- Give the owner an operating runbook for generation, interpretation, privacy review, and rollout decisions in `docs/rewrite-quality-analysis-runbook.md`.
- Produce owner-readable artifacts:
  - `docs/rewrite-quality-analysis-report.md`
  - `exports/rewrite-quality-summary.csv`
  - `exports/charts/*.png`

## Non-Goals

- Do not build an admin UI in the first version.
- Do not expose raw user rewrite text in the report.
- Do not use unavailable writing-signal results as proof that the quality target was met.
- Do not make the report a launch claim by itself; it is internal evidence for owner and engineering decisions.
- Do not let report generation mutate production prompts, strategy versions, billing state, quota state, or user data.

## Current System

The current schema already has the core telemetry tables:

- `RewriteCostLog` records request-level status, `strategyVersion`, scenario, tone, duration, draft/rewrite signal scores, internal strategy counts, escalation flag, token counts, Sapling counts, and estimated costs.
- `RewriteProviderCall` records provider-call details including provider, role, model, tokens or characters, estimated cost, latency, success, and `errorCode`.
- `RewriteLearningSample` records learning-oriented sample details, diagnosis tags, status, and `errorCode`.

M5-002 is responsible for writing these logs from the production rewrite pipeline. Rewrite Quality Analysis starts after M5-002 lands.

## Proposed Architecture

The first implementation is a local/offline analysis job:

```text
Python script -> read-only SQL query -> pandas aggregation -> Markdown + CSV + PNG charts
```

Target command:

```bash
python3 scripts/analyze_rewrite_quality.py \
  --since 2026-05-01 \
  --until 2026-05-22 \
  --output docs/rewrite-quality-analysis-report.md \
  --summary-csv exports/rewrite-quality-summary.csv \
  --charts-dir exports/charts
```

The script should:

1. connect with a read-only database URL from environment;
2. query `RewriteCostLog` and `RewriteProviderCall`;
3. compute summary metrics and per-day/per-strategy aggregates;
4. render six charts;
5. write a concise Markdown report with top risks and next recommended fixes.

## Data Model

The first version should use existing columns where possible.

Required request-level fields:

| Metric group | Existing source |
| --- | --- |
| Request volume and status | `RewriteCostLog.status`, `createdAt` |
| Failure reason | `RewriteCostLog.errorCode`, provider-call `errorCode` |
| Strategy comparison | `RewriteCostLog.strategyVersion` |
| Naturalness improvement | `draftAiLikePercent`, `rewriteAiLikePercent`, `changePoints` |
| Cost | `openAiCostUsd`, `saplingCostUsd`, `totalEstimatedCostUsd` |
| Efficiency | `durationMs`, `internalStrategies`, `usedEscalation` |
| Provider details | `RewriteProviderCall` grouped by `provider`, `role`, `model` |

If M5-002 does not persist enough detail for root-cause diagnosis, add a later schema-compatible enrichment. Preferred first enrichment: make `RewriteCostLog.errorCode` hold the primary machine-readable failure reason, and use provider calls for provider-specific details. Do not add raw rewrite text to the analysis output.

## API and Job Contracts

`scripts/analyze_rewrite_quality.py` should support:

| Argument | Required | Purpose |
| --- | --- | --- |
| `--since YYYY-MM-DD` | no | inclusive report start date |
| `--until YYYY-MM-DD` | no | exclusive report end date |
| `--output PATH` | no | Markdown report path |
| `--summary-csv PATH` | no | CSV summary path |
| `--charts-dir PATH` | no | chart output directory |
| `--database-url-env NAME` | no | environment variable name, default `DATABASE_URL` |

Output artifacts:

- Markdown report with totals, quality, cost, strategy comparison, and top risks.
- CSV file with metric name, segment, value, and period.
- Six PNG charts:
  - daily rewrite volume;
  - success/failure breakdown;
  - signal improvement distribution;
  - draft vs rewrite signal scatter;
  - cost per successful rewrite over time;
  - strategy version comparison.

## State and Error Handling

- Empty dataset: write a report that says no matching rows were found and exit 0.
- Missing database URL: fail with a clear message and do not create misleading artifacts.
- Missing optional columns: fail with a schema-readiness message that points to M5-002/M5-004.
- Rows with unavailable signals: count them separately as `signal_unavailable`; exclude them from average signal-drop calculations.
- Division by zero: render `n/a`, not `0`, when there are no successful rewrites.
- Partial provider-call data: still render request-level metrics and mark provider-cost sections as partial.

## Security and Privacy

- The script must not print database URLs or secrets.
- The report must not include raw message text, rough drafts, rewritten text, email addresses, or user identifiers.
- Aggregations should use status, scenario, strategy version, provider, model, and date buckets only.
- Outputs under `exports/` are local artifacts; do not assume they are safe to publish without review.

## Rollout Plan

1. Finish M5-002 telemetry persistence.
2. Add `scripts/analyze_rewrite_quality.py` with fixture-backed tests.
3. Generate the first local report and commit only non-sensitive summary artifacts.
4. If failure reasons are too coarse, enrich `RewriteCostLog.errorCode` and provider-call error mapping.
5. Use the report to decide whether strategy versions should continue, roll back, or get targeted fixes.
6. Build an admin UI later only after the offline report proves the metric set is useful.

## Verification Plan

- Unit test aggregation on a fixture dataset covering success, quality failure, server failure, unavailable signal, worse-than-draft signal, escalation, and two strategy versions.
- Unit test that empty input writes a clear empty report.
- Unit test that raw text and user identifiers are not emitted.
- Local command generates Markdown, CSV, and six PNG files.
- If production data is used, verify report counts against direct SQL row counts for the selected period.

## Open Questions

- Which read-only database URL name should production operators use for analysis: `DATABASE_URL` or a dedicated `ANALYTICS_DATABASE_URL`.
- Whether `fact_check_failed` should be stored directly in `RewriteCostLog.errorCode` or normalized from existing quality-error classes.
- Whether committed example reports should use synthetic fixture data only, with real production reports kept local.
