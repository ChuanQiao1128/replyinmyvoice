# Codex Worker Inbox

Purpose: this file is the safe handoff path from Claude's low-budget monitor, and from the shell supervisor itself, to the normal Codex repair path. Claude should write concise, sanitized work items here when it sees a non-user blocker that Codex can investigate or fix. The user should not need to copy monitor output into a Codex chat.

This is the machine repair queue. `plans/overnight-progress.md` is the human progress report. Do not mix the two.

## 2026-05-22T11:35:00Z — INV-4: M4-014 task-status.json and board stale after PR #213 merge

- Status: done
- Source: Claude monitor
- Class: docs
- Priority: P2
- Related issue: M4-014 (https://github.com/ChuanQiao1128/replyinmyvoice/issues/204)
- Evidence: git log shows a13f0d4 "Polish app workspace shell (#213)" merged to main; plans/issue-board.md still shows M4-014 as `in_progress`; plans/task-status.json still references M4-014 with status in_progress/next_action:ready_to_commit
- Suggested Codex action: Update plans/issue-board.md to mark M4-014 as `done`; delete or reset plans/task-status.json so the next task write starts clean. Commit the ledger update to main.
- Done condition: plans/issue-board.md shows M4-014 as `done`; plans/task-status.json does not reference M4-014 as in_progress.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T03:41:00+12:00 — Marked M4-014 done in `plans/issue-board.md` and removed stale `plans/task-status.json` after PR #213 had already merged.

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

## 2026-05-22T18:29:32+12:00 — M4-011 codex-no-status

- Status: not_actionable
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M4-011
- Evidence: plans/codex-exec-M4-011.log
- Suggested Codex action: Investigate why Codex did not write plans/task-status.json for M4-011; fix the loop prompt/task contract or requeue the issue with evidence.
- Done condition: The supervisor can run the issue again and receive a valid plans/task-status.json, or the issue is reclassified with a concrete non-user blocker.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-22T18:39:51+12:00 — codex-no-status during repair; log plans/codex-exec-REPAIR-20260522182950.log

## 2026-05-22T18:45:27+12:00 — M6-004 codex-needs-human:BLOCKED-PROVIDER

- Status: not_actionable
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M6-004
- Evidence: plans/task-status.json
- Suggested Codex action: Resolve or narrow the non-user blocker Codex reported for M6-004 without changing live money, dashboards, npm publish state, or secrets.
- Done condition: The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-22T18:48:06+12:00 — Reran secret-free DNS/HTTP checks. Node DNS lookup returned `ENOTFOUND` for `api.cloudflare.com`, `replyinmyvoice.com`, and `example.com`; curl to the Cloudflare API and formal domain also returned `Could not resolve host`. The exact networked prerequisite and commands are already documented in `plans/custom-domain-attach.md`. No autonomous code repair can make this sandbox resolve external DNS, and no live money, npm publish, dashboard, secret, or `.env.local` change was made.

## 2026-05-22T11:08:50Z — overnight-supervisor.sh: persist blocked state to main before branch switch

- Status: done
- Source: Claude monitor
- Class: ci
- Priority: P1
- Related issue: M4-015 (loop target), plans/overnight-supervisor.sh
- Evidence: plans/STOP-OVERNIGHT.txt — "supervisor is repeatedly rerunning M4-015 after Codex reports browser screenshot checks are blocked by sandbox permissions. The blocked/needs_human board state is being written on the issue branch and then stashed, so main still sees M4-015 as pending/in_progress and selects it again."
- Suggested Codex action: Patch plans/overnight-supervisor.sh so that any terminal task outcome (blocked, needs_human, abort, CI failure, merge failure) immediately commits the updated board + ledger lines to main (or cherry-picks just those file changes) before switching branches or stashing, so the next issue-selection loop sees the correct status. After patching, remove plans/STOP-OVERNIGHT.txt.
- Done condition: plans/STOP-OVERNIGHT.txt is absent; a test run of M4-015 selection results in the correct BLOCKED-AUTONOMY or BLOCKED-WAITING-USER status persisted on main without infinite reselection.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes, force-reset migrations
- Worker evidence: 2026-05-23T03:41:00+12:00 — Patched `plans/overnight-supervisor.sh` to persist terminal issue states on main after preserving branch work, classify sandbox browser/server startup failures as `BLOCKED-AUTONOMY`, and treat remotely merged PRs as done when the local merge command fails after the remote merge. Verification: `bash -n plans/overnight-supervisor.sh`, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts`, and full `npm run test` passed. `plans/STOP-OVERNIGHT.txt` remains a local ignored stop signal until the owner chooses to resume the loop.

## 2026-05-23T04:04:59+12:00 — M6-005 codex-needs-human:BLOCKED-AUTONOMY

- Status: not_actionable
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M6-005
- Evidence: plans/task-status.json
- Suggested Codex action: Resolve or narrow the non-user blocker Codex reported for M6-005 without changing live money, dashboards, npm publish state, or secrets.
- Done condition: The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T04:08:07+12:00 — Reproduced the sandbox DNS blocker with secret-free checks: Node DNS lookup returned `ENOTFOUND` for `replyinmyvoice.com` and `example.com`, and curl returned `Could not resolve host` before HTTP status evidence. The exact network-capable M6-005 route checklist and expected statuses are recorded in `docs/preflight-report.md`; the issue board is narrowed to `BLOCKED-PROVIDER`. No live money, npm publish, dashboard, DNS, secret, or `.env.local` change was made.

## 2026-05-23T04:35:30+12:00 — M6-007 codex-needs-human:BLOCKED-AUTONOMY

- Status: not_actionable
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M6-007
- Evidence: plans/task-status.json
- Suggested Codex action: Resolve or narrow the non-user blocker Codex reported for M6-007 without changing live money, dashboards, npm publish state, or secrets.
- Done condition: The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T04:53:03+12:00 — Narrowed M6-007 to the exact remaining runner prerequisite in `plans/m6-validation-report.md`. `npm run lint`, `npm run typecheck`, `npm run test`, `npm run build`, and `npm run cf:build` passed. `npm run test:e2e` is blocked before browser tests execute because this sandbox rejects loopback listen with `EPERM`; a minimal Node HTTP server confirmed the same restriction on `127.0.0.1`. The prior dotnet socket failure is out of scope for M6-007 because this issue does not touch `backend-dotnet/`. No live money, npm publish, dashboard, secret, or `.env.local` change was made.

## 2026-05-22T16:50:26Z — INV-2: repair branch active while M8-001 in_progress on board

- Status: pending
- Source: Claude monitor
- Class: dirty_repo
- Priority: P2
- Related issue: M8-001, M6-007
- Evidence: plans/issue-board.md (M8-001 in_progress), current branch codex/repair-m6-007-codex-needs-human-blocked-autonomy-REPAIR-20260523044817
- Suggested Codex action: No action needed if the current codex exec completes normally and the loop returns to main before picking up the next pending issue. If the loop stalls on this repair branch, run `git checkout main && git pull` to reset before next task.
- Done condition: Loop returns to main branch and resumes normal pending-issue selection; M8-001 PR merges or advances.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
