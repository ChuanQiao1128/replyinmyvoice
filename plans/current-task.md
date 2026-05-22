# Issue M2.5-003

Title: M2.5-003 Failure-mode clustering by diagnosis tags
Milestone: M2.5-Learning
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/86

## Brief

New `lib/learningops/cluster.ts`: group failed cases by primary diagnosis tag (`stock_opening`, `corporate_polish`, `uniform_rhythm`, etc per the AI-like cause taxonomy). For each cluster: count, exemplar case ids, common scenario, common tone. Output: `LearningFinding` table rows (new Prisma migration).

---
Detailed brief will be written at `plans/issues/M2.5-003.md` when this milestone starts.
Source roadmap: `plans/commercialization-roadmap.md`.

## Repository conventions
- Tests: vitest for TypeScript, xunit for .NET
- Lint: eslint via npm run lint
- Types: tsc via npm run typecheck
- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)
- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture
