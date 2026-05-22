# Issue M4-014

Title: M4-014 App workspace visual polish
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/204
Milestone: M4-Landing

## Task

Split from M4-011. Use `/Users/qc/.codex/skills/web-design-engineer/SKILL.md` and `agent-skills/ui-browser-testing/SKILL.md`.

Polish the `/app` workspace shell only: layout density, input panels, quota/paywall/status presentation, and interaction states. The app should feel like a repeated-use writing workspace, not a marketing page.

## Constraints

- Preserve rewrite, quota, billing, API, telemetry, webhook, and auth behavior.
- Do not change provider secrets, dashboard state, infrastructure, or pricing.
- Reuse M4-012 tokens/layout direction.
- Keep textareas, counters, buttons, paywall/status cards, and output areas stable on mobile.

## Verification

- `npm run lint`
- `npm run typecheck`
- `npm run test`
- Browser verify signed-out `/app` redirect and any locally available signed-in preview state.
- Inspect console/network errors and responsive layout.
