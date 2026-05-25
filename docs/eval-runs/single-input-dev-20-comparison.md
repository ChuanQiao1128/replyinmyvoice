# Single-Input Dev 20 Comparison

Date: 2026-05-25

| Run | Customer pass | Strict signal pass | Missing facts | Forbidden claims | Unsupported temporal/options additions | Quality regressions | Avg quality delta | Final signal failures over threshold |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Corrected smoke 10 baseline | 4/10 | 4/10 | 17 | 1 | Under-reported | Not separately gated | -1.2 | 0 |
| Step B smoke 10 | 8/10 | 9/10 | 1 | 0 | 0 | 1/10 | +0.7 | 0 |
| Dev 20 final v5 | 20/20 | 20/20 | 0 | 0 | 0 | 0/20 | +1.2 | 0 |

## Interpretation

Dev 20 passed the acceptance target after two general pipeline fixes and two general
fact-gate refinements. The result suggests the Step B locked-fact strategy generalized
to cases 011-020 once line-wrapped date extraction, conditional next-step dependencies,
time-abbreviation splitting, and short-project-name facts were handled.

## Acceptance Target Check

- Customer-usable pass >= 16/20: met at 20/20.
- Strict signal pass >= 18/20: met at 20/20.
- Missing facts <= 3: met at 0.
- Forbidden claims = 0: met.
- Unsupported temporal/options additions = 0: met.
- Average quality delta >= 0: met at +1.2.
- No repeated scheduling hallucination: met.
- No repeated medical/admin disclaimer loss: met.
- No repeated approval/refund/policy-boundary softening: met.

## Top Recurring Failure Class

Before the final v5 run, the recurring class was short-draft boundary loss caused by
line wrapping and abbreviation splitting rather than by the evaluator itself. The final
run did not have a recurring failure class.

## Recommendation

Expand next to controlled dev 40 by materializing cases 021-040. Do not run the full
100-case suite as the daily loop.
