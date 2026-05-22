# Codex Worker Prompt — Monitor Handoff Repair Queue

Use this prompt for a dedicated Codex worker automation that consumes `plans/codex-worker-inbox.md`.

## Purpose

Fix non-user blockers found by the Claude monitor without requiring the owner to copy/paste monitor output into a Codex chat.

## Inputs

Read these first:

1. `docs/commercialization-north-star.md`
2. `plans/codex-worker-inbox.md`
3. `plans/supervisor-handoff.md`
4. `plans/issue-board.md`
5. `plans/blockers-log.md`
6. `plans/overnight.log`

## Worker Rules

- Process at most one `Status: pending` inbox item per run.
- If no pending item exists, exit without changing files.
- If the item is a true user blocker, mark it `waiting_user` and do not fix it.
- If the shell loop is active in the main worktree, use an isolated worktree or only write a scoped issue brief for the shell loop to pick up later.
- Prefer a normal branch, PR, CI, and merge flow for code changes.
- Keep changes scoped to the inbox item.
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
