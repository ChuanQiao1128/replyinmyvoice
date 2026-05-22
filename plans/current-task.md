# Issue M2.5-004

Title: M2.5-004 Strategy candidate generator: cluster → prompt patch
Milestone: M2.5-Learning
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/88

## Brief

`lib/learningops/candidates.ts`: takes a `LearningFinding` and proposes a targeted prompt/strategy patch. Patches are STRUCTURED (e.g. "add to repair prompt for customer_support scenario: 'avoid balanced 4-paragraph structure'"). Each candidate has risk level, required regression test, evidence count. New `StrategyCandidate` table.

---
Detailed brief will be written at `plans/issues/M2.5-004.md` when this milestone starts.
Source roadmap: `plans/commercialization-roadmap.md`.

## Repository conventions
- Tests: vitest for TypeScript, xunit for .NET
- Lint: eslint via npm run lint
- Types: tsc via npm run typecheck
- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)
- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture
