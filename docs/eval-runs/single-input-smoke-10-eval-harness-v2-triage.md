# Single-Input Smoke 10 Eval Harness V2 Triage

Date: 2026-05-25

Report: `docs/eval-runs/single-input-smoke-10-eval-harness-v2.md`

## Run Scope

- Calibration run only.
- Corpus: `EVAL_CORPUS=email-100`.
- Mode: `smoke`.
- Cases evaluated: `rewrite-draft-001` through `rewrite-draft-010`.
- No cases `011-100` were materialized or evaluated.
- No focused/full mode was run.
- No old dual-input provider eval was run.
- Rewrite provider path remained the single-input compatibility mapping: `input_draft` -> `roughDraftReply`, empty auxiliary context fields, and `tonePreset: Warm`.

## Harness Changes Verified

- Semantic judge calls now use bounded retry/backoff.
- Legacy smoke eval writes per-case progress to `docs/eval-runs/single-input-smoke-10-eval-harness-v2-progress.json`.
- The final progress file contains all 10 case IDs.
- A no-leakage mock-provider contract test verifies corpus judge-only fields are not included in the rewrite provider payload.
- Eval-only unsupported-fact reporting now checks temporal and scheduling-option combinations, without changing the rewrite pipeline gate in Step A.
- Material quality regression is reported explicitly and blocks customer-usable pass.

## Summary

- Customer-usable pass count: 2/10.
- Strict signal pass count: 2/10.
- Fact preservation or unsupported-addition failures: 8.
- Quality regressions: 7/10.
- Average quality-score delta: -1.9 points.
- Rewrite below 50% AI-like signal: 10/10.
- Final selected rewrites worse than draft by signal: 1/10.

## Expected Step A Effects

- Pass count dropped from the corrected smoke baseline, which is acceptable for the harness-only pass.
- Case 002 now fails customer-usable pass because it has a material quality regression.
- Case 006 now reports a hard unsupported scheduling addition: `noon on Friday`.
- Case 009 now reports hard unsupported deadline additions: `today by 9 a.m` and `by 9 a.m`.
- Saved-output fixture tests also catch the previously missed case 006 Wednesday/Thursday 9 a.m. scheduling hallucinations and the case 009 invented `today by 9 a.m.` deadline.

## Remaining Failure Classification

- Primary class: rewrite pipeline issue. Warm rewrites still compress or drop boundary facts.
- Judge/report issue: improved for temporal/options additions; remaining refinement could de-duplicate related deadline phrases such as `today by 9 a.m.` and `by 9 a.m.`.
- Corpus issue: none found in this smoke window.
- Provider/transient issue: no final-run abort. Checkpoint/resume support is now in place for future interruptions.

## Step B Target

- Preserve locked facts from the draft before style changes.
- Reject unsupported dates, times, options, deadlines, and commitments during candidate selection.
- Restore missing boundary facts during repair.
- Return a minimal-edit rewrite instead of an unsafe summary when candidates fail hard gates.
