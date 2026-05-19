# Unified Fact-Preserving Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the core product reliable for the simplest user workflow: optional context/message plus required draft, rewritten with lower AI-like signal while preserving facts.

**Architecture:** Remove user-facing scenario selection from the main workflow. Build one unified fact extraction and fact gate that runs before and after every rewrite, regardless of inferred scenario. Scenario inference may still exist internally for tone/risk guardrails, but it must not decide which facts are required.

**Tech Stack:** Next.js App Router, TypeScript, Vitest, OpenAI API, Sapling writing signal, Cloudflare Workers/OpenNext, Neon Postgres learning samples.

---

## Context

Recent live QA showed two important issues:

- When the user left the UI at the default `Blank / custom` scenario, teacher-parent replies routed into a generic fallback and returned `100% -> 100%`.
- When the user selected `Email or message reply`, the teacher-parent fallback worked and returned `100% -> 0%`, preserving the visible facts.

The next iteration should reduce the chance of user error by removing scenario selection from the main UI and moving routing decisions into backend internals.

## Product Decisions For This Iteration

- Keep `messageToReplyTo` optional.
- Keep `roughDraftReply` required.
- Optimize for draft-only usage as a first-class path. Many users will paste only their own draft and no original message.
- Remove the user-facing scenario selector from `/app`.
- Keep internal scenario/mode inference only as a guardrail helper.
- Replace four tone presets with two visible tones: `Warm` and `Direct`.
- Default tone: `Warm`.
- `Direct` must mean less padding and fewer relationship phrases, not fewer facts.
- Fact extraction must be unified across all inputs. Do not use scenario-specific fact extraction.
- Final result selection must prioritize:
  1. fact preservation
  2. no unsupported new facts
  3. usable natural writing
  4. lower AI-like signal
- Do not return a bad rewrite as normal success just because a fallback exists.
- Do not show a blank failure if a fact-preserving rewrite can be produced.
- Do not deploy this iteration while any known evaluation sample fails. The release gate is 100% customer-usable pass rate on the current evaluation suite, with no fact preservation failures and no unsupported fact additions.

## Non-Goals

- Do not add more user-facing scenarios.
- Do not add more tone buttons.
- Do not build a full user feedback system yet.
- Do not hot-load production prompt changes from database learning rows.
- Do not make Sapling score the only quality definition.
- Do not do Stripe, quota, Azure, or billing changes in this iteration.
- Do not change infrastructure unless deployment verification exposes an existing frontend/backend deployment failure.

## Proposed User Experience

Main `/app` workflow:

```text
Context or message     optional
Draft to rewrite       required
Tone                   Warm | Direct
Begin rewrite
```

No visible scenario card in the main workflow.

Optional future advanced controls can be considered later, but they are not part of this iteration.

## Proposed Backend Flow

```text
1. Normalize input
   - messageToReplyTo optional
   - roughDraftReply required
   - tonePreset is Warm or Direct

2. Extract required facts from all available user-provided text
   - source = messageToReplyTo + roughDraftReply
   - do not branch fact extraction by scenario
   - produce RequiredFact[]

3. Infer internal mode
   - teacher/parent, support, sales, workplace, general reply, or draft-only
   - use only for style guardrails and risk notes
   - never use this to ignore facts

4. Generate rewrite candidates
   - Warm or Direct tone
   - include required facts in prompt context
   - avoid unsupported new facts

5. Check each candidate
   - required facts preserved
   - no unsupported facts introduced
   - no severe over-compression
   - writing signal improved when provider score is available

6. Repair failed candidates
   - missing facts -> restore exact facts
   - unsupported facts -> remove unsupported content
   - high AI-like signal -> remove template phrasing and vary rhythm

7. Final fallback
   - select best fact-preserving candidate
   - if no candidate passes fact gate, run deterministic/plain-language cleanup
   - return only a fact-preserving result
   - if signal is still high, show honest Naturalness Check and review note
```

## Quality Bar

The next run must use a stricter customer-usable quality gate before deployment.

Important scope:

- The system cannot honestly guarantee 100% success for every arbitrary future user input, external provider outage, or ambiguous draft.
- The release requirement for this iteration is stronger and concrete: 100% pass rate on the current curated evaluation suite and no known unresolved live-regression case.
- If a new failure appears during testing, it becomes part of the suite before release.

