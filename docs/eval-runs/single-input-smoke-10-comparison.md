# Single-Input Smoke 10 Comparison

Date: 2026-05-25

| Run | Customer pass | Strict signal pass | Missing facts | Forbidden claims | Unsupported temporal/options additions | Quality regressions | Avg quality delta |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Corrected smoke 10 baseline | 4/10 | 4/10 | 17 | 1 | Under-reported | Not separately gated | -1.2 |
| Eval harness v2 | 2/10 | 2/10 | Multiple hard failures across 8 cases | 1 | Case 006/009 now reported | 7/10 | -1.9 |
| Pipeline fix v1 | 8/10 | 9/10 | 1 | 0 | 0 | 1/10 | +0.7 |

## Interpretation

Step A made the report stricter: pass count dropped because quality regressions and temporal/options hallucinations were no longer hidden.

Step B changed the production rewrite path, not the judge keys. The main improvement came from input-derived locked facts, stronger Warm-tone preservation instructions, temporal/options unsupported detection in the pipeline gate, and candidate/fallback gate tightening.

## Remaining Failure Classes

- Prompt issue: case 002 places the refund boundary before the positive replacement path, causing a quality regression.
- Locked-fact extractor issue: case 006 does not yet lock the dependency that confirmation happens after/as soon as Ren replies.
- Candidate selection issue: no current hard failure, but selection should eventually account for support-flow ordering, not only fact safety.
- Judge/report issue: none observed in this run.
- Corpus issue: none observed in this run.

## Expansion Gate

The suggested Step B floor was customer-usable pass at least 7/10, missing facts 5 or fewer, forbidden claims 0, unsupported temporal/options additions 0, and average quality delta at least 0. Pipeline fix v1 meets those thresholds.

Before materializing 011-020, consider one more narrow patch for case 002 and case 006 if the target is 9/10 or 10/10 stability on smoke 10.
