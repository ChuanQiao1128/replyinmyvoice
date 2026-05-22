# Codex Worker Inbox

Purpose: this file is the safe handoff path from Claude's low-budget monitor to a dedicated Codex worker. Claude should write concise, sanitized work items here when it sees a non-user blocker that Codex can investigate or fix. The user should not need to copy monitor output into a Codex chat.

This is the machine repair queue. `plans/overnight-progress.md` is the human progress report. Do not mix the two.

Claude remains monitor-only: it does not implement code and does not call Codex directly from the scheduled task. Codex worker consumes this inbox with a separate worktree or automation and either fixes the item, turns it into a scoped issue-board row, or marks it not actionable.

## Queue Item Format

```text
## <ISO timestamp> — <short title>

- Status: pending | in_progress | done | not_actionable | waiting_user
- Source: Claude monitor | shell supervisor | user
- Class: provider | prereq | autonomy | ci | dirty_repo | docs | product | security
- Priority: P0 | P1 | P2 | P3
- Related issue: <M-id or GitHub URL if any>
- Evidence: <log path, PR URL, CI URL, or file path only; no secrets>
- Suggested Codex action: <one scoped action>
- Done condition: <observable verification>
- Forbidden actions: <live money, npm publish, dashboard changes, secret changes, or other limits>
```

## Routing Rules

- Use this inbox for `BLOCKED-PROVIDER`, `BLOCKED-PREREQ`, `BLOCKED-AUTONOMY`, plain `BLOCKED`, repeated CI failures, dirty-repo loop failures, stale lock failures, and monitor summaries that point to an engineering fix.
- Do not use this inbox for ordinary progress summaries, issue counts, recent-merge summaries, or "nothing actionable" monitor checkpoints.
- Do not use this inbox for real-money tests, npm publication, provider dashboard changes, missing secrets, legal/product decisions, or anything that truly requires the owner. Those stay `BLOCKED-WAITING-USER`.
- Do not paste raw `.env.local`, `.dev.vars`, token values, API keys, private keys, customer messages, rough drafts, rewritten text, or email addresses.
- Codex worker should process one item at a time, prefer isolated worktrees, and leave evidence in the queue item before marking it done.
- If the shell loop is actively editing the same worktree, Codex worker must not race it. Use an isolated worktree or wait until the loop stops.
- Before adding a new pending item, Claude should check the pending section for an equivalent item and avoid duplicates.

## Pending Items

No queued Codex-worker items yet.
