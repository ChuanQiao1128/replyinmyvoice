# Rewrite Memory Digest

Generated: 2026-05-18T04:34:09.257Z

Learning run: 6ca63151-1ead-4431-8455-9b0be666e909

This digest is generated from internally stored rewrite learning samples. It summarizes patterns and does not print user-submitted text.

## Summary

- Samples scanned: 4
- Measured samples: 4
- Successful rewrites: 4
- Quality-gate failures: 0
- Average signal drop: 49 pts
- Rewrites below 50% AI-like signal: 3/4
- Measured rewrites worse than draft: 1/4
- Promotion decision: promoted_candidate

## Findings

| Failure type | Scenario | Severity | Evidence | Recommendation | Diagnosis tags |
| --- | --- | --- | --- | --- | --- |
| worse_than_draft | Blank / custom | high | 1 | code-change | n/a |

## Strategy Candidates

| Title | Scenario | Risk | Evidence | Status |
| --- | --- | --- | --- | --- |
| Prevent worse-than-draft rewrite selection for Blank / custom | Blank / custom | high | 1 | proposed |

## Promotion Policy

- Run every 24 hours automatically.
- Push/deploy automatically only when a qualified strategy promotion passes all gates.
- Never deploy just because the scheduled job ran.
- Production strategy is promoted through code, tests, GitHub push, and Cloudflare deploy. It is not hot-loaded from database rows.
