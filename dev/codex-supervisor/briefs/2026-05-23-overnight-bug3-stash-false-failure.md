# Codex brief — 2026-05-23 — overnight-supervisor bug #3 (stash false-failure)

Status: drafted by Cowork supervisor on 2026-05-23. MCP dispatch via `mcp__codex__codex` timed out without starting a session; brief preserved here so it can be retried via MCP or copy-pasted into a manual `codex` CLI invocation against `/Users/qc/Desktop/CloudFlare`.

Default args for retry:

- cwd: `/Users/qc/Desktop/CloudFlare`
- sandbox: `workspace-write`
- approval-policy: `never`

---

## TASK

Fix the dirty-worktree stash false-failure in `plans/overnight-supervisor.sh` that produced ~1206 bogus "blocked" repair items overnight, clean the resulting bogus queue, and land a regression test.

## CONTEXT

- Repo root: `/Users/qc/Desktop/CloudFlare`
- Hardening PR #226 (commit `f13bdee`) is already on `main`; this is the **third** structural supervisor bug, NOT fixed by that PR.
- Key function to diagnose: `stash_dirty_worktree()` in `plans/overnight-supervisor.sh`, lines 577–619.
- Primary caller exhibiting the bug: `process_repair_inbox_once()` at lines 874–907 (the failing branch is the `|| { ... append_blocker "repair-inbox" "dirty-worktree-stash-failed" ... }` block at lines 884–890).
- `AGENTS.md` applies: the configured banned-term scan must stay clean in `app/components/public/lib`; no secrets in source; no live payment/deploy/DNS actions; keep Stripe in sandbox mode.
- Current working tree is dirty with files from prior sessions and the planning turn that authored this brief — Stage 0 below cleans this up before the diagnostic work.

## LIVE EVIDENCE (from `plans/overnight.log`, real run, 2026-05-23 NZST)

```text
[2026-05-23T06:01:30+12:00]   Repair progress so far: 7 done, 0 blocked
[2026-05-23T06:11:49+12:00]   ERROR: changed files outside plans/task-status.json files_changed; preserving work and blocking
[2026-05-23T06:12:04+12:00] ─── Repair queue item detected
[2026-05-23T06:12:05+12:00]   Preserving non-runtime dirty worktree paths in stash (pre-repair-inbox)
Saved working directory and index state On main: overnight-preserve-pre-repair-inbox-1779473525
[2026-05-23T06:12:05+12:00]   ERROR: could not preserve dirty worktree before repair
[...repeats every ~15s for ~5.5 hours, ending at 11:28:50 when STOP-OVERNIGHT.txt was honored]
[2026-05-23T11:23:06+12:00]   Repair progress so far: 8 done, 1206 blocked
```

Observe: `git stash push` clearly succeeds (prints "Saved working directory and index state On main: …"), but `stash_dirty_worktree()` still returns non-zero. The caller logs "could not preserve dirty worktree before repair", appends a `dirty-worktree-stash-failed` blocker, increments `REPAIRS_BLOCKED`, and continues. The "Stash failure signature count" / "Dropping partial stash" log lines that the failure path in `stash_dirty_worktree()` should emit are NOT present in the log — which means the function is returning non-zero via a different path than the documented `stash_exit != 0` branch. Verify before patching; do not assume.

## STAGE 0 — STABILIZE WORKING TREE

Run `git status --porcelain` and inspect each modified/untracked file. Commit benign planning artifacts so the bug-fix diff stays clean:

- `plans/monitor-watchdog-prompt.md` (untracked) — created earlier in the supervisor session as the watchdog prompt source for the Claude scheduled task. Commit as: `Add monitor watchdog prompt for scheduled task`.
- `docs/skill-run-log.md` (modified) — append-only audit log from the supervisor session. Commit alongside the prompt.
- `plans/blockers-log.md`, `plans/codex-worker-inbox.md`, `plans/decisions-log.md`, `plans/issue-board.md`, `plans/overnight-progress.md` — loop runtime state from last night's run. Review diffs; if coherent, commit as a separate `Sync supervisor runtime state from 2026-05-23 overnight run` commit. If any look corrupt or bogus (e.g. a thousand identical entries), stash them with a clear label and surface in the PR description.

These two prep commits land before the fix work so the bug-fix PR is reviewable on its own.

## STAGE 1 — DIAGNOSE

1. Instrument `stash_dirty_worktree()` with `set -x` (locally, do not commit the trace) and reproduce: create one non-runtime dirty file (e.g. `echo test > app/_diag.tmp`), call the function with label `diag`, confirm the exit code matches expectation. Then create a non-runtime dirty file plus a runtime dirty file (e.g. modify `plans/issue-board.md` too) and call again.
2. Identify the EXACT line that produces the non-zero return on success. Document it in the PR description.

## STAGE 2 — PATCH

3. Fix the function so a successful `git stash push` (zero exit + "Saved working directory and index state" output) always returns 0 to the caller. If the bug is a missing explicit `return 0` after the success path, add it. If the bug is a flag/var that flips state outside the function, address that root cause rather than papering over it.
4. Preserve the genuine-failure paths exactly: `record_stash_failure` must still fire on real failures, `STASH_FAILURE_COUNT` must still escalate to the STOP signal at `MAX_STASH_FAILURES`.
5. Touch only `plans/overnight-supervisor.sh` and new test files. Do NOT refactor unrelated code.

## STAGE 3 — REGRESSION TEST

6. Add `plans/tests/test_stash_dirty_worktree.sh` (or extend an existing test harness if one already exists — check first). The test must:
   - Fail on the pre-fix code path (assert function returns 0 on success).
   - Pass on the post-fix code.
   - Cover at least: clean worktree (expect 0), dirty non-runtime only (expect 0 after stash), dirty runtime-only (expect 0, no stash), genuine stash failure (expect 1 + blocker recorded — simulate by passing a non-existent path or by mocking git).
7. Wire the test into whatever CI/local test runner already exists for shell tests (check `.github/workflows/` and `package.json` scripts). If none exists, add a minimal `bash plans/tests/run-all.sh` script and document it in `plans/tests/README.md`.

## STAGE 4 — CLEAN THE BOGUS QUEUE

8. Read `plans/codex-worker-inbox.md`. For every queue item whose Class is `dirty_repo` and whose Evidence/title references `dirty-worktree-stash-failed` / `could not preserve dirty worktree` / `Saved working directory and index state` — mark `Status: not_actionable` with a single note `auto-closed: caused by stash false-failure bug fixed in PR <number>`. Do this in a SINGLE follow-up commit, not the bug-fix commit itself.
9. DO NOT touch human-written queue items or product-class items. If in doubt, leave the item alone.
10. Append a one-line entry to `plans/decisions-log.md`:

    ```text
    <ISO date> | overnight-supervisor-bug3 | Closed N auto-generated dirty-worktree-stash-failed inbox items after PR <#> | bug was stash_dirty_worktree returning non-zero on success
    ```

## ACCEPTANCE

- New test fails on pre-fix HEAD, passes after the fix.
- Manual smoke: with `STOP-OVERNIGHT.txt` present, run the new test directly (or equivalent) and see no false "could not preserve" errors.
- Configured banned-term scan clean for `app components public lib`.
- `plans/codex-worker-inbox.md` retains all non-bogus items; bogus items are now `Status: not_actionable` with the auto-closed note.
- `plans/decisions-log.md` has the one-line bug3 entry.
- All commits land on `main` via PR (preferred) or direct push to `main` per sprint policy if the change is contained.
- Banned-term scan and lint/typecheck pass.

## DO NOT

- Modify `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_PRICE_ID`, `STRIPE_WEBHOOK_SECRET`, or anything in `.env.local` / `.dev.vars` / `globalapikey/`.
- Run `npm publish`, real Stripe charges, DNS edits, Cloudflare custom-domain attaches, or Azure paid-resource provisioning.
- Delete `plans/overnight.log` (it has forensic value for this bug).
- Force-push `main`. Squash-merge or direct push is fine; `--force` / `--force-with-lease` is not.
- Touch `plans/STOP-OVERNIGHT.txt` or `plans/MONEY-MADE.txt` — the user controls the start signal.
- Print, log, or commit secret values.
- Add banned-term literals to new code, comments, filenames, commit messages, PR descriptions, decisions log entries, or test fixtures.
- Rewrite history of any branch beyond your own feature branch.

## Report back with

(a) the PR URL(s)
(b) the exact line that caused the false failure
(c) the bogus-item count you closed
(d) test results
