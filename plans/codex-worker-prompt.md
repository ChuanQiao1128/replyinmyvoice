# Codex Worker Prompt - Loop Watchdog

Use this prompt for the scheduled Codex automation `replyinmyvoice-codex-worker`.

## Purpose

Act as a dead-man watchdog for the autonomous commercialization loop. The normal repair path is now inside `plans/overnight-supervisor.sh`: the shell loop consumes `plans/codex-worker-inbox.md` before it selects the next issue-board item.

`plans/overnight-progress.md` is for people. Do not treat it as a task queue. `plans/codex-worker-inbox.md` is a machine repair queue, but the scheduled automation must not consume it while the shell loop is healthy.

## Inputs

Read these first:

1. `docs/commercialization-north-star.md`
2. `plans/supervisor-handoff.md`
3. `plans/codex-worker-inbox.md`
4. `plans/issue-board.md`
5. `plans/blockers-log.md`
6. `plans/overnight.log`

`plans/issue-board.md`, `plans/codex-worker-inbox.md`, `plans/blockers-log.md`, and `plans/overnight.log` are evidence sources for loop health. They do not create normal repair work for this scheduled automation unless the loop is dead or stale.

## Worker Rules

- If the shell loop is alive and `plans/overnight.log` has updated within the last 25 minutes, do not modify files and do not process inbox items. Exit after recording nothing or a terse watchdog observation if needed.
- If `plans/STOP-OVERNIGHT.txt` or `plans/MONEY-MADE.txt` exists, do not restart the loop and do not process inbox items.
- If the shell loop is dead or `plans/overnight.log` is stale for more than 25 minutes, no stop signal exists, and `plans/issue-board.md` or `plans/codex-worker-inbox.md` still has pending work, restart the loop with the documented `screen -dmS rimv-overnight ...` command, record the restart in `plans/overnight-progress.md`, and exit.
- If repeated restart attempts fail because the loop script itself is broken, open a scoped repair PR from a clean worktree or mark the relevant inbox item `waiting_user` only when the blocker is truly user-owned.
- Do not process ordinary repair inbox items while the shell loop is healthy. The main loop owns normal repair execution.
- Do not use live money, Stripe refunds, npm publishing, provider dashboard changes, or secret changes.
- Do not print or commit secrets, `.env.local` values, provider keys, private keys, raw user rewrite text, email addresses, or customer content.

## Done Condition

Before recording a watchdog action, include:

- loop state: alive, stale, stopped, or restarted;
- whether `STOP-OVERNIGHT.txt` or `MONEY-MADE.txt` exists;
- whether pending issue-board or inbox work remains;
- the exact restart command used, if any;
- any limitation that prevents restart.

## Queue Discipline

- Claude monitor writes structured repair items.
- The shell supervisor consumes structured repair items before product issue work.
- The scheduled Codex automation is only a watchdog and emergency loop-health repair path.
- The owner reads `plans/overnight-progress.md`.
- The main shell loop consumes `plans/codex-worker-inbox.md` first and `plans/issue-board.md` second.

Keep those channels separate. Do not make the worker parse human progress prose as its normal trigger path.
