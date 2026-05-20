# Adaptive Rewrite Agent Orchestrator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mostly fixed rewrite flow with a bounded adaptive rewrite agent orchestrator that analyzes each input, chooses an initial strategy, diagnoses failed candidates, switches strategies within budget, reruns evaluation, and returns only fact-safe, send-ready, Naturalness Check-safe results.

**Architecture:** Keep the existing fact-reconstruct pipeline as the safety frame, but add input analysis, initial strategy routing, policy/intent gates, budget management, and an adaptive control loop around generation, review, repair, full restructure, fallback, and escalation. The outer loop is deterministic and bounded; the inner strategy is dynamic through a `Rewrite Quality Strategist Agent`, a strategy catalog, quality gates, and eval-driven learning records.

**Tech Stack:** Next.js 15 App Router, TypeScript, Vitest, OpenAI role-based model config, Sapling Naturalness Check, Cloudflare Workers/OpenNext, Prisma/Neon learning samples, docs-based strategy memory.

---

## Context

Manual website QA showed that the current rewrite system can preserve facts and improve or pass the Naturalness Check while still returning a weak result. The Daniel course-transfer/refund example kept the facts but mainly split the original into one-sentence paragraphs, broke numbered-list formatting, and did not read like a real support email.

The root product issue is broader than that one case:

```text
Different users paste different kinds of text.
Some inputs are short, some are long, some are messy, some contain lists, quotes, policies, dates, signatures, or constraints.
A fixed rewrite path cannot choose the right method for all of them.
```

The new design must make strategy selection adaptive without becoming unbounded or unsafe.

## Product Principle

The system should behave like a controlled writing agent:

```text
try a strategy -> evaluate result -> diagnose failure -> choose a different strategy -> retry within budget -> return only if gates pass.
```

The product must not return a bad rewrite just because it is the best available text. A successful result must pass:

```text
fact safety
unsupported-fact safety
send-ready structure
Naturalness Check rule
```

If no bounded attempt passes, return quality failure / no charge.

## Non-Goals

- Do not claim guaranteed success for every possible input.
- Do not create unbounded retries.
- Do not expose internal diagnosis, sentence scores, model names, or strategy labels to users.
- Do not feed Sapling scores or detector-specific wording into model prompts.
- Do not weaken fact gates to improve style or Naturalness Check.
- Do not change Stripe billing/live-mode behavior.

## Target Architecture

```text
Normalize input
-> Input Analyzer classifies scenario/risk/structure/rewrite freedom
-> Extract facts with critical/supporting/optional importance
-> Build Style / Intent Card
-> Initial Strategy Router chooses first strategy
-> Budget Manager approves attempt budget/model tier
-> Generate candidate set for selected strategy
-> Review candidate set with structured failure kinds
-> Finalize candidate
-> Run deterministic structure checks
-> Run LLM fact check
-> Run Policy / Intent Gate
-> Run Naturalness Check
-> If fail:
     Rewrite Quality Strategist Agent diagnoses failure
     Strategy catalog chooses next action
     Budget Manager approves or stops
     Run selected strategy within budget
     Re-check all gates
-> Return success or quality failure/no charge
```

## Adaptive Agent Components

### 1. Input Analyzer

Responsible for deciding what kind of input this is before the first rewrite strategy is chosen.

Output contract:

```ts
type InputAnalysis = {
  scenario:
    | "short_casual_reply"
    | "normal_email"
    | "long_support_email"
    | "support_policy"
    | "messy_thread"
    | "quote_list_heavy"
    | "draft_only"
    | "already_natural"
    | "general";
  riskLevel: "low" | "medium" | "high";
  factualDensity: "low" | "medium" | "high";
  structureRisk: "low" | "medium" | "high";
  rewriteFreedom: "minimal" | "moderate" | "high";
  requiresPolicyCare: boolean;
  requiresStructurePreservation: boolean;
  recommendedInitialStrategy: RewriteStrategyName;
  reasons: string[];
};
```

Initial routing examples:

```text
already_natural + low risk -> minimal_polish
short_casual_reply -> minimal_polish or targeted_sentence_repair
long_support_email -> full_structure_rewrite
support_policy -> support_policy_options_rewrite
quote_list_heavy -> quote_list_safe_rewrite
messy_thread -> messy_thread_cleanup_rewrite
high factual density -> facts_first_reconstruct
```

The Strategy Router must run before first generation and again after failures.

### 2. Enhanced Fact Extractor

Responsible for extracting facts with enough metadata for gates and strategy selection.

Target fact shape:

```ts
type RequiredFact = {
  id: string;
  text: string;
  source:
    | "messageToReplyTo"
    | "roughDraftReply"
    | "audience"
    | "purpose"
    | "whatHappened"
    | "factsToPreserve";
  importance: "critical" | "supporting" | "optional";
  category:
    | "person"
    | "date"
    | "deadline"
    | "amount"
    | "count"
    | "policy"
    | "condition"
    | "negative_constraint"
    | "next_step"
    | "support_availability"
    | "other";
  canBeRephrased: boolean;
  sourceSpan?: string;
};
```

Rules:

```text
Dates, times, amounts, counts, names, policies, conditions, and negative constraints are critical by default.
Critical facts must pass stricter gates.
Optional facts may be omitted only when the reviewer and policy/intent gate agree the omission is safe.
```

### 3. Style / Intent Card

Responsible for defining the communication goal, not just the tone.

Output contract:

```ts
type IntentCard = {
  targetVoice: "warm" | "direct" | "professional" | "concise" | "supportive";
  audience: string;
  communicationGoal: string;
  mustNotChange: string[];
  mustInclude: string[];
  avoid: string[];
  policyCareRequired: boolean;
  nextStepRequired: boolean;
};
```

For support-policy replies, the card must explicitly preserve:

```text
eligibility language
policy conditions
uncertainty such as "may be eligible"
no-change-without-confirmation
refund/transfer/cancellation limits
```

### 4. Rewrite Orchestrator

Responsible for the bounded control loop.

Inputs:

```text
RewriteRequestInput
FactReconstructConfig
strategy budget
```

Outputs:

```text
RewriteResponsePayload on success
FactReconstructQualityError on quality failure/no charge
```

Rules:

```text
Every candidate must pass all gates before user-visible success.
Every retry must be a strategy change, not a blind repeat.
The orchestrator must record rejected candidate reasons.
The orchestrator must stop at budget limit.
```

### 5. Rewrite Quality Strategist Agent

Responsible for diagnosing failed candidates and choosing the next strategy.

It receives structured evidence:

```text
deterministic issues
missing facts
unsupported facts
reviewer scores/issues
scenario classification
style card id
Naturalness Check result category
high-risk sentence count
failed attempt count
previous strategy names
input analysis
intent card
budget state
```

It returns structured JSON:

```ts
type RewriteStrategyDecision = {
  failureKinds: RewriteFailureKind[];
  nextStrategy:
    | "targeted_sentence_repair"
    | "full_structure_rewrite"
    | "facts_first_reconstruct"
    | "support_policy_options_rewrite"
    | "quote_list_safe_rewrite"
    | "messy_thread_cleanup_rewrite"
    | "minimal_polish"
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
messy_thread_leak
quote_or_list_risk
too_formal
too_casual
too_short
too_long
signal_not_improved
low_signal_got_worse
provider_unavailable
over_rewritten
too_generic
model_low_confidence
```

### 6. Strategy Catalog

The strategy catalog is the set of allowed next actions. It is not a free-form prompt generator.

Initial catalog:

```text
minimal_polish
targeted_sentence_repair
full_structure_rewrite
facts_first_reconstruct
support_policy_options_rewrite
quote_list_safe_rewrite
messy_thread_cleanup_rewrite
strong_model_restructure
quality_failure
```

Routing examples:

```text
fact_loss -> facts_first_reconstruct
unsupported_fact -> facts_first_reconstruct
broken_numbered_list -> quote_list_safe_rewrite or full_structure_rewrite
broken_quote_boundary -> quote_list_safe_rewrite
sentence_per_paragraph -> full_structure_rewrite
line_split_paraphrase -> full_structure_rewrite
support_macro_voice -> support_policy_options_rewrite or targeted_sentence_repair
messy_thread_leak -> messy_thread_cleanup_rewrite
signal_not_improved with <= 3 high-risk sentences -> targeted_sentence_repair
signal_not_improved with broad structural issues -> full_structure_rewrite
low_signal_got_worse -> full_structure_rewrite
repeated structural failure -> strong_model_restructure
provider_unavailable -> quality_failure
```

### 7. Reviewer

Responsible for producing structured evidence that the Strategy Router can consume.

Reviewer output must include issue tags, not only prose:

```ts
type StructuredReviewResult = {
  candidateId: string;
  score: number;
  recommendation: "accept" | "repair" | "regenerate" | "escalate" | "fail";
  failureKinds: RewriteFailureKind[];
  issues: string[];
  strengths: string[];
};
```

Reviewer failure kinds must map into the Strategy Router. For example:

```text
support_macro_voice
policy_memo_voice
line_split_paraphrase
over_rewritten
too_generic
too_formal
too_casual
```

### 8. Quality Gates

Gates remain deterministic where possible.

Required gates:

```text
fact preservation gate
unsupported fact gate
send-ready structural gate
weak line-split rewrite gate
LLM fact consistency gate
Policy / Intent Gate
Naturalness Check gate
budget gate
```

Structural issues that must block success:

```text
broken numbered list
detached bullet or numbered markers
sentence-per-paragraph formatting in long replies
broken quote boundary
line-split paraphrase
internal meta-language leakage
dangling closing
messy thread headers leaking into final reply
```

Policy / Intent Gate must block support replies that:

```text
promise a refund, cancellation, transfer, discount, timeline, or account action not present in the facts
turn "may be eligible" into "is eligible"
drop "subject to availability"
drop "we will not update/cancel unless you confirm"
drop required next-step confirmation
soften or strengthen a policy condition
```

### 9. Budget Manager

Responsible for keeping runtime adaptive behavior bounded.

Budget contract:

```ts
type RewriteBudget = {
  maxInitialCandidates: number;
  maxAdaptiveAttempts: number;
  allowStrongModel: boolean;
  maxStrongModelAttempts: number;
  maxRewriteSignalCalls: number;
  reason: string;
};
```

Suggested runtime budget:

```text
simple / low-risk input:
  maxInitialCandidates = 2
  maxAdaptiveAttempts = 1
  allowStrongModel = false

normal email:
  maxInitialCandidates = 3
  maxAdaptiveAttempts = 2
  allowStrongModel = false

long support-policy / high structure-risk input:
  maxInitialCandidates = 3
  maxAdaptiveAttempts = 4
  allowStrongModel = true
  maxStrongModelAttempts = 1

high fact-risk input:
  prioritize fact gates; quality failure if critical facts cannot be preserved
```

The Budget Manager has final approval before any retry, model escalation, or additional Naturalness Check call.

### 10. Evaluation Learning Loop

During long development runs, the agent should not wait for manual website testing.

For each failed eval case:

```text
1. store failure evidence;
2. classify failure kinds;
3. choose next strategy;
4. rerun with the selected strategy;
5. if reusable, add a regression test;
6. update prompts/style cards/routing only for the diagnosed pattern;
7. rerun eval;
8. promote passing lessons to docs/rewrite-strategy-memory.md.
```

This is not silent production self-training. It is controlled eval-driven improvement with tests and docs.

## Files To Create Or Modify

- Create `lib/rewrite-pipeline/orchestrator.ts`  
  Own the bounded adaptive loop.

- Create `lib/rewrite-pipeline/input-analyzer.ts`  
  Classify input scenario, risk, factual density, structure risk, and rewrite freedom before initial strategy selection.

- Create `lib/rewrite-pipeline/budget-manager.ts`  
  Assign runtime attempt/model/signal-call budgets from input analysis and risk.

- Create `lib/rewrite-pipeline/policy-intent-gate.ts`  
  Check support-policy intent, policy constraints, no-change-without-confirmation, and unsupported commitments.

- Create `lib/rewrite-pipeline/strategy-router.ts`  
  Own failure-kind mapping and strategy decision.

- Create `lib/rewrite-pipeline/strategy-catalog.ts`  
  Define allowed strategies and strategy metadata.

- Modify `lib/rewrite-pipeline/pipeline.ts`  
  Delegate adaptive flow to orchestrator while preserving existing API contract.

- Modify `lib/rewrite-pipeline/model.ts`  
  Add input-analysis, reviewer failure-kind, strategist, and strategy-specific prompt instructions.

- Modify `lib/rewrite-pipeline/checks.ts`  
  Add structural gates and weak-rewrite checks.

- Modify `lib/rewrite-pipeline/style-cards.ts`  
  Add support-policy/options and quote/list-safe style rules.

- Modify `lib/rewrite-pipeline/types.ts`  
  Add `InputAnalysis`, `RequiredFact`, `IntentCard`, `RewriteBudget`, `RewriteFailureKind`, `RewriteStrategyDecision`, and strategy attempt metadata.

- Modify `scripts/eval-scenarios.ts`  
  Add adaptive eval reporting, long support-policy cases, messy thread cases, quote/list cases, and Daniel regression.

- Modify `docs/rewrite-strategy-memory.md`  
  Record promoted adaptive orchestrator strategy and results.

- Modify `docs/scenario-evaluation-results.md`  
  Record measured adaptive run results.

