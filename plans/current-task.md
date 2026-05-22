# Issue M4-013

Title: M4-013 Pricing and auth visual alignment
GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/203
Milestone: M4-Landing

## Task

Split from M4-011. Use `/Users/qc/.codex/skills/web-design-engineer/SKILL.md` and `agent-skills/ui-browser-testing/SKILL.md`.

Align `/pricing`, `/sign-in`, and `/sign-up` with the refreshed visual system from M4-012. Keep the work scoped to pricing/auth UI and shared components required by those routes.

## Constraints

- Preserve Clerk/auth and Stripe/billing behavior.
- Do not change provider secrets, dashboard state, infrastructure, quota logic, rewrite logic, telemetry, or webhook behavior.
- Reuse M4-012 tokens/layout direction instead of introducing a competing palette.
- Keep button, input, and auth-card states accessible and stable on mobile.

## Verification

- `npm run lint`
- `npm run typecheck`
- `npm run test`
- Browser verify `/pricing`, `/sign-in`, and `/sign-up` where available at desktop and mobile sizes.
- Verify expected signed-out/auth redirects still behave correctly.
