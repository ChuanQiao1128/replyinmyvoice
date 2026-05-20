# Send-Ready Structure Rewrite Implementation Plan

> Superseded by `docs/superpowers/plans/2026-05-20-adaptive-rewrite-agent-orchestrator.md`. Keep this file as supporting detail for structural gates and Daniel regression, but use the adaptive orchestrator plan as the next long-run source of truth.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the rewrite engine from score-oriented sentence repair to send-ready structured email rewriting, especially for long customer-support and policy/options replies, with an internal strategy agent that can diagnose failures and choose the next repair/rewrite strategy automatically.

**Architecture:** Keep the current fact-reconstruct pipeline, but add a structural writing layer and a bounded `Rewrite Quality Strategist Agent` before Naturalness Check selection. The system should generate from facts into a realistic email shape, reject weak rewrites that only split or paraphrase the original, let the strategist choose targeted repair vs full restructure vs stronger-model escalation, and evaluate long support messages with send-ready formatting gates before deployment.

**Tech Stack:** Next.js 15 App Router, TypeScript, Vitest, OpenAI role-based model config, Sapling Naturalness Check, Cloudflare Workers/OpenNext, docs-based strategy memory.

---

## Context

The latest manual website test exposed a gap that the 40-case evaluation did not catch.

The Daniel course-transfer/refund support sample preserved facts, but the returned rewrite was not send-ready:

- it mostly copied the original sentence order;
- it split nearly every sentence into its own paragraph;
- it broke a numbered list into `1.` and `2.` as standalone lines;
- it broke quoted-summary boundaries;
- it retained too much customer-support macro wording;
- it optimized for measurable signal and fact preservation more than realistic email structure.

This is not only a checker issue. It is a writing-strategy issue. Adding more deterministic checks without changing generation would keep producing weak candidates and then repeatedly repairing symptoms.

## Non-Goals

- Do not market this as detector bypass, evasion, or guaranteed human writing.
- Do not feed Sapling scores, thresholds, or sentence scores into model prompts.
- Do not create an unbounded retry loop.
- Do not weaken the fact-preservation gate to improve style.
- Do not add user-visible sentence-level scoring or diagnosis labels.

## Current Pipeline Status

The current production route is a sequential internal agent workflow:

```text
Fact extractor
-> scenario classifier
-> style-card loader
-> candidate generator
-> reviewer
-> finalizer
-> deterministic and LLM fact gates
-> Sapling Naturalness Check gate
-> targeted sentence repair
-> strong-model escalation
-> quality failure / no charge
```

This is "multi-agent" in the product-engineering sense: each step has a separate role, prompt, model tier, output schema, and gate. It is not a free-form multi-agent chat where agents talk to each other. The advantage is controllability: facts, style, review, repair, and measurement are independently testable and bounded.

## Required New Internal Agent

Add a bounded internal agent named:

```text
Rewrite Quality Strategist Agent
```

Purpose:

```text
Given a failed candidate, failed checks, reviewer scores, writing-signal result, scenario, style card, and extracted facts, diagnose why the candidate failed and choose the next strategy.
```

This agent must not directly optimize for Sapling or any third-party scoring tool. It may see generic failure categories such as `naturalness_gate_failed`, `signal_not_improved`, `support_macro_voice`, or `sentence_per_paragraph`, but not the prompt "make it pass Sapling" and not detector-specific text.

The strategist output must be structured JSON, not free-form chat:

```ts
type RewriteStrategyDecision = {
  failureKinds: RewriteFailureKind[];
  nextStrategy:
    | "targeted_sentence_repair"
    | "full_structure_rewrite"
    | "facts_first_reconstruct"
    | "support_policy_options_rewrite"
    | "strong_model_restructure"
    | "quality_failure";
  rationale: string[];
  rewriteInstructions: string[];
  mustPreserveFacts: string[];
  mustAvoidPatterns: string[];
};
```

Allowed failure kinds:

```text
fact_loss
unsupported_fact
broken_numbered_list
broken_quote_boundary
sentence_per_paragraph
line_split_paraphrase
support_macro_voice
policy_memo_voice
too_formal
too_casual
too_short
too_long
signal_not_improved
low_signal_got_worse
provider_unavailable
```

Strategy rules:

