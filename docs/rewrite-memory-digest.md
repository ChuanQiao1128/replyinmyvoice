# Rewrite Memory Digest

Generated: 2026-05-19T04:58:03.790Z

This digest is generated from internally stored rewrite learning samples. It intentionally summarizes patterns and does not print user-submitted text.

## Summary

- Samples scanned: 12
- Successful rewrites: 12
- Quality-gate failures: 0
- Measured samples: 12
- Average signal drop: 33 pts
- Rewrites below 50% AI-like signal: 5/12
- Measured rewrites worse than draft: 7/12

## By Scenario

| Scenario | Samples | Avg drop | Below 50% | Quality fails |
| --- | --- | --- | --- | --- |
| Email or message reply | 4 | 73 | 3/4 | 0 |
| Blank / custom | 7 | 0 | 1/7 | 0 |
| Cover letter | 1 | 100 | 1/1 | 0 |

## By Diagnosis Tag

| Diagnosis tag | Samples | Avg drop | Quality fails |
| --- | --- | --- | --- |
| stock_opening | 10 | 29 | 0 |
| over_safe_tone | 2 | 48 | 0 |
| over_explained | 1 | 100 | 0 |

## Recommendations

- Investigate 7 measured sample(s) where rewrite signal was not lower than draft signal.
- Improve scenario guardrails for Blank / custom; fewer than 70% of measured samples are below 50%.

## Promotion Rule

Any recommendation must be promoted through:

1. update `docs/rewrite-strategy-memory.md`
2. add or update an evaluation case
3. add a deterministic test where possible
4. update prompt guardrails, repair logic, or fallback rules
5. rerun tests and scenario evaluation
