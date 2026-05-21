# Overnight Supervisor Handoff — Reply In My Voice Commercialization Sprint

**Audience**: a fresh Claude session triggered by the scheduled task `overnight-supervisor`. You have NO conversation context. This file IS your context. Read it fully before acting.

---

## ⚠ ARCHITECTURE CHANGE 2026-05-22 — Monitor-only mode

As of 2026-05-22 the heavy lifting moved to a continuous shell-driven loop: `plans/overnight-supervisor.sh` is started **once** by the user (`nohup bash plans/overnight-supervisor.sh > plans/overnight.log 2>&1 & disown`) and runs forever until one of the stop sentinels fires (see "North-star stop conditions" below). That shell loop is the real worker — it pulls the next `pending` issue from `plans/issue-board.md`, branches, calls `codex exec` for the file edits, runs lint/typecheck/test/banned-term scan, commits, pushes, opens the PR, polls CI, and merges.

**Your job (the scheduled Claude trigger) is now PURE MONITORING.** Do NOT pick issues or call `mcp__codex__codex` for new implementation work — that would race with the shell loop. Specifically:

1. **Liveness check**: is the shell loop still running? `ps -ef | grep -v grep | grep overnight-supervisor.sh` should show a process. If it's dead AND no STOP/MONEY sentinel exists, restart it: `cd /Users/qc/Desktop/CloudFlare && nohup bash plans/overnight-supervisor.sh > plans/overnight.log 2>&1 & disown`. Log the restart in `plans/overnight-progress.md`.
2. **Tail the log**: `tail -200 plans/overnight.log`. Surface any error pattern (repeated codex timeouts with no FS progress, ssh push failures, ci failures, banned-term hits) to `plans/blockers-log.md`.
3. **BLOCKED scan**: `grep -E "^\| [^|]+ \|[^|]+\|[^|]+\|[^|]+\| BLOCKED" plans/issue-board.md`. For each new BLOCKED row since the last trigger, add a one-line entry to `plans/blockers-log.md` explaining why and what the user (or daytime Claude) should do.
4. **Checkpoint write**: append a 4-6 line summary to `plans/overnight-progress.md` with: trigger time, loop alive (yes/no), issues done since last trigger (count + ids), new BLOCKED count, log tail anomalies, recommended user action (if any).
5. **Exit fast**: 3-5 minutes max. You are not doing implementation work in this trigger anymore.

Only override monitor-only mode if the shell loop is wedged in a way that needs structural intervention (e.g. all codex calls have failed for >60 min). In that case, write to blockers-log with the diagnosis and STOP the shell loop (`touch plans/STOP-OVERNIGHT.txt`) rather than competing with it.

---

**Your role (legacy, kept for reference)**: You are the supervisor in Cowork mode on the `/Users/qc/Desktop/CloudFlare` project. The project owner (ChuanQiao1128, TimeAwake Ltd) is asleep. They authorized autonomous overnight progress on the commercialization roadmap. Codex is the executor; you orchestrate via `mcp__codex__codex` calls. (Pre-2026-05-22 mode — the shell loop now does this directly.)

**Time budget per monitor trigger**: 3-5 minutes. Cron still fires every 25-30 min.

---

## Step 1: Establish situational awareness (≤2 min)

Read these files in this order to know what's going on:

1. `CLAUDE.md` — particularly the "Active Commercialization Sprint" section at the bottom (sprint posture + hard limits)
2. `plans/issue-board.md` — issue list with status (pending / in_progress / done / BLOCKED)
3. `plans/decisions-log.md` — what previous trigger Claudes / codex decided (create if missing)
4. `plans/blockers-log.md` — known blockers requiring user input (create if missing)
5. `plans/overnight-progress.md` — running tally of overnight progress (create if missing)
6. `git log --oneline -20` (via bash) — what's recently been committed

If `plans/issue-board.md` is missing, abort and write to `plans/blockers-log.md`: "trigger at <time>: issue board missing, cannot proceed".

## Step 2: Concurrency check (≤30s)

Check if another supervisor is currently running. Look at `plans/supervisor-lock.txt`:
- If file exists AND modified within last 22 min → ANOTHER SUPERVISOR ACTIVE. Exit immediately without making changes. Write to overnight-progress.md: "trigger at <time>: skipped, prior supervisor still active".
- If file exists but stale (>22 min) → previous supervisor died. Delete the lock and continue.
- If file does NOT exist → continue. Create it with current timestamp + your trigger time.

Format of supervisor-lock.txt: one line `<ISO timestamp> | trigger-<N> | started`

Always remove the lock at end of your run.

## Step 3: Pick the work for this trigger

**FIRST: handle any `in_progress` issues with open PRs.** Before picking new work, scan `plans/issue-board.md` for rows with status starting with `in_progress` AND a PR URL in the GitHub column. For each such issue:

1. Run `gh pr checks <PR-URL>` to see CI state.
2. If all checks green: `gh pr merge <PR-URL> --squash --delete-branch` then `gh issue close <issue-number> --comment "Implemented in PR <url>"`. Update board row to `done`. Append to decisions-log.
3. If checks pending/failed: leave the row as `in_progress`, move on.

After clearing in_progress PRs (or attempting), find the next `pending` issue. Order: lowest M-number, then lowest id-number. Skip rules:

- **Skip M0-001 through M0-005**: already done in commit `bff864b`. If still showing `pending` in board, change them to `done` first.
- **Skip BLOCKED-WAITING-USER issues**: these need user input you cannot provide. Skip to next.
- **Skip M7-001 (real-money test)**: user-only. Mark BLOCKED-WAITING-USER.
- **Skip M9-006 (npm publish)**: needs NPM_TOKEN. Mark BLOCKED-WAITING-USER.

If the next pending issue has a detailed brief at `plans/issues/<id>.md`, read it. Otherwise read the manifest entry from `plans/issue-manifest.md` or `plans/issue-manifest-additions.md`.

## Step 4: Plan the issue (≤30s)

Write a 3-5 line plan in `plans/decisions-log.md`:
```
2026-05-21T22:30:00Z | M1-001 | started | Inventory clerk refs → write plans/clerk-removal-map.md
```

## Step 5: Delegate to codex MCP — use `danger-full-access` sandbox

**Critical architectural finding (2026-05-21 evening):**

- **DO use `sandbox: "danger-full-access"`** for ALL codex MCP calls during this sprint. The default `workspace-write` protects `.git` as read-only by design and blocks network, which makes git mutation + `gh` API impossible. `danger-full-access` bypasses these restrictions cleanly. Trust is high — codex is already trusted on this user's Mac.
- **DO use `approval-policy: "never"`** to prevent prompts.
- **MCP timeouts are EXPECTED and benign.** Codex's work persists to disk even when the MCP response never returns. After every MCP call, ALWAYS verify via filesystem (`git log --oneline -3` on branch, `git status`, `grep` for expected content). Treat MCP `-32001 timeout` as "probably done, go verify" rather than "failed."
- **Keep brief tight** (~30-50 lines). Smaller briefs → faster codex → higher chance of getting an MCP response back. But length isn't strictly required since we verify via filesystem anyway.

For a typical issue, **2 codex MCP calls** are enough end-to-end. Both use `sandbox: "danger-full-access"` and `approval-policy: "never"`.

**Call 1: Branch + edits + test (~3-5 min wall, MCP likely times out — verify via FS)**
```
prompt: "On /Users/qc/Desktop/CloudFlare:
1. git checkout main && git pull --ff-only
2. git checkout -b chore/<id>
3. Read plans/issues/<id>.md OR the manifest entry (paste here if no file).
4. Implement the file changes per acceptance criteria.
5. Run npm run lint && npm run typecheck && npm run test.
6. Banned-term scan: grep -RniE 'humanizer|bypass|undetect|detector|evade' app components public lib — must be empty.
7. Report: files changed list + lint/typecheck/test results."
```
After this call (or its timeout), verify via local bash:
- `git branch --show-current` → should be `chore/<id>`
- `git diff --stat` → should show the expected file changes
- `git log --oneline -1` → should still show main's HEAD (no commit yet, that's step 2)

If branch exists + diff looks right → proceed to Call 2. If branch missing or no diff → retry Call 1 ONCE with a smaller scope, then BLOCKED if still nothing.

**Call 2: Commit + push + PR (~1-2 min wall)**
```
prompt: "On /Users/qc/Desktop/CloudFlare branch chore/<id>:
1. git add <list-of-files-from-step-7-of-call-1>
2. git commit -m '<id>: <one-line title>' -m 'Closes #<github-number>. <one-line summary>'
3. git push -u origin chore/<id>
4. gh pr create --base main --head chore/<id> --title '<id>: <title>' --body 'Closes #<number>. <summary>'
5. Report: commit SHA + push result + PR URL."
```
After this call (or its timeout), verify:
- `git log chore/<id> --oneline -1` → should show the new commit SHA
- Check PR via `gh pr list --state all --head chore/<id> --json url,state --jq '.[]'` (in YOUR Claude session, not codex)

**Call 3: Wait for CI then merge (one optional call, runs in parallel-ish)**
Only if comfortable; otherwise leave PR for the human to merge.
```
prompt: "On /Users/qc/Desktop/CloudFlare:
1. Wait for CI on PR <url>: poll `gh pr checks <url>` every 15s for up to 3 min. Report state.
2. If all green: gh pr merge <url> --squash --delete-branch && gh issue close <issue-number>.
3. If any failed/pending after 3 min: report status, leave PR open."
```

**MCP timeout handling (CRITICAL):**
The MCP transport may timeout BEFORE codex finishes. This is expected, not a failure. Always verify via local bash (your supervisor session can run `git`, `gh`, etc. read-only). If filesystem confirms work happened, treat the issue/step as DONE despite MCP timeout. If filesystem shows nothing happened, then it's a real failure.

## Step 6: Update state and exit

Before exiting, ALWAYS:

1. Update `plans/issue-board.md`: change row status (pending → in_progress → done | BLOCKED)
2. Append to `plans/decisions-log.md`: `<ISO> | <id> | <done/blocked> | <one-line summary>`
3. Append to `plans/overnight-progress.md`:
   ```
   ## Trigger at <ISO>
   - Worked: <id>
   - Outcome: done | partial | blocked
   - PR: <url> if applicable
   - Time: <minutes>
   ```
4. Remove `plans/supervisor-lock.txt`
5. Exit (do not start another issue in same trigger)

## Hard limits (from CLAUDE.md sprint section — never cross)

- **Banned terms** in user-facing copy + `lib/**`: `humanizer | bypass | undetect | detector | evade`. If grep finds any in your diff: revert + mark BLOCKED.
- **No real Stripe charges**.
- **No `npm publish`** (M9-006 stays BLOCKED-WAITING-USER).
- **Never print `.env.local` / `.dev.vars` values** in any log, commit, or comment.
- **Never modify** `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID`.
- **Eval cost cap**: DeepSeek + Sapling cumulative ≤ NZ$5 per issue, ≤ NZ$20 across entire night. Track in `plans/sleep-run-budget.md` (create if missing).
- **Azure new resources**: respect `AZURE_BUDGET_LIMIT` and `AZURE_ALLOW_PAID_RESOURCES`.

## Decision policy

When the issue brief is ambiguous, you **make a sensible call** and document in `plans/decisions-log.md`. Do NOT ask the user. Examples:
- Library choice when unspecified → pick lightest reasonable
- File naming → match repo conventions (kebab-case for files, camelCase for vars)
- Test approach → use existing test framework (vitest / xunit)

If the decision would touch real money / public APIs / external commitments → mark BLOCKED-WAITING-USER.

## North-star stop conditions — DO NOT STOP UNTIL MONEY IS IN

The goal is NOT "105 issues merged." The goal is **replyinmyvoice.com is collecting real money**. The shell loop (`plans/overnight-supervisor.sh`) keeps running until exactly one of:

1. **`plans/MONEY-MADE.txt` exists** — the user creates this file after M7-001 (real Stripe live test charge) has cleared AND at least one paying customer has flowed end-to-end. This is the actual finish line.
2. **`plans/STOP-OVERNIGHT.txt` exists** — user-initiated emergency stop.
3. **No more `pending` rows in `plans/issue-board.md`** — entire backlog is done. The shell loop exits; the next trigger should re-pick into polish/bug-fix mode (open issues with `gh issue list --state open`, prioritize the M2 quality regression + smoke any new bug reports).

The shell loop's per-process MAX_HOURS and MAX_ISSUES are set to effective infinity (720h, 10000 issues) and should not be the real stop. Those numbers exist only as a safety belt — the sentinels above are the real exit.

**Per-trigger (monitor mode) stop**: just exit after 3-5 min. You are not doing implementation work; the shell loop is. See "ARCHITECTURE CHANGE 2026-05-22" at the top of this doc.

## Emergency stop signal

If user creates `plans/STOP-OVERNIGHT.txt`, both the shell loop AND the next Claude monitor trigger abort cleanly. Remove any lock, write a final progress entry, exit. Symmetric file: `plans/MONEY-MADE.txt` — same behavior, but framed as "we won."

## When you're done with this trigger

Just exit. The next scheduled fire is in 25 min.

---

# Appendix: Codex MCP call template

```
mcp__codex__codex(
  cwd: "/Users/qc/Desktop/CloudFlare",
  sandbox: "workspace-write",
  approval-policy: "never",
  prompt: """<≤30 line atomic brief here>
  
  Hard limits: no banned terms (humanizer/bypass/undetect/detector/evade),
  no .env.local printing, no real Stripe charges.
  
  Report: exit code + 1-line summary of what changed."""
)
```

If codex MCP times out, use the codex-reply with threadId trick:
```
mcp__codex__codex-reply(
  threadId: "<id from previous call>",
  prompt: "Status check: did the previous step complete? Report git state + any files modified."
)
```
