# Adaptive sentence-targeted rewrite loop — eval + ship (2026-05-26)

## Goal
Drive the 100-case post-rewrite AI Signal reliably **< 25** (stretch **< 15**) without losing
facts or warmth. Premise: facts must never drop; don't write the hard tail (medical / formal
rejection / announcement) stiffly to chase the number.

## What changed (engine)
The rewrite loop in `FactReconstructRewriteProvider` no longer returns the first gate-passing
candidate. It now runs an **adaptive sentence-targeted refinement loop**:

1. Rewrite from draft + facts; measure overall AI signal **+ per-sentence scores**
   (Sapling already returns these via `sent_scores=true`; `SaplingWritingSignalClient` now
   surfaces them as `SentenceSignalScore[]` instead of discarding them after the mean).
2. If overall ≤ `TargetAiLikePercent` → return immediately (most drafts clear it on attempt 1).
3. Otherwise feed back the overall score + the **highest-scoring (most AI-like) sentences** +
   a targeted-repair directive ("rewrite specifically these sentences; keep every fact"), and
   try again — converging instead of resampling.
4. Every candidate still clears the same structure + fact (incl. identifier fidelity +
   certainty-drift) + naturalness-floor gates, so refinement can never lower fact fidelity.
5. Soft target: keep the lowest-scoring send-ready (≤ floor) candidate and return it; only fail
   if nothing ever cleared the 40 floor (same failure condition as before — no regression).

Supporting changes: `RewriteBudgetManager` honors the requested attempt budget up to 10 for all
risk levels; bounded model-retry while no candidate exists (a transient timeout no longer
fail-closes the request); model timeout default 35→60s; prod wired to target=20 / max=10
(tunable via `AI_SIGNAL_TARGET` / `REWRITE_MAX_ATTEMPTS` app settings).

## Results — full 100 (real DeepSeek + Sapling; target=20, max=10)

| Metric | Baseline (first-passing) | Adaptive |
| --- | ---: | ---: |
| rewrite **< 25** | 79/100 | **100/100** |
| rewrite < 20 | — | 96/100 |
| rewrite < 15 | 64/100 | **84/100** |
| cases still ≥ 25 | 21 | **0** |
| real fact loss | — | **0** |
| forbidden violations | 1 | **0** |
| engine success | 100/100 | 100/100 |
| model calls | 107 | 174 (~1.7/case) |

Convergence: 74 cases win on loop 1 (no refinement); 26 iterate, mostly 2–5 loops, max 8.
Biggest drops: 029 40→5, 048 39→0, 058 34→0, 057 32→0 — all fact-safe and warm (the HR
rejections read warmer, not colder).

## Fact-pass caveat (measurement, not engine)
The eval matcher reports factPass 89/100, but **all 11 "misses" are matcher false-negatives** —
the facts are present, just reworded. Verified by hand, e.g.:
- 099: matcher flags "donation amount is $500" missing; text literally says "Your $500 gift".
- 032: "five business days" — text: "I'll review it within five business days."
- 089: "No perishable donations" — text: "No perishables…"
- 031/088: synonyms ("digging into it" = investigating; "go with another candidate" = moving
  forward with another applicant).

The engine's own `RewriteFactGate` passed all 100. The more-natural rewrites use more synonyms,
which trips the over-literal matcher more often (11 FNs vs 5 at baseline). Real factPass ≈ 100.
The AI-signal goal is met; the matcher number is the known noise the eval understates.

## Why not chase 0% / switch detectors
Sapling is a blunt, lenient instrument for short email (per-sentence scores are near-binary),
so 0% means "no AI-pattern sentences", not "objectively perfect". Switching to a more accurate
detector (e.g. Pangram) would make scores *higher* (harder to satisfy) and pushes toward a
detector arms race that conflicts with the product positioning (banned terms). The signal is an
internal quality proxy; target < 25 with best-effort + no fact/quality sacrifice is the policy.

## Operational envelope (cap=10 is safe)
Rewrites run async in `RewriteJobProcessor` (Service Bus worker), not a sync HTTP request.
Reservation TTL = 15 min ≫ worst-case ~6 min for 10 loops. Service Bus auto-lock-renewal (5 min)
< worst case, but redelivery is idempotent (`MarkProcessingAsync` claim + terminal-status guard).
The old code could already make up to 10 model calls (gate failures), so the worker was already
built for a 10-call job; the loop just makes the tail use that existing budget.