Definition of a customer-usable pass:

- required facts preserved
- no unsupported facts added
- no malformed output such as duplicated greetings or `Quick update: Hi Monica,.`
- tone matches `Warm` or `Direct`
- output is meaningfully different from the input unless the input is already strong
- if the Sapling score is available, the rewrite is not worse than the draft
- target is below 50% AI-like signal or at least 30 points lower when feasible
- if signal remains high, the output still preserves facts and includes an honest review note

Deployment threshold:

```text
Minimum seed evaluation cases: 40
Minimum final evaluation cases before deploy: 60
Minimum draft-only cases before deploy: 40
Minimum customer-usable pass rate before deploy: 100% on the known suite
Maximum allowed unresolved failures before deploy: 0
Final selected rewrites worse than draft: 0
Fact preservation failures allowed: 0
Unsupported fact additions allowed: 0
```

If any evaluation run is below 100% on known samples, do not push/deploy as final. Continue the loop:

```text
analyze failed cases -> update strategy memory -> add/adjust tests -> fix rewrite logic -> rerun full eval
```

Only deploy after the threshold is met or after the user explicitly accepts a lower result.

## Testing And Learning Mode

Testing/learning mode is allowed to be more aggressive than production. Its job is to discover why samples fail and promote the fix into code, tests, and strategy memory.

For every failed sample:

```text
1. classify failure
   - missing fact
   - unsupported new fact
   - malformed output
   - too short / over-compressed
   - still high AI-like signal
   - worse than draft
   - provider unavailable

2. retry in learning mode
   - same primary model with improved fact prompt
   - repair pass with explicit missing/unsupported facts
   - escalation model comparison
   - deterministic/plain-language cleanup

3. decide root cause
   - strategy/prompt problem
   - fact extraction problem
   - final selection problem
   - model capability problem
   - third-party signal/provider problem

4. promote fix
   - add or update a regression test
   - update rewrite strategy memory
   - update code/prompt/fallback/model routing
   - rerun full evaluation suite
```

Testing mode may try more candidates than production, but it must record the attempt count, model used, signal result, fact result, and final decision. Do not let testing-mode behavior silently become production behavior unless it is converted into a bounded production rule.

## Model Escalation Policy

Current model:

```text
OPENAI_MODEL=gpt-4o-mini
```

Next iteration should introduce explicit model tiers:

```env
OPENAI_MODEL_PRIMARY=gpt-4o-mini
OPENAI_MODEL_REPAIR=gpt-4o-mini
OPENAI_MODEL_ESCALATION=gpt-5.4-mini
OPENAI_MODEL_FINAL_STRONG=gpt-5.4
OPENAI_MAX_MODEL_CALLS_PER_REWRITE=3
OPENAI_ENABLE_FINAL_STRONG_MODEL=false
```

Testing/learning mode:

- use primary model first so strategy failures are visible
- if a case fails after strategy/repair, run the same case with the escalation model
- if escalation succeeds and primary repeatedly fails, add a bounded escalation rule
- if escalation also fails, treat it as a strategy/fact/fallback problem, not a model problem
- use the final strong model only for internal comparison or user-approved high-cost testing

Production mode target:

```text
1. primary rewrite
2. primary repair
3. escalation rewrite/repair only if strict failure conditions are met
4. deterministic/plain-language cleanup
```

Production should not run unbounded retries. If the best fact-preserving result still has a high writing signal, return it with an honest review note rather than hiding the limitation.

## File Map

Expected modifications:

- `components/app/rewrite-workspace.tsx`
  - remove visible scenario selector
  - replace tone buttons with `Warm` and `Direct`
  - default to `Warm`
  - keep context/message optional and draft required

- `lib/rewrite-presets.ts`
  - reduce visible tone presets to `Warm` and `Direct`
  - keep internal scenario types if existing tests or logging still need them

- `lib/validation.ts`
  - accept `Warm` and `Direct`
  - keep backward compatibility for old payloads if needed

- `lib/facts.ts` or `lib/fact-extraction.ts`
  - create a unified `RequiredFact` type
  - extract names, roles, dates, deadlines, amounts, counts, tasks, ordered steps, constraints, promises, signoffs, and quoted must-keep phrases

- `lib/rewrite.ts`
  - call unified fact extraction once per request
  - use facts in completeness checks
  - reject candidates that drop required facts or introduce unsupported facts
  - change final fallback selection so `100% -> 100%` weak rewrites are not treated as clean success without a warning

- `lib/openai.ts`
  - update prompts to use required facts
  - make Warm and Direct behavior explicit
  - remove dependence on user-selected scenario for the main path

- `lib/rewrite-diagnosis.ts`
  - keep internal mode/diagnosis as guardrails only
  - do not drive fact extraction from scenario

- `tests/unit/fact-extraction.test.ts`
  - new tests for unified fact extraction

- `tests/unit/rewrite-quality.test.ts`
  - tests for fact gate and unsupported fact rejection

- `tests/unit/openai-output.test.ts`
  - tests for final deterministic/plain-language cleanup

- `tests/unit/rewrite-presets.test.ts`
  - update tone expectations

- `docs/rewrite-strategy-memory.md`
  - record the unified fact preservation rule

- `docs/rewrite-learning-system.md`
  - record that live learning promotes through facts/tests/docs/code, not scenario-specific one-off patches

- `docs/scenario-evaluation-results.md` or a new eval result doc
  - record the next measured evaluation run

## Required Types

Target shape:

```ts
export type RequiredFactCategory =
  | "person"
  | "role"
  | "date"
  | "deadline"
  | "amount"
  | "count"
  | "task"
  | "ordered_step"
  | "constraint"
  | "promise"
  | "signoff"
  | "quoted_phrase";

export type RequiredFact = {
  id: string;
  text: string;
  normalizedText: string;
  category: RequiredFactCategory;
  source: "message" | "draft" | "both";
  required: true;
};
```

## Tone Contract

Warm:

- may include a short relationship phrase
- sounds human and helpful
- keeps necessary softening when relationship matters
- must not become polished corporate language

Direct:

- uses fewer softeners
- shorter sentences and paragraphs
- clearer action order
- must preserve the same RequiredFact list
- must not behave like the old `Concise` mode that can delete facts

## Test Sample Matrix

Each sample must run through both `Warm` and `Direct` unless noted.

Pass rules:

- all required facts preserved
- no unsupported names, dates, amounts, promises, discounts, outcomes, policies, or signoffs added
- if Sapling is available: target below 50% or at least 30-point reduction when feasible
- if strict signal target cannot pass: final output must still be fact-preserving and must include an honest review note
- no result should show malformed prefixes such as `Quick update: Hi Monica,.`
- at least 40 of the final 60+ eval cases must be draft-only.

### Short Samples

1. Teacher-parent reply, draft only
   - Facts: Monica, Jordan, reading response, vocabulary practice, short reflection paragraph from Friday, end of this week, partial credit, after class/lunch, Ms. Carter.
   - Expected: no scenario selection needed; fact-preserving rewrite; lower signal.

2. Teacher-parent reply, context plus draft
   - Context: parent asks what is missing and whether make-up work is allowed.
   - Draft contains full teacher response.
   - Expected: context helps framing but does not invent new policy.

3. Sales follow-up
   - Facts: proposal sent Tuesday, customer comparing two vendors, may need another week, no discount promised.
   - Expected: no invented meeting, discount, deadline, or commitment.

4. Workplace update
   - Facts: source file arrived late, revised numbers needed, partner update, final version by 4pm Friday.
   - Expected: direct version can be shorter but must preserve all four facts.

5. Client report reply
   - Facts: Priya, report totals changed, hidden category now included, line-by-line note today.
   - Expected: no fake apology escalation or unsupported root cause.

### Medium Samples

6. Customer support billing
   - Facts: May usage report, 18 active seats, 15 approved seats, three temporary contractors, NZD $126 increase, finance manager.
   - Expected: all numbers and role phrase preserved.

7. Customer support workspace access
   - Facts: old pilot workspace, resent invite twice, user still lands in wrong workspace, support should check association.
   - Expected: no invented password reset, no new ticket status.

8. Sales/customer relationship reply
   - Facts: still comparing vendors, reporting feature, team templates, first week of June.
   - Expected: preserve product details and timing.

9. Draft-only generic note
   - Facts: section three pricing language, section five implementation timeline, May 12 call.
   - Expected: no scenario selector; no malformed `Quick update` prefix.

### Long Samples

10. Long teacher-parent grade reply
    - Facts: same teacher facts plus participation in discussions, missing written work affecting grade, partnership/responsibility closing.
    - Expected: preserve enough explanation without returning a polished memo.

11. Long billing/proration reply
    - Facts: current plan, old/new plan charge or credit, period dates, seat counts, amount, finance manager.
    - Expected: lower macro feel, preserve all billing facts.

12. Long support incident update
    - Facts: ticket number, duplicate notifications, opened Friday, delivery logs under review, decision before noon, pause campaign not confirmed.
    - Expected: no unsupported root cause or false promise.

13. Long messy email thread
    - Includes quoted older messages, signatures, and repeated greetings.
    - Expected: extract current reply facts without copying old quoted text as new promises.

14. Long workplace/customer update with multiple tasks
    - Facts: three next steps in order, one blocker, one owner, one deadline.
    - Expected: ordered steps preserved.

### Edge And Failure Samples

15. Message empty, draft only
    - Expected: works without optional context.

16. Message filled, draft short
    - Expected: rewrite the draft, not the whole incoming message.

17. Direct tone with many facts
    - Expected: shorter style but same fact list.

18. Draft contains unsupported promise-like wording
    - Expected: preserve it if the user wrote it, but do not add any new promise.

19. Provider signal unavailable
    - Expected: return fact-preserving rewrite with neutral signal unavailable state.

20. All generated candidates fail fact gate
    - Expected: deterministic/plain-language cleanup produces a fact-preserving output or the API returns a non-charged quality failure if no safe output exists.

### Additional Draft-Only Samples

These are required because many real users will not paste the original message.

21. Draft-only teacher absence note
    - Facts: Alex missed class Tuesday, assignment can be submitted by Friday, no grade penalty if submitted by then.
    - Expected: preserve student name, date, deadline, and penalty condition.

22. Draft-only parent behavior update
    - Facts: Sam interrupted group work twice, improved after seat change, check-in planned tomorrow.
    - Expected: no invented discipline action or meeting.

23. Draft-only sales renewal nudge
    - Facts: renewal is due June 30, current plan includes 12 seats, customer asked for usage summary.
    - Expected: no invented discount or contract terms.

24. Draft-only customer refund boundary
    - Facts: refund window ended May 10, support can offer account credit, manager approval required.
    - Expected: no promise of refund.

25. Draft-only project delay update
    - Facts: design handoff moved to Thursday, engineering review starts Friday, launch date unchanged.
    - Expected: preserve sequence and unchanged launch date.

26. Draft-only manager escalation
    - Facts: two blockers, vendor API timeout, legal approval pending, next update by 3pm.
    - Expected: preserve blockers and update time.

27. Draft-only client report correction
    - Facts: April chart used old filter, corrected report attached, totals changed by 3.8%.
    - Expected: preserve percentage and reason.

28. Draft-only vendor reply
    - Facts: invoice PO number is missing, payment cannot process until corrected, deadline Wednesday.
    - Expected: no invented payment date.

29. Draft-only apology without overpromising
    - Facts: reply was delayed, user checked logs this morning, will send confirmed answer after review.
    - Expected: no invented resolution or compensation.

30. Draft-only short Slack-style update
    - Facts: build passed, migration still running, smoke test starts after database is ready.
    - Expected: Direct tone remains short but preserves all three steps.

31. Draft-only messy AI paragraph
    - Facts: parent meeting is Thursday 2pm, student should bring draft outline, final essay due Monday.
    - Expected: remove polished wording and preserve all dates/tasks.

32. Draft-only customer onboarding note
    - Facts: SSO setup is complete, CSV import failed on 14 rows, team will resend cleaned file today.
    - Expected: preserve SSO, CSV count, and today.

33. Draft-only HR/interview reply
    - Facts: interview availability Tuesday morning or Thursday after 1pm, remote preferred, resume attached.
    - Expected: no invented timezone or location.

34. Draft-only invoice explanation
    - Facts: tax line increased because billing address changed to Australia, base subscription unchanged.
    - Expected: preserve cause and unchanged base subscription.

35. Draft-only progress note with names
    - Facts: Nina owns API fix, Omar owns QA script, both due before Friday demo.
    - Expected: preserve ownership and deadline.

36. Draft-only client cancellation boundary
    - Facts: cancellation can be scheduled for June 1, data export available for 30 days, admin must confirm.
    - Expected: no immediate cancellation promise.

37. Draft-only school policy reply
    - Facts: retake allowed once, highest score capped at 85, request must be submitted by Wednesday.
    - Expected: preserve policy limit exactly.

38. Draft-only bug report response
    - Facts: issue affects Safari only, Chrome workaround works, fix is planned for next patch.
    - Expected: no exact release date invented.

39. Draft-only stakeholder update
    - Facts: budget file is locked, finance needs to unlock it, numbers cannot be finalized until then.
    - Expected: preserve dependency and no false completion.

40. Draft-only polite decline
    - Facts: cannot join Friday call, can review notes Monday, no decision before review.
    - Expected: preserve refusal, Monday review, and no decision.

### Failure-Driven Expansion Samples

Before deployment, add at least 20 more samples from:

- failed eval cases discovered during this run
- live QA failures
- cases where primary model failed but escalation model succeeded
- cases where both models failed and strategy/fact extraction had to change

Each added sample must include:

```text
input type: draft-only or context+draft
tone: Warm or Direct
required facts
unsupported facts to reject
observed failure
fix applied
final result
whether it became a regression test
```

## Implementation Tasks

### Task 1: Lock Product Surface

**Files:**
- Modify: `components/app/rewrite-workspace.tsx`
- Modify: `lib/rewrite-presets.ts`
- Modify: `tests/unit/rewrite-presets.test.ts`

- [ ] Write a failing test that visible tone options are exactly `Warm` and `Direct`.
- [ ] Write a failing test or component assertion that scenario selection is not part of the main submit payload.
- [ ] Remove the visible scenario card from `/app`.
- [ ] Keep backend compatibility by sending an internal default mode if needed.
- [ ] Run targeted UI/preset tests.

### Task 2: Add Unified Fact Extraction

**Files:**
- Create: `lib/fact-extraction.ts`
- Create: `tests/unit/fact-extraction.test.ts`

- [ ] Write failing tests for names, dates, amounts, counts, tasks, ordered steps, signoffs, and must-keep phrases.
- [ ] Implement deterministic extraction for explicit facts.
- [ ] Normalize facts for comparison without losing exact display text.
- [ ] Run fact extraction tests.

### Task 3: Replace Scenario-Specific Fact Gate

**Files:**
- Modify: `lib/rewrite.ts`
- Modify: `tests/unit/rewrite-quality.test.ts`

- [ ] Write failing tests showing facts are checked the same way for `Blank / custom`, email reply, support, and draft-only payloads.
- [ ] Replace one-off teacher-parent fact checks with unified required facts where possible.
- [ ] Keep any scenario-specific guardrail only when it protects style or risk, not fact selection.
- [ ] Run rewrite quality tests.

### Task 4: Update Rewrite And Repair Prompts

**Files:**
- Modify: `lib/openai.ts`
- Modify: `tests/unit/openai-output.test.ts`

- [ ] Write failing tests that Warm and Direct both preserve the same fact list.
- [ ] Pass RequiredFact summaries into rewrite and repair prompts.
- [ ] Make Direct explicitly preserve facts while reducing padding.
- [ ] Ensure malformed prefixes like `Quick update: Hi Monica,.` cannot pass.
- [ ] Run OpenAI output tests.

### Task 5: Final Fallback Behavior

**Files:**
- Modify: `lib/rewrite.ts`
- Modify: `tests/unit/rewrite-quality.test.ts`
- Modify: `app/api/rewrite/route.ts` only if response status/charging policy changes.

- [ ] Write failing tests for all-candidates-rejected cases.
- [ ] Select the best fact-preserving candidate when strict signal gates fail.
- [ ] Run deterministic/plain-language cleanup when no candidate passes the fact gate.
- [ ] If no safe output exists, return quality failure and do not charge usage.
- [ ] If a usable fact-preserving high-signal rewrite is returned, include a risk note.

### Task 6: Evaluation Set And Learning Agent Update

**Files:**
- Modify: `docs/rewrite-strategy-memory.md`
- Modify: `docs/rewrite-learning-system.md`
- Modify: `docs/scenario-evaluation-results.md` or create a dated eval result doc
- Modify or create eval fixtures under the existing scripts/tests pattern

- [ ] Add the 40-sample matrix above to the evaluation source.
- [ ] Add at least 20 failure-driven expansion samples before final deployment.
- [ ] Run Warm and Direct for required samples.
- [ ] Record draft score, rewrite score, fact preservation, unsupported facts, tone quality, and selection status.
- [ ] Calculate the customer-usable pass rate.
- [ ] If pass rate is below 100% on known samples, stop deployment and continue the repair loop.
- [ ] For failed cases, compare primary model, repair strategy, escalation model, and deterministic cleanup.
- [ ] Promote escalation model usage only when the same failure class is not fixed by strategy/fact-gate improvements.
- [ ] Run `npm run memory:rewrite` after enough learning samples exist.
- [ ] Promote repeated failures into tests plus strategy-memory entries.
- [ ] Update `docs/optimization-notes.md` with both failed and successful patterns.
- [ ] Update `docs/rewrite-strategy-memory.md` with reusable fixes.
- [ ] Update `AGENTS.md` only for stable development rules that should guide future autonomous runs.

### Task 7: Verification And Deployment

**Files:**
- No source edits unless verification fails.

- [ ] Run `npm test`.
- [ ] Run `npm run typecheck`.
- [ ] Run `npm run build`.
- [ ] Run banned-copy scan:

```bash
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

- [ ] Run local manual smoke for teacher, support, sales, and workplace samples.
- [ ] Commit in meaningful stages.
- [ ] Push to GitHub.
- [ ] Wait for GitHub Actions, including `cloudflare-worker` and the active backend deployment workflow.
- [ ] Confirm Cloudflare deployment.
- [ ] Confirm the .NET/Azure backend workflow remains green if it is still enabled for the repository.
- [ ] Remote smoke:

```bash
curl -sS -o /dev/null -w '%{http_code} %{url_effective}\n' https://replyinmyvoice.com/
curl -sS -o /dev/null -w '%{http_code} %{url_effective}\n' https://replyinmyvoice.com/pricing
curl -sS -o /dev/null -w '%{http_code} %{redirect_url}\n' https://replyinmyvoice.com/app
curl -sS -w '\n%{http_code}\n' https://replyinmyvoice.com/api/health/db
```

- [ ] If a separate Azure backend URL is active in the current deployment config, run its health endpoint smoke test and record the result in the eval/deploy notes.

## Acceptance Criteria

- A user can get a useful rewrite by filling only `Draft to rewrite`.
- A user can optionally add `Context or message`.
- User-facing app has no visible scenario selector in the main workflow.
- User-facing tone choices are exactly `Warm` and `Direct`.
- Unified fact extraction runs for every request.
- Candidate selection never treats a malformed `100% -> 100%` fallback as clean success.
- Direct tone preserves the same required facts as Warm.
- Final fallback prefers fact safety over score chasing.
- Learning docs record reusable failures and fixes.
- Evaluation set has at least 60 final cases, at least 40 draft-only cases, and 100% customer-usable pass rate on the known suite.
- Fact preservation failures are 0.
- Unsupported fact additions are 0.
- If any known sample fails, the agent continues fixing and rerunning evaluation instead of deploying.
- Model escalation is configured and tested in learning mode; production escalation remains bounded.
- The next deployed version passes local tests, CI, Cloudflare deploy, active backend workflow checks, and remote smoke.

## Open Questions For User

1. If the best fact-preserving rewrite still has a high AI-like signal, should it consume one rewrite usage?
   - Decision: charge only if the system returns a copyable rewritten result that is meaningfully different from the input. Do not charge for provider/server errors or no-safe-output quality failures.

2. Should `Context or message` stay visible above `Draft to rewrite`, or should the required draft field move first?
   - Decision: keep context first for the reply-focused product, but draft-only must still be a first-class tested path.

3. Should old homepage copy still mention teacher, sales, workplace, and client/customer examples?
   - Decision: yes. Examples are useful marketing categories, but they should not be user-facing routing controls in the app.

4. Should cover letters remain supported in this MVP?
   - Decision: do not advertise cover letters as a core workflow until the reply workflow is stable. Draft-only rewriting can still handle them as general drafts.
