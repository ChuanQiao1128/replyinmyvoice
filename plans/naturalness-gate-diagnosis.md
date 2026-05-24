# Naturalness gate diagnosis — the signal is noise-dominated, not the facts

Status: **FIXED 2026-05-24** — robust-median aggregate + graceful degradation landed (`lib/writing-signal.ts`, `lib/rewrite-pipeline/pipeline.ts`). Unit suite 361 green; 5/5 dense cases (041/043/044/047/050) now return non-empty. Supersedes the earlier "non-deterministic" read — **Sapling is deterministic** (correction below). Still TODO: one fresh full 100-case eval to measure the true rate.

## TL;DR
The naturalness gate empties fact-perfect dense emails. Root cause is **NOT** dense factual content (it scores 0% = maximally human) and **NOT** simply a strict threshold. It's that the gate uses Sapling's **raw overall AI-score**, which is: (a) hostage to a few short **boilerplate** sentences, (b) corrupted by **list-marker segmentation artifacts**, and (c) **non-deterministic**. The agent is not the bottleneck.

## Evidence — `scripts/_debug-signal-probe.ts` per-sentence scores
| candidate | overall | fact sentences | what scored 100% |
|---|---|---|---|
| 050 (PASSED in pipeline) | 100% | all 0% (e.g. "Proposal P-311 … $18,400 annually" = 0%) | "I want to make sure you have the full picture." (filler) |
| 043 (BLOCKED) | 100% | all 0% | "Please let me know how you would like to proceed." (boilerplate close) + one 59% |
| 047 (BLOCKED) | 100% | all 0% | list-marker fragments "Here are your options: 1." / "2." / "3." |

Key observations:
- **Facts are the most human-looking part (0%).** Refutes the "dense content inherently high" hypothesis.
- **Overall is outlier-dominated**: 8/9 sentences at 0% yet overall = 100%.
- **~~Non-determinism~~ — CORRECTED (2026-05-24):** Sapling is **deterministic**. 050 ×5 = all 100% with the *same* single outlier ("I want to make sure you have the full picture.") each run; 043 ×5 = [100,100,100,100,100]; DeepSeek extraction ×5 = identical. The "pipeline passed / probe 100%" contradiction was a **text-version mismatch** (the pipeline gated a different wording), not noise. Run-to-run eval flips (39/40/23) came from DeepSeek rewrite **temperature jitter** producing different wordings that landed differently on the brittle gate, plus the already-fixed matcher/fact-gate FPs.
- **Segmentation bug**: numbered-list markers scored as standalone sentences.

## Fix direction (production; lib/writing-signal.ts + lib/rewrite-pipeline/pipeline.ts naturalnessPasses)
1. **Robust sentence-level signal** instead of raw overall: median / trimmed-mean of sentence scores, or "fraction of sentences > threshold". Existing `calibrateWithSentenceScores` already hints at this — extend it.
2. **Fix sentence segmentation** so "1."/"2."/"3." and other fragments aren't scored as sentences.
3. **Boilerplate closings** ("Please let me know how you would like to proceed", "I want to make sure you have the full picture") — exclude short generic closers from scoring, and/or prompt the agent to vary/drop them.
4. **Noise**: a single Sapling call is unstable. For the gate decision, rely on sentence-level robustness (don't let one sentence empty the email); optionally average calls for borderline cases.
5. **Graceful degradation (most important):** the gate currently HARD-fails to empty output. A fact-perfect, natural-reading email emptied because one boilerplate sentence/list-marker tripped a noisy detector is the wrong product behavior. Return the best fact-complete candidate (with a "reads slightly formal" note) instead of nothing.

## Validate after fix
- Re-run `scripts/_debug-candidate-dump.ts` (041/043/044/047/050) → expect non-empty for the fact-clean ones (043/047 should now pass).
- One fresh full-100 on the fixed eval matcher → measure true rate (expected well above 42). ~NZ$1–3, needs a fresh budget window.

## Companion records
- Fact-gate FP fix LANDED this session: 044/050 now produce complete output; 043/047 fact-clean; residual 041 same-class FP ("Delivered May" common-word+month over-capture — see plans/gate-false-positive-fix.md). Suite 360 green.
- Throwaway diagnostics kept for re-verification: `scripts/_debug-candidate-dump.ts`, `scripts/_debug-signal-probe.ts`, `scripts/_debug-050-probe.ts`, `scripts/_debug-stability-probe.ts`.

## What landed (2026-05-24)
1. **Robust aggregate** (`lib/writing-signal.ts` → new `robustAiLikePercent`): `aiLikePercent` is now the **median of cleaned sentence scores**, not Sapling's outlier-dominated overall (kept as `rawAiLikePercent`). One boilerplate/list-marker sentence can no longer pin the score to 100%. Applied unconditionally (draft + candidates) so comparisons stay consistent; the `calibrateSentenceScores` flag is now a no-op.
2. **Segmentation cleanup** (`isScorableSentence`): bare list markers (`1.`, `2)`, `•`, `-`) and single-token fragments are dropped before aggregating, so they don't vote.
3. **`naturalnessPasses` simplified** (`pipeline.ts`): now just `rewritePercent <= naturalnessThreshold` (40). Removed both `.every()` paths (Rule A escape hatch + the timid calibration) and the perverse Rule B (`rewrite <= draft` for already-clean drafts, which forced e.g. ≤5%).
4. **Graceful degradation** (`pipeline.ts`): the two `naturalness_gate_failed` empties (lines ~1312, ~1327) now return the fact-safe `finalEmail` flagged `selectionStatus: "best_available"` with a "reads slightly formal" risk note — never empty a fact-complete reply over a style score. (Note: the facts-first fallback already degraded on fact-safety; the only true naturalness-empty was the `maxEscalations === 0` path.) Genuine fact failures still produce no output.

Design call: naturalness is now a **soft signal** (median ≤ 40, tolerates a minority of templated sentences). Threshold/aggregate are tunable; default median ≈ "≥ half the sentences read human." Re-tune on the full eval if it proves too lenient.

Tests updated: `tests/unit/writing-signal.test.ts` (median contract + 050-outlier and 047-list-marker regression tests), `tests/unit/rewrite-pipeline.test.ts` (fallback path retriggered via >threshold finals, not the old draft-relative rule).