```text
fact_loss -> facts_first_reconstruct or quality_failure if facts cannot be restored
unsupported_fact -> facts_first_reconstruct
broken_numbered_list -> full_structure_rewrite
broken_quote_boundary -> full_structure_rewrite
sentence_per_paragraph -> full_structure_rewrite
line_split_paraphrase -> full_structure_rewrite
support_macro_voice -> support_policy_options_rewrite or targeted_sentence_repair
signal_not_improved -> targeted_sentence_repair if only a few high-risk sentences exist; otherwise full_structure_rewrite
low_signal_got_worse -> full_structure_rewrite, not sentence repair
repeated failed structure repair -> strong_model_restructure
```

This is the main automation layer. During long-run development, Codex should not wait for the user to manually find each bad website output. It must run eval, collect failures, let the strategist categorize them, add/adjust tests and strategy rules, rerun eval, and document promoted lessons.

## Automatic Strategy Adjustment Protocol

During the next long development run, failures are expected. Do not stop when eval finds weak rewrites.

For each failed eval case:

```text
1. save the failed candidate metadata;
2. classify failure kinds with deterministic checks and the Rewrite Quality Strategist Agent;
3. choose the next strategy from the allowed strategy catalog;
4. add or update a regression test when the failure is reusable;
5. update prompts/style cards/routing logic only for the diagnosed failure pattern;
6. rerun the focused test;
7. rerun eval;
8. promote the lesson to docs/rewrite-strategy-memory.md only after it passes regression.
```

The long run may revise:

```text
style cards
reviewer rubric
candidate generator instructions
strategy router rules
targeted repair prompts
full restructure prompts
eval sample set
deterministic structure checks
```

The long run must not revise:

```text
user-facing positioning into bypass/evasion language
fact gates to be weaker
usage accounting rules
secret handling
production request caps without documenting the cost tradeoff
```

Production remains bounded. The strategist can choose among strategies, but it cannot create an unbounded loop.

## Root Cause

The eval set and gate definitions were incomplete:

- `scripts/eval-scenarios.ts` had many short draft-only samples but too few long support-policy samples.
- The documented 40-case run did not include enough 300+ word support emails with options, quoted summaries, policy constraints, and cancellation/refund language.
- Customer-usable pass focused on fact preservation, unsupported additions, quality failure, and Sapling score.
- Unit tests covered meta-language leakage and dangling closings, but not bad email structure.
- The generator and repair prompts allowed sentence-level repair to preserve a bad paragraph shape.

## Design Principle

For long customer-support and policy/options replies, the writing strategy must be:

```text
facts -> communication plan -> structured email -> reviewer -> send-ready gate -> Naturalness Check
```

Not:

```text
original email -> sentence-by-sentence paraphrase -> line splitting -> signal check
```

Sapling remains a reference gate at the end. It must not be the shape of the writing strategy.

## Target Behavior

For the Daniel sample, a good rewrite should look like a real support email:

```text
Hi Daniel,

Thanks for explaining what changed with your work schedule.

Your current registration is for the June weekend cohort, which starts on Saturday, 6 June. Because you contacted us before the course begins, you may still be able to move to a later cohort if seats are available.

The next available cohort currently starts on Saturday, 20 July. If you would rather attend that session, we can move your registration after you confirm. Your course access, learning materials, and live session links would be updated once the transfer is complete.

For a refund, our policy requires requests to be submitted at least seven days before the course begins. Since your message came in close to that deadline, we would need to check the exact registration timestamp before confirming whether a full refund is available. If it is not, we may still be able to offer a course credit or move your enrollment to a future session.

Please reply with the option you prefer: moving to the July cohort, or having us review your refund eligibility. We will not update your registration or cancel your current seat unless you clearly confirm.

Best regards,
Customer Support Team
```

This version is acceptable because:

- facts are preserved;
- related facts are grouped;
- paragraphs have natural length;
- no broken numbered list appears;
- no quote boundary is broken;
- it is not merely the original with extra line breaks.

## Files To Modify

- `lib/rewrite-pipeline/checks.ts`  
  Add send-ready structural checks and weak-rewrite checks.

- `lib/rewrite-pipeline/strategy-router.ts`  
  Create the `Rewrite Quality Strategist Agent` deterministic/LLM-backed strategy router. It should convert failed checks and reviewer/signal evidence into a structured `RewriteStrategyDecision`.

- `lib/rewrite-pipeline/model.ts`  
  Update generator, reviewer, finalizer, targeted repair, strategist, and escalation prompts so long support messages are rebuilt from facts into a realistic email structure.

- `lib/rewrite-pipeline/style-cards.ts`  
  Add or refine customer-support style cards for policy/options/refund/course-transfer messages.

- `lib/rewrite-pipeline/types.ts`  
  Add `RewriteFailureKind`, `RewriteStrategyDecision`, and fields needed for structure-plan output or reviewer score metadata.

- `lib/rewrite-pipeline/pipeline.ts`  
  Route structural failures through the `Rewrite Quality Strategist Agent` into targeted sentence repair, full restructure, facts-first reconstruct, support-policy rewrite, strong-model restructure, or quality failure.

- `scripts/eval-scenarios.ts`  
  Add long support-policy cases and make customer-usable pass include send-ready structural checks.

- `tests/unit/rewrite-pipeline-checks.test.ts`  
  Add deterministic tests for broken lists, sentence-per-paragraph output, broken quotes, and weak line-splitting rewrites.

- `tests/unit/rewrite-pipeline-model.test.ts`  
  Verify prompts instruct the model to group facts, avoid sentence-by-sentence paraphrase, and return structured strategist decisions.

- `tests/unit/rewrite-pipeline.test.ts`  
  Add pipeline tests showing a low-score but structurally broken candidate is rejected, diagnosed by the strategist, and repaired before return.

- `tests/unit/rewrite-strategy-router.test.ts`  
  Add tests for failure-kind to strategy mapping.

- `docs/rewrite-strategy-memory.md`  
  Record the promoted strategy and the Daniel regression.

- `docs/scenario-evaluation-results.md`  
  Record measured results after implementation.

## Task 1: Add Send-Ready Structural Checks

- [ ] **Step 1: Write tests for malformed support-email structure**

Add tests to `tests/unit/rewrite-pipeline-checks.test.ts`:

```ts
it("rejects a broken numbered list where markers are detached from items", () => {
  const rewritten = [
    "Hi Daniel,",
    "",
    "In plain terms, you currently have two possible options: 1.",
    "",
    "You can transfer your enrollment to a later cohort, subject to availability.",
    "",
    "2.",
    "",
    "You can request a refund review.",
  ].join("\n");

  const result = deterministicCheck(input, emptyFacts, rewritten, styleCard);

  expect(result.safe).toBe(false);
  expect(result.issues).toContain("malformed:broken_numbered_list");
});

it("rejects sentence-per-paragraph formatting for long support replies", () => {
  const rewritten = [
    "Hi Daniel,",
    "",
    "Thank you for contacting us and for explaining your situation in detail.",
    "",
    "We understand that your work schedule has changed unexpectedly.",
    "",
    "After reviewing the information you provided, it appears that your current enrollment is for the June weekend cohort.",
    "",
    "Since you notified us before the course start date, you may still be eligible to move your enrollment.",
    "",
    "At this stage, the next available cohort is scheduled to begin on Saturday, 20 July.",
  ].join("\n");

  const result = deterministicCheck(input, emptyFacts, rewritten, styleCard);

  expect(result.safe).toBe(false);
  expect(result.issues).toContain("malformed:sentence_per_paragraph");
});

it("rejects a broken quote boundary that merges quoted summary with the next instruction", () => {
  const rewritten =
    "The summary would be: \"A refund may also be possible, but it depends on the exact timing.\" Before we make any changes, please confirm.";

  const result = deterministicCheck(input, emptyFacts, rewritten, styleCard);

  expect(result.safe).toBe(false);
  expect(result.issues).toContain("malformed:broken_quote_boundary");
});
```

Use existing local helper shapes in the file instead of inventing global fixtures if names differ.

- [ ] **Step 2: Implement structural checks**

In `lib/rewrite-pipeline/checks.ts`, add helpers equivalent to:

