# Issue M2.5-010

Title: M2.5-010 Strategy rollback on regression
Milestone: M2.5-Learning
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/100

## Brief

Add persisted rollback for promoted rewrite strategy canaries. If the active canary strategy regresses against control over the configured rolling window, record an unresolved rollback, force request-time canary traffic to 0, and optionally alert an admin email plus a GitHub follow-up issue. Keep all outbound alert failures non-fatal once rollback is persisted.

---
Detailed brief will be written at `plans/issues/M2.5-010.md` when this milestone starts.
Source roadmap: `plans/commercialization-roadmap.md`.

## Repository conventions
- Tests: vitest for TypeScript, xunit for .NET
- Lint: eslint via npm run lint
- Types: tsc via npm run typecheck
- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)
- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture
