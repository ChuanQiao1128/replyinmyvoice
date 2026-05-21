# DeepSeek Adaptive Rewrite Attempt Ledger Strategy

Date: 2026-05-21

This document records the current strategy for the next rewrite-quality test window. It is a planning and test target, not a production rollout by itself.

## Purpose

The rewrite engine should behave like a bounded adaptive rewrite orchestrator. The current priority is quality, fact preservation, and reliable gate behavior, not minimizing model spend.

The immediate goal is to make failed rewrite attempts useful. When a candidate fails, the next attempt must receive the original request, the reviewed facts, every prior failed candidate, and every failure analysis so it can change strategy instead of blindly retrying.

## Provider And Model Policy

Use DeepSeek through the OpenAI-compatible API surface:

```env
OPENAI_BASE_URL=https://api.deepseek.com
DEEPSEEK_API_KEY=
OPENAI_MODEL_CHEAP_STRUCTURED=deepseek-v4-pro
OPENAI_MODEL_MID_WRITER=deepseek-v4-pro
OPENAI_MODEL_STRONG_ESCALATION=deepseek-v4-pro
```

Do not write a real API key into this repository, docs, logs, prompts, evaluation files, or run results.

For the next quality pass, all model roles should use `deepseek-v4-pro`:

| Pipeline role | Current model |
| --- | --- |
| Fact Extractor | `deepseek-v4-pro` |
| Fact Review / Fact Ledger Review | `deepseek-v4-pro` |
| Scenario Classifier | `deepseek-v4-pro` |
| Strategy Router | `deepseek-v4-pro` |
| Candidate Generator | `deepseek-v4-pro` |
| Reviewer / Judge | `deepseek-v4-pro` |
| Finalizer | `deepseek-v4-pro` |
| Fact Consistency Gate | `deepseek-v4-pro` |
| Escalation / Hard Repair | `deepseek-v4-pro` with thinking/high reasoning |

`deepseek-v4-flash` may be reconsidered later for cheap structured roles only after repeated evaluation runs show stable fact extraction, fact review, routing, repair decisions, and final gate behavior.

## Thinking Policy

Ordinary steps should run in non-thinking mode. This includes extraction, classification, initial routing, normal generation, normal review, and normal finalization.

Thinking/high reasoning should be reserved for hard repair and escalation near the end of the attempt budget. The purpose is to spend extra reasoning only when cheaper strategy changes have already failed.

## Source Of Truth

Every attempt must treat these as authoritative:

1. Original request fields, including message to reply to, rough draft reply, audience, purpose, what actually happened, and facts to preserve.
2. Reviewed fact ledger created from the original request.
3. User-selected tone and product constraints.
4. Prior failed candidates and their failure analyses as negative evidence only.

Prior failed candidates must not become a new source of facts. They are useful because they show what went wrong, not because they are trusted content.

## Runtime Pipeline

The target orchestration shape is:

```text
normalize input
-> input analyzer
-> fact extraction
-> fact ledger review
-> style / intent card
-> initial strategy router
-> attempt budget manager
-> candidate generation
-> structured reviewer
-> fact gates
-> structural send-ready gates
-> policy / intent gate
-> Sapling Naturalness Check gate
-> rewrite quality strategist
-> next attempt with full attempt history
-> success or quality failure / no charge
```

The strategy router must run before the first attempt and again after failures using real gate and reviewer evidence.

## Attempt Budget

The maximum is 10 attempts per user request. This is a hard cap, not the expected path.

| Attempts | Model / mode | Intended use |
| --- | --- | --- |
| 1-3 | `deepseek-v4-pro`, non-thinking | Initial strategy candidates and normal facts-first reconstruction. |
| 4-6 | `deepseek-v4-pro`, non-thinking | Strategy changes based on concrete failure evidence. |
| 7-8 | `deepseek-v4-pro`, non-thinking | Full structure rewrite, quote/list-safe rewrite, support-policy/options rewrite, or messy-thread cleanup. |
| 9-10 | `deepseek-v4-pro`, thinking/high reasoning | Hard repair or strong-model escalation. |

If no candidate passes all required gates inside the budget, return a quality-failure/no-charge response. Do not return a weak fallback as a successful rewrite.

## Attempt Ledger

Each attempt should produce a structured record that survives into the next attempt:

```text
attemptNo
strategy
modelRole
modelName
thinkingMode
candidateText
failureAnalysis
failureKinds
factGateResult
structureGateResult
policyIntentGateResult
saplingResult
nextStrategyDecision
```

`failureAnalysis` should explain the concrete reason a candidate failed. `failureKinds` should use stable tags so evaluation can aggregate patterns over time.

Required failure tags include:

```text
fact_loss
unsupported_fact
broken_numbered_list
broken_quote_boundary
sentence_per_paragraph
line_split_paraphrase
support_macro_voice
messy_thread_leak
quote_or_list_risk
signal_not_improved
low_signal_got_worse
too_generic
uniform_structure
policy_intent_drift
no_change_without_confirmation_missing
```

## Repair Prompt Inputs

Every repair or retry after a failure must receive:

1. Original user input.
2. Reviewed fact ledger.
3. Style / intent card.
4. All previous failed candidate texts.
5. All previous failure analyses.
6. The current failure tags.
7. The selected next strategy.
8. The remaining attempt budget.

The prompt should make the required strategy change explicit. For example, if the failure is `sentence_per_paragraph` or `line_split_paraphrase`, the next strategy should be a full structure rewrite from facts, not a sentence-level repair.

## Sapling Usage

Sapling remains a final reference gate and analysis signal. It must not become the generator's optimization target.

Sapling feedback can help diagnose why a candidate failed by using:

1. Overall draft and rewrite scores.
2. Score delta.
3. Sentence-level scores.
4. Token-level or phrase-level high-signal regions when available.

Do not feed detector-specific wording, score-hacking instructions, or raw heatmap text directly into generation prompts. Convert Sapling evidence into neutral writing-quality tags such as:

```text
sentence_per_paragraph
support_macro_voice
line_split_paraphrase
too_generic
uniform_structure
signal_not_improved
low_signal_got_worse
```

User-facing copy must keep the product framed as voice, clarity, naturalness, and fact preservation. Do not position the product as detector bypass, evasion, undetectable writing, or score hacking.

## Gate Calibration Policy

Failure reasons can be used to repair gate rules, but only to improve gate precision. Do not lower the quality bar to make failures pass.

Classify gate-related findings into three groups:

1. Real output failure: fix the generator, fact ledger, strategy router, or repair prompt. Do not weaken the gate.
2. Gate false positive: adjust the gate so harmless text is a soft diagnostic instead of a blocker.
3. Gate false negative: strengthen the gate because a bad candidate passed or almost passed.

Hard fact and intent failures must block success:

```text
money
dates
deadlines
counts
named people
policy constraints
promises
refunds
charges
subscriptions
transfer or availability rules
lost or returned status
required confirmation
no-change-without-confirmation constraints
```

Soft issues should usually be diagnostics, not blockers:

```text
generic signoff
polite opener
harmless formula phrase
safe redundant wording
minor preference mismatch
```

Structural failures must block successful output:

```text
broken numbered list
detached list marker
sentence-per-paragraph long reply
broken quote boundary
weak line-split paraphrase
internal meta-language leakage
messy thread leakage
support macro voice
```

Every gate repair must include regression coverage:

1. One observed false-positive case that now passes.
2. One similar hard-fact or hard-structure case that still fails.

## Evaluation Corpus

The synthetic 100-case email corpus is:

```text
/Users/qc/Desktop/CloudFlare/docs/rewrite-email-eval-cases-100.md
```

Use staged evaluation:

| Mode | Cases | Purpose |
| --- | ---: | --- |
| smoke | 10 | Fast verification after small prompt or routing changes. |
| focused | 40 | Targeted quality checks after strategy or gate changes. |
| full | 100 | Required before push/deploy after major rewrite strategy changes. |

For each failed case, record:

1. Case id and category.
2. Draft score and final score when Sapling is available.
3. Failed candidate text.
4. Failure tags.
5. Failure analysis.
6. Selected next strategy.
7. Attempt count.
8. DeepSeek call count.
9. Sapling call count.
10. Reusable lesson, if any.

Stable lessons should be promoted into `docs/rewrite-strategy-memory.md` only after they are backed by tests or repeatable evaluation evidence.

## Success Criteria For The Next Test Window

The next test window should verify that:

1. DeepSeek provider configuration can route through `OPENAI_BASE_URL=https://api.deepseek.com` without writing secrets to repo files.
2. Ordinary steps use non-thinking mode.
3. Escalation attempts can enable thinking/high reasoning.
4. The attempt ledger carries all prior failed candidates and failure analyses.
5. The loop stops at 10 attempts.
6. Quality failure returns no charge when no candidate passes gates.
7. Sapling unavailable or timed out behavior is treated as quality failure in the fact-reconstruct production route.
8. Gate repairs include paired regression cases and do not weaken hard fact protections.

## Non-Goals

This document does not authorize:

1. Writing DeepSeek, Sapling, OpenAI, Stripe, Clerk, Azure, Cloudflare, or GitHub secrets into docs or source files.
2. Deploying a provider change without evaluation.
3. Running unlimited retries in production.
4. Marketing the product as a detector bypass tool.
5. Silently training or self-modifying production prompts from private user content.
