# M2.5-002: Incremental + Resumable 100-Case Baseline Eval

Author: Claude Cowork supervisor
Date: 2026-05-22 NZST / 2026-05-22T00:25Z UTC
Status: Implementation plan for codex (delegated)

## Background

`scripts/eval-scenarios.ts` currently calls a single `await writeFile()`
at the end of the run (around line 1269 of pre-refactor). A 100-case
DeepSeek+Sapling run takes 30-60 min — well past the loop's 600s
codex-exec budget. Every timeout-kill produces zero output but still
burns DeepSeek budget.

This plan adds per-case streaming writes + resume so the loop can
process the 100-case corpus across many iterations.

## Deliverables (in order)

### A. `scripts/eval-scenarios.ts` refactor

1. **Additive CLI flags** (existing `--mode=` keeps working when
   `--corpus` is absent):
   - `--corpus=<path>` — load eval cases from a markdown corpus
   - `--output=<path>` — output file path (default
     `docs/scenario-evaluation-results.md`)
   - `--progress=<path>` — checkpoint JSON (default
     `plans/learning-baseline-progress.json`)
   - `--limit=<N>` — SOFT cap: process at most N un-done cases this run
   - `--resume` — load progress JSON, skip already-done IDs
   - `--time-budget-ms=<N>` — HARD time cap, default 540000 (540s, leaves
     60s of the loop's 600s budget for cleanup)

2. **Corpus markdown loader.** Parse the 5 tables in
   `docs/learning-baseline-corpus.md` (sections: Blank, Email, Customer
   support, Cover letter, Work update). Columns:
   `ID | Source | Scenario | Tone | Draft text | Expected facts to preserve | Expected draft AI-like signal range`.
   - Skip the `Source` and `Expected draft AI-like signal range` columns.
   - Map `Scenario` text to `ScenarioOption` (read `lib/rewrite-presets.ts`
     for exact union members; expected values: `"Blank / custom"`,
     `"Email"`, `"Customer support"`, `"Cover letter"`, `"Work update"`).
   - `Tone` must be `"Warm"` or `"Direct"`; reject unknown with a clear
     parse error citing the row ID.
   - `messageToReplyTo` defaults to `""` (corpus is draft-only style).
   - `expectedFacts` = split column 6 on `;` and trim.

3. **Per-case streaming write.** When `--corpus=` is in effect, replace
   the in-memory `rows.push(...)` + end-of-run `writeFile` pattern:
   - After each case completes, immediately `fs.appendFile` the
     `## <id>` block to `--output`.
   - After each case completes, atomically rewrite `--progress` JSON
     (write tmp + rename).

4. **Progress JSON shape**
   ```jsonc
   {
     "corpusPath": "docs/learning-baseline-corpus.md",
     "outputPath": "docs/learning-baseline.md",
     "startedAt": "2026-05-22T...Z",
     "lastUpdatedAt": "2026-05-22T...Z",
     "strategyVersion": "v3.1",
     "naturalnessThreshold": 50,
     "completedCases": [
       {
         "id": "lbc-blank-001",
         "scenario": "Blank / custom",
         "tonePreset": "Warm",
         "draftAiLikePercent": 84,
         "rewriteAiLikePercent": 32,
         "changePoints": -52,
         "factsPreserved": true,
         "unsupportedFactsCount": 0,
         "forbiddenViolationsCount": 0,
         "qualityFailure": false,
         "customerUsablePass": true,
         "strictSignalPass": true,
         "completedAt": "..."
       }
     ]
   }
   ```

5. **Resume semantics.** With `--resume`, load progress JSON, build
   `Set<string>` of completed IDs, skip them silently.

6. **Soft limit + hard time budget (auto-adapts).** At script startup,
   capture `startTime = Date.now()`. At the TOP of each per-case loop
   iteration (BEFORE making the expensive pipeline call):

   ```ts
   if (processedThisRun >= limit) {
     console.log(`[eval] --limit=${limit} reached; ${processedThisRun} cases this run; exiting cleanly`);
     break;
   }
   if (Date.now() - startTime > timeBudgetMs) {
     console.log(
       `[eval] time-budget ${timeBudgetMs}ms exhausted after ${processedThisRun} cases — exiting cleanly. ` +
       `Total completed: ${progress.completedCases.length}/${corpus.length}. ` +
       `Next --resume run will continue.`
     );
     break;
   }
   ```

   Then exit 0 normally. **This is the auto-throttle**: if `--limit=20`
   would overshoot the budget, the script processes only what fits (say
   12 or 15) and exits gracefully. The supervisor loop's next iteration
   `--resume` picks up from there with another 540s budget. No external
   auto-downshift logic needed — the script self-adapts to whatever
   per-case latency the providers are giving today.

   With atomic per-case JSON writes, even SIGKILL mid-case preserves
   prior completed cases. Per-case wall time is typically 20-40s, so
   540s budget realistically processes 12-25 cases per iteration.
   100-case corpus completes in 4-8 loop iterations.

7. **End-of-corpus summary.** When iteration finishes because every
   corpus case is in completedCases (NOT because limit or time-budget
   hit), rewrite the `<!-- summary -->...<!-- /summary -->` block at
   the end of the output file with aggregates from the progress JSON.
   This is M2.5-002's "done" signal.

8. **Per-case error handling.** Try/catch around each pipeline call.
   On unexpected throw (not `FactReconstructQualityError`), append a
   minimal `## <id>` block with `Quality failure reason: script_error:
   <first line>`, mark `qualityFailure: true` in progress JSON,
   continue. Don't let one bad case crash the run.

9. **Header refactor.** Extract the existing summary header (current
   lines 1198-1221) into a helper function. Legacy
   `--mode=smoke|focused|full` path keeps writing summary-at-top +
   per-case blocks + single end-of-run writeFile, behavior unchanged.

### B. New per-issue brief `plans/issues/M2.5-002.md`

```
# M2.5-002 Run 100-case baseline; record results to docs/learning-baseline.md

GitHub: #84
Milestone: M2.5-Learning

## Loop invocation

This issue is data collection, not source code. The supervisor loop
should run:

    npm run -s eval:scenarios -- \
      --corpus=docs/learning-baseline-corpus.md \
      --output=docs/learning-baseline.md \
      --progress=plans/learning-baseline-progress.json \
      --resume --limit=20

across N iterations (each handles up to 20 cases OR ~540s of work,
whichever comes first) until completedCases.length == 100 AND
docs/learning-baseline.md has a populated `<!-- summary --><!-- /summary -->`
block.

## "Done" definition

- `plans/learning-baseline-progress.json.completedCases.length === 100`
- `docs/learning-baseline.md` contains a populated summary block
- Banned-term scan (scoped) clean
```

### C. `plans/codex-implementation-prompt.md` addendum

Append a section telling the supervisor loop that for `ID == "M2.5-002"`
specifically, codex should RUN the eval invocation above (data
collection) rather than implement source changes. The invocation
produces incremental output across iterations.

### D. Unit test `tests/unit/eval-scenarios-corpus.test.ts`

Vitest. Test the corpus markdown parser only. Use fixture
`tests/fixtures/learning-corpus-mini.md` (~5 rows across 2 scenarios).
Cover: happy path, unknown-tone rejection, missing-column rejection,
trim handling. DO NOT run the rewrite pipeline in tests.

### E. Decisions log entry

Append one line to `plans/decisions-log.md`:

```
2026-05-22 | M2.5-002-infra | eval-scenarios.ts adds --corpus/--output/--progress/--limit/--resume/--time-budget-ms; soft-limit + hard-time-budget auto-adapts to per-case provider latency; SIGKILL-safe via per-case atomic JSON writes | needed because single end-of-run writeFile produced zero output on 600s timeout while burning DeepSeek budget; auto-throttle removes need for external --limit tuning
```

## Acceptance criteria

- `npm run typecheck` exits 0
- `npm run lint` exits 0 (no NEW violations)
- `npm run test -- --run tests/unit/eval-scenarios-corpus.test.ts` passes
- Existing `npm run eval:scenarios -- --mode=smoke` unchanged
- Smoke (if DeepSeek+Sapling env present):
  ```bash
  npm run -s eval:scenarios -- \
    --corpus=docs/learning-baseline-corpus.md \
    --output=/tmp/m25-002-out.md \
    --progress=/tmp/m25-002-progress.json \
    --resume --limit=2
  # Expect: 2 case blocks in output, 2 entries in JSON, ~1-2 min wall time
  ```
- Banned-term grep (scoped to `app components public lib`): 0 matches

## Out of scope

- The actual 100-case run (loop does that across iterations after merge)
- Changes to `lib/rewrite-pipeline/*` (pipeline behavior unchanged)
- Changes to `plans/overnight-supervisor.sh` (already fixed in 0283be8)
- Changes to `plans/issue-board.md` — supervisor flips M2.5-002 from
  BLOCKED-WAITING-ENG → pending AFTER this PR merges

## File deltas

- `scripts/eval-scenarios.ts` — significant refactor, ~200 lines net add
- `plans/issues/M2.5-002.md` — new file
- `plans/codex-implementation-prompt.md` — small append (~25 lines)
- `tests/unit/eval-scenarios-corpus.test.ts` — new test (~80 lines)
- `tests/fixtures/learning-corpus-mini.md` — new fixture (~5 rows)
- `plans/decisions-log.md` — append one line

## Constraints

- Banned-term grep on new code MUST return 0 matches
- Do NOT touch .env.local, LAUNCH_CONFIRMED, STRIPE_LIVE_CUTOVER_APPROVED,
  STRIPE_WEBHOOK_SECRET, STRIPE_PRICE_ID
- Do NOT modify DNS, Cloudflare Pages custom domain, or run
  `wrangler deploy`
- Do NOT run the actual 100-case eval (acceptance is unit test + 2-case
  smoke if env available)
- Open a PR — do NOT push to main directly. Supervisor reviews then
  merges.
- After merge: supervisor (NOT codex) flips
  `plans/issue-board.md` M2.5-002 row from `BLOCKED-WAITING-ENG` →
  `pending` so the loop picks it up next iteration.
