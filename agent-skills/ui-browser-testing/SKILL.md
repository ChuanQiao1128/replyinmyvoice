---
name: ui-browser-testing
description: Use when adding, changing, reviewing, or debugging frontend UI, Playwright tests, browser flows, screenshots, responsive layout, visual regressions, auth redirects, form behavior, console errors, network errors, or local webpage verification.
---

# UI Browser Testing

Use this skill to verify frontend behavior in a real browser, not only by reading code. It covers Playwright E2E tests, Codex Browser checks, screenshots, responsive layout, and visual review.

## When To Use

Use for changes to:

- landing pages, `/app`, pricing, auth pages, admin pages, forms, buttons, navigation, copy actions, local history, paywalls, or loading/error states
- Playwright tests or `tests/e2e/**`
- responsive layout, mobile behavior, text overflow, clipping, overlap, visual spacing, or screenshot review
- Clerk redirects, signed-out gates, same-origin API calls, checkout/portal buttons, and browser-visible error states

Do not use this skill for backend-only EF Core, quota, webhook, worker, or xUnit changes. Use `dotnet-backend-testing` for those. Use both skills when an API/backend change also changes browser-visible UI behavior.

## Workflow

1. Identify the user-visible flow and expected result.
2. Choose verification:
   - focused Playwright test for repeatable browser behavior
   - Codex Browser manual check for visual or interactive verification
   - screenshots for layout, responsive, or visual review
3. Start the local dev server when the app requires one.
4. Check desktop and mobile sizes when layout can change.
5. Inspect console and network failures when the page loads or actions run.
6. Review screenshots for overlap, clipped text, broken spacing, blank content, wrong redirects, and unreadable controls.
7. Run the focused command first, then the broader E2E suite when risk warrants it.

## What To Test

| Area | Required checks |
| --- | --- |
| navigation/auth | expected redirects, signed-out gates, no broken links |
| forms | validation, disabled/loading state, submit result, error state |
| rewrite workspace | input limits, rewrite button, output, copy, try again, local history, clear history |
| billing UI | usage text, paywall, upgrade/manage billing buttons, safe failure states |
| responsive layout | desktop and mobile, no overlap, no horizontal scroll, readable controls |
| visual quality | text fits, buttons have stable size, cards/panels align, no blank regions |
| browser health | no unexpected console errors or failed network requests |

## Commands

```bash
npm run test:e2e
npx playwright test tests/e2e/<file>.spec.ts
npx playwright test --project=chromium
```

Use `npm test -- <unit test>` for React/TypeScript unit tests only. It does not replace browser verification for UI behavior.

## Screenshot Review Rules

- Capture at least one desktop viewport and one mobile viewport for visual/layout changes.
- Do not rely on a screenshot existing; inspect it.
- Look for text overflow, clipped labels, overlapping controls, blank panels, bad contrast, broken spacing, and hidden primary actions.
- If a local page is visually changed, use Codex Browser or Playwright screenshots before claiming the UI is ready.

## Resume Evidence Rule

Only claim tools actually present in this repo:

```text
Playwright, browser E2E tests, responsive screenshots, GitHub Actions frontend checks
```

Do not claim Percy, Chromatic, Cypress, visual-diff baselines, or browser-stack style cross-browser labs unless real artifacts and verification are added.
