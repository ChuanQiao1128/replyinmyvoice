# Issue M2.5-001

Title: M2.5-001 Define 100-case baseline corpus across 5 scenarios
Milestone: M2.5-Learning
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/82

## Brief

Build `docs/learning-baseline-corpus.md`: 100 representative drafts, 20 per scenario (Blank/Email/Customer support/Cover letter/Work update). Each case has: draft text, scenario, tone, expected facts to preserve, expected draft AI-like signal range. Source: 50% from `RewriteLearningSample` real failures + 50% hand-crafted edge cases. NO real user PII in committed text — fictional or stripped.

---
Detailed brief will be written at `plans/issues/M2.5-001.md` when this milestone starts.
Source roadmap: `plans/commercialization-roadmap.md`.

## Repository conventions
- Tests: vitest for TypeScript, xunit for .NET
- Lint: eslint via npm run lint
- Types: tsc via npm run typecheck
- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)
- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture
