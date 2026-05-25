# Single-Input Smoke 10 Pipeline Fix v1 Triage

Date: 2026-05-25
Source report: `docs/eval-runs/single-input-smoke-10-pipeline-fix-v1.md`

## Summary

- Customer-usable pass: 8/10.
- Strict signal pass: 9/10.
- Fact preservation or unsupported-addition failures: 1.
- Missing facts: 1 total.
- Forbidden-claim violations: 0.
- Unsupported temporal/options additions: 0.
- Quality regressions: 1/10.
- Average quality-score delta: +0.7.
- Average AI-like signal drop: 0 pts; all 10 rewrites were below 50%.

## Required Smoke Checks

- No judge-only leakage observed in the single-input rewrite path.
- No old dual-input eval path was run.
- No focused/full mode was run.
- Case 005 retained the approval-cycle and June 7 order-form boundaries.
- Case 006 did not invent Wednesday, Thursday 9 a.m., or any other scheduling option.
- Case 008 retained the report received, portal, non-clinician, cannot-interpret, Dr. Chen queue, and clinic-line boundaries.
- Case 009 did not invent a 9 a.m. photo deadline and retained the rent-credit/access-confirmation boundary.
- Case 010 retained the count of two cleanup volunteers.

## Remaining Failures

Case 002 fails customer-usable pass only because of a quality regression:

- Facts preserved: yes.
- Unsupported additions: none.
- Quality regression: yes, score 8 -> 5.
- Classification: prompt/style issue.
- Detail: the rewrite places the refund denial before the replacement path, making the message less warm and less aligned with the desired support flow.

Case 006 fails because one dependency was softened:

- Missing fact: sender will confirm the selected slot after Ren replies.
- Unsupported additions: none.
- Invented scheduling options: none.
- Classification: locked-fact extractor issue plus prompt issue.
- Detail: the rewrite says the sender will confirm the selected time, but drops the reply dependency in "as soon as you reply."

## Non-Failing Residual Risks

- Case 008 has a formatting split in `Dr.\n\nChen's queue`; facts passed, but this is a structural polish issue.
- Case 009 has `9 a.m. And noon`; facts passed, but sentence-casing around time ranges should be cleaned in a future style/format gate.

## Recommendation

Do not expand to cases 011-020 yet unless this 8/10 result is acceptable for the next calibration window. The next targeted fix should be small:

- Preserve reply-dependent confirmation clauses such as `after/as soon as you reply`.
- Add support-flow ordering guidance so positive replacement/remedy paths stay before hard refund-denial boundaries when both are present.
