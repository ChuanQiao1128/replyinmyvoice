# Issue M2.5-005

Title: M2.5-005 Auto-draft PR from promotable StrategyCandidate
Milestone: M2.5-Learning
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/90

## Brief

Scheduled job (M2.5-007) calls a codex MCP session with the StrategyCandidate brief to draft a PR. Codex modifies the relevant prompt/scenario file, adds a regression test, updates `docs/rewrite-strategy-memory.md`. Opens PR, NEVER auto-merges.

---
Detailed brief will be written at `plans/issues/M2.5-005.md` when this milestone starts.
Source roadmap: `plans/commercialization-roadmap.md`.

## Repository conventions
- Tests: vitest for TypeScript, xunit for .NET
- Lint: eslint via npm run lint
- Types: tsc via npm run typecheck
- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)
- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture
