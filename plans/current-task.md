# Repair REPAIR-20260523054614

Title: .claude/ untracked directory not in .gitignore
Source: plans/codex-worker-inbox.md

## Repair item

## 2026-05-22T17:39:37Z — .claude/ untracked directory not in .gitignore

- Status: pending
- Source: Claude monitor
- Class: docs
- Priority: P3
- Related issue: M7-003 (current active task on branch chore/M7-003)
- Evidence: `git status --porcelain` shows `?? .claude/` in dirty worktree; no `.claude` entry in `.gitignore`
- Suggested Codex action: add `.claude/` to `.gitignore` if the directory contains only local tool state (confirm contents first); or commit intentionally if it should be tracked
- Done condition: `git status --porcelain` no longer shows `.claude/` as untracked, OR `.claude/` appears in `.gitignore`
- Forbidden actions: live money, npm publish, dashboard changes, secret changes

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
