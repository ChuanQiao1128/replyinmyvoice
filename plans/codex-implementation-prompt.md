# Codex Implementation Prompt (no git, no gh)

When the shell supervisor invokes you (`codex exec`), follow this protocol:

## Inputs (placed by shell before invoking you)
- `plans/current-task.md` — single file describing the issue you must implement (copied from `plans/issues/<id>.md` or manifest entry). READ THIS FIRST.
- Current branch: shell has already created `chore/<issue-id>` from main. You're on it. DO NOT switch.

## Your job
1. Read `plans/current-task.md` carefully — title, files to touch, acceptance criteria.
2. Make the required file changes per the brief.
3. Run validations:
   ```
   npm run lint
   npm run typecheck
   npm run test
   ```
   If the issue touches `backend-dotnet/`, also: `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo`
4. Write `plans/task-status.json` with the exact schema:
   ```json
   {
     "issue_id": "<id from current-task.md>",
     "branch": "chore/<id>",
     "files_changed": ["AGENTS.md", "lib/foo.ts", "..."],
     "lint": "pass" | "fail",
     "typecheck": "pass" | "fail",
     "tests": "pass" | "fail" | "skipped",
     "dotnet_tests": "pass" | "fail" | "skipped",
     "banned_terms_found": [],
     "summary": "one-line summary for commit message body",
     "title": "concise commit title under 72 chars",
     "next_action": "ready_to_commit" | "needs_human" | "abort"
   }
   ```
5. Print the path of `plans/task-status.json` to stdout. Exit.

## Hard rules

- **DO NOT touch git**. No `git checkout`, `git add`, `git commit`, `git push`, `git stash`, `git rebase`. The shell handles all git.
- **DO NOT call `gh`**. The shell handles all GitHub API.
- **DO NOT modify** `.env.local`, `.dev.vars`, `globalapikey/`, `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_PRICE_ID`, `STRIPE_WEBHOOK_SECRET`.
- **Banned terms** in any new content + `lib/**`: `humanizer | bypass | undetect | detector | evade`. If you would introduce any, abort: set `"next_action": "abort"` in status JSON and write the reason in `summary`.
- **No real Stripe charges**. No `stripe trigger` against live keys.
- **Eval cost cap per issue**: DeepSeek + Sapling ≤ NZ$5. If your work would exceed this, stop and set `"next_action": "needs_human"`.

## Decision authority

When the brief is ambiguous:
- Make a sensible choice that fits the repo's existing patterns (naming, structure, test framework).
- Document the choice in the `summary` field of the status JSON.
- Do NOT ask the human (they're asleep) — make the call.

When the decision affects real money, public APIs, external commitments, or new paid resources beyond `AZURE_BUDGET_LIMIT`:
- Stop. Set `"next_action": "needs_human"` and explain in `summary`.

## What "ready_to_commit" means

Set `next_action = "ready_to_commit"` ONLY when:
- All file changes implementing the issue are done
- Lint passes
- Typecheck passes
- Tests pass (or skipped intentionally per brief)
- No banned terms
- No secrets in files

If anything fails, set `next_action = "abort"` (revert your changes first) or `"needs_human"` (leave changes for human review).

## Time budget per invocation

Aim for ≤10 min of work per issue. If the issue is bigger, do what you can, then set `"next_action": "needs_human"` with a note about the remaining scope.

## M2.5-002 incremental eval special case

When `plans/current-task.md` has `ID == "M2.5-002"` or is copied from
`plans/issues/M2.5-002.md`, this is a data-collection issue rather than
a source-change issue. Do not implement source changes for that invocation.

Run this command instead:

```bash
npm run -s eval:scenarios -- \
  --corpus=docs/learning-baseline-corpus.md \
  --output=docs/learning-baseline.md \
  --progress=plans/learning-baseline-progress.json \
  --resume --limit=20
```

The script writes per-case output and progress after each completed case.
If the command exits 0 with fewer than 100 completed cases, write
`plans/task-status.json` with `next_action: "needs_human"` and summarize
that the next supervisor iteration should run the same command again. Set
`next_action: "ready_to_commit"` only after the progress JSON has 100
completed cases, the learning baseline document has a populated summary
block, validations pass, and the scoped banned-term scan is clean.
