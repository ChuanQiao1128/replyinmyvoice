# Repair REPAIR-20260523045804

Title: INV-2: repair branch active while M8-001 in_progress on board
Source: plans/codex-worker-inbox.md

## Repair item

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

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
