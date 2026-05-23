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

- Status: done
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M6-007
- Evidence: plans/task-status.json
- Suggested Codex action: Resolve or narrow the non-user blocker Codex reported for M6-007 without changing live money, dashboards, npm publish state, or secrets.
- Done condition: The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T04:53:03+12:00 — Narrowed M6-007 to the exact remaining runner prerequisite in `plans/m6-validation-report.md`. `npm run lint`, `npm run typecheck`, `npm run test`, `npm run build`, and `npm run cf:build` passed. `npm run test:e2e` is blocked before browser tests execute because this sandbox rejects loopback listen with `EPERM`; a minimal Node HTTP server confirmed the same restriction on `127.0.0.1`. The prior dotnet socket failure is out of scope for M6-007 because this issue does not touch `backend-dotnet/`. No live money, npm publish, dashboard, secret, or `.env.local` change was made.
- Worker evidence: 2026-05-23T04:57:46+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/217; Documented M6-007 validation evidence and non-sandboxed Playwright runner prerequisite.

## 2026-05-22T16:50:26Z — INV-2: repair branch active while M8-001 in_progress on board

- Status: done
- Source: Claude monitor
- Class: dirty_repo
- Priority: P2
- Related issue: M8-001, M6-007
- Evidence: plans/issue-board.md (M8-001 in_progress), current branch codex/repair-m6-007-codex-needs-human-blocked-autonomy-REPAIR-20260523044817
- Suggested Codex action: No action needed if the current codex exec completes normally and the loop returns to main before picking up the next pending issue. If the loop stalls on this repair branch, run `git checkout main && git pull` to reset before next task.
- Done condition: Loop returns to main branch and resumes normal pending-issue selection; M8-001 PR merges or advances.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T05:02:06+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/218; Recorded status-only repair result; branch reset is owned by the shell supervisor under the no-git protocol.

## 2026-05-23T05:05:40+12:00 — M6-008 codex-needs-human:BLOCKED-AUTONOMY

- Status: done
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M6-008
- Evidence: plans/task-status.json
- Suggested Codex action: Resolve or narrow the non-user blocker Codex reported for M6-008 without changing live money, dashboards, npm publish state, or secrets.
- Done condition: The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T05:08:19+12:00 — Reclassified M6-008 from `BLOCKED-AUTONOMY` to `BLOCKED-WAITING-USER` because the remaining evidence requires an operator-run live Stripe webhook event plus production DB checks. Documented the exact verification checklist in `plans/m6-validation-report.md`, including the StripeEvent lifecycle, required event types, and the fact that synthetic sample events may only prove endpoint delivery unless the event maps to an existing production user/customer/subscription. No live Stripe trigger, dashboard action, secret edit, `.env.local` edit, or real-money action was performed.
- Worker evidence: 2026-05-23T05:15:05+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/219; Reclassified M6-008 as operator-only live Stripe webhook verification and documented the exact DB evidence checklist.

## 2026-05-22T17:08:43Z — INV-2: repair branch active while M8-001 in_progress on board

- Status: done
- Source: Claude monitor
- Class: dirty_repo
- Priority: P1
- Related issue: M8-001, M6-008
- Evidence: plans/issue-board.md (M8-001 in_progress PR #173), current branch codex/repair-m6-008-codex-needs-human-blocked-autonomy-REPAIR-20260523050558
- Suggested Codex action: No action needed if the current codex exec completes normally and the loop returns to main before picking up the next pending issue. If the loop stalls on this repair branch, run `git checkout main && git pull` to reset before the next task selection.
- Done condition: Loop returns to main branch and resumes normal pending-issue selection; M8-001 PR merges or advances.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T05:19:51+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/220; Recorded status-only repair result; branch cleanup remains with the shell supervisor under the no-git protocol.

## 2026-05-23T05:28:55+12:00 — M7-002 undeclared-files-in-diff

- Status: done
- Source: shell supervisor
- Class: dirty_repo
- Priority: P1
- Related issue: M7-002
- Evidence: plans/task-status.json
- Suggested Codex action: Inspect the preserved stash, split unrelated work into scoped branches, and restore the supervisor to clean-branch operation.
- Done condition: No PR commits files outside the Codex-declared files_changed list.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T05:35:36+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/221; Classified Codex task status as supervisor runtime so clean issue diffs are not blocked as undeclared.

## 2026-05-22T17:39:37Z — .claude/ untracked directory not in .gitignore

- Status: done
- Source: Claude monitor
- Class: docs
- Priority: P3
- Related issue: M7-003 (current active task on branch chore/M7-003)
- Evidence: `git status --porcelain` shows `?? .claude/` in dirty worktree; no `.claude` entry in `.gitignore`
- Suggested Codex action: add `.claude/` to `.gitignore` if the directory contains only local tool state (confirm contents first); or commit intentionally if it should be tracked
- Done condition: `git status --porcelain` no longer shows `.claude/` as untracked, OR `.claude/` appears in `.gitignore`
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T05:51:18+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/222; Added .claude/ to .gitignore after confirming it only contains local Claude settings.

## 2026-05-23T05:45:56+12:00 — M7-003 codex-needs-human:BLOCKED-AUTONOMY

- Status: done
- Source: shell supervisor
- Class: autonomy
- Priority: P1
- Related issue: M7-003
- Evidence: plans/task-status.json
- Suggested Codex action: Resolve or narrow the non-user blocker Codex reported for M7-003 without changing live money, dashboards, npm publish state, or secrets.
- Done condition: The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T05:55:17+12:00 — Reclassified M7-003 from `BLOCKED-AUTONOMY` to `BLOCKED-PROVIDER` after reproducing the npm registry blocker: `npm view @sentry/nextjs version --json` failed with `ENOTFOUND registry.npmjs.org`, and no local `@sentry/nextjs` cache was present. The exact rerun prerequisite and state model are documented in `plans/m7-003-sentry-prerequisite.md`. No source implementation, live money, npm publish, dashboard, secret, `.env.local`, or `.dev.vars` change was made.
- Worker evidence: 2026-05-23T06:01:30+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/223; Reclassified M7-003 as npm registry provider-blocked and documented the networked lockfile prerequisite.

## 2026-05-22T18:08:57Z — INV-1: Finder-duplicate files outside M7-008 task scope

- Status: done
- Source: Claude monitor
- Class: dirty_repo
- Priority: P1
- Related issue: M7-008
- Evidence: `git status --porcelain` on branch chore/M7-008 shows five untracked files with macOS space-numbered names — `plans/task-status 2.json`, `plans/task-status 3.json`, `plans/task-status 4.json`, `plans/m6-validation-report 2.md`, `plans/m6-validation-report 3.md` — none of which are in M7-008 scope (KPI report script). task-status.json is also deleted (D) in the worktree.
- Suggested Codex action: Delete the five space-named duplicate files (`git rm --cached` + filesystem delete) and commit the cleanup on chore/M7-008 or a separate chore branch. Confirm contents match their canonical originals before deleting.
- Done condition: `git status --porcelain` no longer shows the five space-named files as untracked; no data is lost (canonical originals remain).
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T11:23:06+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/224; Confirmed the five Finder duplicate files are absent and canonical files remain.

## 2026-05-23T06:11:49+12:00 — M7-008 undeclared-files-in-diff

- Status: done
- Source: shell supervisor
- Class: dirty_repo
- Priority: P1
- Related issue: M7-008
- Evidence: plans/task-status.json
- Suggested Codex action: Inspect the preserved stash, split unrelated work into scoped branches, and restore the supervisor to clean-branch operation.
- Done condition: No PR commits files outside the Codex-declared files_changed list.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T11:28:35+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/225; Recorded status-only repair: M7-008 mixed diff was caused by already-removed Finder duplicate files; stash review remains owned by the shell supervisor under the no-git protocol.

## 2026-05-22T18:38:00Z — stash-before-repair fails on filenames with spaces (loop hard-stalled)

- Status: not_actionable
- Source: Claude monitor
- Class: dirty_repo
- Priority: P0
- Related issue: M7-008 (Finder-duplicate files in worktree)
- Evidence: plans/overnight.log — repeated `fatal: pathspec ':(prefix:0)"plans/m6-validation-report 2.md"' did not match any files` every 15 s since 06:34 NZ (18:34 UTC); plans/blockers-log.md tail confirms ~50+ `dirty-worktree-stash-failed` entries in a row
- Suggested Codex action: Fix `plans/overnight-supervisor.sh` stash-before-repair-inbox path-quoting so that filenames with spaces (untracked files) are handled correctly — use `git stash -u` or `git ls-files --others --exclude-standard -z | xargs -0 git stash -u` approach rather than per-file pathspec. Also delete the Finder-duplicate files (`plans/m6-validation-report 2.md`, `plans/m6-validation-report 3.md`, `plans/task-status 2.json`, `plans/task-status 3.json`, `plans/task-status 4.json`) before the stash so they stop triggering the bug.
- Done condition: `plans/overnight.log` shows no more `dirty-worktree-stash-failed` entries; loop processes a repair item successfully and moves on to normal issue work
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T13:08:00+12:00 — auto-closed after confirming the space-named Finder duplicates are gone, stash count is 0, and the supervisor stash-success path now has explicit success return coverage.

## 2026-05-22T18:38:00Z — INV-4: task-status.json stale (M7-008 BLOCKED on board, file says ready_to_commit)

- Status: not_actionable
- Source: Claude monitor
- Class: docs
- Priority: P2
- Related issue: M7-008
- Evidence: plans/task-status.json `issue_id: M7-008, next_action: ready_to_commit`; plans/issue-board.md shows M7-008 as BLOCKED; M8-001 is listed as in_progress on board
- Suggested Codex action: Either commit or discard the M7-008 work in the worktree (scripts/launch-kpi-report.ts, tests/unit/launch-kpi-report.test.ts, docs/launch-day-report.md), then reset task-status.json to reflect the active in_progress task M8-001 (or blank if none is currently active)
- Done condition: task-status.json `issue_id` matches a board in_progress entry (or is reset blank); worktree is clean of M7-008 artefacts
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T13:08:00+12:00 — auto-closed as stale forensic state after PR #225 recorded the M7-008 status-only repair; the supervisor deletes task-status before launching each repair or issue.

## 2026-05-22T19:40:25Z — stash-accumulation-livelock

- Status: not_actionable
- Source: Claude monitor
- Class: dirty_repo
- Priority: P1
- Related issue: M7-008 (cascades from space-file stash failure)
- Evidence: `git stash list | wc -l` → 387 stashes on main, all named `overnight-preserve-pre-repair-inbox-<epoch>`; loop creating ~1 new stash every 15 s since at least 06:34 NZ today. Prerequisite P0 item (space-named files) already exists but is itself unconsumed due to same livelock.
- Suggested Codex action: After space-named files are removed (see P0 item), run `git stash clear` to drop all 387+ accumulated stashes, then confirm `git stash list` is empty.
- Done condition: `git stash list` returns empty; no new overnight-preserve stashes accumulate in next loop cycle.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
- Worker evidence: 2026-05-23T13:08:00+12:00 — auto-closed after `git stash list | wc -l` returned 0.

## 2026-05-23T13:34:00+12:00 — M2.5-002-infra: incremental + resumable eval-scenarios.ts

- Status: not_actionable
- Source: Cowork supervisor (Claude)
- Class: autonomy
- Priority: P1
- Related issue: M2.5-002 (https://github.com/ChuanQiao1128/replyinmyvoice/issues/84)
- Evidence: `plans/m25-002-incremental-eval.md` is the complete implementation plan (~235 lines). `scripts/eval-scenarios.ts` (1809 lines) currently calls a single end-of-run `writeFile`, so a 100-case DeepSeek+Sapling run takes 30–60 min and exceeds the 600s `codex exec` budget — every timeout produces zero output. The plan adds CLI flags `--corpus / --output / --progress / --limit / --resume / --time-budget-ms`, per-case streaming appends, atomic progress JSON checkpointing, and soft-limit + hard-time-budget auto-throttle. M2.5-002 is intentionally held `BLOCKED-AUTONOMY` on `plans/issue-board.md` until this refactor lands; after merge the supervisor (not Codex) flips that row to `pending` so the loop runs the 100-case eval across 4–8 iterations.
- Suggested Codex action: Read `plans/m25-002-incremental-eval.md` in full and implement Deliverables A through E exactly as specified. Touch only the files listed in the plan's `## File deltas` section (`scripts/eval-scenarios.ts`, `plans/issues/M2.5-002.md`, `plans/codex-implementation-prompt.md`, `tests/unit/eval-scenarios-corpus.test.ts`, `tests/fixtures/learning-corpus-mini.md`, and one appended line in `plans/decisions-log.md`). Open a PR. Under sprint policy (`CLAUDE.md` "Active Commercialization Sprint"), Codex may merge / direct-push when CI is green and self-validation (lint + typecheck + corpus-parser unit test + scoped banned-term scan) passes. When writing the `plans/decisions-log.md` entry, note that the plan's references to `0283be8` and `BLOCKED-WAITING-ENG` are stale historical context — current truth is hardening PRs `f13bdee` (#226) and `52d272e` (#228), and the active label is `BLOCKED-AUTONOMY`.
- Done condition: PR merged on `main`. `npm run typecheck` exits 0. `npm run lint` adds no new violations. `npm run test -- --run tests/unit/eval-scenarios-corpus.test.ts` passes. `scripts/eval-scenarios.ts` accepts the six new flags and the legacy `--mode=smoke|focused|full` path is behavior-unchanged. New files `plans/issues/M2.5-002.md` and `tests/fixtures/learning-corpus-mini.md` are present. Configured banned-term scan stays clean across `app components public lib`.
- Forbidden actions: running the actual 100-case eval (Codex must NOT issue real DeepSeek+Sapling calls beyond the 2-case acceptance smoke if env is available); modifying the `M2.5-002` row in `plans/issue-board.md` (that flip belongs to the supervisor after merge); npm publish; live Stripe charges; DNS / Cloudflare Pages custom-domain edits; `wrangler deploy`; force-push to `main`; touching `.env.local`, `.dev.vars`, `globalapikey/`, `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_WEBHOOK_SECRET`, or `STRIPE_PRICE_ID`.
- Worker evidence: 2026-05-23T13:40:16+12:00 — repair changed files outside declared files_changed list; changes stashed for split/review

## 2026-05-23T13:42:00+12:00 — supervisor-skip-relax: release 5 scoped rows + remove daytime-only rationale

- Status: in_progress
- Source: Cowork supervisor (Claude)
- Class: autonomy
- Priority: P1
- Related issue: M1-007 (#85), M1-009 (#87), M3-001 (#???), M3-002 (#???), M3-005 (#???); supervisor policy
- Evidence: User directive on 2026-05-23 set continuous 24/7 operation as the policy — no time-of-day-based skip rationales remain valid. Audit identified 5 currently `BLOCKED-AUTONOMY` rows that are scope-isolated, additive, and safe to run unattended: M1-007 (add `entraUserId String? @unique` to Prisma User + migration), M1-009 (new `tests/unit/entra-auth.test.ts` against mock JWKS, no live Entra calls), M3-001 (additive scenarios in `lib/rewrite-presets.ts`), M3-002 (reduce tone presets to 4 in `lib/rewrite-presets.ts` with backward-compat mapping retained), M3-005 (zod cap in `lib/validation.ts`). Skip heuristics currently at `plans/overnight-supervisor.sh:1175-1186` (M1-Entra cluster, stated reason "daytime only / supervised implementation") and `:1194-1206` (M3 V2 cascade, stated reason "typed refactor across lib"). Remaining M1 rows (M1-002/003/004/005/006/008/010) still couple to the live auth path and stay BLOCKED-AUTONOMY for coupling-risk reasons, NOT time-of-day. Remaining M3 rows (M3-003/004/006/007/008) stay blocked because they require M3-001/002/005 to land first, or are the actual cascade refactor (M3-004), or depend on M3-004 (M3-006/007/008).
- Suggested Codex action: Make three coordinated edits in a single PR.
  (1) `plans/overnight-supervisor.sh:1178` — change the M1-Entra case branch from `M1-002|M1-003|M1-004|M1-005|M1-006|M1-007|M1-008|M1-009|M1-010)` to `M1-002|M1-003|M1-004|M1-005|M1-006|M1-008|M1-010)`. Replace the `log "  Skipping $ID (Entra auth migration cluster — daytime only)"` line with `log "  Skipping $ID (Entra auth cluster — couples to live auth path; release individually after per-issue brief)"`. Replace the `append_decision` text from `"...deferred to supervised implementation, not a user blocker"` to `"...auth coupling risk; release individually"`.
  (2) `plans/overnight-supervisor.sh:1196` — change the M3 case branch from `M3-001|M3-002|M3-003|M3-004|M3-005|M3-006|M3-007|M3-008)` to `M3-003|M3-004|M3-006|M3-007|M3-008)`. Replace the `log` text with `log "  Skipping $ID (V2 layout cascade — depends on M3-001/002/005 + M3-004 component rewrite)"`. Update the `append_decision` text accordingly.
  (3) `plans/issue-board.md` — flip the 5 rows (`M1-007`, `M1-009`, `M3-001`, `M3-002`, `M3-005`) from `BLOCKED-AUTONOMY` to `pending` in the rightmost state column. Use `git status` between edits so the loop's own concurrent writes don't conflict.
  (4) `plans/issues/` — create per-issue briefs for the 5 released IDs based on the source-of-truth descriptions in `plans/issue-manifest.md` (M1 section lines ~17-60, M3 section lines ~135-173). Each brief should be 10-30 lines, scoped to the single file(s) the issue touches, with banned-term reminder.
  (5) `plans/decisions-log.md` — append one line: `<ISO date> | supervisor-skip-relax | Released M1-007, M1-009, M3-001, M3-002, M3-005 to pending; removed daytime-only rationale from supervisor.sh M1 and M3 case branches; remaining M1/M3 BLOCKED-AUTONOMY rows kept with coupling-risk / cascade-prereq rationale per user 24/7 operation policy.`
- Done condition: PR merged on `main`. The 5 named rows show `pending` on `plans/issue-board.md`. `plans/overnight-supervisor.sh:1178` and `:1196` no longer reference the 5 released IDs in their case patterns and no longer contain the substring "daytime". Per-issue briefs exist at `plans/issues/M1-007.md`, `plans/issues/M1-009.md`, `plans/issues/M3-001.md`, `plans/issues/M3-002.md`, `plans/issues/M3-005.md`. `npm run lint`, `npm run typecheck`, `bash -n plans/overnight-supervisor.sh` all pass. Configured banned-term scan stays clean across `app components public lib`.
- Forbidden actions: changing any other case branch in supervisor.sh; flipping any row other than the 5 named; touching M1-002/003/004/005/006/008/010 or M3-003/004/006/007/008 on the board; modifying `.env.local`, `.dev.vars`, `globalapikey/`, `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID`; force-push `main`; npm publish; live money; DNS / Cloudflare dashboard edits.

## 2026-05-23T13:33:53+12:00 — M9-002 undeclared-files-in-diff

- Status: pending
- Source: shell supervisor
- Class: dirty_repo
- Priority: P1
- Related issue: M9-002
- Evidence: plans/task-status.json
- Suggested Codex action: Inspect the preserved stash, split unrelated work into scoped branches, and restore the supervisor to clean-branch operation.
- Done condition: No PR commits files outside the Codex-declared files_changed list.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes
