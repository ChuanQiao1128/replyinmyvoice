# Blockers Log

Issues that hit BLOCKED state during overnight runs — needs user action when they wake up.

Format per entry:
```
## <ISO timestamp> — <issue-id> — <BLOCKED reason>
What was attempted: <one paragraph>
What user needs to do: <one paragraph>
```

---

## 2026-05-21T11:13:33Z — M0-006 — mcp-timeout (workspace-write)

What was attempted: First overnight supervisor trigger. M0-001..M0-005 already marked done in board. Picked M0-006 (Update AGENTS.md to reflect Stripe live state) as next pending. Sent four `mcp__codex__codex` calls in `workspace-write` sandbox mode (one full brief, one minimal branch-create, one one-line branch-create, one with codex-reply on a read-only thread) — three timed out before any output, one returned successfully in `read-only` sandbox but could not create the branch because `read-only` blocks .git ref writes. No source files were modified, no branch was created, working tree unchanged from pre-trigger state (the pre-existing modified CLAUDE.md / plans/create-issues.py and untracked plans/* files remain as they were).

What user needs to do: Investigate codex MCP workspace-write health — possibly restart the codex MCP server, verify it can spawn workspace-write sessions, check codex daemon logs. The read-only sandbox responds, so the MCP transport is alive; the timeout is specific to workspace-write session startup. Once codex MCP workspace-write is working, the next scheduled supervisor trigger will pick M0-006 back up automatically. No code changes needed by user.

Trigger statistics: 4 codex MCP calls attempted; 3 timeouts (initial, retry, third), 1 success (read-only probe). Per handoff stop condition ">3 timeouts in a row → write blockers-log and exit", this trigger exits without completing an issue.

---

## 2026-05-21T13:18:00Z — M1-011 + M1-012 — codex MCP non-responsive (work never persisted to disk)

What was attempted: After the 13:03Z trigger merged PR #152 and PR #153 successfully (M1-001, M1-013, M1-014 → done on main), supervisor delegated M1-011 + M1-012 bundle to codex with `sandbox=danger-full-access` and `approval-policy=never`. Two MCP calls sent:
1. Full brief (~50 lines: branch + 6 file edits + lint/typecheck/test + commit/push/PR) — MCP timed out.
2. Shortened brief (~30 lines: branch + same 6 edits + commit/push/PR, NO npm/lint/test, CI handles them) — MCP timed out.

Unlike the 12:34Z trigger where codex completed work despite MCP timeouts, this trigger codex did NOT write to disk at all. Verified ~6 min after first call via local bash: branch `chore/m1-011-012-clerk-env-cleanup` does NOT exist locally or in `git for-each-ref refs/remotes/origin/chore`. `lib/admin-auth.ts:45` still shows `adminClerkUserIds: optionalEnv("ADMIN_CLERK_USER_IDS", "")` (unchanged). The earlier read-only codex calls in this same trigger (PR status + merge actions) DID succeed, so MCP transport is alive — but write-path codex appears wedged.

Hypothesis: prior codex session may have hung mid-process on an earlier task and is blocking subsequent calls. Or codex daemon needs restart. Or write-path took >5min for npm install which exceeded MCP transport patience.

What user needs to do: Likely safe to do nothing — next scheduled trigger (~25 min from 13:03Z = ~13:28Z) will retry M1-011 + M1-012. If those also fail, user should manually run `pkill -f codex` and `claude doctor` or equivalent to reset codex MCP daemon.

Pre-checked file targets (so next trigger doesn't have to re-discover):
- `lib/admin-auth.ts:45` — `adminClerkUserIds: optionalEnv("ADMIN_CLERK_USER_IDS", "")` (rename to `adminUserIds` + `"ADMIN_USER_IDS"`)
- `lib/admin-auth.ts:21,24` — param `adminClerkUserIds` and `clerkUserId` (rename to `adminUserIds` + `userId`)
- `lib/admin-visible.ts:6,15` — param `clerkUserId` + env `ADMIN_CLERK_USER_IDS`
- `lib/entra-auth.ts:139` — delete `optionalEnv("CLERK_SECRET_KEY") ||` line
- `.env.example:102` — `ADMIN_CLERK_USER_IDS=` → `ADMIN_USER_IDS=`
- `app/app/page.tsx` — caller of shouldShowAdminEntry (need to pass `userId:` instead of `clerkUserId:`)
- `tests/unit/admin-auth.test.ts` — uses `clerkUserId` + `adminClerkUserIds` in test fixtures
- Append to `docs/manual-setup.md` a "## Clerk removal — rollback notes" section

No @clerk/* deps in package.json or package-lock.json (M1-011 is verification-only — confirmed via grep this trigger).

## 2026-05-22T10:31:07+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:31:22+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:31:37+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:31:52+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:32:07+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:32:22+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:32:37+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:32:53+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:33:08+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:33:23+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T10:33:38+12:00 — M1-002 — git-checkout-main-failed

What was attempted: checkout to main failed

What user needs to do: review the branch chore/M1-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

---

## 2026-05-21T22:33Z — overnight-supervisor.sh v2 — CRASHED AT STARTUP, STOPPED BY MONITOR

**USER ATTENTION** — The new shell-based continuous worker (`plans/overnight-supervisor.sh`) was launched at 22:31:07 UTC (10:31:07+12:00 NZST). It died on its first iteration and entered a tight ~15-second failure-thrash loop, polluting this very file with 11 duplicate `M1-002 — git-checkout-main-failed` entries before the monitor trigger at 22:33Z touched `plans/STOP-OVERNIGHT.txt` to halt it. Script exited cleanly at 22:33:53Z: 11 issues processed, 0 done, 11 blocked, 0 needs-human.

Three independent root causes — all need fixing before restart:

1. **Dirty repo state from prior work.** Working tree was already on `chore/m1-002-entra-middleware` with an uncommitted `middleware.ts` modification when the loop started. The script's first action per iteration is `git checkout main` — which fails ("could not checkout main") because of the uncommitted change AND the lock file below.
2. **Stale `.git/index.lock`** (0 bytes, dated May 21 22:16 UTC — 15 minutes before the script ran). A prior git op crashed and left the lock. Any git write blocks until it's removed.
3. **`overnight-supervisor.sh` issue-board sed is broken on macOS BSD sed.** Every iteration emitted `sed: 1: "s|^(\| M1-002 \|[^|]*\| ...": RE error: empty (sub)expression`. BSD sed needs `-E` for unescaped `(...)` capture groups (or POSIX `\(...\)`). Non-fatal (script continues) but means the issue-board would not auto-update even after the git issues are fixed — status tracking would silently desync.

Suggested resume sequence:

```bash
cd /Users/qc/Desktop/CloudFlare
rm -f .git/index.lock
git status                                                    # confirm middleware.ts is the only modified file
git stash push -m "pre-overnight-restart" middleware.ts       # safest — preserves the work
git checkout main && git pull
# (optional but recommended) patch the sed in plans/overnight-supervisor.sh:
#   - use `sed -E -i '' "s|^\(\| ... \)|...|"`  OR
#   - replace the board-update with a python heredoc (proven pattern from prior triggers)
# Then delete the STOP sentinel and relaunch:
rm plans/STOP-OVERNIGHT.txt
nohup bash plans/overnight-supervisor.sh > plans/overnight.log 2>&1 & disown
ps -ef | grep overnight-supervisor.sh | grep -v grep          # verify PID
```

Also worth a moment of thought: the *prior* worker pattern (Claude supervisor in Cowork driving `mcp__codex__codex` directly) was working well — 5 PRs merged in the most recent active trigger (M4-007, M4-008, M5-001, M2-007, M8-001), backlog at 33 done. The shell-loop is a NEW worker pattern that has not been validated against this repo's macOS toolchain — it failed on the very first iteration on three independent issues. Consider whether to revert to the proven MCP-driven supervisor pattern instead.

Cleanup hint: the 11 duplicate `M1-002 — git-checkout-main-failed` entries above (between the original block and this entry) are spam from the thrash loop — safe to delete in bulk before resuming work.

Sentinels at monitor exit: `plans/MONEY-MADE.txt` absent, `plans/STOP-OVERNIGHT.txt` PRESENT (touched by monitor at 22:33Z to halt the thrash). Banned-term scan: clean across app/components/public/lib.

---

## 2026-05-22T12:56:53+12:00 — M2.5-007 — codex-no-status

What was attempted: codex exec did not write plans/task-status.json. Log: plans/codex-exec-M2.5-007.log

What user needs to do: review the branch chore/M2.5-007 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T13:16:38+12:00 — M2.5-002 — codex-needs-human:BLOCKED-AUTONOMY

What was attempted: M2.5 baseline could not run: npm tsx IPC failed in sandbox; loader reached corpus parser but the coverage summary table causes a 7-column parse failure.

What happens next: Codex/engineering should fix the corpus parser or reroute this baseline run. This is not a user decision blocker.

## 2026-05-22T16:02:46+12:00 — M4-001 — codex-needs-human:BLOCKED-PROVIDER

What was attempted: Recorded the four-case M4-001 refresh attempt; Sapling returned timeout_or_network before measured signals could be produced.

What happens next: retry or route around the provider outage from Codex/engineering. This is not a user decision blocker.

## 2026-05-22T16:30:03+12:00 — M5-002 — codex-no-status

What was attempted: codex exec did not write plans/task-status.json. Log: plans/codex-exec-M5-002.log

What happens next: Codex rescued the timed-out WIP on branch codex/rewrite-quality-analysis-telemetry, verified the focused telemetry tests, lint, typecheck, and full Vitest suite, and is preparing a normal PR. This is not a user decision blocker.

## 2026-05-22T17:00:22+12:00 — M5-003 — codex-no-status

What was attempted: codex exec did not write plans/task-status.json. Log: plans/codex-exec-M5-003.log

What user needs to do: review the branch chore/M5-003 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T17:30:46+12:00 — M6-001 — codex-needs-human:BLOCKED-WAITING-USER

What was attempted: Documented blocked Cloudflare Worker secret-name diff; Wrangler could not resolve Cloudflare API in this sandbox.

What user needs to do: review the branch chore/M6-001 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T17:35:29+12:00 — M6-002 — codex-needs-human:BLOCKED-WAITING-USER

What was attempted: Blocked Worker secret push because M6-001 missing-secret diff is unavailable and Cloudflare API DNS fails in this sandbox.

What user needs to do: review the branch chore/M6-002 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T18:12:46+12:00 — M6-003 — codex-needs-human:BLOCKED-PROVIDER

What was attempted: Documented M6-003 Worker preview smoke attempt blocked by sandbox DNS; rerun curl checks from a networked shell.

What user needs to do: review the branch chore/M6-003 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T18:29:32+12:00 — M4-011 — codex-no-status

What was attempted: codex exec did not write plans/task-status.json. Log: plans/codex-exec-M4-011.log

What user needs to do: review the branch chore/M4-011 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.

## 2026-05-22T18:45:26+12:00 — M6-004 — codex-needs-human:BLOCKED-PROVIDER

What was attempted: Documented networked Cloudflare domain verification rerun because sandbox DNS blocked the live API and formal-domain smoke.

What user needs to do: review the branch chore/M6-004 (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.
