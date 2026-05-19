# Rewrite Strategy Memory

Date: 2026-05-18

This document is the long-term memory for the rewrite and repair strategy behind Reply In My Voice. It should be updated after measured evaluation rounds, live manual QA, or user-approved support cases reveal a new repeatable failure pattern or a working repair pattern.

The goal is not to store user content. The goal is to preserve reusable product learning: what made a draft score high, what repair was tried, what worked, what failed, and which rule should be promoted into the production rewrite engine.

## Why This Exists

A simple prompt such as "make this sound more natural" is not enough. Generic LLM rewrites often become more polished, balanced, and corporate. That can make the output feel less personal and can increase the AI-like signal.

Reply In My Voice should become better through measured experience:

1. diagnose why the draft looks or feels AI-like
2. create a scenario-specific rewrite plan
3. generate a targeted rewrite
4. measure the before/after writing signal
5. repair the candidate if it failed
6. select only a usable candidate
7. return a best-available facts-first candidate rather than a blank result when strict selection cannot pass
8. record the strategy lesson for future runs

This is the product advantage over asking a general chatbot for another rewrite. The app uses measurement, failure analysis, scenario guardrails, quality gates, and accumulated strategy memory.

## Agent Design

The long-term design should treat the rewrite system as a small internal agent loop, not one generic prompt.

### Rewrite Agent

Responsibilities:

- read the scenario, tone preset, optional context/message, and required draft
- apply scenario-specific guardrails
- diagnose likely AI-like causes
- create a rewrite plan
- generate the first targeted candidate
- avoid invented names, dates, numbers, promises, policies, discounts, outcomes, or experience

The Rewrite Agent should optimize for useful communication first, then for lower signal. It must not over-compress a complex reply just to get a lower score.

### Repair Agent

Responsibilities:

- receive the rejected candidate, draft score, candidate score, failure reason, diagnosis tags, and critical facts
- identify why the candidate failed
- produce a targeted repair, not a generic retry
- preserve the facts that the quality gate extracted
- remove or reduce the observed failure pattern
- return a candidate that still answers the original situation

The Repair Agent should be invoked when:

- the candidate signal is worse than the draft
- the candidate remains above the allowed target and did not reduce enough
- the candidate lost critical facts
- the candidate introduced unsupported facts
- the candidate sounds like a support macro, policy memo, polished marketing paragraph, or application cliche

### Strategy Memory Agent

This can be a future offline/development agent rather than a production request-time agent.

Responsibilities:

- read `docs/scenario-evaluation-results.md`
- read `docs/optimization-notes.md`
- identify recurring pass/fail patterns
- summarize which repairs worked for each scenario
- update this document with reusable lessons
- propose production prompt or rule changes
- propose regression tests for newly discovered failure modes

Implemented MVP command:

```bash
npm run memory:rewrite
```

This command reads stored internal learning samples and writes:

```text
docs/rewrite-memory-digest.md
```

The Strategy Memory Agent must not silently rewrite production prompts from one unreviewed live sample. It should write proposed lessons and test cases first. A developer or controlled automation then promotes stable lessons into code, tests, and prompt guardrails.

## Privacy Rule For Learning

MVP policy:

- The app may store submitted message context, rough drafts, rewritten replies, writing-signal results, and rewrite metadata for internal quality improvement.
- The Privacy page must disclose this internal storage.
- Do not sell, publish, or expose learning samples publicly.
- Do not use learning samples in marketing without explicit user approval.
- Do not store payment details.

The system should prefer pattern-level learning whenever possible. Even when internal samples include submitted content for quality work, production strategy updates should be promoted from repeatable patterns such as:

- which diagnosis tags are common
- which repair strategies reduce the signal
- which scenarios fail most often
- which quality gates reject candidates
- which prompt/rule changes improve measured eval sets

## Current Pipeline

The current production-grade strategy is:

1. measure draft signal
2. diagnose AI-like causes
3. create a rewrite plan
4. generate a targeted candidate
5. measure candidate signal
6. reject candidates that are worse or still too high without enough reduction
7. repair failed candidates using the failure reason and critical facts
8. remeasure repaired candidates
9. select the best candidate that passes quality gates
10. when strict gates fail but a complete candidate exists, return the best available candidate with a review note rather than an empty rewrite panel
11. when all measured candidates are incomplete, build a guaranteed facts-first fallback from the original request fields

Current measured status from `docs/scenario-evaluation-results.md`:

- 26 cases evaluated
- 10 long cases
- 5 long customer-support cases
- average AI-like signal drop: 60 points
- 20/26 rewrites below 50%
- 0/26 final selected rewrites worse than the draft
- Priya billing/proration regression: 89% -> 0%, facts preserved
- Priya live 100% -> 100% regression: fixed at 100% -> 0%, facts preserved

Measured update from the 2026-05-19 business QA run:

- 26 cases evaluated across blank notes, teacher replies, sales follow-ups, customer support, cover letters, and work updates.
- 24 cases returned available final writing-signal scores; 2 cases had unavailable final signal from the third-party provider during the run.
- Average AI-like signal drop across measured cases: 49 points.
- 16/24 measured rewrites were below 50% AI-like signal.
- Final selected rewrites worse than the draft: 0/24.
- Final outputs preserved expected facts for all 26 cases in the final run.
- Case pass count: 16/26 under the strict eval rule requiring facts preserved, available scores, no worse signal, and either below 50% or at least a 30-point drop.

Lessons promoted from this run:

- Do not route all customer-support facts-first fallbacks through the invoice fallback. Support fallbacks must branch by issue type: seat billing, plan-change billing, export problems, workspace access, incident status, and generic support.
- A candidate that is complete but measures worse than the draft should not be selected over a safe original when the original preserves all critical facts. This protects the core product promise that measured quality should not regress.
- Length completeness must be scenario-specific. Long customer-support replies still need substantial explanatory answers, but long sales replies, work updates, and policy notes can be compact if they preserve the exact required facts.
- Critical-fact extraction must include operational phrases, not only numbers and dates. Added phrases include course policy, old pilot workspace, billing report folder, weekly partner updates, two other vendors, pause the campaign, 2pm launch check, and related work/support facts.

Live teacher-parent regression promoted on 2026-05-19:

- User reproduced a teacher reply that stayed at 100% -> 100% when using the `Friendly` tone preset from the app UI.
- Root cause: the frontend sends only the scenario, message, draft, and tone preset. Without the optional context fields, `Email or message reply` entered the generic message fallback before teacher/parent grade rules could run.
- Fix: route grade/missing-work parent replies to a dedicated teacher-parent deterministic fallback before the generic email fallback. Preserve the parent name, student name, missing work, make-up timing, partial-credit rule, and help availability.
- Smoke result with the same front-end-shaped request: 100% draft signal -> 2% rewrite signal while preserving Jordan, the reading response, vocabulary practice, short reflection paragraph from Friday, end-of-week partial credit, and after-class/lunch help.
- Guardrail: title names such as `Ms. Carter` must not be mistaken for the student name.
- Follow-up fact-preservation fix: the first fallback over-compressed the reply and lost the recommended work order. Teacher-parent replies must preserve action sequence facts such as first doing the reading response and vocabulary practice because those can be done quickly, then doing the reflection paragraph. Candidates that drop this sequence are incomplete and must be rejected before display.

Remaining strategy work:

- Blank update notes and some cover-letter rewrites still often preserve facts but fail to reduce the third-party signal enough.
- Long workspace-access support replies can preserve facts while still scoring high; the next repair strategy should make these less macro-like without losing exact account/workspace details.
- Third-party signal unavailability should be tracked separately from rewrite failure so development evals do not overstate quality when provider scores are missing.

## Current Diagnosis Tags

Use these tags to explain why a draft or failed candidate is likely scoring high:

- `stock_opening`: template starts such as "Thank you for reaching out" or "I understand your concern"
- `corporate_polish`: too smooth, formal, balanced, or customer-service-like
- `uniform_rhythm`: sentence lengths and paragraph shapes are too even
- `over_explained`: every detail is explained in a complete but unnatural way
- `generic_transitions`: repeated connectors such as "Additionally", "Furthermore", "In conclusion"
- `policy_memo_voice`: sounds like a formal policy note rather than one person replying
- `low_specificity`: too many safe generic phrases and not enough concrete details
- `too_balanced_structure`: each paragraph follows the same acknowledge/explain/next-step shape
- `over_safe_tone`: emotionally flat or risk-averse
- `support_template_voice`: sounds like a support macro
- `application_cliche`: cover-letter claims with no grounding in the user's facts

## Current Repair Playbook

### Stock Opening

Problem:

- starts with a generic support or business opener
- sounds like a macro before the reply has said anything specific

Repair:

- replace with a situational opener
- use the recipient's name only if it appears in the input
- start directly with the concrete issue when appropriate

Working patterns:

- "Got it, Priya."
- "Thanks for laying this out."
- "I checked the timing you mentioned."
- "This is a fair question."

### Corporate Polish

Problem:

- the text is grammatically clean but too smooth
- phrasing feels like a public statement or policy page

Repair:

- use plain verbs
- reduce abstract nouns
- avoid polished symmetry
- keep one or two naturally short sentences

Avoid:

- "We understand that there appears to be..."
- "Please be advised..."
- "We apologize for any inconvenience caused..."

### Uniform Rhythm

Problem:

- every sentence has similar length
- every paragraph is balanced and complete

Repair:

- vary paragraph length
- use one short acknowledgement where natural
- split long formal sentences into direct lines
- keep some conversational roughness without typos

### Over-Explained Support Reply

Problem:

- the draft explains too much in a polished way
- or the repair over-compresses and drops useful explanation

Repair:

- keep 3 to 5 short paragraphs for complex support issues
- preserve the forwardable explanation if the user asked for one
- preserve requested next steps
- remove macro framing, not the useful operational detail

Important lesson from Priya billing/proration:

- A very short response can score low but be a bad product result.
- The repair must preserve dates, counts, amounts, billing period, proration explanation, and the next-step request.
- The final answer should still be useful enough for the recipient to forward internally.
- If the draft says the explanation is for a `finance manager`, preserve that phrase. Shortening it to only `finance` can make the fact gate reject an otherwise low-signal candidate.
- Do not leave the user with a blank failure state. If strict signal selection fails, return the best complete candidate with a review note; if every measured candidate is incomplete, use a guaranteed facts-first fallback from the user's own facts.

### Low Specificity

Problem:

- the output sounds generic because it avoids the real facts

Repair:

- pull concrete facts from the draft/context
- preserve numbers, dates, emails, product names, team names, and next steps
- do not invent missing facts

### Support Template Voice

Problem:

- output sounds like a canned help-desk answer

Repair:

- remove generic empathy blocks
- answer the actual issue earlier
- keep boundaries clear
- use "I" or "we" only where the draft does
- end with the next concrete action instead of a generic availability phrase

### Cover Letter Cliche

Problem:

- output claims motivation, passion, or fit without evidence

Repair:

- keep only experience or motivation present in the user's draft
- make claims smaller and more specific
- do not invent companies, achievements, metrics, or qualifications

## Scenario-Specific Lessons

### Blank / Custom

Use when the user only pastes a generic draft. Since there may be no context, preserve the draft's actual meaning and avoid adding new details. The safest repair is often structural: less polished, more direct, less symmetrical.

### Email Or Message Reply

Preserve the relationship signal. Do not add names. Keep the reply natural enough to send as an actual message, not a statement.

Teacher-parent grade replies need a stricter preservation rule than generic email replies. If the draft gives an action order, keep the order and the reason for the order. If the draft includes a supportive closing or teacher signoff, keep those elements unless the user explicitly removes them; they carry relationship tone and sender identity even when they are not numeric facts.

### Customer Support

This is the hardest scenario because support text naturally becomes polished and macro-like.

Current best strategy:

- direct situational opener
- concrete issue summary
- plain-English explanation
- requested next step
- preserve amounts, seat counts, dates, products, and policy details
- avoid "From what you described", "It seems", and "For next steps" when they create a template feel

### Cover Letter

Do not optimize by making the letter casual. The target is grounded and specific, not chatty. Avoid adding achievements or experience.

### Work Update

Preserve status, blocker, owner, date, and next step. Repairs should remove formal memo phrasing while keeping accountability.

## Promotion Rules

A strategy lesson can be promoted into production only when:

- it appears in at least two eval or QA cases, or one serious regression
- it improves the measured signal or prevents a quality failure
- it does not reduce fact preservation
- it has a regression test or evaluation case
- it does not depend on sample-specific hardcoded facts

Promotion path:

1. record the lesson in this document
2. add or update an eval case
3. add a unit or integration test if the behavior is deterministic
4. update scenario guardrails, diagnosis tags, repair prompts, or fallback logic
5. run evaluation
6. update `docs/scenario-evaluation-results.md`
7. update `docs/optimization-notes.md`

## Anti-Patterns

Do not promote strategies that:

- chase 0% signal by deleting useful detail
- add unsupported names, dates, or promises
- turn every reply into a short chat message
- overfit to a single sample's exact wording
- make customer support answers too vague to be useful
- show internal repair notes to users
- rely on user-visible language about evasion or guaranteed scoring

## Open Next Improvements

- Store aggregate candidate telemetry without message content.
- Add a feedback button for "This sounds too generic", "It lost detail", and "Try less polished".
- Let user-approved bad examples become eval cases.
- Add a Strategy Memory Agent as an offline maintenance task.
- Split prompt modules into scenario guardrails, diagnosis rules, repair rules, and selection policy.
- Add dashboards for pass rate by scenario and repair type.
