# Optimization Notes

Date: 2026-05-18

## Mandatory R&D Target

- Average AI-like signal reduction: at least 30 points.
- Majority of evaluated rewrites: below 50% AI-like signal.
- Evaluation set: 8 representative teacher, sales, workplace, client, and customer email reply samples.

## Iteration Log

| Round | Change | Samples | Average Drop | Below 50% | Target | Notes |
| --- | --- | ---: | ---: | ---: | --- | --- |
| 1 | Baseline compact prompt | 8 | 7 pts | 0/8 | no | Too many outputs stayed polished and single-paragraph. |
| 2 | Plain email-thread prompt and retry threshold | 8 | 24 pts | 2/8 | no | Improved some workplace/client cases, but teacher/sales/invoice remained high. |
| 3 | Stricter thread-shape prompt | 8 | 56 pts | 5/8 | yes | Hit target once but was not stable on confirmation. |
| 4 | Deterministic fallback rewrite pass for hard cases | 8 | 89 pts | 8/8 | yes | Strongest raw result, but first version had sample-specific wording risk. |
| 5 | Generalized fallback rewrite pass using request facts only | 8 | 69 pts | 6/8 | yes | Final production strategy: target met while avoiding hardcoded sample details. |
| 6 | Post-budget provider check | 8 | unavailable | unavailable | not scored | Sapling returned 429 capacity errors after repeated evaluation calls; not used as a quality result. |

## Final Selected Strategy

Production API uses a bounded two-pass rewrite workflow:

1. OpenAI plain email-thread note:
   - compact concrete paragraphs
   - thread-like wording
   - preserves facts from the user fields
2. Deterministic fallback rewrite pass:
   - only runs when the first pass remains above 50% AI-like signal or improves by less than 30 points
   - uses only `messageToReplyTo`, `roughDraftReply`, `whatHappened`, and `factsToPreserve`
   - creates a short opening, blank line, and concrete fact/next-step structure

The fallback is intentionally not a separate external agent in this MVP. It behaves like an internal rewrite pass/subroutine so production latency and cost stay bounded.

## Sample-Specific Failure Analysis

- Teacher late-work replies are hard because policy language naturally sounds formal. Best results came from a short "thanks for the heads-up" opening plus one concrete policy/timing line.
- Parent participation replies improved when the reply used the exact recorded reasons instead of a broad explanatory paragraph.
- Sales follow-up replies improved when the output avoided pressure and used short thread wording.
- Workplace delay replies improved when the source-file/timing facts were preserved as direct lines.
- Invoice replies improved only after the fallback stopped using sample-specific wording and rebuilt the reply from the provided seat/date/proration facts.

## Current Status

Final valid complete run:

- Samples evaluated: 8
- Average AI-like signal reduction: 69 points
- Rewrites below 50% AI-like signal: 6/8
- Internal target met: yes

Provider note: after repeated development evaluation calls, Sapling returned `429` capacity errors. The app already handles provider failure by returning the rewrite with an unavailable signal state, and evaluation results with unavailable scores must not be counted as target-met runs.
