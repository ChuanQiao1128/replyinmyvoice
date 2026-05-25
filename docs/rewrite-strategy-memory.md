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
7. return a quality-failure/no-charge response when the bounded workflow cannot produce a fact-safe result under the Naturalness Check quality bar
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

Implemented LearningOps promotion handoff:

- `lib/learningops/promotion-brief.ts` turns a promotable `StrategyCandidate` into a safe Codex task brief.
- `npm run learningops:run` writes `plans/learningops-promotion-task.md` after each run.
- `worker.js` adds a Cloudflare scheduled handler that runs LearningOps every 24 hours through the shared DB pipeline.
- Scheduled runs read only the last 7 days of `RewriteLearningSample` rows and record `LearningRun.status` as `digest_only`, `docs_only`, `promoted`, or `blocked`.
- When a candidate exists, the task brief includes the patch target, suggested files, required regression coverage, required docs update, validation commands, and safety constraints.
- The handoff is PR-drafting only. It must open a draft pull request for review and must not merge or deploy automatically.
- The brief must not contain raw learning sample content or secrets. It promotes pattern-level changes into code and tests only.

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

The current production-grade strategy selected on 2026-05-19 is `fact_reconstruct`:

1. measure the draft Naturalness Check signal
2. extract facts from all available user-provided fields
3. classify the scenario for style/risk only, not for deciding which facts matter
4. load a scenario style card, with `general_professional_reply` as the low-confidence fallback
5. generate three candidates from facts plus style card, without feeding the original wording into the writer prompt
6. run an internal reviewer for factual accuracy, tone, concision, low-template feel, and clarity
7. finalize the selected candidate with light edits only
8. run deterministic checks and an LLM fact-consistency gate
9. measure the final Naturalness Check signal
10. if the signal misses the threshold, run one bounded strong-model escalation from facts only
11. if the escalated result still misses the fact or Naturalness Check gate, return quality failure with no charge

Sapling is a final reference gate only. Do not put Sapling scores, thresholds, or detector-specific language into prompts.

Production success rule:

- If the draft AI-like signal is above `NATURALNESS_THRESHOLD` (default 40%), the rewrite must be at or below the threshold.
- If the draft AI-like signal is already at or below the threshold, the rewrite must not raise the signal.
- Fact gates and reviewer gates must also pass.
- Signal unavailability is a quality failure in the fact-reconstruct production route and must not charge usage.

Previous measured status from `docs/scenario-evaluation-results.md` before `fact_reconstruct`:

- 66 cases evaluated
- 44 draft-only cases
- 10 long cases
- 5 long customer-support cases
- average AI-like signal drop: 50 points
- 40/66 rewrites below 50%
- 0/66 final selected rewrites worse than the draft
- 0 fact preservation or unsupported-addition failures
- 66/66 customer-usable pass count
- 42/66 strict signal pass count
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

- Re-run the full scenario evaluation through `fact_reconstruct` and document the new measured pass/fail set.
- Continue improving Naturalness Check pass rate without weakening the fact gate. Do not choose a lower-score candidate if it drops required facts or adds unsupported details.
- Third-party signal unavailability is tracked separately in evaluation and is a quality-failure/no-charge condition in production.

## Unified Fact-Gate Lessons Promoted On 2026-05-19

- Fact extraction is unified across all user-provided fields. It must not depend on the visible scenario, because the visible scenario has been removed from the app workflow.
- Internal mode inference is allowed for style and risk guardrails only. It must not decide which facts can be ignored.
- Draft-only usage is first-class. Many users paste only their own draft, so the system must infer people, dates, constraints, tasks, negative promises, signoffs, and ordered steps from the draft alone.
- Fact gates must catch short constraints that models often soften away: `not promising`, `cannot approve`, `not a duplicate charge`, `invoice screenshot`, `not push for a decision`, `logo color has not changed`, `base plan did not change`, `not be recalculated`, `second quote`, and similar phrases.

## Fact-Reconstruct Evaluation Run On 2026-05-19

Latest focused evaluation result from `docs/scenario-evaluation-results.md`:

- 40 cases evaluated.
- 29 draft-only cases.
- 40/40 measured cases returned available Naturalness Check scores.
- 40/40 rewrites finished below 50% AI-like signal.
- Average AI-like signal drop: 89 points.
- Final selected rewrites worse than draft: 0/40.
- Customer-usable pass count: 38/40.
- Strict signal pass count: 38/40.

Lessons promoted:

- Reviewer-threshold misses should not stop immediately. They should try the deterministic facts-first fallback and still pass the same fact and Naturalness Check gates.
- A deterministic fallback should prefer extractive rewrites before richer scenario fallbacks for short factual drafts, because extractive rewrites preserve names, dates, counts, and constraints with less hallucination risk.
- Model fact extraction must ignore placeholder values such as `Not specified`, `unknown`, `N/A`, and boolean `false` when they appear inside fact arrays.
- Short teacher and work-update drafts need reusable extractive patterns for make-up quiz scheduling, parent grade explanations, and teacher-interview research summaries.
- Evaluation equivalence needs continued improvement for semantically equivalent wording such as `manager approval` versus `manager approves`.

Remaining risk:

- The `work-03-research-summary` case can still fail conservatively when the LLM fact checker produces inconsistent fact-gate output. The current production behavior is safe because it returns a quality failure and no charge rather than exposing a weak rewrite.
- Preserve contacts and workspace/account identifiers such as email addresses, `old pilot workspace`, `billing report folder`, and repeated-invite facts.
- Evaluation can normalize safe semantic equivalents, for example `can't guarantee` as `not promising`, `on hold` as `paused`, and `not to cut down` as `not cutting down`. This is not permission to drop facts; it prevents false failures when natural wording keeps the same fact.
- If all measured candidates are incomplete and the original draft also lacks message/context facts, the API must raise a quality failure instead of returning an incomplete original as success.
- Customer-usable pass and strict signal pass are separate metrics. Customer-usable release gating is facts preserved, no unsupported additions, no quality failure, and no worse selected signal. Strict signal pass is still tracked for ongoing optimization.

