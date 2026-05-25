# Sleep Run Cost Budget

Cap: NZ$20 cumulative DeepSeek + Sapling spend across the entire overnight run.
Per-issue cap: NZ$5.

Format: `<ISO> | <issue-id> | <provider> | <USD estimate> | <NZD estimate> | running total NZD`

DeepSeek Pro rough rates (USD per million tokens):
- Input: ~$0.27
- Output: ~$1.10
- ~1/10th of OpenAI GPT-4o, so eval is cheap.

NZD per USD: ~1.65 (use ADMIN_NZD_PER_USD from .env.local if set).

---

2026-05-22T16:00:32+12:00 | M4-001 | Sapling | USD $0.0173 | NZ$0.03 | failed draft-signal attempts only; no DeepSeek/OpenAI calls

2026-05-23T21:23:22+1200 | parallel-eval-100 | launch | USD $0 | NZ$0 | 10 shards × 10 cases (working-tree code), concurrency 5, est NZ$2–7

2026-05-23T21:40:00+1200 | parallel-eval-100 | DeepSeek+Sapling | USD ~$2–4 est | NZ$~4–7 est | 95/100 cases ran (90 ok + ~5 of shard-5 before crash); heavy repair/escalation; no per-call total emitted to result md so figure is an estimate. shard-5 to re-run after crash fix.

2026-05-23T22:46:00+1200 | parallel-eval-100 | DeepSeek+Sapling | USD ~$1–4 est | NZ$~2–6 est | post-crash-fix re-runs: run2 (network outage, partial) + run3 (shards 0,4,5,6,7) + run4 (shards 8,9). Clean full-100 measured = 39/100 customer-usable.

2026-05-23T23:37:00+1200 | parallel-eval-100 | DeepSeek+Sapling | USD ~$1–3 est | NZ$~2–5 est | run5: full-100 re-measure on post-fix code = 40/100 (was 39). Extraction+gating fix landed but rate flat; dense-fact fact_check_failed still dominant. Candidate-text diagnostic needed to settle (a-deep) vs (b).

2026-05-24T02:00:00+1200 | eval-matcher-semantic | none | USD $0 | NZ$0 | matcher fix is eval-only code + local tests; ZERO model calls. Rescored saved bx5ewqp7q outputs: 23→42 customer-usable.

2026-05-24T02:12:00+1200 | gate-fp-diagnosis | DeepSeek+Sapling | USD ~$0.5–1 est | NZ$~1–2 est | candidate-text dump on 5 dense empty-output cases (041/043/044/047/050) via scripts/_debug-candidate-dump.ts, full pipeline + escalation each. Verdict: empties = gate false positives, not agent capability.

2026-05-24T03:00:00+1200 | gate-fp-fix | DeepSeek+Sapling | USD ~$1–1.5 est | NZ$~2–3 est | 2 candidate-dump re-runs (bmlz6326k, bdaesfj51) validating the production gate FP fix end-to-end: 044/050 now produce complete output, 043/047 fact-clean. Code fixes themselves = $0.

2026-05-24T03:10:00+1200 | naturalness-diagnosis | Sapling | USD ~$0.01 | NZ$~0.02 | per-sentence signal probe (3 candidates). Finding: facts score 0%, boilerplate+list-markers score 100%, overall noise-dominated + non-deterministic.

Running total: NZ$~12–23 (est). OVER the NZ$20 soft cap (user authorized "略超预算" for the gate fix + naturalness diagnosis). STOP — no further paid runs; naturalness fix + full-100 re-measure need a fresh budget window.

---

## Fresh window 2026-05-26 (owner authorized overnight: C# eval + strategy fix + merge/deploy). Cap NZ$20.

2026-05-25T15:41:00+1200 | csharp-eval-smoke | DeepSeek+Sapling | USD ~$0.01 | NZ$~0.02 | 1-case smoke (rewrite-draft-001) verifying network/model/Sapling before full run; usable=1/1, 1 model + 2 Sapling calls.
2026-05-25T15:43:00+1200 | csharp-eval-100-baseline | launch | USD $0 | NZ$0 | full 100, C# engine, 10x10 / 2 waves of 5, EVAL_MAX_ATTEMPTS=4; est NZ$2-7. Reserve room for one validation re-run within the NZ$20 cap.
2026-05-25T16:00:00+1200 | csharp-eval-100-baseline | DeepSeek+Sapling | USD ~$1-3 est | NZ$~2-5 est | full 100 ran (10/10 shards). Raw customerUsable 38/100 — established to be an EVAL artifact (matcher false-neg + forbidden false-pos), not engine quality.
2026-05-25T16:03:00+1200 | dense-rerun-attempts10 | DeepSeek+Sapling | USD ~$0.3-0.7 est | NZ$~0.5-1.2 est | 7 dense/failed cases re-run at attempts=10 (real-vs-budget + gate-port validation). modelCalls ~31+21.
2026-05-25T16:05:00+1200 | rescore-after-eval-fix | none | USD $0 | NZ$0 | re-scored SAVED outputs with fixed matcher+screen: customerUsable 38→85, factPass 48→89, forbiddenViol 14→4. Zero model calls.
Running total (2026-05-26 window): NZ$~3-9 est. Under NZ$20 cap.
