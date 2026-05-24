# Rewrite Pipeline — Design/Strategy Analysis

Date: 2026-05-23. Question: is the ~40/100 customer-usable rate on dense business emails a pipeline DESIGN/STRATEGY problem (not just gate-matching)? **Answer: yes — and the decisive evidence is in the failure-reason split, not speculation.**

## Decisive evidence

`lib/rewrite-pipeline/pipeline.ts:979-981` sets the final failure reason:
```js
reason: fallbackAttempt.factSafe ? "naturalness_gate_failed" : "fact_check_failed"
```
Across the clean 100-case run: **39 `fact_check_failed`, 0 `naturalness_gate_failed`, 0 `reviewer_threshold_failed`, 0 `signal_unavailable`.**

So:
- **Naturalness is NOT the bottleneck** (0 naturalness failures). The earlier suspicion that "≤40% AI-like is unachievable for dense content" is wrong for this corpus.
- **Every quality failure is the fact gate** — and specifically, `fact_check_failed` only fires when the **deterministic facts-first fallback** (which rebuilds the email FROM the extracted locked facts) ITSELF fails the fact gate (`fallbackAttempt.factSafe == false`).

## The design contradiction

The pipeline's safety net (`tryGuaranteedFallback`) constructs the reply directly from `facts.facts_that_must_not_change`, then that output is checked by `preservesMustNotChange` (checks.ts:336) — a gate that verifies those same locked facts are present. **An email built from the facts cannot pass the gate that checks for the facts** unless the gate or the locked-fact set is broken. It's failing, so one or both are:

1. **The fact gate is literal token-AND** (checks.ts:336): every non-generic token of a locked fact must appear verbatim (facts ≤5 tokens). The fallback writes natural sentences, so it drops literal tokens ("rep", "price", "reallocation"→"reallocate") and gets flagged — even though the fact is asserted. The narrow normalize-equivalence patch (048-specific) is whack-a-mole and didn't generalize (re-measure stayed flat: 40 vs 39).

2. **Over-anchoring inflates the locked set**: extraction locks redundant decompositions ("Price was $689" + "$689" + "April 15" + "Payment") and (pre-fix) spurious single-word fragments. The more (and more literal) the locked entries, the less any natural reconstruction can satisfy them all verbatim.

3. **The "guaranteed fallback" is not guaranteed**: it is gated by the same brittle check and can `throwQualityFailure` (empty, no charge) instead of always emitting the fact-built email. The design prefers returning NOTHING over returning a fact-faithful-but-not-token-identical email.

Net: for dense multi-fact business emails (orders, quotes, POs, refunds, transfers), the system throws away ~40% of usable, fact-safe replies because a literal gate rejects even its own deterministic reconstruction.

## Why the narrow fixes didn't move the rate

The crash/network/spurious-fragment fixes were real wins, but the gate-equivalence fix only added 048-specific paraphrase rules. The defect is the gate's APPROACH (literal token presence), not a missing rule. Patching paraphrases one-by-one cannot win.

## Design-fix direction (strategy change)

In leverage order:

A. **Make the facts-first fallback terminal/guaranteed.** It is constructed from the locked facts; it must ALWAYS be returned (never empty). This alone converts the ~39 empty-fails into usable fact-safe replies — IF the fallback's text is actually fact-faithful (see "confirm" below). The whole point of a guaranteed fallback is to never return nothing.

B. **Replace literal token-AND fact verification with semantic entailment** ("does the output assert this fact?") instead of "are these exact tokens present?". Consistent with how the fallback builds, and ends the paraphrase whack-a-mole. Keep it strict on contradiction (wrong amount/date/name, changed policy).

C. **Stop over-anchoring.** Dedupe atom+sentence locks; drop spurious/non-source locks; keep the locked set minimal and satisfiable.

## One confirmation needed before choosing A vs C

Fix A (always ship the fallback) is safe ONLY if the fallback is fact-faithful but token-mismatched (gate too strict → A + B win). If the fallback genuinely DROPS facts (incompleteness), shipping it would send fact-incomplete emails → then C + improving the fallback construction is needed first.

Decisive check: dump the **fallback candidate text** (`attemptLedger[].candidateText`, stage=fallback) for ~5 `fact_check_failed` cases and compare against `facts_to_preserve`:
- fact-faithful but flagged → gate is the problem → A + B.
- genuinely missing facts → fallback construction is the problem → C + better fallback.

Either way the design flaw (a gated, non-guaranteed safety net + a literal gate) is real and is the root cause of the ~40% rate — not agent writing capability and not naturalness.
