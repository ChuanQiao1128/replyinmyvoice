# Single-Input Dev 20 Pipeline Fix Triage

Date: 2026-05-25

Corpus: `docs/rewrite-email-eval-cases-100.md`

Command shape: `EVAL_CORPUS=email-100 npx tsx scripts/eval-scenarios.ts --mode=focused --limit=20`

## Initial Dev-20 Run

Initial report: `docs/eval-runs/single-input-dev-20-pipeline-fix-v1.md`

- Customer-usable pass: 16/20.
- Strict signal pass: 18/20.
- Missing facts: 4.
- Forbidden-claim violations: 1.
- Unsupported temporal/options additions: 0.
- Quality regressions: 4/20.
- Average quality-score delta: +0.6.

## Failure Classes

- `rewrite-draft-002`: support replacement path lost Lena, May 6 delivery, and address-confirmation dependency; also triggered a forbidden shipping-before-confirmation violation.
- `rewrite-draft-006`: valid scheduling facts were preserved, but the rewrite introduced detached sentence fragments around `a.m.` / `p.m.` abbreviations.
- `rewrite-draft-012`: rewrite degraded upload evidence by changing `empty file attempt` into a less precise message reference.
- `rewrite-draft-017`: conditional photo request was dropped when the rail had worsened.

## General Fixes Applied

- Preserve line-wrapped dates and confirmation dependencies during deterministic fact extraction.
- Lock conditional action dependencies such as `If ... please send ...`.
- Avoid splitting extractive fallback sentences at `a.m.` and `p.m.` abbreviations.
- Reject actual detached fragments such as uppercase `Because` / `Or` after time abbreviations while allowing valid lowercase continuations.
- Lock reusable phrases discovered in dev 20: `empty file attempt`, named project handoff phrases, and `room is unavailable`.
- Allow valid `If` action sentences after time abbreviations, such as `5 p.m. If the rail...`.

## Final Dev-20 Run

Final report: `docs/eval-runs/single-input-dev-20-pipeline-fix-v5.md`

- Customer-usable pass: 20/20.
- Strict signal pass: 20/20.
- Missing facts: 0.
- Forbidden-claim violations: 0.
- Unsupported temporal/options additions: 0.
- Quality regressions: 0/20.
- Average quality-score delta: +1.2.
- Final signal failures over threshold: 0.

## Acceptance

The final dev-20 run meets the acceptance target. Do not expand directly to 100. The
next useful step is a controlled 40-case extension using cases 021-040.
