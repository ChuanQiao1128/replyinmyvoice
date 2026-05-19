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
diagnose draft
create rewrite plan
generate candidate
measure candidate
repair rejected candidates
remeasure repair
select passing candidate
return best available complete candidate if strict gates do not pass
generate guaranteed facts-first fallback if no measured candidate is complete
log learning sample
```

This is the part that feels "real-time smarter" to the user. If the first candidate is bad, the request does not end there; the repair pass receives the failure reason and tries to fix that exact problem.

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
- A rewrite request should not show an empty quality-failure panel when a copyable facts-preserving candidate can be produced.
- Live regressions that expose missing fact preservation, such as the Priya `finance manager` case, must become tests plus strategy-memory updates before deployment.
- Evaluation reports must distinguish customer-usable pass from strict signal pass. A strict Sapling miss is still a learning signal, but it is not the same as a product failure when facts are preserved, no unsupported facts are added, and the selected rewrite is not worse than the draft.
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
- Learning conclusion: the current product-quality gate should prioritize fact preservation, unsupported-fact prevention, and no worse selected signal. Strict Sapling improvement remains an optimization target, but the score alone cannot decide whether a reply is usable.
