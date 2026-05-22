# Issue M2.5-008

Title: M2.5-008 Promotion approval UX in /admin/learning
Milestone: M2.5-Learning
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/96

## Brief

`app/admin/learning/page.tsx`: list of recent `LearningRun` rows with status, finding counts, PR links if any. Per-finding view: cluster details, evidence cases, proposed candidate. Admin can mark candidate as `approved` / `needs_revision` / `rejected`. Updates `StrategyCandidate.status` in DB.

---
Detailed brief will be written at `plans/issues/M2.5-008.md` when this milestone starts.
Source roadmap: `plans/commercialization-roadmap.md`.

## Repository conventions
- Tests: vitest for TypeScript, xunit for .NET
- Lint: eslint via npm run lint
- Types: tsc via npm run typecheck
- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)
- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture
