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
| 7 | Long client-support prompt guardrail | 1 live manual sample | 89 pts | 1/1 | yes | Real billing-support test dropped from 89% to 0%, but the first output over-compressed the explanation. Prompt now preserves long support explanations, forwardable summaries, and requested next steps. |
| 8 | Workspace V2 scenario guardrails plus diagnosis/repair/select flow | 15 | 64 pts | 11/15 | yes | Five-scenario evaluation passed the internal target. Critical-fact repair restores emails, dates, amounts, counts, and requested details before selection. |
| 9 | No-bad-result quality gate plus targeted repair | 26 | 60 pts | 20/26 | yes | Expanded to 10 long cases and 5 long support cases. Rejects worse/high candidates, repairs failed candidates, and fails safely without charging usage when no candidate passes. Priya long billing/proration regression passed at 89% -> 0%. |
| 10 | Facts-first complete fallback for live Priya regression | 1 live manual sample | 100 pts | 1/1 | yes | User reproduced a 100% -> 100% empty-result failure. Root cause: low-signal deterministic candidates were rejected as incomplete because `finance manager` was not preserved. Fixed fact preservation and changed selection so the API returns the best complete fallback instead of an empty quality failure. Smoke result: 100% -> 0%. |
| 11 | Teacher-parent grade reply deterministic fallback | 1 live manual sample | 98 pts | 1/1 | yes | User reproduced a Friendly teacher reply that stayed 100% -> 100% through the app UI. Root cause: front-end-shaped requests lacked optional context fields and hit the generic email fallback. Added a grade/missing-work parent fallback. Smoke result: 100% -> 2%. |

## Final Selected Strategy

Production API uses a bounded diagnosis-driven rewrite workflow:

1. Draft diagnosis:
   - tags stock openings, corporate polish, uniform rhythm, over-explaining, generic transitions, policy memo voice, low specificity, over-safe tone, support template voice, and application cliches
   - applies scenario-specific backend guardrails
2. OpenAI targeted rewrite:
   - compact concrete paragraphs
   - thread-like wording
   - preserves facts from the user fields
3. Measure and repair:
   - measures draft and candidate AI-like signal
   - restores missing critical facts before selection
4. Deterministic fallback rewrite pass:
   - only runs when the first pass remains above 50% AI-like signal or improves by less than 30 points
   - uses only the provided request fields
   - creates a short opening, blank line, and concrete fact/next-step structure
5. Best-available safety:
   - never show an empty rewrite panel when a complete candidate exists
   - if a strict signal target cannot be met, return the best complete candidate and include a review note
   - if all candidates are incomplete, generate a facts-first fallback and show it rather than a blank quality failure

The fallback is intentionally not a separate external agent in this MVP. It behaves like an internal rewrite pass/subroutine so production latency and cost stay bounded.

## Sample-Specific Failure Analysis

- Teacher late-work replies are hard because policy language naturally sounds formal. Best results came from a short "thanks for the heads-up" opening plus one concrete policy/timing line.
- Parent participation replies improved when the reply used the exact recorded reasons instead of a broad explanatory paragraph.
- Sales follow-up replies improved when the output avoided pressure and used short thread wording.
- Workplace delay replies improved when the source-file/timing facts were preserved as direct lines.
- Invoice replies improved only after the fallback stopped using sample-specific wording and rebuilt the reply from the provided seat/date/proration facts.
- A live client-support test showed that optimizing too hard for a short thread style can remove useful explanation. Long support and billing replies now ask the model to keep 3 to 5 short paragraphs and preserve forwardable summaries plus detail requests.
- A later live Priya support test showed a different failure: the low-signal fallback scored 0% but was rejected because it shortened `finance manager` to `finance`. The fact gate was correct to notice the loss; the fallback now preserves that phrase so the low-signal candidate can be selected.

## Current Status

Final valid complete run:

- Samples evaluated: 26
- Long cases: 10
- Long customer-support cases: 5
- Average AI-like signal reduction: 60 points
- Rewrites below 50% AI-like signal: 20/26
- Final selected rewrites worse than draft: 0/26
- Case pass count: 14/26
- Priya long billing/proration regression: passed
- Priya live 100% -> 100% regression: passed after facts-first preservation fix
- Internal target met: yes

Provider note: after repeated development evaluation calls, Sapling returned `429` capacity errors. The app already handles provider failure by returning the rewrite with an unavailable signal state, and evaluation results with unavailable scores must not be counted as target-met runs.

Latest implementation notes:

- Removed the old internal fact-restoration append behavior so user-visible output never includes `Key details to keep`.
- Added a bounded measured repair loop: diagnose, candidate, measure, repair, remeasure, select.
- Tightened candidate selection so a measured successful response must be below 50% AI-like signal or at least 30 points lower than the draft.
- Added safe failure behavior for quality-gate misses; these requests are not charged as successful usage.
- Added deterministic fallback handling for partner updates, export-support replies, invoice/proration support replies, and sales follow-ups when general rewrite attempts stay too generic.
- Added a `testing` subscription status for internal QA accounts with a 10,000 rewrite quota.
- Added no-empty-result fallback behavior: provider/model failures continue to deterministic strategies, and quality-gate misses return the best complete or guaranteed facts-first candidate instead of a blank failure.
- Added a regression rule from the Priya live test: preserving `finance manager` matters because the fact gate treats it as critical context; replacing it with only `finance` can reject an otherwise strong low-signal candidate.
- Added a regression rule from the teacher-parent live test: app UI requests may not include optional context fields, so teacher/parent grade replies need a deterministic fallback that extracts the student name, missing work, make-up timing, partial credit, and help availability from the message/draft alone.

## Strategy Memory

Reusable rewrite and repair lessons from these optimization rounds are now tracked in:

```text
docs/rewrite-strategy-memory.md
```

Future evaluation rounds should update both files:

- `docs/optimization-notes.md` for measured round-level results
- `docs/rewrite-strategy-memory.md` for reusable diagnosis, repair, and promotion lessons