```ts
function detectBrokenNumberedList(text: string) {
  return /\boptions:\s*1\.\s*(?:\n|$)/i.test(text) || /(?:^|\n)\s*\d+\.\s*(?:\n|$)/.test(text);
}

function detectSentencePerParagraph(text: string) {
  const paragraphs = text
    .trim()
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean);

  if (paragraphs.length < 6) {
    return false;
  }

  const proseParagraphs = paragraphs.filter(
    (paragraph) =>
      !/^(hi|hello|dear)\b/i.test(paragraph) &&
      !/^(best regards|thanks|sincerely|regards),?$/i.test(paragraph) &&
      paragraph.split(/\s+/).length >= 6,
  );

  const singleSentenceParagraphs = proseParagraphs.filter(
    (paragraph) => (paragraph.match(/[.!?](?:\s|$)/g) ?? []).length <= 1,
  );

  return proseParagraphs.length >= 5 && singleSentenceParagraphs.length / proseParagraphs.length >= 0.8;
}

function detectBrokenQuoteBoundary(text: string) {
  return /would be:\s*["“][\s\S]{40,}?["”]\s+(?:Before we make any changes|Please reply|If you would like)/i.test(text);
}
```

Add issues:

```text
malformed:broken_numbered_list
malformed:sentence_per_paragraph
malformed:broken_quote_boundary
```

- [ ] **Step 3: Run the focused tests**

```bash
npm test -- tests/unit/rewrite-pipeline-checks.test.ts
```

Expected: tests pass after implementation.

## Task 1A: Add Rewrite Quality Strategist Agent

- [ ] **Step 1: Add strategy-router tests**

Create `tests/unit/rewrite-strategy-router.test.ts` with cases proving the strategy router does not blindly retry:

```ts
import { describe, expect, it } from "vitest";

import { chooseRewriteStrategy } from "../../lib/rewrite-pipeline/strategy-router";

describe("chooseRewriteStrategy", () => {
  it("routes structural list failures to full structure rewrite", () => {
    const decision = chooseRewriteStrategy({
      deterministicIssues: ["malformed:broken_numbered_list"],
      reviewerIssues: [],
      naturalnessFailure: false,
      highRiskSentenceCount: 0,
      failedAttempts: 1,
      scenarioDomain: "customer_support",
      styleCardId: "customer_support_policy_options",
      missingFacts: [],
      unsupportedFacts: [],
    });

    expect(decision.failureKinds).toContain("broken_numbered_list");
    expect(decision.nextStrategy).toBe("full_structure_rewrite");
    expect(decision.rewriteInstructions.join(" ")).toMatch(/group related facts/i);
  });

  it("routes line-split paraphrase to full structure rewrite", () => {
    const decision = chooseRewriteStrategy({
      deterministicIssues: ["weak_rewrite:line_split_paraphrase"],
      reviewerIssues: [],
      naturalnessFailure: true,
      highRiskSentenceCount: 0,
      failedAttempts: 1,
      scenarioDomain: "customer_support",
      styleCardId: "customer_support_policy_options",
      missingFacts: [],
      unsupportedFacts: [],
    });

    expect(decision.failureKinds).toContain("line_split_paraphrase");
    expect(decision.nextStrategy).toBe("full_structure_rewrite");
  });

  it("uses targeted repair only when the failure is sentence-level", () => {
    const decision = chooseRewriteStrategy({
      deterministicIssues: [],
      reviewerIssues: ["support_macro_voice"],
      naturalnessFailure: true,
      highRiskSentenceCount: 2,
      failedAttempts: 1,
      scenarioDomain: "customer_support",
      styleCardId: "customer_support_resolution",
      missingFacts: [],
      unsupportedFacts: [],
    });

    expect(decision.failureKinds).toContain("support_macro_voice");
    expect(decision.nextStrategy).toBe("targeted_sentence_repair");
  });

  it("does not continue after repeated failed structure attempts", () => {
    const decision = chooseRewriteStrategy({
      deterministicIssues: ["malformed:sentence_per_paragraph"],
      reviewerIssues: [],
      naturalnessFailure: true,
      highRiskSentenceCount: 0,
      failedAttempts: 3,
      scenarioDomain: "customer_support",
      styleCardId: "customer_support_policy_options",
      missingFacts: [],
      unsupportedFacts: [],
    });

    expect(decision.nextStrategy).toBe("strong_model_restructure");
  });
});
```

- [ ] **Step 2: Create `lib/rewrite-pipeline/strategy-router.ts`**

Implement deterministic strategy selection first. Keep an LLM-backed strategist optional until deterministic routing passes.

Required exports:

```ts
export type RewriteFailureKind =
  | "fact_loss"
  | "unsupported_fact"
  | "broken_numbered_list"
  | "broken_quote_boundary"
  | "sentence_per_paragraph"
  | "line_split_paraphrase"
  | "support_macro_voice"
  | "policy_memo_voice"
  | "too_formal"
  | "too_casual"
  | "too_short"
  | "too_long"
  | "signal_not_improved"
  | "low_signal_got_worse"
  | "provider_unavailable";

export type RewriteStrategyDecision = {
  failureKinds: RewriteFailureKind[];
  nextStrategy:
    | "targeted_sentence_repair"
    | "full_structure_rewrite"
    | "facts_first_reconstruct"
    | "support_policy_options_rewrite"
    | "strong_model_restructure"
    | "quality_failure";
  rationale: string[];
  rewriteInstructions: string[];
  mustPreserveFacts: string[];
  mustAvoidPatterns: string[];
};
```

The router must map:

```text
missing facts -> facts_first_reconstruct
unsupported facts -> facts_first_reconstruct
broken list / broken quote / sentence-per-paragraph / line-split paraphrase -> full_structure_rewrite
support macro voice with <= 3 high-risk sentences -> targeted_sentence_repair
support macro voice with broad structural issues -> support_policy_options_rewrite
repeated structural failures -> strong_model_restructure
provider unavailable -> quality_failure
```

- [ ] **Step 3: Add optional LLM strategist prompt**

In `lib/rewrite-pipeline/model.ts`, add an optional `diagnoseRewriteStrategy` helper only after deterministic routing exists.

Prompt requirements:

```text
You are an internal rewrite strategy reviewer.
Choose the next rewrite strategy from the allowed strategy list.
Do not optimize for third-party scoring tools.
Do not mention Sapling or specific score thresholds.
Do not propose unbounded retries.
Return valid JSON only.
```

The LLM strategist may refine `rewriteInstructions`, but it must not override deterministic hard safety routing:

```text
fact_loss cannot route to naturalness-only repair
unsupported_fact cannot route to naturalness-only repair
broken_numbered_list cannot route to targeted_sentence_repair
sentence_per_paragraph cannot route to targeted_sentence_repair
provider_unavailable cannot route to another model call
```

- [ ] **Step 4: Wire strategist into pipeline**

In `lib/rewrite-pipeline/pipeline.ts`, after a candidate fails deterministic, reviewer, fact, or Naturalness gates:

```text
collect deterministic issues
collect missing/unsupported facts
collect reviewer issues
collect high-risk sentence count
call chooseRewriteStrategy
execute the selected strategy
record strategy decision in candidateSignals or optimization metadata
```

The pipeline must stop returning weak fallbacks as success. A final result must pass:

```text
fact gate
structural send-ready gate
Naturalness Check rule
```

- [ ] **Step 5: Run strategy-router tests**

```bash
npm test -- tests/unit/rewrite-strategy-router.test.ts
```

Expected: all strategy routing tests pass.

## Task 2: Add Weak-Rewrite Similarity Detection

- [ ] **Step 1: Write a failing test**

Add a test showing that line-splitting the original is rejected even if facts are preserved:

```ts
it("rejects a weak rewrite that mostly preserves original sentence order and only changes line breaks", () => {
  const original =
    "Hi Daniel, Thank you for contacting us and for explaining your situation in detail. Your current enrollment is for the June weekend cohort. The next available cohort is Saturday, 20 July.";
  const rewritten =
    "Hi Daniel,\n\nThank you for contacting us and for explaining your situation in detail.\n\nYour current enrollment is for the June weekend cohort.\n\nThe next available cohort is Saturday, 20 July.";

  const result = deterministicCheck(
    { ...input, roughDraftReply: original },
    emptyFacts,
    rewritten,
    styleCard,
  );

  expect(result.safe).toBe(false);
  expect(result.issues).toContain("weak_rewrite:line_split_paraphrase");
});
```

- [ ] **Step 2: Implement approximate similarity**

In `lib/rewrite-pipeline/checks.ts`, compare normalized source text and output when the source is long enough:

```ts
function normalizedWordSet(text: string) {
  return new Set(
    normalize(text)
      .replace(/[^a-z0-9' -]+/g, " ")
      .split(/\s+/)
      .filter((word) => word.length > 3),
  );
}

function jaccardSimilarity(a: Set<string>, b: Set<string>) {
  const intersection = [...a].filter((word) => b.has(word)).length;
  const union = new Set([...a, ...b]).size;
  return union === 0 ? 0 : intersection / union;
}
```

Flag `weak_rewrite:line_split_paraphrase` when:

```text
original roughDraftReply has at least 80 words
rewritten has many paragraph breaks
word-set similarity is >= 0.82
rewritten paragraph count is much higher than original paragraph count
```

Do not use this check on very short drafts; short factual messages often need high overlap.

## Task 3: Add Support Policy/Options Style Card

- [ ] **Step 1: Add style card**

In `lib/rewrite-pipeline/style-cards.ts`, add:

```ts
customer_support_policy_options: {
  style_card_id: "customer_support_policy_options",
  voice: "calm, practical, and specific",
  paragraph_style: "group related facts into 4 to 6 natural email paragraphs; do not put every sentence in its own paragraph",
  sentence_style: "plain sentences with varied length; avoid policy-memo rhythm",
  opening_style: "acknowledge the concrete situation in one sentence",
  body_style: "explain current status, available option, policy constraint, and required confirmation in that order",
  closing_style: "ask for the user's preferred next step without extra appreciation filler",
  good_phrases: [
    "Thanks for explaining what changed.",
    "Your current registration is",
    "If that works better for you",
    "For a refund,"
  ],
  phrases_to_avoid_or_limit: [
    "Thank you for contacting us and for explaining your situation in detail",
    "After reviewing the information you provided",
    "Regarding your question",
    "In plain terms",
    "If you would like to explain this internally",
    "Thank you again for your understanding"
  ],
  rules: [
    "Preserve policy rules, dates, cohort names, refund conditions, and no-change-without-confirmation constraints.",
    "Use a normal numbered list only if each marker and item stay on the same line.",
    "Prefer prose options over broken list formatting.",
    "Do not include a quoted internal summary unless the user explicitly asked for a reusable wording block.",
    "Do not split each sentence into a separate paragraph."
  ]
}
```

- [ ] **Step 2: Update classifier allowed style-card list**

In `lib/rewrite-pipeline/model.ts`, include `customer_support_policy_options` in the allowed style cards and tell the classifier to choose it for refund, course transfer, enrollment change, cancellation, eligibility review, policy deadline, or options-confirmation support replies.

## Task 4: Change Generation From Sentence Repair To Structured Rewrite

- [ ] **Step 1: Update candidate generator prompt**

In `generateCandidates`, add requirements:

```text
For long support, policy, refund, course-transfer, cancellation, or options replies:
- build a fresh email from the facts instead of paraphrasing sentence by sentence;
- group related facts into natural paragraphs;
- do not preserve the original sentence order when a clearer structure is possible;
- do not put every sentence in its own paragraph;
- do not create detached list markers such as "1." on its own line;
- omit quoted internal summaries unless the user explicitly asked for wording they can reuse.
```

- [ ] **Step 2: Update reviewer prompt**

Add reviewer penalties:

```text
Penalize sentence-per-paragraph output, broken numbered lists, broken quote boundaries, and rewrites that only change line breaks while preserving the original sentence order.
For long support replies, prefer a structured email with grouped facts over a low-score extractive rewrite.
```

- [ ] **Step 3: Update finalizer and escalation prompts**

Add:

```text
If the selected candidate has mechanical paragraphing or list formatting, restructure the whole email from the facts instead of lightly editing one sentence.
Keep related facts together. Do not split every sentence into its own paragraph.
```

- [ ] **Step 4: Update targeted repair routing**

In `lib/rewrite-pipeline/pipeline.ts`, if deterministic issues include:

```text
malformed:broken_numbered_list
malformed:sentence_per_paragraph
malformed:broken_quote_boundary
weak_rewrite:line_split_paraphrase
```

then skip sentence-only repair and route directly to full restructure escalation, because sentence-level repair preserves the bad structure.

