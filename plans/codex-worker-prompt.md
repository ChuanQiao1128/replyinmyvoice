# Codex Worker Prompt - Monitor Handoff Repair Queue

Use this prompt for a dedicated Codex worker automation that consumes `plans/codex-worker-inbox.md`.

## Purpose

Fix non-user blockers found by the Claude monitor without requiring the owner to copy/paste monitor output into a Codex chat.

`plans/overnight-progress.md` is for people. Do not treat it as a task queue. The worker's task source is `plans/codex-worker-inbox.md`.

## Inputs

Read these first:

1. `docs/commercialization-north-star.md`
2. `plans/codex-worker-inbox.md`
3. `plans/supervisor-handoff.md`
4. `plans/issue-board.md`
5. `plans/blockers-log.md`
6. `plans/overnight.log`

`plans/issue-board.md`, `plans/blockers-log.md`, and `plans/overnight.log` are evidence sources only. They help diagnose an inbox item, but they do not create work by themselves unless the loop is dead and the restart rule below applies.

## Worker Rules

- Process at most one `Status: pending` inbox item per run.
- If no pending item exists, do not infer repair work from `plans/overnight-progress.md`, `plans/blockers-log.md`, or `plans/issue-board.md`.
- Exception: if the shell loop is dead or `plans/overnight.log` is stale for more than 25 minutes, no `plans/STOP-OVERNIGHT.txt` or `plans/MONEY-MADE.txt` exists, and `plans/issue-board.md` still has pending work, restart the loop with the documented `screen -dmS rimv-overnight ...` command, record the restart, and exit without taking any other work.
- If the item is a true user blocker, mark it `waiting_user` and do not fix it.
- If the shell loop is active in the main worktree, use an isolated worktree or only write a scoped issue brief for the shell loop to pick up later.
- Prefer a normal branch, PR, CI, and merge flow for code changes.
- Keep changes scoped to the inbox item.
- Do not merge to `main` while the shell loop is actively running unless the inbox item is specifically about repairing a wedged or unsafe loop and the loop has been stopped first.
- Do not use live money, Stripe refunds, npm publishing, provider dashboard changes, or secret changes.
- Do not print or commit secrets, `.env.local` values, provider keys, private keys, raw user rewrite text, email addresses, or customer content.

## Done Condition

Before marking an item `done`, record:

- branch or PR URL;
- files changed;
- verification commands;
- remaining limitations;
- whether the shell loop can continue.

If a fix is too large, create or update a scoped row in `plans/issue-board.md` and mark the inbox item `done` with the new issue id.

## Queue Discipline

- Claude monitor writes structured repair items.
- Codex worker consumes structured repair items.
- The owner reads `plans/overnight-progress.md`.
- The main shell loop consumes `plans/issue-board.md`.

Keep those channels separate. Do not make the worker parse human progress prose as its normal trigger path.