## Certainty Preservation Lesson Promoted On 2026-05-25

- A rewrite can lower the AI-like signal and still create fact risk by strengthening uncertainty. Logistics, billing, support-policy, legal, medical, financial, and eligibility replies must preserve modal language such as `may`, `might`, `seems`, `appears`, `likely`, and `looks like`.
- Do not rewrite `The delay seems to be related to...` as `The delay is due to...` unless the source facts explicitly confirm the cause. A safe rewrite can still be concise by using language such as `The delay looks like it is due to...`.
- Promote uncertainty preservation into deterministic fact gates, not only prompt guidance. If a candidate restates the same uncertain source claim without an uncertainty marker, reject it as policy/intent drift and retry through the facts-first or policy-safe strategy.
- Prompt guidance should explicitly tell model candidates not to turn uncertain source claims into definite claims such as `is`, `will`, `confirmed`, or `due to`.

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
- Do not return a weak rewrite as a successful result. If targeted repair, escalation, and fallback still miss the fact or Naturalness Check gate, return quality failure/no charge.

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

Canary rollout for promoted strategies:

- A promoted strategy must ship with `REWRITE_STRATEGY_CANARY_ENABLED=true`
  and a distinct `REWRITE_STRATEGY_CANARY_VERSION`.
- The default rollout is 10% of rewrite traffic, assigned deterministically by
  user/request key so one user sees a stable route during the window.
- The canary compares existing `RewriteCostLog` signal-change distributions for
  the control and canary strategy versions.
- After 24 hours or 200 measured rewrites, lower average signal drop pauses
  canary traffic; higher average signal drop ramps through 25%, 50%, then 100%.
- Post-promotion rollback is stricter and scenario-specific: the scheduled
  LearningOps job checks the active canary strategy's latest 50 successful
  measured rewrites per scenario. If that rolling scenario average trails the
  matching control scenario by 3 signal-drop points or more, it writes an open
  `RewriteCanaryRollback` row, sends an admin email when alert email is
  configured, and opens a GitHub follow-up issue when the issue token is
  configured.
- Request-time assignment must honor unresolved rollback rows before normal
  ramping logic. An unresolved rollback forces effective canary traffic to 0
  until the row is manually resolved after a corrected strategy is ready.
- The rollout uses existing database telemetry and does not require a new
  Cloudflare KV namespace or another paid runtime resource.

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

## 2026-05-20 Sentence-Level Targeted Repair

Problem:

- Whole-message escalation can be slower and can churn facts that were already correct.
- A high Naturalness Check score is often caused by a few generic or overly polished sentences, not the whole reply.

Promoted strategy:

- Request Sapling sentence scores internally.
- Do not show sentence scores or reason tags to users.
- Select at most three high-risk sentences above the current Naturalness Check threshold.
- Diagnose those sentences with internal tags such as `generic_empathy`, `corporate_template`, `over_polished`, `vague_filler`, `low_specificity`, and `policy_statement_voice`.
- Repair only the diagnosed sentences, then rerun fact gates and the Naturalness Check.
- Use strong escalation only after targeted repair misses.

Eval lessons promoted into tests:

- Preserve `desktop today`; do not collapse it into only `Wednesday morning`.
- Preserve `will not include pricing`; do not move that constraint onto unrelated agenda items.
- Preserve `permission slip`; do not generalize it to `signed slip`.
- Do not extract sentence-starting `Did` as a person/name fact.
- Reject dangling closings such as `Best regards,` with no sender name.

Latest focused evaluation:

- 40/40 customer-usable pass
- 40/40 strict signal pass
- 0 fact preservation or unsupported-addition failures
- 37/40 cases used targeted repair

## 2026-05-20 Clean-Final Gate

Problem:

- A rewrite can preserve facts and score well on the Naturalness Check while still leaking internal analysis wording, for example `The May 8 client handover is referenced.`
- Sapling is not a sendability checker, so this must be caught by deterministic product quality gates.

Promoted strategy:

- Reject final emails that contain internal analysis language such as `is referenced`, `Based on the provided context`, `the source says`, `extracted facts`, or `reviewer notes`.
- Pass clean-final issues into bounded repair/escalation notes.
- Do not return a candidate just because the Naturalness Check score is low; it must also be fact-safe and send-ready.

Regression tests:

- `rewrite-pipeline-checks.test.ts` covers deterministic meta-language detection.
- `rewrite-pipeline.test.ts` verifies a Sapling-passing meta-language final is repaired before return.
- `rewrite-pipeline-model.test.ts` verifies finalizer, targeted repair, and escalation prompts forbid internal note leakage.

## 2026-05-20 Send-Ready Structure Rewrite

Problem:

- Manual website QA found a long customer-support course-transfer/refund reply that preserved facts but was not send-ready.
- The output mostly preserved the original sentence order and split nearly every sentence into its own paragraph.
- The output broke numbered-list formatting with detached `1.` and `2.` markers.
- The output broke the quoted-summary boundary by merging a quoted block with the next instruction.
- The output still sounded like a support macro even though the Naturalness Check/fact gates could pass.

Root cause:

- Previous evaluation focused too heavily on fact preservation and Naturalness Check scores.
- The 40-case focused eval had many short draft-only samples and not enough long support-policy/options samples.
- Customer-usable pass did not require realistic email structure.
- Sentence-level repair can preserve a bad structure when the real problem is paragraph/list/quote organization.

Promoted strategy:

- Treat long support, policy, refund, cancellation, transfer, options, and eligibility-review replies as structured communication tasks.
- Generate from extracted facts into a fresh email structure instead of paraphrasing sentence by sentence.
- For long support-policy messages, group facts into natural paragraphs:
  1. concrete acknowledgement,
  2. current status,
  3. available option or policy constraint,
  4. user confirmation / next step,
  5. no-change-without-confirmation constraint when present.
- Reject candidates that only line-split or lightly paraphrase the original.
- Reject detached numbered-list markers, sentence-per-paragraph formatting, and broken quote boundaries.
- Route structural failures to full restructure escalation, not targeted sentence repair.

Required regression:

- Add `support-02-daniel-course-transfer-refund` to `scripts/eval-scenarios.ts`.
- Expected facts include Daniel, changed work schedule, June weekend cohort, Saturday 6 June, Saturday 20 July, seat availability, course access/materials/live session links, seven-day refund rule, exact registration timestamp, full refund, course credit, future session, July cohort transfer, refund review, no update/cancel without clear confirmation, and Customer Support Team.

Verification requirement:

- Expanded eval must include at least 12 long support-policy/options cases.
- Customer-usable pass must require structural send-readiness, not only fact preservation and signal improvement.
- 0 successful eval outputs may contain broken numbered lists, sentence-per-paragraph formatting, broken quote boundaries, or weak line-split paraphrasing.

Implementation plan:

- `docs/superpowers/plans/2026-05-20-send-ready-structure-rewrite.md`

## 2026-05-20 Adaptive Rewrite Agent Orchestrator

Updated direction:

- The rewrite system should not be treated as one fixed prompt chain.
- The safe outer frame stays fixed: facts, review, fact gates, structural gates, Naturalness Check, budget, no-charge quality failure.
- The inner strategy must be dynamic: the system should diagnose why a candidate failed and choose a different bounded strategy.

Required internal agent:

- `Rewrite Quality Strategist Agent`

Responsibilities:

- use `InputAnalysis` to choose the first strategy before generation;
- read deterministic issues, reviewer issues, missing facts, unsupported facts, Naturalness Check category, high-risk sentence count, scenario, style card, and failed attempt count;
- classify failure kinds such as `fact_loss`, `unsupported_fact`, `broken_numbered_list`, `broken_quote_boundary`, `sentence_per_paragraph`, `line_split_paraphrase`, `support_macro_voice`, `messy_thread_leak`, `quote_or_list_risk`, `signal_not_improved`, and `low_signal_got_worse`;
- choose one allowed next strategy: targeted sentence repair, full structure rewrite, facts-first reconstruct, support-policy/options rewrite, quote/list-safe rewrite, messy-thread cleanup rewrite, strong-model restructure, or quality failure.

Runtime rule:

- Every retry must be a strategy change, not a blind repeat.
- The orchestrator is bounded; it cannot create unlimited attempts.
- A Budget Manager must approve retries, strong-model escalation, and additional Naturalness Check calls.
- Support-policy replies must pass a Policy / Intent Gate for refund/transfer/cancellation/eligibility/no-change-without-confirmation constraints.
- A successful result must pass fact, unsupported-fact, structural send-ready, and Naturalness Check gates.
- If no bounded attempt passes, return quality failure/no charge rather than exposing a weak rewrite.

Development rule:

- Eval failures should automatically produce diagnosis tags, strategy decisions, regression tests, prompt/style-card/routing updates, and strategy-memory notes.
- Do not wait for the user to discover obvious failures through manual website testing.
- Promote lessons only after regression tests and eval pass.
- Expand the eval suite to 60 cases, but run it in staged modes (`smoke`, `focused`, `full`) and record OpenAI/Sapling usage so strategy work does not waste calls.

Implementation plan:

- `docs/superpowers/plans/2026-05-20-adaptive-rewrite-agent-orchestrator.md`

## 2026-05-21 Package Delivery Delay Support Regression

Observed failure:

- Manual website QA found a package-delay customer-support draft where Naturalness Check improved strongly, but the final result was rejected as `Facts need another pass`.
- The selected candidate was not the problem; the fact gate was over-strict.

Root cause:

- The fact extractor treated sentence-openers such as `There` and apology openers such as `Sorry` as possible person names.
- A generic `Customer Support Team` signoff was treated as a hard signoff fact.
- `no action required` and `no action is required` were not normalized to the same fact.
- The deterministic support fallback did not have a delivery-delay branch, so the last fallback could produce a generic quick-update shape.

Promoted strategy:

- Add delivery-delay support facts to the required fact library: fulfillment center, delivery carrier, temporary processing issue, local distribution facility, still in transit, lost/returned status, one-to-two-business-day update window, no-action-required state, and carrier follow-up investigation.
- Treat generic support-team signoffs as optional brand/footer text rather than hard facts.
- Normalize equivalent no-action-required phrasing.
- Route package/order/tracking/carrier/local-distribution-facility drafts to a delivery-delay support fallback instead of generic quick-update fallback.

Required regression:

- Package-delay support drafts must preserve delivery status, delay reason, in-transit/not-lost state, update window, no-action-required state, follow-up-investigation next step, and support-team signoff when present.
- The fallback must not return `Quick update: Hi ...` or a one-line generic reply for medium customer-support drafts.

## 2026-05-21 Adaptive Gate Calibration

Observed failure:

- Manual website QA found package-delay and support-policy rewrites where the Naturalness Check improved strongly, but the final result still returned the no-charge quality failure state.
- The rewritten text preserved the important business facts, but deterministic checks treated optional footer/phrasing differences as hard fact failures.

Root cause:

- The gate was binary: every deterministic issue blocked the result.
- Generic support footer labels such as `Customer Support Team` were mixed with critical facts such as dates, amounts, counts, refund/transfer policy, and no-change-without-confirmation constraints.
- Soft wording differences caused the same retry/escalation path as real factual drift.

Promoted strategy:

- Use an Adaptive Gate Calibrator after deterministic checks.
- Keep hard blocking for money, dates/deadlines, counts, named people, policy constraints, promises, refunds, charges, subscriptions, transfer/availability rules, lost/returned status, and required confirmation conditions.
- Downgrade generic support-team footers and polite formula phrases to soft issues. Record them for diagnostics, but do not block an otherwise safe rewrite.
- Route repair/escalation from hard blocking issues only; soft issues should not trigger a no-charge quality failure.