## Task 5: Expand Evaluation Samples

- [ ] **Step 1: Add at least 12 long support-policy cases**

Update `scripts/eval-scenarios.ts` with cases covering:

```text
course transfer + refund review + exact cohort dates + no-change-without-confirmation
subscription cancellation + refund eligibility + account access dates
shipping delay + replacement option + refund condition + no address change without confirmation
invoice dispute + plan change + prorated amount + finance-manager explanation
event reschedule + ticket transfer + refund deadline + accessibility request
appointment reschedule + cancellation fee + credit option + confirmation required
membership pause + next billing date + credit balance + reactivation condition
training cohort waitlist + seat availability + materials update + support contact
vendor onboarding delay + policy exception review + two possible next steps
customer support escalation + exact ticket number + response-time promise already provided
```

Each case must have:

```text
300-900 words where possible
policy/condition facts
at least one date/time/deadline
at least one "do not change without confirmation" fact
expected facts array
```

- [ ] **Step 2: Include Daniel regression exactly**

Add the Daniel course-transfer/refund sample as a named regression:

```text
support-02-daniel-course-transfer-refund
```

Expected facts must include:

```text
Daniel
work schedule changed unexpectedly
June weekend cohort
Saturday, 6 June
before the course start date
later cohort
seat availability
Saturday, 20 July
course access
learning materials
live session links
refund requests must be submitted at least seven days before the course begins
exact registration timestamp
full refund
course credit
future session
move to July cohort
refund review
will not update registration or cancel current seat unless clearly confirmed
Customer Support Team
```

- [ ] **Step 3: Make eval fail on structural issues**

In the eval runner, call `deterministicCheck` on final selected rewrites and record:

```text
sendReadyStructuralPass
structuralIssues
weakRewriteIssues
customerUsablePass
```

Customer-usable pass must require:

```text
facts preserved
no unsupported facts
no deterministic send-ready structural issues
no weak line-split paraphrase
no quality failure
Naturalness Check not worse than draft
```

## Task 6: Verify And Deploy

- [ ] **Step 1: Run unit tests**

```bash
npm test
```

Expected: all tests pass.

- [ ] **Step 2: Run type/lint/build**

```bash
npm run typecheck
npm run lint
npm run build
```

Expected: all pass.

- [ ] **Step 3: Run focused eval**

```bash
npm run eval:scenarios
```

Required before push/deploy:

```text
Daniel regression passes customer-usable gate.
At least 12 long support-policy cases are measured.
0 structurally malformed successful rewrites.
0 weak line-split paraphrase successful rewrites.
All measured successful rewrites preserve expected facts.
No final selected rewrite is worse than the draft when scores are available.
```

- [ ] **Step 4: Update docs**

Update:

```text
docs/rewrite-strategy-memory.md
docs/scenario-evaluation-results.md
```

Record:

```text
sample count
long support-policy count
Daniel regression result
structural failure count
weak-rewrite failure count
Naturalness Check summary
fact-preservation summary
```

- [ ] **Step 5: Deploy only after verification passes**

```bash
npm run cf:build
npx wrangler deploy
```

Then smoke:

```text
https://replyinmyvoice.com/
https://replyinmyvoice.com/api/health/db
```

## Stop Conditions

Stop only if:

- required secrets are missing or invalid;
- Sapling is unavailable long enough that measured evaluation cannot complete;
- Cloudflare deploy permission is denied;
- GitHub push is denied;
- continuing would expose or commit secrets;
- a real Stripe live-mode charge or production billing action is required.

Do not stop for ordinary prompt failures, low Naturalness Check scores, bad candidates, broken formatting, unit test failures, build failures, or eval failures. Fix the strategy and continue.

## Resume/Interview Impact

After this plan is implemented, the rewrite quality claim becomes stronger:

```text
Built a bounded multi-step rewrite pipeline that extracts facts, classifies communication context, generates multiple structured candidates, reviews factual/style quality, applies send-ready formatting gates, repairs targeted failures, and uses a third-party Naturalness Check as a final reference signal without weakening fact preservation.
```

Do not claim the system guarantees a low AI-like score. Claim bounded quality gates, fact preservation, structured rewrites, and no-charge quality failures.
