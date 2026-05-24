# Codex Brief — Fact-Gate Redesign (atoms-deterministic + semantics-LLM)

Scope: fix the design defect where the deterministic token-AND fact gate over-rejects fact-faithful candidates, discarding ~39/100 usable replies as empty `fact_check_failed`. **Do NOT change rewrite/repair prompts, agent voice, or candidate-generation strategy.** Full analysis: `plans/rewrite-pipeline-design-analysis.md`.

---

## Confirmed root cause (with evidence)

A candidate-text dump of 5 dense `fact_check_failed` cases showed every `mid_writer` candidate had **LLM `factGate=pass`** (facts semantically preserved) yet still failed, because the **deterministic token-AND gate `preservesMustNotChange` (checks.ts:336) is a HARD veto inside `factCheckSafe`** and over-rejects natural paraphrase. Examples (candidate text → false flag):
- "renew with **27 seats** … confirm the **27-seat** count … hold off on any revised quote until you give the go-ahead" → falsely flagged `missing_locked: Requested count is 27 seats` / `No revised quote without confirming 27 seats` (tokens "requested"/"count"/"confirming" absent).
- "Accept the **split** Model S shipment … confirm your **choice** by **noon May 28**" → falsely flagged because "split"≠"partial", "choice"≠"decision"; also falsely flagged `unsupported:accept` / `unsupported:switch` (natural option verbs).
- "phased launch for the first 25 users" → falsely flagged `missing_locked: Possible phased launch for first 25 users` ("possible" absent).

Counter-evidence that the deterministic gate is still NEEDED for atoms: case 041's first candidate genuinely dropped the price "$689" — and there the **LLM check wrongly passed** it. So neither gate is sufficient alone: deterministic is right for ATOMS (money/dates/counts/ids/names), LLM is right for SEMANTICS/policy.

## Required design change

1. **`preservesMustNotChange` (lib/rewrite-pipeline/checks.ts:336) — make it ATOMS-ONLY.** For each locked fact, require only its **atomic tokens** to be literally present: money amounts ($689, NZD $126), dates/times (May 30, 11:00 AM), counts/quantities (27 seats, 80, 40), identifiers (PO-9012, #WA-1187, Q-7442, P-311), and proper names (Priya Shah). Do NOT require non-atomic words (verbs/articles/connectors like "requested", "count", "confirming", "arrive", "possible", "split"). If a fact has no atomic token, the deterministic gate must NOT flag it — defer it to the LLM check.
   - Result: "Price was $689" still FAILS if "$689" is absent (catches the 041 drop); "Requested count is 27 seats" PASSES against "27-seat count" (atom present).

2. **`factCheckSafe` (lib/rewrite-pipeline/pipeline.ts ~754) — deterministic no longer vetoes LLM-passed prose/policy facts.** The deterministic gate's authority is now limited to atomic presence (per #1) + structure + policy/forbidden gates. Semantic/policy preservation of multi-word facts is judged by the LLM `llmFactCheck`. A candidate that passes atoms-deterministic AND llm-fact-check AND policy/structure must be accepted.

3. **Fix the `unsupported` false-positives.** The detector flags natural option verbs ("accept", "switch") and similar as unsupported added facts. It must not flag common verbs / option labels that paraphrase the source's own options. (Locate the source of the `unsupported:` issue — likely the unsupported-fact detector used by the gate.) Keep catching genuinely invented names/amounts/dates.

4. **Guarantee the facts-first fallback is terminal.** `tryGuaranteedFallback` builds the reply from the locked facts; when its output preserves all atomic facts (per #1) and passes policy/forbidden, it must be RETURNED, never `throwQualityFailure` to empty. Reserve empty `fact_check_failed` for genuine atomic-fact loss or policy violation that cannot be repaired.

## Constraints (hard)

- Do NOT change rewrite/repair prompts, agent voice, candidate-generation, the corpus, or shard files.
- Stay strict where it matters: a missing/changed ATOM (wrong amount, dropped date/name/id/count) must STILL fail; a forbidden-claim or policy violation must STILL fail; an invented name/amount/date must STILL be flagged unsupported.
- Banned terms: humanizer, bypass, undetect, detector, evade — nowhere.
- No secrets in source; validate env at runtime. No commit/push/deploy.

## Regression tests

- `preservesMustNotChange`: "Requested count is 27 seats" PASSES vs "renew with 27 seats / 27-seat count"; "80 arrive by May 30 and 40 by June 6" PASSES vs "deliver 80 by May 30, and the final 40 by June 6"; "Price was $689" FAILS vs text omitting $689 and PASSES vs text containing $689; "Possible phased launch for first 25 users" PASSES vs "a phased launch for the first 25 users".
- unsupported detector: "Accept the split shipment" / "Switch to Model T" in an options reply are NOT flagged unsupported; an invented amount/name IS flagged.
- fallback: a facts-first reconstruction that contains all atomic facts is returned, not thrown as empty.

## Acceptance

- New + existing unit tests pass; `npm run lint` + typecheck pass; banned-term scan clean.
- Append to `plans/decisions-log.md`: `<ISO> | parallel-eval-100 | fact-gate redesign: atoms-deterministic + semantics-LLM, guarantee fallback | stop discarding fact-faithful replies; agent voice untouched`.

## After Codex

Supervisor re-runs the full 100. Expect a substantial jump in customer-usable (the mid_writer candidates that already had `factGate=pass` should now pass). A flat result would mean the LLM fact-check or atom detection needs more work.
