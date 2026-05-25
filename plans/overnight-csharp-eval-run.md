# Overnight autonomous run — C# rewrite eval → strategy fix → merge+deploy

Owner mandate (2026-05-26, ChuanQiao1128, before sleeping): run the 100-case eval in
parallel against the **C# production engine**, modify the rewrite strategy from the
findings, and **if validation passes, merge + deploy**; fix problems autonomously. If a
prior task already deployed the change, instead analyze and write rewrite-improvement
suggestions. Owner reviews in the morning.

This file is my durable state across background-completion wake-ups. Update the progress
log as each phase completes.

## Locked config
- Engine: **C# `FactReconstructRewriteProvider`** (in-process via `ReplyInMyVoice.Eval`). See [[eval-engine-vs-prod-divergence]].
- Sharding: 10 shards x 10 via `EVAL_CASE_IDS`, 2 waves of 5 concurrent (`run-csharp-eval-100.sh`).
- `EVAL_MAX_ATTEMPTS=4` (cost cap). Single pass (measure noise first).
- Budget: **<= NZ$20** this window (fresh; the 05-24 STOP was a prior window). Ledger every run in `plans/sleep-run-budget.md`.

## Pre-run findings (already verified)
- Harness fixed + committed on branch `codex/eval-harness-naturalness-gate` (c5f5df7): section parser, faithful draft+tone input, must_not_claim screen, customer-usable composite, relaxed-gate probe, `EVAL_DRY_RUN`. 96/96 backend tests green; dry-run parses 100; 1-case smoke usable.
- **#4 naturalness gate divergence (the prime suspect):** C# `FactReconstructRewriteProvider.PassesNaturalnessRule` (:217-220) still has the old `rewrite <= draft` punishment for clean drafts that TS removed (TS = `rewrite <= threshold`). See [[csharp-eval-harness-not-corpus-ready]], [[naturalness-gate-noise]]. The harness's `relaxedRecoverable` count measures how many naturalness failures this rule causes. If high, **porting that 1-line rule fix is the prime, low-risk, TS-proven strategy change**.

## Deploy pipeline (verified — `.github/workflows/dotnet-azure.yml`)
- push to `main` → build-test (Release build + full `dotnet test`) → **deploy** job (ref==main only): Azure OIDC login (GitHub secrets, NOT local az), `dotnet ef database update` on LIVE Azure SQL, deploy Functions zip, smoke `curl /api/health`.
- push to `codex/**` or any PR → build-test ONLY (no deploy). This is the pre-merge gate.
- **MERGE-SAFE RULES:** (1) NO new EF migration in the change (the deploy applies migrations to live SQL). (2) build-test green on the PR (both dotnet-azure + cloudflare-worker). (3) re-run eval shows improvement, no regression. (4) after deploy, verify prod 200s; rollback on break.

## Plan / phases
1. [DONE] Harness fix + commit + smoke.
2. [RUNNING] Full 100 baseline → `docs/rewrite-eval-results/run-<stamp>/` (path in `plans/overnight-eval-latest-outroot.txt`).
3. [ ] Aggregate + bucket failures (fact_loss / forbidden / naturalness-gate-divergence (relaxedRecoverable) / structure / blank).
4. [ ] Strategy change on the branch. Prime candidate: port naturalness rule. Validate: Release build + full dotnet test + banned-term + NO migration.
5. [ ] Re-run eval (all 100) → confirm customerUsable improves, no regression. Ledger.
6. [ ] Push branch → open PR → wait both CI build-tests green → merge to main → deploy job runs.
7. [ ] Verify prod: `/api/health`, a real rewrite call, key frontend pages 200. Rollback (`wrangler rollback` frontend; revert commit + push for backend) if broken.
8. [ ] Write morning analysis: results, what changed + why, deploy status, and further rewrite-improvement recommendations.

## Rollback
- Last good main = `0d2f194`. Backend rollback = revert the merge commit + push (CI redeploys prior). Frontend = `wrangler rollback`.

## Progress log
- 2026-05-26 ~15:42 NZT: harness committed (c5f5df7); Release build green; 1-case smoke usable (network/model/Sapling OK). Launching full 100 baseline in background.
- 2026-05-26 ~03:50 NZT: BASELINE DONE (run-20260526-034425). Aggregate: customerUsable 38/100, engine success 96/100, factPass 48/100, forbiddenViol 14, **relaxedRecoverable 1**, below50 96/96.
- **PIVOT (the data overturns the pre-run hypothesis):** naturalness gate divergence is a NON-issue (relaxed=1; rewrites score very human, below50=96/96). The 38 is an EVAL-MEASUREMENT artifact, not engine quality:
  - **Fact matcher false-negatives:** 5/5 inspected "fact failures" are present in the rewrite (must_keep phrased "The recipient is Ren." vs rewrite "Hi Ren"; "candidate is Alina" vs "Hi Alina"; "interview"→"meeting" paraphrase). Mirrors [[eval-gate-over-literal-fact-matching]] (the agent isn't the bottleneck).
  - **Forbidden screen false-positives:** flagged refund/discount cases are policy-careful negated mentions ("I cannot issue a cash refund … not a direct refund"); my 40-char negation window misses them.
- **Therefore: no engine/strategy change to deploy.** The fix is the EVAL gates (matcher anchor-first + forbidden sentence-level negation), then re-score saved outputs ($0, no model calls), then report the TRUE number. Engine recommendations only for any GENUINE failures that survive the re-score. Deploy decision: an eval-only fix has no product benefit and merging triggers a full prod redeploy (risk without upside) — so PR for morning review, do NOT auto-deploy. Document reasoning.
