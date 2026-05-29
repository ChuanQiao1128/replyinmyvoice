# Translation-Roundtrip Flow — Closeout

**Date:** 2026-05-30 · **Status:** mapped & mature; eval-only; branch `exp/ai-draft-cleanup-ab`
**One line:** lower a support email's GPTZero AI-likelihood while keeping every fact, via a Chinese
round-trip + a calibrated faithfulness gate + surgical span repair. Works on some emails; the per-round
cost is intrinsic. **NOT a product feature — eval research only. Never claim "passes detection" in product copy.**

---

## The flow (`FIRST_LOW_REPAIR`)

```
EN source
 └─ Youdao EN→ZH (zh0, once)
    └─ loop, each round:
         DeepSeek essay-polish(zh0)            ← rough Chinese prose (temp 0.9, varies each round)
         Youdao ZH→EN                          ← back-translation; translationese surface = LOW score
         GPTZero score → re-score K× (median)  ← noise-confirm (GPTZero is ±large on identical text)
         if median ≤ target:
            REPAIR-THEN-VERIFY:
              calibrated gate(source, candidate) → REAL drifts
              LLM-guarded English surgical fix of ONLY those spans   ← not a rewrite
              re-gate + re-score the REPAIRED text
              ACCEPT  iff  repaired is faithful (0 residual)  AND  repaired median ≤ target
         else keep looping
 └─ best near-miss (fewest residual, then lowest score) kept as best-effort
```

**Acceptance criterion (owner's framing):** not "an especially low score" — just **facts intact (0 residual
drifts) AND score low (median ≤ target, default 30).** Casual/translationese-rough phrasing is acceptable as
long as facts aren't distorted.

## How to run (eval-only)

Keys in env: `GPTZero_API_KEY`, `YOUDAO_APP_KEY`/`YOUDAO_APP_SECRET`, DeepSeek via the standard `OPENAI_*`.

```bash
# Full flow on one email:
FIRST_LOW_REPAIR=1 EL_FILE=/path/source.txt EL_ITERS=8 EL_TARGET=30 EL_RESCORE=3 \
  dotnet run --project backend-dotnet/tools/ReplyInMyVoice.Eval -c Release

# One-shot: surgically repair a given English candidate vs source, score before/after:
EN_SURGICAL_ONCE=1 EL_FILE=src.txt ES_EN=candidate.txt dotnet run --project ...Eval -c Release

# Run the calibrated gate alone (drift spans): FAITHFULNESS_GATE=1 FG_SOURCE=.. FG_CANDIDATE=..
# Run the promoted Domain judge:             FIDELITY_JUDGE=1 FG_SOURCE=.. FG_CANDIDATE=.. [FG_TERMS="a,b"]
# best-of-N stability harness (separate):     ZH_SURGICAL_LOOP=1 EL_FILE=.. (collects faithful candidates,
#                                             robust-rescores the lowest — honest "no stable-low" verdict)
```

## Components built (all on `exp/ai-draft-cleanup-ab`)

| Component | Where | Commit |
|---|---|---|
| `FaithfulnessGate` (calibrated: fact-ledger + FACT/truth-condition tests + materiality; cross-lingual variant; `PruneNoOpDrifts`) | `backend-dotnet/tools/ReplyInMyVoice.Eval/FaithfulnessGate.cs` | cb52cb9, fb5dcf2, **cc7f648** |
| `FidelityJudge` (the calibrated judge promoted to a Domain component, LLM Func-injected) + `QualityGateChain.EvaluateWithFidelityAsync` | `backend-dotnet/src/ReplyInMyVoice.Domain/Quality/FidelityJudge.cs` | **b00707a** |
| `FIRST_LOW_REPAIR` flow + `LlmSurgicalEnFixWithGuard` + repair-then-verify | `.../Eval/TranslationDirectPilot.cs` | 6ce4576, … **c020c15** |
| best-of-N + robust acceptance (`ZH_SURGICAL_LOOP`) | same | 6667737 |
| `EN_SURGICAL_ONCE` diagnostic | same / `Program.cs` | cc7f648 |
| Regression fixtures (gate precision/recall, object-substitution) | `.../Eval/fixtures/gate-regression/` | cc7f648, cab62ef |

xUnit: `FaithfulnessGateTests` + `FidelityJudgeTests` + `QualityGateChainTests` (339 green).

## Results (the three test emails, repair-then-verify)

| email | genre | accepted at | candidate | repaired | residual |
|---|---|---|---|---|---|
| **Kwame** | dense renewal/billing | round 0 | 8%, 6 drifts | **1%** | 0 — FAITHFUL |
| **c005** (Dev/Northstar quote) | dense quote | round 4 | 0%, 4 drifts | **0%** | 0 — FAITHFUL |
| **c006** (Ren reschedule) | light scheduling | round 1 | 8%, 1 drift | **12%** | 0 — FAITHFUL |

All land {facts intact + low}. The earlier "dense = pick-two wall" was an artifact of an over-strict gate;
with the calibrated gate + repair-then-verify, dense emails thread it too.

## What was tried and rejected (so we don't repeat it)

- **Drift-count stop gate (≤3 drifts)** — too strict and NON-MONOTONIC (Kwame's 6-drift candidate repaired to
  1% faithful; c006's 5-drift repaired to 100%). Replaced by repair-then-verify (test the actual repaired
  outcome, not a proxy count). `c020c15`.
- **Missing-anchor type gate** — surgical can't INSERT a dropped fact, only replace/delete; subsumed by
  repair-then-verify (a missing anchor → residual → rejected).
- **Generation "roughness feedback"** (push the polish rougher when rounds score high) — BACKFIRED: raised the
  low-rate but the rougher candidates had more drift → failed repair-then-verify → net slower. Reverted.
- **best-of-N** — honest harness, but the {low ∩ faithful} candidate is ~1/3-per-round; it can't manufacture a
  candidate that doesn't appear in N tries.
- **EN-side surgical "finish" applying ALL gate flags blindly** — snaps to 100% when the gate over-flags
  faithful paraphrase (restoring source wording = restoring the generation fingerprint). Only fixing GENUINE
  drifts holds the low score.

## Hard boundaries (the intrinsic limits)

1. **The coupling:** low GPTZero ⟺ translationese / non-native MT surface ⟺ fact drift. You cannot push one
   without dragging the others. Every "make it lower" lever (roughness, more rounds) moves along this curve;
   none decouples low-ness from drift.
2. **Per-round generation lottery is intrinsic.** Whether a given round back-translates LOW is a coin-flip
   (some rounds come out ~100%). repair-then-verify accepts the first {low AND repairable} one; some wasted
   rounds are unavoidable. Dense emails take more rounds.
3. **The low score rides the translationese surface.** The output is acceptably-rough-sendable, NOT crisp
   professional English — polish it crisp and the score climbs back. So it threads {low + faithful +
   acceptably-rough}, never {low + faithful + polished}.
4. **Single-detection-model + noisy.** Measured only on GPTZero, which is ±large on identical text (hence the
   median re-score). Cross-model transfer is UNVERIFIED — and a prior cross-check found GPTZero "wins" did NOT
   transfer (a GPTZero-low text was flagged elsewhere). Treat any low reading as GPTZero-specific.
5. **Surgical repair can't insert** a fully-dropped fact (no span to replace).

## Product constraint (load-bearing)

This is **eval-only research tooling**, never wired to production. Per `AGENTS.md`: the banned substrings
`humanizer | bypass | undetect | detector | evade` must not appear in product copy/metadata/UI, and we must
**never claim "passes AI detection / detector-safe / drops to score X."** It would be both a policy violation
and provably false (single-model, non-transferring, lottery). The product promise stays: *natural, concise,
your voice, meaning + facts preserved.*

## The durable asset (decoupled from detection)

The real win is the **calibrated `FidelityJudge`** (now in `Domain.Quality`): a precision+recall-balanced
faithfulness judge that catches object/term substitution and truth-value flips while passing faithful
paraphrase. It is useful to the **Voice+Fidelity quality track** (`plans/voice-fidelity-quality-track-spec.md`)
regardless of the detection question — that is where it should next be exercised (full-corpus A/B, then Phase 2
wiring behind a default-off flag).

## Pointers

- Findings memory: `detection-three-axis-tradeoff` (the coupling, all the rejected levers, the repair-then-verify rule).
- Standing directive: `stop-chasing-ai-detection` (2026-05-30 reversal note: owner chose to keep developing this line; the hard "never claim detector-safe" line is unchanged).
- Quality-track asset: `voice-fidelity-quality-gates`.