- Add `tests/unit/rewrite-strategy-router.test.ts`
- Add `tests/unit/rewrite-input-analyzer.test.ts`
- Add `tests/unit/rewrite-budget-manager.test.ts`
- Add `tests/unit/policy-intent-gate.test.ts`
- Add `tests/unit/rewrite-orchestrator.test.ts`
- Modify `tests/unit/rewrite-pipeline-checks.test.ts`
- Modify `tests/unit/rewrite-pipeline-model.test.ts`
- Modify `tests/unit/rewrite-pipeline.test.ts`

## Task 0: Add Input Analysis, Intent, And Budget Contracts

- [ ] **Step 1: Add input analyzer tests**

Create `tests/unit/rewrite-input-analyzer.test.ts` with tests for:

```text
long support-policy refund/transfer input -> support_policy, high structure risk, support_policy_options_rewrite
messy thread with From/On wrote headers -> messy_thread, messy_thread_cleanup_rewrite
quote/list-heavy input -> quote_list_heavy, quote_list_safe_rewrite
already natural low-risk note -> already_natural, minimal_polish
high factual density policy text -> facts_first_reconstruct
```

- [ ] **Step 2: Create `lib/rewrite-pipeline/input-analyzer.ts`**

Implement deterministic signals first:

```text
word count
paragraph count
presence of From:/On ... wrote:/forwarded headers
numbered list or bullet markers
quoted blocks
policy/refund/cancel/transfer/eligibility words
negative constraints such as do not / will not / unless confirmed
dates, amounts, counts
```

Use those signals to produce `InputAnalysis`. Add an optional LLM classifier only after deterministic tests pass.

- [ ] **Step 3: Add budget manager tests**

Create `tests/unit/rewrite-budget-manager.test.ts` with tests for:

```text
already_natural low-risk input gets small budget and no strong model
normal email gets medium budget
long support-policy high-risk input gets larger bounded budget and one strong-model attempt
provider unavailable prevents further model calls
budget exhausted returns quality failure
```

- [ ] **Step 4: Create `lib/rewrite-pipeline/budget-manager.ts`**

Implement:

```ts
export function createRewriteBudget(inputAnalysis: InputAnalysis): RewriteBudget
export function canAttemptStrategy(budget: RewriteBudget, trace: RewriteAttemptTrace[], strategy: RewriteStrategyName): boolean
```

The Budget Manager has final authority before retries, model escalation, or extra Naturalness Check calls.

- [ ] **Step 5: Add policy/intent gate tests**

Create `tests/unit/policy-intent-gate.test.ts` with tests proving the gate rejects:

```text
"may be eligible" changed to "is eligible"
"subject to availability" dropped
"we will not update/cancel unless you confirm" dropped
unsupported refund promise
unsupported cancellation/transfer/account action
missing required next-step confirmation
```

- [ ] **Step 6: Create `lib/rewrite-pipeline/policy-intent-gate.ts`**

Implement deterministic checks for common support-policy constraints first. Add LLM judge only if deterministic checks cannot cover a case.

## Task 1: Add Failure Kinds And Strategy Catalog

- [ ] **Step 1: Add strategy types**

Modify `lib/rewrite-pipeline/types.ts`:

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
  | "messy_thread_leak"
  | "quote_or_list_risk"
  | "too_formal"
  | "too_casual"
  | "too_short"
  | "too_long"
  | "signal_not_improved"
  | "low_signal_got_worse"
  | "provider_unavailable"
  | "over_rewritten"
  | "too_generic"
  | "model_low_confidence";

export type RewriteStrategyName =
  | "minimal_polish"
  | "targeted_sentence_repair"
  | "full_structure_rewrite"
  | "facts_first_reconstruct"
  | "support_policy_options_rewrite"
  | "quote_list_safe_rewrite"
  | "messy_thread_cleanup_rewrite"
  | "strong_model_restructure"
  | "quality_failure";

export type InputAnalysis = {
  scenario:
    | "short_casual_reply"
    | "normal_email"
    | "long_support_email"
    | "support_policy"
    | "messy_thread"
    | "quote_list_heavy"
    | "draft_only"
    | "already_natural"
    | "general";
  riskLevel: "low" | "medium" | "high";
  factualDensity: "low" | "medium" | "high";
  structureRisk: "low" | "medium" | "high";
  rewriteFreedom: "minimal" | "moderate" | "high";
  requiresPolicyCare: boolean;
  requiresStructurePreservation: boolean;
  recommendedInitialStrategy: RewriteStrategyName;
  reasons: string[];
};

export type RequiredFact = {
  id: string;
  text: string;
  source:
    | "messageToReplyTo"
    | "roughDraftReply"
    | "audience"
    | "purpose"
    | "whatHappened"
    | "factsToPreserve";
  importance: "critical" | "supporting" | "optional";
  category:
    | "person"
    | "date"
    | "deadline"
    | "amount"
    | "count"
    | "policy"
    | "condition"
    | "negative_constraint"
    | "next_step"
    | "support_availability"
    | "other";
  canBeRephrased: boolean;
  sourceSpan?: string;
};

export type IntentCard = {
  targetVoice: "warm" | "direct" | "professional" | "concise" | "supportive";
  audience: string;
  communicationGoal: string;
  mustNotChange: string[];
  mustInclude: string[];
  avoid: string[];
  policyCareRequired: boolean;
  nextStepRequired: boolean;
};

export type RewriteBudget = {
  maxInitialCandidates: number;
  maxAdaptiveAttempts: number;
  allowStrongModel: boolean;
  maxStrongModelAttempts: number;
  maxRewriteSignalCalls: number;
  reason: string;
};

export type RewriteStrategyDecision = {
  failureKinds: RewriteFailureKind[];
  nextStrategy: RewriteStrategyName;
  rationale: string[];
  rewriteInstructions: string[];
  mustPreserveFacts: string[];
  mustAvoidPatterns: string[];
};

export type RewriteAttemptTrace = {
  strategy: RewriteStrategyName | "initial_candidate_generation";
  failureKinds: RewriteFailureKind[];
  deterministicIssues: string[];
  reviewerIssues: string[];
  naturalnessLabel: string;
  budgetRemaining: number;
  passed: boolean;
};
```

- [ ] **Step 2: Create strategy catalog**

Create `lib/rewrite-pipeline/strategy-catalog.ts`:

```ts
import type { RewriteFailureKind, RewriteStrategyName } from "./types";

export type StrategyCatalogEntry = {
  name: RewriteStrategyName;
  handles: RewriteFailureKind[];
  requiresStrongModel: boolean;
  maxAttemptsPerRequest: number;
  description: string;
};

export const rewriteStrategyCatalog: StrategyCatalogEntry[] = [
  {
    name: "minimal_polish",
    handles: ["too_formal", "too_generic"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 1,
    description: "Lightly improve already-natural or low-risk text without over-rewriting.",
  },
  {
    name: "targeted_sentence_repair",
    handles: ["support_macro_voice", "too_formal", "signal_not_improved"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 2,
    description: "Repair a small number of weak sentences while preserving structure.",
  },
  {
    name: "full_structure_rewrite",
    handles: ["sentence_per_paragraph", "line_split_paraphrase", "low_signal_got_worse"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 2,
    description: "Rebuild the whole email from facts into grouped paragraphs.",
  },
  {
    name: "facts_first_reconstruct",
    handles: ["fact_loss", "unsupported_fact"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 2,
    description: "Restore fact safety before optimizing style.",
  },
  {
    name: "support_policy_options_rewrite",
    handles: ["support_macro_voice", "policy_memo_voice"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 2,
    description: "Rewrite support-policy/options messages into a practical customer reply.",
  },
  {
    name: "quote_list_safe_rewrite",
    handles: ["broken_numbered_list", "broken_quote_boundary", "quote_or_list_risk"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 2,
    description: "Preserve list and quote meaning while avoiding broken formatting.",
  },
  {
    name: "messy_thread_cleanup_rewrite",
    handles: ["messy_thread_leak"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 1,
    description: "Remove thread headers/signature noise and write only the reply.",
  },
  {
    name: "strong_model_restructure",
    handles: ["sentence_per_paragraph", "line_split_paraphrase", "support_macro_voice"],
    requiresStrongModel: true,
    maxAttemptsPerRequest: 1,
    description: "Use the strong model once after repeated bounded failures.",
  },
  {
    name: "quality_failure",
    handles: ["provider_unavailable"],
    requiresStrongModel: false,
    maxAttemptsPerRequest: 1,
    description: "Stop and return no-charge quality failure.",
  },
];
```

## Task 2: Add Rewrite Quality Strategist Agent

- [ ] **Step 1: Write strategy-router tests**

Create `tests/unit/rewrite-strategy-router.test.ts` with tests for:

```text
initial support_policy input -> support_policy_options_rewrite before generation
initial quote_list_heavy input -> quote_list_safe_rewrite before generation
initial already_natural input -> minimal_polish before generation
broken numbered list -> quote_list_safe_rewrite
sentence-per-paragraph -> full_structure_rewrite
line-split paraphrase -> full_structure_rewrite
fact loss -> facts_first_reconstruct
unsupported fact -> facts_first_reconstruct
support macro voice with 2 high-risk sentences -> targeted_sentence_repair
support macro voice with structural issue -> support_policy_options_rewrite
repeated structural failure -> strong_model_restructure
provider unavailable -> quality_failure
```

- [ ] **Step 2: Create `lib/rewrite-pipeline/strategy-router.ts`**

Implement deterministic routing first:

```ts
import type {
  InputAnalysis,
  RewriteBudget,
  RewriteFailureKind,
  RewriteStrategyDecision,
  RewriteStrategyName,
} from "./types";

export type StrategyRouterInput = {
  phase: "initial" | "after_failure";
  inputAnalysis: InputAnalysis;
  budget: RewriteBudget;
  deterministicIssues: string[];
  reviewerIssues: string[];
  naturalnessFailure: boolean;
  rewriteWorseThanDraft: boolean;
  highRiskSentenceCount: number;
  failedAttempts: number;
  previousStrategies: RewriteStrategyName[];
  scenarioDomain: string;
  styleCardId: string;
  missingFacts: string[];
  unsupportedFacts: string[];
  providerUnavailable: boolean;
};

export function classifyFailureKinds(input: StrategyRouterInput): RewriteFailureKind[] {
  const kinds = new Set<RewriteFailureKind>();

  if (input.providerUnavailable) kinds.add("provider_unavailable");
  if (input.missingFacts.length > 0) kinds.add("fact_loss");
  if (input.unsupportedFacts.length > 0) kinds.add("unsupported_fact");
  if (input.deterministicIssues.includes("malformed:broken_numbered_list")) kinds.add("broken_numbered_list");
  if (input.deterministicIssues.includes("malformed:broken_quote_boundary")) kinds.add("broken_quote_boundary");
  if (input.deterministicIssues.includes("malformed:sentence_per_paragraph")) kinds.add("sentence_per_paragraph");
  if (input.deterministicIssues.includes("weak_rewrite:line_split_paraphrase")) kinds.add("line_split_paraphrase");
  if (input.reviewerIssues.includes("support_macro_voice")) kinds.add("support_macro_voice");
  if (input.reviewerIssues.includes("policy_memo_voice")) kinds.add("policy_memo_voice");
  if (input.deterministicIssues.includes("malformed:messy_thread_leak")) kinds.add("messy_thread_leak");
  if (input.naturalnessFailure) kinds.add(input.rewriteWorseThanDraft ? "low_signal_got_worse" : "signal_not_improved");

  return [...kinds];
}

export function chooseRewriteStrategy(input: StrategyRouterInput): RewriteStrategyDecision {
  if (input.phase === "initial") {
    return buildDecision([], input.inputAnalysis.recommendedInitialStrategy, [
      "Initial strategy selected from input analysis.",
    ]);
  }

  const failureKinds = classifyFailureKinds(input);

  if (failureKinds.includes("provider_unavailable")) {
    return buildDecision(failureKinds, "quality_failure", ["Provider or signal unavailable."]);
  }

  if (input.failedAttempts >= 3 && hasStructuralFailure(failureKinds)) {
    return buildDecision(failureKinds, "strong_model_restructure", ["Repeated structural failures need one strong-model restructure."]);
  }

  if (failureKinds.includes("fact_loss") || failureKinds.includes("unsupported_fact")) {
    return buildDecision(failureKinds, "facts_first_reconstruct", ["Restore fact safety before optimizing style."]);
  }

  if (failureKinds.includes("broken_numbered_list") || failureKinds.includes("broken_quote_boundary")) {
    return buildDecision(failureKinds, "quote_list_safe_rewrite", ["Repair list or quote structure from facts."]);
  }

  if (failureKinds.includes("sentence_per_paragraph") || failureKinds.includes("line_split_paraphrase") || failureKinds.includes("low_signal_got_worse")) {
    return buildDecision(failureKinds, "full_structure_rewrite", ["Rebuild the whole email instead of repairing one sentence."]);
  }

  if (failureKinds.includes("messy_thread_leak")) {
    return buildDecision(failureKinds, "messy_thread_cleanup_rewrite", ["Remove thread/signature noise and write only the reply."]);
  }

  if (failureKinds.includes("support_macro_voice") && input.highRiskSentenceCount <= 3) {
    return buildDecision(failureKinds, "targeted_sentence_repair", ["Repair the specific weak support-template sentences."]);
  }

  if (failureKinds.includes("support_macro_voice") || failureKinds.includes("policy_memo_voice")) {
    return buildDecision(failureKinds, "support_policy_options_rewrite", ["Use support policy/options structure."]);
  }

  if (failureKinds.includes("signal_not_improved")) {
    return buildDecision(failureKinds, "full_structure_rewrite", ["Use a different structure because the current candidate did not improve."]);
  }

  return buildDecision(failureKinds, "quality_failure", ["No safe bounded strategy remains."]);
}
```

Use helper functions `hasStructuralFailure` and `buildDecision` to keep the file readable.

- [ ] **Step 3: Add optional LLM strategist helper**

Modify `lib/rewrite-pipeline/model.ts` only after deterministic routing passes tests. Add a helper that can refine instructions but cannot override hard safety routing:

```text
The LLM strategist can add rewriteInstructions.
The deterministic router keeps final authority for fact loss, unsupported facts, provider unavailable, and structural failures.
```

## Task 3: Add Structural And Weak-Rewrite Gates

- [ ] Add tests in `tests/unit/rewrite-pipeline-checks.test.ts` for:

```text
broken numbered list
detached bullet marker
sentence-per-paragraph long reply
broken quote boundary
weak line-split paraphrase
messy thread header leak
```

- [ ] Implement checks in `lib/rewrite-pipeline/checks.ts`.

Required issue strings:

```text
malformed:broken_numbered_list
malformed:detached_bullet
malformed:sentence_per_paragraph
malformed:broken_quote_boundary
weak_rewrite:line_split_paraphrase
malformed:messy_thread_leak
```

## Task 4: Add Strategy-Specific Rewrite Methods

- [ ] Update reviewer output in `lib/rewrite-pipeline/model.ts` so it returns structured failure kinds that the Strategy Router can consume:

```text
support_macro_voice
policy_memo_voice
line_split_paraphrase
over_rewritten
too_generic
too_formal
too_casual
model_low_confidence
```

- [ ] Add tests in `tests/unit/rewrite-pipeline-model.test.ts` proving the reviewer prompt asks for structured issue tags and does not only return prose feedback.

- [ ] Add or modify model helpers in `lib/rewrite-pipeline/model.ts`:

```text
generateStructuredCandidates
rewriteMinimalPolish
rewriteFromFactsFirst
rewriteSupportPolicyOptions
rewriteQuoteListSafe
rewriteMessyThreadCleanup
restructureWithStrongModel
```

- [ ] Prompt rules common to all:

```text
Do not mention scores, detectors, Sapling, or internal checks.
Use only extracted facts and user-provided context.
Preserve names, dates, amounts, counts, policies, conditions, and signoffs.
Do not add unsupported promises, refunds, timelines, discounts, actions, or outcomes.
Return only the final email text or strict JSON when the caller requires JSON.
```

- [ ] Strategy-specific rules:

```text
minimal_polish:
  lightly edit already-natural text; do not expand, restructure, or chase score

support_policy_options_rewrite:
  current status -> option/policy constraint -> confirmation needed

quote_list_safe_rewrite:
  keep list markers attached to items; avoid quoted summary unless explicitly needed

messy_thread_cleanup_rewrite:
  strip From/On wrote/forwarded headers from final reply; do not quote the thread

full_structure_rewrite:
  group related facts; do not preserve original sentence order when it causes stiffness

facts_first_reconstruct:
  restore missing facts and remove unsupported facts before style optimization
```

## Task 5: Add Adaptive Orchestrator

- [ ] Create `lib/rewrite-pipeline/orchestrator.ts`.

The orchestrator must:

```text
run Input Analyzer
extract facts with importance/evidence
build Style / Intent Card
create budget from input analysis
choose initial strategy before generation
run initial candidate generation
run all gates, including Policy / Intent Gate
if fail, build StrategyRouterInput
call chooseRewriteStrategy
ask Budget Manager whether the selected strategy is allowed
execute selected strategy
rerun all gates
record RewriteAttemptTrace
stop at budget
return success only if all gates pass
throw FactReconstructQualityError if no passing result
```

- [ ] Modify `lib/rewrite-pipeline/pipeline.ts` so `rewriteWithFactReconstruct` delegates to orchestrator.

- [ ] Keep the existing API response shape unchanged.

Production budget:

```text
1 draft Naturalness Check call
up to 3 initial candidates
up to 4 adaptive strategy attempts
up to 1 strong-model restructure
up to 6 rewrite Naturalness Check calls total
```

If the budget is reached without a passing candidate, return quality failure/no charge.

## Task 6: Expand Evaluation Set To 60 Cases With Cost Controls

- [ ] Expand the evaluation set to 60 total cases.
- [ ] Add at least 12 long support-policy/options cases.
- [ ] Add the Daniel regression as `support-02-daniel-course-transfer-refund`.
- [ ] Add at least 6 messy input cases:

```text
email thread with From/On wrote headers
forwarded support thread
signature block and legal disclaimer
mixed English/Chinese note
bullet-heavy draft
quote-summary-heavy draft
```

- [ ] Add at least 5 low-signal original cases where the rewrite must not increase the signal.
- [ ] Add at least 5 negative-constraint cases:

```text
do not cancel until confirmed
do not promise refund
do not change address
do not mention pricing
not full credit
not a duplicate charge
```

- [ ] Ensure the 60-case set includes:

```text
20 draft-only cases
12 long support-policy/options cases
6 messy-thread/signature/forwarded-message cases
6 quote/list-heavy cases
5 already-natural low-signal cases
5 negative-constraint cases
6 mixed general/workplace/sales/teacher cases
```

- [ ] Add staged evaluation modes to avoid unnecessary cost:

```text
smoke: 10 cases, no repeated full-run loops
focused: 30 cases, includes Daniel and all structural regressions
full: 60 cases, run before push/deploy or after major strategy changes only
```

- [ ] Record cost-sensitive counters in the eval output:

```text
OpenAI model calls
strong-model calls
Sapling calls
estimated input/output tokens
estimated Sapling characters
strategy attempts per case
```

- [ ] Update eval output to include:

```text
inputAnalysis
failureKinds
strategyDecisions
attemptTraces
budgetUsed
policyIntentIssues
structuralIssues
weakRewriteIssues
customerUsablePass
```

## Task 7: Automatic Eval-Driven Repair Loop

- [ ] Update `scripts/eval-scenarios.ts` so an eval failure writes enough metadata to diagnose the failure.

- [ ] During long-run development, use this protocol:

```text
eval failure -> classify failure -> add/adjust regression -> update strategy/prompt/style/check -> rerun focused test -> rerun eval -> document lesson
```

- [ ] Do not require manual website testing from the user before discovering obvious regressions.

- [ ] Update `docs/rewrite-strategy-memory.md` only after a regression passes.

## Task 8: Verification And Deployment Gate

Run:

```bash
npm test
npm run typecheck
npm run lint
npm run build
npm run eval:scenarios -- --mode=smoke
npm run eval:scenarios -- --mode=focused
npm run eval:scenarios -- --mode=full
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

Deployment is allowed only when:

```text
Daniel regression passes.
60 total cases are present in the full eval set.
At least 12 long support-policy/options cases are measured.
At least 6 messy input cases are measured.
At least 5 already-natural low-signal cases are measured and do not get worse.
At least 5 negative-constraint cases are measured.
0 successful outputs contain structural malformed issues.
0 successful outputs contain weak line-split paraphrase.
0 successful outputs lose expected facts.
0 successful outputs add unsupported critical facts.
0 successful support-policy outputs violate Policy / Intent Gate.
No low-signal original case gets worse.
Strong-model escalation stays within budget.
Sapling/OpenAI usage summary is recorded in docs/scenario-evaluation-results.md.
All tests/typecheck/lint/build pass.
```

Then:

```bash
npm run cf:build
npx wrangler deploy
```

Remote smoke:

```text
https://replyinmyvoice.com/
https://replyinmyvoice.com/api/health/db
authenticated app manual smoke if browser session is available
```

## Stop Conditions

Stop only if:

```text
required secrets are missing or invalid
Sapling is unavailable long enough that measured evaluation cannot complete
Cloudflare deploy permission is denied
GitHub push is denied
continuing would expose or commit secrets
a real Stripe live-mode charge or production billing action is required
```

Do not stop for:

```text
bad candidates
failed eval cases
low Naturalness Check results
broken formatting discovered by tests
prompt failures
ordinary build/test/type errors
```

Fix, update strategy, rerun, and continue.

## Relationship To Previous Plan

This plan supersedes the narrower send-ready plan:

```text
docs/superpowers/plans/2026-05-20-send-ready-structure-rewrite.md
```

The send-ready structural checks remain required, but they are now one part of the broader adaptive rewrite agent orchestrator.

## Resume/Interview Wording After Implementation

Use conservative wording:

```text
Built a bounded adaptive rewrite-agent orchestrator that extracts facts, classifies communication context, generates and reviews candidate replies, diagnoses failed candidates, selects targeted repair or full-structure rewrite strategies, and gates final output through fact, structure, and Naturalness Check validation.
```

Do not claim guaranteed low AI-like scores or guaranteed success for all inputs.
