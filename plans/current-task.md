# Issue M2.5-009

Title: M2.5-009 Canary deploy for new strategy
Milestone: M2.5-Learning
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/98

## Brief

When a StrategyCandidate is promoted to production via merge, use a feature flag (env or KV) to route N% (default 10%) of rewrite traffic to the new strategy. After 24h or 200 rewrites, compare signal-change distributions: if new strategy is worse, auto-disable flag; if better, gradually ramp.

---
Detailed brief will be written at `plans/issues/M2.5-009.md` when this milestone starts.
Source roadmap: `plans/commercialization-roadmap.md`.

## Repository conventions
- Tests: vitest for TypeScript, xunit for .NET
- Lint: eslint via npm run lint
- Types: tsc via npm run typecheck
- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)
- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture
