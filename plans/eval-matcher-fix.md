# Eval matcher fix — the metric was the bug

Date: 2026-05-24 (~00:40, supervisor turn after atoms-only gate regression 40→23)

## Finding (evidence-backed, not theory)

The 100-case headline (23, and the prior 40) is dominated by **eval-matcher false negatives**, not agent quality. Hand-adjudicated all 13 "produced-output-but-marked-failing" cases in shards 0/8/9 against their saved output text: **12/13 are false negatives** — natural emails with every fact present, wrongly marked missing. (The 1 real defect, case 082, is a different problem: the degenerate facts-first fallback dumping locked facts as telegraphic lines that pass naturalness at 0% but aren't prose.)

### Mechanism

`scripts/eval-scenarios.ts` `includesFact` (line ~389-405):
1. literal substring, OR
2. `includesEquivalentFact` — ~15 hand-coded case-specific rules (the "narrow 048-specific" patch; unmaintainable whack-a-mole), OR
3. `tokens.every(t => normalizedText.includes(t))` — **requires every content token of the expected fact as a literal substring.**

Rule 3 is the killer. Examples:
- 093: output "renewed on August 1 for $288" vs fact "Annual **renewal charged** $288 on August 1" → fails on `renewal`≠`renewed` + missing `charged`.
- 091: fails only because literal `requested` is absent.

### True rate
23 counted passes + ~29 of 32 produced-output cases are FNs ≈ **~52/100**. The 45 empty-output `fact_check_failed` cases are genuine (dense multi-fact emails) — the real, smaller remaining problem.

## Fix this turn (eval-only, zero prod risk, no LLM spend)

Production pipeline (`lib/rewrite-pipeline/*`) does NOT import this script. Changing the matcher changes only how the eval scores already-generated outputs.

1. Rewrite `includesFact` to be **semantically tolerant**: keep literal fast-path; replace token-`every`-literal with stem/inflection-tolerant + coverage matching. Delete the hand-coded `includesEquivalentFact` branches.
2. **Guardrail (so the ruler doesn't go too loose):** anchor tokens — money `$N`, percentages, standalone numbers, dates (month+day), times `H:MM`, identifiers (`#R8142`, `PO-944`, `INV-404`), proper-name tokens — MUST be present and exact. Prose/verb tokens may match by stem or coverage. Genuine loss of an amount/date/id/name must STILL fail.
3. **Bidirectional unit tests:** confirmed FNs (093/091/081/086/088/093/096/099 pairs) now PASS; injected real losses (wrong amount $289≠$288, wrong date Apr 12≠Apr 2, dropped `#R8142`, dropped surname) STILL FAIL.
4. **Zero-generation rescore:** re-score the saved bx5ewqp7q outputs (docs/eval-results/worker-*.md) with the fixed matcher → report corrected pass count. No model calls.

## NOT this turn (decide against the honest baseline)
- Whether to revert the atoms-only gate redesign (it regressed the broken metric and added ~6 real empties).
- Dense-case empty-output work (the genuine 45) + the 082 fact-dump-fallback quality issue.
