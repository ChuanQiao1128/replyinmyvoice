# Codex Worker Inbox

Purpose: this file is the safe handoff path from Claude's low-budget monitor, and from the shell supervisor itself, to the normal Codex repair path. Claude should write concise, sanitized work items here when it sees a non-user blocker that Codex can investigate or fix. The user should not need to copy monitor output into a Codex chat.

This is the machine repair queue. `plans/overnight-progress.md` is the human progress report. Do not mix the two.

Claude remains monitor-only: it does not implement code and does not call Codex directly from the scheduled task. The shell supervisor consumes this inbox before selecting new product issues and either fixes the item, turns it into a scoped issue-board row, or marks it not actionable. The scheduled Codex automation is only a watchdog/emergency loop-health fallback.

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
- The shell supervisor should process one item at a time before product issue work and leave evidence in the queue item before marking it done.
- The scheduled Codex automation must not race a healthy shell loop. It may restart a dead/stale loop when safe, and it may only attempt emergency loop-health repair when repeated safe restarts fail.
- Before adding a new pending item, Claude should check the pending section for an equivalent item and avoid duplicates.

## Pending Items

No queued repair items yet.
