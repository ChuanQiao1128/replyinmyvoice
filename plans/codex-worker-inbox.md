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

## 2026-05-22T17:59:41+12:00 — M6-001 Cloudflare secret-name diff retry

- Status: done
- Source: shell supervisor
- Class: provider
- Priority: P2
- Related issue: M6-001
- Evidence: plans/worker-secret-diff.md
- Suggested Codex action: Retry or narrow the read-only Worker secret-name diff path without printing secret values, pushing secrets, deploying, or changing provider dashboards.
- Done condition: `plans/worker-secret-diff.md` contains a completed name-only diff, or the inbox item records a provider/DNS failure with current evidence and no user-only action hidden inside it.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes, deploys, printing `.env.local` values
- Result: Retried the read-only `wrangler secret list --name replyinmyvoice-app --format json` path with Wrangler logs redirected to writable temp storage. Wrangler still failed before returning Worker metadata because `api.cloudflare.com` / `dash.cloudflare.com` DNS resolution is unavailable from this shell. Direct DNS lookup returned `ENOTFOUND` for both hosts. No secret values were printed or written, no secrets were pushed, no deploy ran, no dashboard state changed, and `.env.local` was not modified. Current evidence is recorded in `plans/worker-secret-diff.md`.
- Worker evidence: 2026-05-22T18:06:47+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/195; Retried read-only Wrangler secret listing, recorded current Cloudflare DNS blocker evidence, and reclassified M6-002 as prerequisite-blocked.

## 2026-05-22T18:12:46+12:00 — M6-003 codex-needs-human:BLOCKED-PROVIDER

- Status: not_actionable
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M6-003
- Evidence: plans/task-status.json
- Suggested Codex action: Resolve or narrow the non-user blocker Codex reported for M6-003 without changing live money, dashboards, npm publish state, or secrets.
- Done condition: The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-22T18:14:30+12:00 — M6-003 already recorded the sandbox DNS blocker in docs/preflight-report.md and plans/issue-board.md. No autonomous code repair can make this local shell resolve the Worker host; rerun the documented curl checks from a networked shell.
