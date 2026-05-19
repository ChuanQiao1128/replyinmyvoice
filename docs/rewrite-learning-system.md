# Rewrite Learning System

Date: 2026-05-18

## Goal

Reply In My Voice should improve from measured rewrite outcomes instead of relying on a fixed prompt forever.

The system should learn which rewrite and repair strategies work for real communication scenarios while still protecting product quality, user privacy expectations, and production stability.

## Confirmed Product Decisions

- No user feedback buttons in this phase.
- The production request must still do request-time improvement:
  - diagnose
  - rewrite
  - measure
  - repair if needed
  - select only a passing candidate
- Add an offline Strategy Memory Agent for learning from saved internal cases and evaluation results.
- Internal learning samples may store submitted drafts, optional context, rewritten text, diagnosis tags, repair metadata, and writing-signal results for quality improvement.
- The Privacy page must disclose that submitted content and rewrites may be stored and reviewed internally to improve quality.
- Do not expose internal learning samples publicly.
- Do not let production prompts self-modify automatically from one live sample.
- Learned strategies must become active only through a code change, tests, GitHub push, and Cloudflare deploy. Do not hot-load live rewrite strategy from database rows.

## Architecture

### Request-Time Loop

Production rewrite requests run a bounded loop:

```text
measure draft
extract facts
classify scenario
load style card
generate three candidates
review and select
finalize
run deterministic and LLM fact gates
measure final candidate
run one strong-model escalation if the Naturalness Check gate misses
return quality failure with no charge if the escalated result still misses the fact or Naturalness Check gate
log learning sample
```

This is the part that feels "real-time smarter" to the user. If the first final candidate is bad, the request does not end there; a bounded escalation tries again from facts and style guidance. Sapling/Naturalness Check scores are not fed into prompts and are not used to rank candidates; they are a final reference gate.

### Offline Strategy Memory Agent

The Strategy Memory Agent is an offline maintenance process, not a customer-facing chat agent.

Responsibilities:

- read stored rewrite learning samples
- aggregate pass/fail by scenario, tone, diagnosis tag, and repair usage
- identify repeated failures
- identify repair patterns that worked
- write a digest for review
- suggest which lessons should be promoted to production guardrails, tests, or fallback logic

The first implementation can be deterministic and local:

```text
npm run memory:rewrite
```

It should write:

```text
docs/rewrite-memory-digest.md
```

Later, this can become a model-assisted internal agent that proposes updates to:

```text
docs/rewrite-strategy-memory.md
lib/rewrite-diagnosis.ts
lib/openai.ts
tests/unit/*
evals/*
```

### Promotion Workflow

Do not promote a strategy directly from one live sample into production.

Do not make production behavior change by reading a new strategy rule from the database at request time. The database is the learning source and audit trail, not the live prompt-control surface.

Promotion path:

1. Store the learning sample.
2. Strategy Memory Agent identifies a repeated pattern or severe regression.
3. Add or update a documented strategy in `docs/rewrite-strategy-memory.md`.
4. Add an evaluation case or unit test.
5. Update prompt guardrails, repair instructions, or fallback logic.
6. Run tests and scenario evaluation.
7. Commit and push the verified code change.
8. Deploy only if quality gates pass.

Required promotion shape:

```text
RewriteLearningSample -> analysis -> strategy candidate -> code/test change -> validation -> push -> deploy
```

Forbidden promotion shape:

```text
RewriteLearningSample -> database rule -> production prompt changes immediately
```

### LearningOps V1 Direction

The next system iteration should turn the current script-based memory workflow into a first-class internal LearningOps pipeline.

Recommended V1 components:

- `LearningRun`
  - records each daily/offline learning job
  - stores sample counts, findings count, status, validation result, and promotion decision
- `LearningFinding`
  - records repeated failure patterns or successful repair patterns
  - groups by scenario, tone, diagnosis tags, status, and signal outcome
- `StrategyCandidate`
  - records proposed changes before they become code
  - includes evidence, risk level, required eval/test coverage, and promotion status

The production rewrite API should continue using code-based strategy modules such as rewrite diagnosis, scenario guardrails, OpenAI prompt construction, repair logic, and fallback logic. Strategy candidates are promoted into those modules only after validation.

