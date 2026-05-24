# Production gate false-positive fix — next-session brief

Status: **FIX LANDED 2026-05-24** (two Codex rounds). 044/050 now produce complete output; 043/047 fact-clean (now naturalness-limited — see plans/naturalness-gate-diagnosis.md); suite 360 green; matcher untouched (negatives hold). Residual: **041 same-class FP** — "Delivered May" (common word + month) still over-captured as a name; precise low-risk fix is to exclude month/weekday tokens from name atoms (deferred — naturalness binds 041 anyway). Companion: `plans/eval-matcher-fix.md` (eval-matcher half, DONE).

## TL;DR
The 100-case eval rate was ~2× understated by over-literal fact matching in TWO layers. Layer 1 (eval matcher) is FIXED (23→42 on saved outputs, eval-only). Layer 2 (production gate in `lib/rewrite-pipeline/`) is diagnosed but NOT fixed — it false-positives on complete, customer-usable emails and empties them out. Fixing it should lift the true rate well past 42 (likely 70%+). **The agent is not the bottleneck; the gate is.**

## Evidence — candidate dump on 5 dense empty-output cases
Tool: `node --import tsx scripts/_debug-candidate-dump.ts` (DENSE_CASE_IDS = 041/044/047/050/043). In every case the best (escalation) candidate was complete and customer-usable, rejected on a false positive:

| case | candidate actually contains | gate falsely flagged |
|---|---|---|
| 041 | "order #WA-1187 … at a price of $689" (all 8 facts) | `missing_locked:Order #WA-1187` |
| 044 | "quote Q-7442 has 38 seats at $19 per seat per month" | `missing_locked:Quote Q-7442 shows 38 seats at $19 per seat per month` |
| 047 | "PO-9012 … 120 Model S … at $42 each" + all 3 options | `missing_locked:PO-9012 …`, fragment `missing_locked:Option`, `unsupported:remember` |
| 050 | "Proposal P-311 … $18,400 annually" (all facts) | `missing_locked:Proposal P-311`, `meta_language:fact_reference` (from "fee **is included**") |
| 043 | escalation has full transfer policy | `unsupported:per` ("Per our policy") |

(043's FIRST candidate genuinely dropped the 10-business-day policy — one real miss — but escalation fixed it and was then rejected spuriously. So even the one "real" case had a usable final candidate.)

## Four false-positive classes (all in `lib/rewrite-pipeline/`)
1. **Atom over-literal matching** — `checks.ts` `preservesMustNotChange` / `lockedFactAtoms` / `atomIsPresent`. Identifiers/amounts visibly present flagged missing. **This is the atoms-only redesign — net-harmful as shipped.** HIGHEST RISK to change (it is the fact-safety gate). Decide: surgically fix identifier/money/count matching + add prose tolerance, OR revert atoms-only to the pre-redesign gate.
2. **Fragment locks** — bare "Option"/"Annual"/"Decision" become must-not-change facts. Root cause upstream: `fact-extraction.ts` person-name regex grabs capitalized common words → `fact-ledger.ts addAnchor` → `facts_that_must_not_change`. Drop single-token non-anchor fragments from the locked set.
3. **Meta-language detector** — `checks.ts detectMetaLanguage` fires on normal prose ("fee is included"). Narrow the `(is|was|were)\s+(included|…)` pattern so business facts don't match.
4. **Unsupported-word check** — flags common words "per"/"remember" as `unsupported`. Tighten so lone common words aren't treated as unsupported concrete facts.

Classes 2–4 are LOW risk (remove false positives without weakening real fact checks). Class 1 is the high-risk core.

## GUARDRAIL (critical — no-bad-result fact-safety gate)
Any loosening MUST still catch genuine fact loss/alteration. Bidirectional tests required (same discipline as the eval-matcher fix):
- POSITIVE: the 5 dumped escalation candidates must PASS the gate.
- NEGATIVE: genuine loss must STILL fail — wrong amount ($289≠$288), wrong/dropped date, dropped identifier (#WA-1187 absent), dropped name. Reuse the negative fixtures already in `tests/unit/eval-scenarios-corpus.test.ts`.

## Validation sequence
1. Fix → re-run `scripts/_debug-candidate-dump.ts` on 041/043/044/047/050 → confirm non-empty output (gate now passes them).
2. ONE fresh full-100 eval on the already-fixed matcher → measure true rate. ~NZ$1–3. (Budget is at the NZ$20 cap — needs a fresh budget window.)
