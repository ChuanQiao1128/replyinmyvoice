# Repair REPAIR-20260523052914

Title: M7-002 undeclared-files-in-diff
Source: plans/codex-worker-inbox.md

## Repair item

## 2026-05-23T05:28:55+12:00 — M7-002 undeclared-files-in-diff

- Status: pending
- Source: shell supervisor
- Class: dirty_repo
- Priority: P1
- Related issue: M7-002
- Evidence: plans/task-status.json
- Suggested Codex action: Inspect the preserved stash, split unrelated work into scoped branches, and restore the supervisor to clean-branch operation.
- Done condition: No PR commits files outside the Codex-declared files_changed list.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