The scheduled job may use Codex or another local agent runner as the execution backend, but the source of learning is the production database.

## Data Stored For Learning

For each rewrite attempt, store:

- user id
- scenario
- tone preset
- optional context/message
- rough draft
- rewritten text when available
- draft AI-like signal
- rewrite AI-like signal
- signal change
- diagnosis tags
- rewrite plan summary
- candidate signal statuses and rejection reasons
- internal strategy count
- repair count
- quality status
- error code for safe-failure cases

## Privacy And Safety

The product should disclose internal quality storage in Privacy.

Rules:

- Do not store payment details.
- Do not expose learning samples in the public UI.
- Do not use learning samples in marketing without explicit user approval.
- Do not use user content to automatically self-modify production prompts.
- Keep the existing in-app reminder telling users not to paste passwords, payment details, government identifiers, or highly sensitive personal information.

## Success Criteria

- Successful rewrites and quality-gate failures are logged as internal learning samples.
- Learning logging failure never breaks the user rewrite request.
- `npm run memory:rewrite` creates a useful digest from stored samples.
- Privacy page discloses internal quality storage.
- Strategy memory docs explain how learning becomes production improvements.
- A rewrite request should return a quality-failure/no-charge response if the bounded `fact_reconstruct` workflow cannot produce a fact-safe result that satisfies the Naturalness Check quality bar.
- Live regressions that expose missing fact preservation, such as the Priya `finance manager` case, must become tests plus strategy-memory updates before deployment.
- Evaluation reports must distinguish fact failures, Naturalness Check quality failures, signal-unavailable failures, and provider/server failures. In `fact_reconstruct`, a Naturalness Check miss is a product quality failure for that request and should not be charged as a successful rewrite.
- Reusable semantic-equivalence lessons from evaluation, such as `can't guarantee` -> `not promising` and `on hold` -> `paused`, must be promoted through tests and code-based normalization rather than being silently stored as live prompt rules.
- Typecheck, lint, unit tests, build, OpenNext build, and production smoke tests pass before deployment.

## 2026-05-19 Unified Rewrite Learning Run

The unified fact-preserving run promoted these learning outcomes:

- Removed user-facing scenario selection from `/app`; backend inference now supports the general reply workflow.
- Reduced visible tones to `Warm` and `Direct`.
- Added a unified fact extraction/gate layer for draft-only and optional-context usage.
- Expanded evaluation to 66 cases, including 44 draft-only cases.
- Final run result:
  - customer-usable pass count: 66/66
  - fact preservation or unsupported-addition failures: 0
  - final selected rewrites worse than draft: 0/66
  - strict signal pass count: 42/66
  - average AI-like signal drop: 50 points
- Learning conclusion at the time: the previous product-quality gate prioritized fact preservation, unsupported-fact prevention, and no worse selected signal. This has been superseded by the stricter Naturalness Check threshold rule plus fact gates.

## 2026-05-19 Fact Reconstruct Decision

The next rewrite engine is `fact_reconstruct`:

- Sapling is a final reference gate only, not prompt input and not a model optimization target.
- Default model roles are config-driven:
  - `cheap_structured`: fact extraction, scenario classification, reviewer, LLM fact check.
  - `mid_writer`: candidate generation and finalization.
  - `strong_escalation`: one bounded escalation when the first final misses the quality bar.
- Default threshold is `NATURALNESS_THRESHOLD=40`.
- If the draft starts above the threshold, the rewrite must finish at or below the threshold.
- If the draft starts at or below the threshold, the rewrite must not raise the signal.
- If Sapling is unavailable or the escalated result still misses the gate, the API returns quality failure and no usage charge.

## 2026-05-19 Fact-Reconstruct Eval Lessons

- The official rewrite engine is now the fact-reconstruct pipeline; the old `rewriteWithOptimization` production engine has been removed.
- The focused 40-case evaluation reached 38/40 strict passes, 40/40 measured outputs below 50% AI-like signal, and 0/40 selected rewrites worse than the draft.
- Parser normalization now filters placeholder fact values (`Not specified`, `unknown`, `N/A`, `none`) and boolean values inside fact arrays.
- The fallback layer now tries an extractive facts-first candidate before richer deterministic scenario fallbacks.
- Strategy updates from this run were encoded as tests rather than silently learned from live user content.
