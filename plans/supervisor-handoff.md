# Overnight Supervisor Handoff — Reply In My Voice Commercialization Sprint

**Audience**: a fresh Claude session triggered by the scheduled task `overnight-supervisor`. You have NO conversation context. This file IS your context. Read it fully before acting.

---

## ⚠ ARCHITECTURE CHANGE 2026-05-22 — Monitor-only mode

As of 2026-05-22 the heavy lifting moved to a continuous shell-driven loop: `plans/overnight-supervisor.sh` is started **once** by the user and runs until one of the stop sentinels fires (see "North-star stop conditions" below). That shell loop is the real worker: it pulls the next `pending` issue from `plans/issue-board.md`, branches, calls `codex exec` for the file edits, runs lint/typecheck/test/banned-term scan, commits, pushes, opens the PR, polls CI, and merges.

The durable commercial target is:

```text
docs/commercialization-north-star.md
```

Read that file first. It defines the shared goal for Claude, Codex, and the human owner.

Use `screen` for local restarts in this Codex desktop environment. `nohup ... & disown` has been observed to be killed after the launching command exits.

Recommended restart command:

```bash
cd /Users/qc/Desktop/CloudFlare
screen -dmS rimv-overnight bash -lc 'cd /Users/qc/Desktop/CloudFlare && bash plans/overnight-supervisor.sh'
```

**Your job (the scheduled Claude trigger) is now PURE MONITORING.** Do NOT pick issues or call `mcp__codex__codex` for new implementation work — that would race with the shell loop. Specifically:

1. **Liveness check**: confirm `screen -ls` shows `rimv-overnight` or `ps -ef | grep -v grep | grep overnight-supervisor.sh` shows a process. Also check `plans/overnight.log` modification time. If the log has not updated in more than 20 minutes and no stop signal exists, report the stall.
2. **Restart only when safe**: if the loop is dead, no `plans/STOP-OVERNIGHT.txt` exists, and no `plans/MONEY-MADE.txt` exists, restart it with the `screen` command above. Log the restart in `plans/overnight-progress.md`.
3. **Tail the log**: `tail -200 plans/overnight.log`. Surface repeated codex timeouts with no file progress, checkout failures, dirty-repo failures, ssh/push failures, CI failures, gh auth failures, banned-term hits, eval signal unavailable runs, or provider budget failures to `plans/blockers-log.md`.
4. **Issue progress**: count `pending`, `done`, `in_progress`, `BLOCKED`, and `BLOCKED-WAITING-USER` rows from `plans/issue-board.md`.
5. **BLOCKED scan**: scan `plans/blockers-log.md` and `plans/issue-board.md` for new blockers since the prior trigger.
6. **Recent main commits**: read `git log origin/main -5 --oneline`.
7. **North-star signal**: if `plans/MONEY-MADE.txt` exists, immediately tell the user that real revenue was confirmed and the unattended loop should remain stopped for human review.
8. **Checkpoint write**: append a 4-6 line summary to `plans/overnight-progress.md` with trigger time, loop alive yes/no, issue counts, recent merge summary, new blockers, and the next commercial gate from `docs/commercialization-north-star.md`.
9. **Exit fast**: 3-5 minutes max. You are not doing implementation work in this trigger anymore.

Only override monitor-only mode if the shell loop is wedged in a way that needs structural intervention (e.g. all codex calls have failed for >60 min). In that case, write to blockers-log with the diagnosis and STOP the shell loop (`touch plans/STOP-OVERNIGHT.txt`) rather than competing with it.

**Time budget per monitor trigger**: 3-5 minutes. Cron still fires every 25-30 min.

---

## Step 1: Establish situational awareness (≤2 min)

Read these files in this order to know what's going on:

1. `docs/commercialization-north-star.md` — durable definition of the commercial target and agent responsibilities
2. `CLAUDE.md` — particularly the "Active Commercialization Sprint" section at the bottom (sprint posture + hard limits)
3. `plans/issue-board.md` — issue list with status (pending / in_progress / done / BLOCKED)
4. `plans/decisions-log.md` — what previous trigger Claudes / codex decided (create if missing)
5. `plans/blockers-log.md` — known blockers requiring user input (create if missing)
6. `plans/overnight-progress.md` — running tally of overnight progress (create if missing)
7. `git log origin/main -5 --oneline` — what's recently been merged

If `plans/issue-board.md` is missing, abort and write to `plans/blockers-log.md`: "trigger at <time>: issue board missing, cannot proceed".

## Step 2: Concurrency check (≤30s)

Check if another supervisor is currently running. Look at `plans/supervisor-lock.txt`:
- If file exists AND modified within last 22 min → ANOTHER SUPERVISOR ACTIVE. Exit immediately without making changes. Write to overnight-progress.md: "trigger at <time>: skipped, prior supervisor still active".
- If file exists but stale (>22 min) → previous supervisor died. Delete the lock and continue.
- If file does NOT exist → continue. Create it with current timestamp + your trigger time.

Format of supervisor-lock.txt: one line `<ISO timestamp> | trigger-<N> | started`

Always remove the lock at end of your run.

## Step 3: Monitor and exit

Before exiting, ALWAYS:

1. Append to `plans/overnight-progress.md`:
   ```
   ## Trigger at <ISO>
   - Loop: alive | stalled | stopped
   - Board: <done> done / <pending> pending / <blocked> blocked / <in_progress> in_progress
   - Recent main: <latest merged commit or PR>
   - Blockers: <new blockers or none>
   - Next commercial gate: <auth | rewrite eval | billing | API | MCP | monitoring>
   ```
2. Append to `plans/blockers-log.md` only if a new user or engineering action is required.
3. Remove `plans/supervisor-lock.txt` if this monitor created it.
4. Exit. Do not start an issue, edit source files, commit, push, merge, or call Codex for implementation.

## Monitor hard limits

- Do not implement product issues.
- Do not call Codex MCP for new work.
- Do not run real Stripe charges or refunds.
- Do not run `npm publish`.
- Do not print `.env.local`, `.dev.vars`, tokens, API keys, private keys, or provider secrets.
- Do not modify launch, Stripe, Azure, or provider dashboard configuration.
- Do not resolve ambiguous product decisions yourself; record them in `plans/blockers-log.md` or `plans/overnight-progress.md` for the user/daytime engineering session.

## North-star stop conditions

The goal is not "105 issues merged." The commercial target is defined in `docs/commercialization-north-star.md`.

The shell loop (`plans/overnight-supervisor.sh`) keeps running until exactly one of:

1. **`plans/MONEY-MADE.txt` exists** — the user creates this file after real revenue is confirmed. This stops the current unattended loop for human review. If API/MCP gates remain open, continue with a new supervised run after review.
2. **`plans/STOP-OVERNIGHT.txt` exists** — user-initiated emergency stop.
3. **No more `pending` rows in `plans/issue-board.md`** — entire backlog is done. The shell loop exits; the next trigger should re-pick into polish/bug-fix mode (open issues with `gh issue list --state open`, prioritize the M2 quality regression + smoke any new bug reports).

The shell loop's per-process MAX_HOURS and MAX_ISSUES are set to effective infinity (720h, 10000 issues) and should not be the real stop. Those numbers exist only as a safety belt — the sentinels above are the real exit.

**Per-trigger (monitor mode) stop**: just exit after 3-5 min. You are not doing implementation work; the shell loop is. See "ARCHITECTURE CHANGE 2026-05-22" at the top of this doc.

## Emergency stop signal

If user creates `plans/STOP-OVERNIGHT.txt`, both the shell loop AND the next Claude monitor trigger abort cleanly. Remove any lock, write a final progress entry, exit. Symmetric file: `plans/MONEY-MADE.txt` — same behavior, but framed as "we won."

## When you're done with this trigger

Just exit. The next scheduled fire is in 25 min.

---

# Appendix: division of labor

- Claude scheduled task: monitor, summarize, alert, restart only when safe.
- Shell supervisor: git, GitHub, CI polling, merge, board status.
- Codex implementation step: scoped file edits and validations only.
- Human owner: live money actions, production dashboard decisions, npm token/publish approval, final launch calls.
