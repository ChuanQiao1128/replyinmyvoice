# Repair REPAIR-20260522180011

Title: M6-001 Cloudflare secret-name diff retry
Source: plans/codex-worker-inbox.md

## Repair item

## 2026-05-22T17:59:41+12:00 — M6-001 Cloudflare secret-name diff retry

- Status: pending
- Source: shell supervisor
- Class: provider
- Priority: P2
- Related issue: M6-001
- Evidence: plans/worker-secret-diff.md
- Suggested Codex action: Retry or narrow the read-only Worker secret-name diff path without printing secret values, pushing secrets, deploying, or changing provider dashboards.
- Done condition: `plans/worker-secret-diff.md` contains a completed name-only diff, or the inbox item records a provider/DNS failure with current evidence and no user-only action hidden inside it.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes, deploys, printing `.env.local` values

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
