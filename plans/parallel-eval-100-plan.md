# Parallel 100-Case Eval + Learning Promotion — Execution Brief

Status: **STAGED, NOT STARTED.** The eval run is the only money/API-spending step and is gated on the operator's explicit "go". This doc is self-contained so a future Codex session can pick up the promotion phase cold.

Owner authorization: ChuanQiao1128. Created 2026-05-23. Supersedes ad-hoc smoke runs.

## Goal

Run the full 100-case rewrite-quality corpus split into 10 independent groups of 10, in parallel, mutually non-interfering. Then harvest failures and promote reusable lessons into the rewrite + repair agents via code/test changes (offline, code-based — never hot-loaded).

## Locked configuration (operator-confirmed)

| Dimension | Choice |
|---|---|
| Run engine | Background shell process pool (supervisor runs `tsx` directly; no extra Codex token cost) |
| Concurrency | ≤5 at a time → 2 waves of 5 over the 10 shards (rate-limit safety vs DeepSeek/Sapling 429) |
| Sharding | Split corpus into 10 files — **zero change to the runner** |
| Scope | Full 100, fresh |
| Code version | Current **working tree, in-place** (matches latest smoke; eval only reads source so concurrent reads don't conflict) |
| Promotion | Always via Codex (supervisor mode forbids direct source edits by Claude) |

## Why this is feasible without changing the runner

- `lib/rewrite-eval-cases.ts:137` `selectRewriteEmailEvalCases` only does `slice(0, limit)` (smoke=10 / focused=40 / full=100) — **no offset/range**.
- But `scripts/eval-scenarios.ts` (`parseEvalCliOptions`, ~line 1194) lets each run set its own `--output`, `--progress`, and read from `EVAL_CASES_PATH`. Setting `EVAL_CASES_PATH` alone triggers the email-100 path (`shouldUseEmailMarkdownCorpus`, ~line 1391).
- So: 10 single-shard corpus files × per-worker output/progress = 10 independent runs, no shared write target, runner untouched.

## Critical constraint: learning is OFFLINE / code-based

Per `docs/rewrite-learning-system.md:108`:
- **Forbidden:** `RewriteLearningSample -> DB rule -> immediate production prompt change`
- **Required:** `sample -> analysis -> StrategyCandidate -> code/test change -> validation -> push -> deploy`

Workers do **not** teach the agents mid-run. They emit failure rows; a single synthesis step buckets them; lessons become code/prompt/test changes via Codex; then re-run to validate. It is a loop, not live feedback.

---

## Phase A — Prep (no spend)

1. Create dirs: `docs/eval-results/`, `plans/eval-progress/`, `plans/eval-shards/`.
2. Split `docs/rewrite-email-eval-cases-100.md` into `plans/eval-shards/shard-{0..9}.md`, 10 `### Case NNN` blocks each.
   - **Boundary rule:** cut only on `^### Case \d{3} ` headers; copy each block whole.
   - **Field completeness:** the parser (`lib/rewrite-eval-cases.ts:83`) hard-validates every required field per case (`id, category, risk_tags, message_to_reply_to, rough_draft_reply, audience, purpose, what_actually_happened, facts_to_preserve, expected_rewrite_challenges`). A truncated block throws — keep blocks intact.
   - Shard k holds cases `[k*10+1 .. k*10+10]` (shard-0 = 001–010, …, shard-9 = 091–100). Original 3-digit numbering preserved; contiguity from 001 is not required by the parser.

## Phase B — Parallel run (the only money/API step)

Per worker k = 0..9:

```bash
EVAL_CORPUS=email-100 \
EVAL_CASES_PATH=plans/eval-shards/shard-k.md \
npx tsx scripts/eval-scenarios.ts \
  --mode=full \
  --output=docs/eval-results/worker-k.md \
  --progress=plans/eval-progress/worker-k.json \
  --resume
```

- `--mode=full` on a 10-case file = `slice(0,100)` = runs all 10 in that shard.
- `--resume` = a crashed worker resumes from its own progress file; workers never touch each other's files.
- Launch in 2 waves of 5 (cap concurrency at 5). Wait for wave 1 to drain before wave 2.
- Pre-launch ledger line + post-run settlement line in `plans/sleep-run-budget.md`.

## Phase C — Merge + triage (no spend)

1. Merge the 10 `worker-k.md` into `plans/eval-results/combined-summary.md`; recompute aggregates: customer-usable pass, fact failures, unsupported additions, forbidden-claim violations, naturalness misses, strict-signal pass — broken down by category.
2. Bucket every failure into the existing taxonomy:
   - `fact_loss` (missing expected facts)
   - `unsupported` (invented recipient / invented policy / invented number)
   - `forbidden` (refund / guarantee / blame claims)
   - `naturalness_miss` (Sapling above threshold)
   - `changed_policy_or_condition`
   - `blank` / `fact_dump` / `repetition` / `context_leak`

## Phase D — Promote to agents (via Codex; supervisor mode requires it)

For each repeated pattern, produce a StrategyCandidate:
```
{ agent: rewrite | repair,
  change: prompt-guardrail | normalization-rule | deterministic-fallback | repair-class-switch,
  evidence: [case ids],
  regression: <new unit/eval case to add> }
```
Promotion targets:
- **rewrite:** `lib/rewrite-pipeline/{pipeline,strategy-router,model,style-cards,config}.ts`, `lib/rewrite-diagnosis.ts`
- **repair:** `pipeline.ts` / `strategy-router.ts` / `targeted-repair.ts` / `model.ts` (strong escalation)
- **deterministic fallback + normalization:** `lib/rewrite-pipeline/{strategy-catalog,checks}.ts`, the `normalize()` semantic-equivalence rules in `scripts/eval-scenarios.ts`
- **tests:** `tests/unit/*`
- **docs:** `docs/rewrite-strategy-memory.md`, `docs/skill-run-log.md`

Codex must: make the change → add the regression test → run lint + typecheck + unit tests → run banned-term scan → append decision to `plans/decisions-log.md`. Trigger the matching skill when relevant (provider-failure handling → `resilience-test-generation`; schema → `data-module-review`).

## Phase E — Validate

Re-run the affected shard(s) (or full 100) to confirm no regression. Ledger the spend.

---

## Guards

- Concurrency ≤5; per-worker independent `--output`/`--progress`; in-place run only while overnight supervisor is dormant (re-check at go-time).
- Banned-term scan after any generated copy or code change: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` — halt on match.
- Budget: full 100 ≈ DeepSeek $0.5–1 + Sapling $1–3 ≈ **NZ$2–7**; cap NZ$20/turn per `plans/sleep-run-budget.md`. Parallel ≠ cheaper (same call count), only faster (~1.5h serial → ~15–20 min).
- Ledger format: `<ISO> | <issue-id> | <provider> | <USD> | <NZD> | running total NZD`.

## Preflight (2026-05-23, green)

- banned-term working tree: clean.
- overnight supervisor: dormant (no process, no lock).
- corpus: 100 cases confirmed.
