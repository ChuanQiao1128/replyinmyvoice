# Codex Implementation Prompt (no git, no gh)

When the shell supervisor invokes you (`codex exec`), follow this protocol:

## Inputs (placed by shell before invoking you)
- `docs/commercialization-north-star.md` — durable commercial target. READ THIS to understand the overall goal, then keep your edits scoped to the current issue.
- `plans/current-task.md` — single file describing the issue you must implement (copied from `plans/issues/<id>.md` or manifest entry). READ THIS FIRST.
- Current branch: shell has already created `chore/<issue-id>` from main. You're on it. DO NOT switch.

## Your job
1. Read `plans/current-task.md` carefully — title, files to touch, acceptance criteria.
2. Check `docs/commercialization-north-star.md` only for goal alignment. Do not expand the issue scope just because the north-star doc contains broader gates.
3. Make the required file changes per the brief.
4. Run validations:
   ```
   npm run lint
   npm run typecheck
   npm run test
   ```
   If the issue touches `backend-dotnet/`, also: `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo`
5. Write `plans/task-status.json` with the exact schema:
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
6. Print the path of `plans/task-status.json` to stdout. Exit.

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

## Permission and scope judgment

Before editing, classify the task:

- Autonomous engineering: source, tests, docs, prompts, local scripts, and non-secret configuration within the issue scope.
- Provider/sandbox blocker: DNS/network/API availability, rate limits, unavailable browser/server binding, or CI/provider failures that need retry, documentation, or repair but not user permission.
- User-only blocker: live money, refunds, npm publish, provider dashboard changes, secret values, legal/product decisions, or explicit owner approvals.
- Workspace-race blocker: another active agent or loop is editing the same worktree, branch, status file, or issue-board state.

Proceed only for autonomous engineering. For the other categories, write a precise `needs_human` or `abort` status with evidence and do not mutate secrets, dashboards, money, publish state, or another live agent's work.

## Work allocation judgment

Do not split a coherent task just because it spans several files. A strong model with enough context should solve one integrated product problem end to end when the acceptance criteria share UI, API, data, tests, copy, or deployment behavior. Splitting such a task burns tokens on repeated context, loses prompt/context-cache efficiency, and creates integration risk.

Split only when the task is naturally independent, can be assigned to separate worktrees or disjoint files, has clear interfaces, and can be verified independently. If a task is too large for the timebox but is conceptually coherent, leave a `needs_human` summary explaining the smallest safe continuation rather than creating many partial, overlapping edits.

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

## Timebox preflight

This preflight happens before editing files. Decide whether the current task can realistically finish inside the supervisor timebox, including required skill use, implementation, validation, banned-term scanning, and writing `plans/task-status.json`.

If the task spans many independent surfaces, requires broad browser screenshots across several routes, changes more than three major modules, or is otherwise unlikely to finish inside the timebox, do not start source edits. Write `plans/task-status.json` immediately with `"next_action": "needs_human"`, mark validations as `"skipped"`, and explain the smallest safe split in `summary`.

For broad frontend redesign work like M4-011, create or update `plans/frontend-redesign-followups.md` with scoped follow-up tasks before writing the status file. This prevents the supervisor from killing a long-running edit before status is written.

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