Required regression:

- A package-delay rewrite that omits only a generic `Customer Support Team` footer can pass if the delivery facts are preserved.
- A billing rewrite that changes `NZD $126` to another amount must still fail the hard fact gate.
- Pipeline tests must prove that soft footer misses do not trigger escalation, while hard fact losses still do.

Implementation plan:

- `docs/superpowers/plans/2026-05-21-adaptive-gate-calibrator.md`

## 2026-05-21 Reviewed Fact Ledger Before Rewrite

Observed failure:

- Manual website QA repeatedly hit `Facts need another pass` even when Naturalness Check improved.
- Some failures came from bad or incomplete extracted facts entering the generator and later gates as if they were authoritative.

Root cause:

- The first LLM fact extractor can miss short but critical anchors such as money, seat counts, timelines, delivery states, and policy constraints.
- It can also over-extract unsupported hard facts such as refund guarantees, lost-package claims, or generic sentence openers treated as people.
- Downstream rewrite/gate logic was trying to repair candidates after the fact instead of first reviewing the fact ledger itself.

Promoted strategy:

- Add a reviewed fact ledger between `extractFacts` and scenario/candidate generation.
- Merge deterministic anchors from user-provided text into `facts_that_must_not_change` before any rewrite prompt runs.
- Reject unsupported hard extracted facts before they can steer generation.
- Treat generic support-team signoffs and polite formula phrases as soft/footer text rather than locked business facts.
- Filter generic placeholder people such as `There`, `This`, `We`, and `You` out of `people_mentioned`.

Required regression:

- Package-delay support drafts must add delivery anchors such as fulfillment center, carrier, local distribution facility, still-in-transit state, not-lost/returned status, update window, no-action-required state, and carrier follow-up investigation.
- Billing/proration drafts must promote money, seat counts, and base-plan constraints into the hard fact ledger.
- Unsupported hard facts such as `full refund available` or `The package is lost` must be rejected when the source text does not support them.
- Candidate generation must receive the reviewed facts, not the raw extraction result.

## 2026-05-21 DeepSeek Smoke Test Lessons

Observed failure:

- The first DeepSeek smoke run against the synthetic 100-case corpus timed out on the first case during fact extraction.
- After increasing model timeout, a later run reached strong escalation but returned an empty content payload.
- The first full smoke result reported 0/10 customer-usable passes because the eval harness passed only message, draft, and tone into `rewriteRequestSchema`, dropping audience, purpose, what happened, and facts to preserve.

Root cause:

- DeepSeek v4 requests can spend extra time or return non-content reasoning output unless ordinary JSON steps explicitly disable thinking.
- Strong escalation was being treated as thinking/high-reasoning by default, even though the current production loop can call it before the final hard-repair attempts.
- The new markdown corpus contains essential context in `what_actually_happened` and `facts_to_preserve`; dropping those fields makes the eval measure an incomplete request, not the intended product workflow.

Promoted strategy:

- For ordinary DeepSeek JSON roles, send `thinking: { type: "disabled" }` and bounded `max_tokens`.
- Keep `DEEPSEEK_ENABLE_STRONG_THINKING=false` by default. Enable thinking only for a deliberately selected final hard-repair path.
- The email 100-case eval loader must preserve all request fields: message, rough draft, audience, purpose, what happened, and facts to preserve.
- Treat 10-case smoke as a provider-readiness and fail-closed behavior check, not as a launch-quality pass gate.

Latest smoke evidence:

- Date: 2026-05-21.
- Mode: `EVAL_CORPUS=email-100 EVAL_MODE=smoke`.
- Cases evaluated: 10.
- Average AI-like signal drop: 60 points.
- Rewrites below 50% AI-like signal: 10/10.
- Final selected rewrites worse than draft: 0/10.
- Customer-usable pass count: 2/10.
- Strict signal pass count: 2/10.
- Fact preservation or unsupported-addition failures: 8.

Required regression:

- DeepSeek request-body tests must prove ordinary JSON calls disable thinking and set `max_tokens`.
- Strong escalation tests must prove thinking is disabled by default and enabled only when `DEEPSEEK_ENABLE_STRONG_THINKING=true`.
- Eval harness tests must prove markdown cases map all context fields into rewrite request inputs.
- Future strategy work must target fact reconstruction and gate precision before running focused or full 100-case evals.

## 2026-05-21 DeepSeek Smoke Failure Diagnosis And Next Strategy

Observed failure:

- The corrected 10-case DeepSeek smoke completed with live provider calls, but only 2/10 cases were customer-usable.
- All 10 measured cases lowered or preserved the Naturalness Check signal, and none selected a rewrite worse than the draft by score.
- Eight cases ended in fail-closed `fact_check_failed` with no final user-facing rewrite.
- The two passing cases still exposed structural quality problems: invented or awkward greetings such as department/status nouns, repeated facts, and fact-dump paragraphs.

Root cause:

- Naturalness signal improvement is currently easier than producing a complete send-ready reply. Sapling can be low while the candidate is blank, repetitive, or structurally weak.
- The eval corpus encodes two different things inside `facts_to_preserve`: facts that must appear and constraints that must remain true by absence, such as refund limits, no early promise, or no implication of blame. Treating all lines as literal required facts creates noisy failures and can send repair attempts in the wrong direction.
- The rewrite path still over-anchors on the rough draft and generated candidates. For this corpus, `what_actually_happened` and `facts_to_preserve` are often the authoritative source of truth and must dominate the draft when they conflict.
- The attempt loop has evidence of failures, but it is not yet using a reviewed fact/constraint ledger strongly enough to choose a true facts-first reconstruct strategy before spending repair attempts.
- Greeting and recipient inference is unsafe. When no real person name is present, the engine can infer labels such as `Finance` or `Reopening` as addressee names.
- Current customer-usable gates are too permissive for structure. A fact-preserving reply can still be unacceptable if it repeats facts, echoes internal context, or reads like a ledger dump.

Promoted strategy:

- Split the eval and runtime ledger into `must_include_facts`, `must_not_claim`, `policy_constraints`, `allowed_options`, and `forbidden_actions`.
- Build the candidate from the reviewed ledger, not from failed candidates or from the rough draft alone. `facts_to_preserve` and `what_actually_happened` should override the draft when there is conflict.
- Add an explicit `no_real_recipient_name` path. When the source does not contain a real addressee name, omit the greeting or use a neutral opener instead of inventing one from audience, department, policy, or status terms.
- Route policy, billing, deadline, eligibility, refund, cancellation, transfer, access, and customer-support cases to structured options/policy templates before generic naturalness repair.
- Require candidate JSON metadata that lists included fact IDs, satisfied constraints, forbidden claims absent, and greeting source. Use this metadata for diagnosis, but verify with deterministic checks.
- Add structural blockers for repeated hard facts, context echo, sentence-per-fact dumps, department/status greetings, detached list markers, broken quote boundaries, and blank final success.
- Keep the 10-attempt maximum, but do not spend attempts on the same prompt shape after a hard fact miss. Escalation should switch strategy class: full facts-first reconstruct, policy/options rewrite, quote/list-safe rewrite, or quality failure.

Required regression:

- `rimv-email-001`, `rimv-email-002`, `rimv-email-003`, `rimv-email-005`, `rimv-email-006`, `rimv-email-008`, `rimv-email-009`, and `rimv-email-010` must preserve hard facts or fail for a specific hard constraint reason.
- `rimv-email-004` must not pass if the output repeats the same facts or echoes internal context such as user corrections.
- `rimv-email-007` must not produce `Hi Reopening` or any greeting inferred from a policy/status noun.
- Constraint lines such as `Do not offer a refund` and `Do not promise completion before June 1` must be checked as forbidden-claim absence, not as literal sentences that must appear.
- Do not run focused or full 100-case eval until local ledger/gate regressions pass and smoke reaches a materially better result, with no known weird-greeting or fact-dump pass cases.

## 2026-05-21 DeepSeek Local Repair Pass 1

Observed failure:

- The first corrected DeepSeek smoke exposed a mismatch between provider-level success and product-level success.
- Local inspection showed the deterministic fallback could preserve facts, but the gates and fallback router were still overfitting to background context, treating constraint instructions as literal facts, and misrouting non-delivery cases into the package-delay support fallback.

Root cause:

- `message_to_reply_to` contains useful context but should not automatically become the hard fact ledger when `facts_to_preserve` is present.
- Constraint sentences such as `Do not offer a refund`, `Do not promise completion before June 1`, and `Do not imply the parent is wrong or careless` are absence or policy checks, not mandatory output sentences.
- Delivery-delay routing matched too broadly on generic terms such as `order` or `business days`, causing sales onboarding and damaged-item support cases to use package-delay language.
- Structural gates did not block department/status greetings, repeated facts, or internal context wording such as `the user`.

Promoted strategy:

- When `facts_to_preserve` is present, use it as the hard deterministic fact source for missing-fact checks. Use broader request context as evidence and unsupported-fact source, not as automatic mandatory output.
- Filter forbidden-claim instructions out of hard fact extraction and evaluate them separately in the eval harness.
- Tighten delivery-delay routing so it requires multiple delivery-status signals and does not catch damaged-item replacement or sales onboarding cases.
- Add deterministic fallback branches for damaged-item replacement, sales pricing/onboarding timing, account verification lockout, and subscription cancellation/downgrade confirmation.
- Add structural blockers for unsupported greetings such as `Hi Finance` or `Hi Reopening`, repeated fact sentences, and internal user-context echoes.

Verification evidence:

- Added local regressions for the smoke failure modes in fact extraction, structure gates, fallback routing, and email eval expectation parsing.
- Focused unit run passed: `tests/unit/fact-extraction.test.ts`, `tests/unit/rewrite-pipeline-checks.test.ts`, `tests/unit/openai-output.test.ts`, and `tests/unit/rewrite-email-eval-cases.test.ts`.
- Full local verification passed: `npm run test` with 32 files and 201 tests, `npm run typecheck`, and `npm run lint`.
- No live DeepSeek, OpenAI, or Sapling provider call was run in this repair pass.

Next regression:

- Rerun only 10-case smoke before any focused/full eval.
- Expected improvement: no package-delay fallback on sales onboarding or damaged-item cases, no `Hi Reopening`/`Hi Finance`, no fact-dump pass for `rimv-email-004`, and materially fewer fail-closed fact-check failures.

## 2026-05-21 DeepSeek Smoke After Local Repair Pass 1

Observed result:

- Ran one provider-backed 10-case smoke and stopped.
- Customer-usable pass improved from 2/10 to 4/10.
- Strict signal pass improved from 2/10 to 4/10.
- Fact preservation or unsupported-addition failures dropped from 8 to 6.
- Average AI-like signal drop stayed strong at 62 points.
- Rewrites below 50% AI-like signal stayed 10/10.
- Final selected rewrites worse than draft stayed 0/10.

What improved:

- `rimv-email-001` and `rimv-email-002` now pass with facts preserved and no quality failure.
- `rimv-email-006` and `rimv-email-007` now pass by the current eval criteria.
- The damaged-item and sales-onboarding cases no longer show package-delay text in the final result because they fail closed instead of returning the wrong support fallback.
- `Hi Finance` and `Hi Reopening` did not reappear.

Remaining failures:

- `rimv-email-003`, `rimv-email-004`, `rimv-email-005`, `rimv-email-008`, `rimv-email-009`, and `rimv-email-010` still end as no-charge `fact_check_failed` with blank final output.
- Live rejected-candidate notes still show `missing_locked:Do not promise completion before June 1`, `missing_locked:Do not offer a refund`, and `missing_locked:Do not cancel or downgrade without confirmation`.
- This means forbidden-claim constraints were separated in the eval harness, but the runtime reviewed fact ledger can still preserve LLM-extracted `Do not...` instructions as literal locked facts.
- Passing outputs still expose unsafe recipient inference: `rimv-email-006` produced `Hi Upgrade`, and `rimv-email-007` produced `Hi Original`.
- These are not department/status greetings caught by the new structure gate, but raw LLM `recipient_name` mistakes restored into deterministic fallback greetings.

Root cause:

- The local repair filtered forbidden-claim instructions from deterministic `extractRequiredFacts`, but did not classify or remove equivalent LLM-extracted `facts_that_must_not_change` values during `reviewExtractedFacts`.
- The reviewed fact ledger still accepts short capitalized source words such as `Upgrade` and `Original` as `recipient_name` when the word appears in source text, even though it is not a person or valid addressee.
- The structure gate catches a few hardcoded unsafe greeting labels, but the safer fix is earlier: recipient review must require explicit greeting/name evidence or a real person-like token, not any capitalized word with source evidence.
- The runtime can still spend repair/escalation attempts before the deterministic fallback has a clean reviewed ledger, so failures are often gate-data failures rather than writer failures.

Promoted next strategy:

- Add constraint classification inside `reviewExtractedFacts`, not only inside eval parsing. LLM-extracted locked facts beginning with `Do not`, `Don't`, `No guarantee`, `Cannot`, or `Do not ... without confirmation` should become policy/forbidden constraints, not required output text.
- Add a reviewed-ledger field or metadata bucket for forbidden claims and policy constraints if needed; until then, reject them from `facts_that_must_not_change` and rely on `policy-intent-gate`/eval forbidden-claim checks.
- Harden `recipient_name` review. Reject source-backed but non-person labels such as `Upgrade`, `Original`, `Reopening`, `Finance`, `Billing`, `Cancellation`, `Verification`, `Support`, and similar status/category nouns.
- Make `withRestoredRecipientGreeting` restore a name only when the reviewed recipient is safe. If unsafe or absent, keep `Hi,` or omit the greeting.
- Add structure-gate regressions for `Hi Upgrade` and `Hi Original`, but treat them as backup protection rather than the primary fix.

Required regression:

- `rimv-email-003` must not treat `Do not promise completion before June 1` as a `missing_locked` required phrase.
- `rimv-email-005` must not treat `Do not offer a refund` as a `missing_locked` required phrase.
- `rimv-email-009` must not treat `Do not cancel or downgrade without confirmation` as a literal required phrase, while still blocking a rewrite that actually cancels or downgrades without confirmation.
- `rimv-email-006` must not output `Hi Upgrade`.
- `rimv-email-007` must not output `Hi Original`.
- Do not run focused/full eval until the next smoke eliminates these locked-constraint and recipient-name failures.

## 2026-05-23 DeepSeek 10-Case Smoke Lessons For Rewrite And Repair Agents

Latest smoke evidence:

- Date: 2026-05-23.
- Mode: `EVAL_CORPUS=email-100 --mode=smoke`.
- Cases evaluated: 10.
- Measured cases: 10/10.
- Average AI-like signal drop: 60 points.
- Rewrites below 50% AI-like signal: 10/10.
- Final selected rewrites worse than draft: 0/10.
- Fact preservation or unsupported-addition failures: 0.
- Customer-usable pass count: 10/10.
- Strict signal pass count: 10/10.
- Full 100-case eval was intentionally stopped after the user asked to summarize these 10-case lessons first.

### Rewrite Agent Lessons

- Treat `facts_to_preserve` and `what_actually_happened` as the authoritative content source when present. The rough draft often contains wrong promises, missing facts, or dismissive phrasing that must not steer the final answer.
- Do not infer a recipient from departments, product plans, status labels, policy nouns, or mentioned students. Examples now treated as unsafe addressees include `Finance`, `Basic`, `Team`, `Solo`, `Acceptable`, `Upgrade`, `Original`, `Reopening`, `Order`, and `Liam` when Liam is the student, not the parent. If there is no explicit addressee, use a neutral opener or omit the name.
- Split mandatory facts from policy constraints. `Do not offer a refund`, `Do not promise completion before June 1`, and `Do not cancel or downgrade without confirmation` are absence/intent constraints, not literal sentences that must appear in the rewrite.
- Prefer a facts-first deterministic shape when the message is short, factual, and high-risk: billing proration, cancellation/downgrade, damaged replacement, account verification, teacher scheduling, and workplace status/dependency updates.
- A low Naturalness Check score is not enough. The rewrite still has to be send-ready: no blank success, no fact dumps, no repeated hard facts, no internal-context phrases such as `the user`, and no mechanical sentence-per-fact formatting.
- When the draft already scores low, the rewrite must be especially conservative. Do not add polished filler or broad possibility language just to sound warm.
- Preserve concrete option/dependency language exactly enough to stay actionable: dates, times, counts, prices, deadlines, confirmation requirements, and available options should survive even if sentence wording changes.

### Repair Agent Lessons

- A repair attempt must change strategy class after a hard miss. Do not keep retrying the same polished email shape after `fact_loss`, `unsupported_greeting`, `changed_policy_or_condition`, or `naturalness_not_improved`.
- Use failed candidates as negative evidence only. The repair prompt may inspect them to avoid repeated mistakes, but the source of truth remains the original input plus reviewed fact/constraint ledger.
- For `fact_loss`, route to full facts-first reconstruct or a scenario deterministic fallback, not sentence-level polish.
- For `changed_policy_or_condition`, repair the policy meaning before naturalness. Example: saying onboarding completion is `possible` near a no-promise deadline still violates `Do not promise completion before June 1`.
- For `unsupported_greeting`, remove the greeting name instead of trying a different guessed name.
- For `naturalness_not_improved` with facts already safe, try a shorter facts-first structure before strong escalation. The parent-conference case passed only after the fallback became direct and concrete instead of a polished teacher email.
- For billing and subscription cases, preserve the money and lifecycle facts in the same repair: upgrade date, billing cycle, plan prices, invoice total, renewal date, downgrade option, admin approval, and no-change-without-confirmation.

### Gate And Evaluation Lessons

- Runtime gates need semantic normalization, but only for fact-equivalent wording that tests cover. Promoted examples include `$1,800/month` -> `$1,800 per month`, `until you confirm` -> `without confirmation`, `please confirm whether` -> `ask whether`, `upgrading` -> `upgrade`, and `did not fail` -> `not a failure`.
- Eval matcher fixes must not weaken production fact gates. Each equivalence promotion should have a unit test and should still require the concrete dates, amounts, counts, and options.
- Provider failures should not abort an evaluation window. The model wrapper now retries transient OpenAI-compatible timeouts/429/5xx responses before failing the current rewrite step.
- Sapling sentence scores can be noisy for greetings and option sentences. They are useful as a gate, but repair should optimize for concise human structure, not for score wording.

### Required Regression Set Before Full 100-Case Eval

- 10-case smoke must stay at 10/10 customer-usable and 10/10 strict signal before starting full 100.
- `rimv-email-003` must block broad `possible before June 1` wording unless the no-promise constraint is explicitly preserved.
- `rimv-email-004` must preserve the Q2 forecast dependency, Finance confirmation count, 9:30 AM May 7 -> 11:00 AM timing, and duplicate renewal-row delay.
- `rimv-email-006` must preserve Basic/Team prices, May 1-May 31 cycle, May 10 upgrade, `$27.43`, and not-random-fee explanation without `Hi Basic` or `Hi Team`.
- `rimv-email-008` must not greet the student as recipient and must preserve both meeting slots plus the ask for which time works.
- `rimv-email-009` must preserve renewal date, current/Solo prices, admin/no-change confirmation boundary, and ask for cancellation-at-renewal versus downgrade.

## 2026-05-23 Extraction And Gate Resilience Lessons

- Sentence-initial business labels are not people. Words such as `Move`, `Options`, `Requested`, `Manager`, `Sender`, `Suggested`, `Purpose`, `Staff`, `Exact`, `Next`, and `Signing` should not become person anchors or locked output facts only because they are capitalized at the start of a sentence.
- Real full names must remain locked. The extraction filter should keep person-shaped names such as `Priya Shah`, `Martin Hale`, `Lee Tran`, and `Elena Ruiz` while rejecting common labels.
- The internal locked-fact gate should recognize tested semantic equivalents while still requiring the concrete data. Examples: `Price was $689` can be preserved as `cost is $689`, and `Credit covers invoice` can be preserved as `credit can be used for the invoice`; wrong amounts, dropped names, and dropped dates remain hard failures.
- OpenAI-compatible network failures should use the same bounded retry budget as other transient provider failures. If the retry budget is exhausted, the request should become a no-charge quality failure path rather than an uncaught request crash.

## 2026-05-24 Fact-Gate Redesign Lesson

- Deterministic locked-fact checks should own concrete atoms only: money, dates/times, counts, identifiers, and proper names. Non-atomic prose such as `requested`, `confirming`, `arrive`, or `possible` should not be required literally.
- Semantic and policy preservation belongs to the LLM fact check plus the policy/forbidden gate. A candidate that keeps every atom, passes structure, passes policy/forbidden checks, and receives an LLM fact pass should not be rejected only because the prose was naturally reworded.
- Facts-first fallback is terminal when it keeps all locked atoms and passes structure plus policy/forbidden checks. Empty `fact_check_failed` should be reserved for real atom loss, invented concrete data, or unrepairable policy/forbidden violations.
- Option labels such as `Accept` and `Switch` can be natural action wording for source-provided options. They should not be treated as invented person facts, while invented names, amounts, dates, counts, and IDs remain hard failures.

## 2026-05-24 C# DeepSeek/Sapling 100-Case Eval Lessons

Latest C# provider evidence:

- Date: 2026-05-24.
- Mode: `EVAL_MODE=full EVAL_LIMIT=100 EVAL_MAX_ATTEMPTS=10`.
- Provider route: C# `FactReconstructRewriteProvider` with DeepSeek-compatible chat completions and Sapling writing signal.
- Cases evaluated: 100.
- Successful rewrites: 100/100.
- Measured cases: 100/100.
- Rewrites below 50% signal: 100/100.
- Baseline-above-threshold average signal drop: 60 points across 13 cases.
- Total real-provider calls: 133 model calls and 206 Sapling calls.
- Report artifact: `docs/rewrite-eval-results/20260524-034340-csharp-rewrite-full.md`.

Promoted lessons:

- C# rewrite parity must be measured through the production provider path, not only the legacy TypeScript eval harness. A small C# eval runner now exercises the real provider, gates, DeepSeek-compatible model client, and Sapling client together.
- Sapling unavailable/timeout responses need bounded retry before no-charge quality failure. The C# provider retries transient writing-signal unavailability up to three attempts.
- Exact fact gates must normalize common count equivalents: `both` and number words should satisfy matching digit count facts, while wrong or missing amounts/dates still fail.
- Amount extraction must support thousands separators and cents, including values such as `$2,220`, `$2,012.50`, and `$9,212.50`.
- Dense billing, membership, refund, return, renewal, and transfer cases must explicitly include original paid amounts and original purchase/start dates when provided.
- Sponsor/package and scheduling replies must explicitly preserve included benefits, requested days, and available options before asking for assets, confirmation, or next decisions.
- Workplace coaching replies must not invent judgment labels such as `dismissive`, `careless`, `rude`, `negligent`, or `unprofessional` unless the source facts provide that wording.
- No-advice constraints should be handled by stating operational facts only. Do not add professional-advice redirects such as asking an accountant, lawyer, or doctor unless the source facts provide that next step.

## 2026-05-25 Two-Field Product/Eval Contract Lesson

- The public product contract is now draft-first: frontend sends only `roughDraftReply`
  and `tone`. Optional fields such as `messageToReplyTo`, `audience`, `purpose`,
  `whatHappened`, and `factsToPreserve` may remain for backend compatibility, but the
  main product/eval path must not rely on them.
- The 100-case Markdown corpus uses `must_keep` and `must_not_claim` as evaluator
  answer keys only. They must not be passed into the rewrite engine as a pre-digested
  fact list.
- The engine-visible text for email-100 provider tests is `rough_draft_reply`. If a
  fact is graded in `must_keep`, it must be checkable from that rough draft.
- Draft input should be controlled by word count, not character count. The product
  cap is 400 words, with a practical target under 300 words for ordinary email
  replies.
- The next quality window should target most successful measured rewrites below 30%
  Naturalness Check signal, with below 20% as a stretch target, while keeping 0
  critical fact losses and 0 forbidden-claim violations.

## 2026-05-25 Single-Input Draft Eval Correction

- The new Markdown regression corpus must model the actual product surface as
  `input_draft` plus `tone_preset: warm`. Do not evaluate a hidden richer interface
  that supplies `message_to_reply_to`, `audience`, or `purpose` to the rewrite engine.
- Old dual-input email scenarios can be reused only as seeds. Each materialized eval
  case must be rewritten into one self-contained user draft, with all graded facts
  visible inside `input_draft`.
- `what_actually_happened`, `must_keep`, `must_not_claim`, quality targets, and challenge
  notes are judge-only fields. They are not prompt input and must not be mapped into
  `factsToPreserve`.
- The first stable corpus shape is: a 100-row case plan, 10 materialized smoke cases,
  local parser validation, then provider smoke only after local validation passes.
- Quality scoring should compare original draft versus rewrite for clarity, warmth,
  structure, conciseness, actionability, voice, and format. Sapling remains diagnostic
  and gate-supporting, not the only pass/fail metric.

## 2026-05-25 Warm Rewrite Locked-Fact Calibration

- Warm tone must not mean shorter-at-all-costs. For short draft-only inputs, the
  safest behavior is usually a close rewrite with paragraphing and tone cleanup, not
  a summary.
- Locked facts used by the rewrite path must come only from the user's draft. Eval
  answer keys such as `must_keep`, `must_not_claim`, and `what_actually_happened`
  remain judge-only.
- Scheduling rewrites need hard unsupported checks for new weekdays, dates, times,
  time ranges, deadlines, and options. Candidate selection should reject invented
  alternatives before finalization.
- Boundary sentences with `cannot`, `not`, `without`, `unless`, `only if`, `must`,
  `requires`, `before`, `after`, and similar dependency wording should be locked, but
  the deterministic gate should allow natural paraphrases when polarity and concrete
  atoms are preserved.
- Smoke 10 pipeline-fix-v1 improved from eval-harness-v2 customer pass 2/10 to 8/10,
  with missing facts reduced to 1, forbidden claims 0, unsupported temporal/options
  additions 0, and average quality delta +0.7.
- Remaining lessons: support replies should keep the positive remedy path before a
  refund-denial boundary, and confirmation clauses such as `as soon as you reply`
  need to be locked as next-step dependencies.

## 2026-05-25 Single-Input Dev-20 Expansion Lessons

- Dev 20 final v5 passed 20/20 customer-usable and 20/20 strict signal on
  `docs/rewrite-email-eval-cases-100.md` cases 001-020.
- Line-wrapped Markdown prose can hide facts when extraction splits on single newlines.
  Normalize wrapped lines before sentence extraction so dates such as `May 6` remain
  visible to deterministic gates.
- Conditional next-step dependencies are locked facts when they contain concrete user
  actions, for example `If ... please send one updated photo today`.
- Extractive fallback must protect `a.m.` and `p.m.` abbreviations before sentence
  splitting. Otherwise it creates false fragments such as `Because the room is
  unavailable` and `Or Friday at 9 a.m.`.
- Structural fragment gates should reject actual detached `Because` / `Or` fragments
  after time abbreviations, but allow valid lowercase continuations and valid `If`
  action sentences after a time.
- Upload-evidence facts such as `empty file attempt`, named project handoff phrases,
  and operational reasons such as `room is unavailable` need deterministic anchors
  because they are easy for warm rewrites to compress away.
- Provider reruns should not full-run dev-20 after every small patch. The correct
  loop is unit test first, then failed case ids plus a small sentinel set, then one
  full dev-20 validation only after the partial passes.
- The eval runner now supports `--case-ids` so focused mode can run the current
  failures first while keeping `--limit` applied after filtering. Reports include
  the selected case ids for auditability.
- Provider failures must be classified before rerun: judge false positive, rewrite
  fact drift, extraction/ledger miss, provider/infrastructure failure, or artifact
  ambiguity. `rewrite-draft-007` and `rewrite-draft-013` showed true rewrite fact
  drift: source anchors such as `product team` and `Beacon handoff` were compressed
  into role/project context and omitted from the final text. These are not judge
  bugs unless the final text explicitly preserves the anchor.
- Rewrite fact drift needs deterministic unit coverage before another provider
  replay. Add a small phrasing matrix for the observed compressed output shape,
  then run only the failed case plus sentinels. Full dev-20 remains the final
  validation step after partial passes.
- Eval artifacts must expose extracted facts and reviewed/locked ledgers. Without
  those intermediate records, a missing final fact cannot be reliably attributed to
  extraction, ledger review, generation, finalization, restoration, or judge logic.
- Property/logistics replies must keep the access choice explicit when the source
  asks whether someone can provide access or whether a lockbox code should be used.
  Saying only that the lockbox is an option plus `access confirmation first` is too
  weak and can fail the actionability gate.
- The same stabilizers must run on deterministic fallback candidates before fact
  gates and signal measurement. Otherwise fallback can reintroduce formatting
  regressions such as splitting `Dr. Chen's queue` into `Dr.` and `Chen's queue`
  across separate paragraphs.
- Next expansion should materialize cases 021-040 and run a controlled dev-40 pass.
  Full 100 should remain a release or major-strategy-change gate.
