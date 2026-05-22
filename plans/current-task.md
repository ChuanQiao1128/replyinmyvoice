# Repair REPAIR-20260523111837

Title: INV-1: Finder-duplicate files outside M7-008 task scope
Source: plans/codex-worker-inbox.md

## Repair item

## 2026-05-22T18:08:57Z — INV-1: Finder-duplicate files outside M7-008 task scope

- Status: pending
- Source: Claude monitor
- Class: dirty_repo
- Priority: P1
- Related issue: M7-008
- Evidence: `git status --porcelain` on branch chore/M7-008 shows five untracked files with macOS space-numbered names — `plans/task-status 2.json`, `plans/task-status 3.json`, `plans/task-status 4.json`, `plans/m6-validation-report 2.md`, `plans/m6-validation-report 3.md` — none of which are in M7-008 scope (KPI report script). task-status.json is also deleted (D) in the worktree.
- Suggested Codex action: Delete the five space-named duplicate files (`git rm --cached` + filesystem delete) and commit the cleanup on chore/M7-008 or a separate chore branch. Confirm contents match their canonical originals before deleting.
- Done condition: `git status --porcelain` no longer shows the five space-named files as untracked; no data is lost (canonical originals remain).
- Forbidden actions: live money, npm publish, dashboard changes, secret changes

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
