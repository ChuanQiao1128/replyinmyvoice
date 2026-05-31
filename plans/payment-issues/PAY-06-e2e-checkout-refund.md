# PAY-06: Playwright e2e — checkout→quota and refund→clawback (Stripe test mode)

**Priority:** P1 · **Owner:** Codex · **Skill:** ui-browser-testing · **Depends on:** PAY-05 (nice-to-have, not blocking)

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Frontend: `app/pricing/page.tsx`, `components/landing/buy-button.tsx` (POSTs `{sku}` to `/api/stripe/checkout`, redirects to `payload.url`, 401 → `/sign-in?redirectTo=/pricing`). Account balance shown via `/api/me` / `components/app/subscription-status.tsx`.
- Existing e2e: `tests/e2e/commercial-site.spec.ts` (only `/` + `/privacy`), `auth-*.spec.ts`. Config: `playwright.config.ts`.
- **Gap:** the full purchase→grant→balance and refund→clawback path is never exercised automatically (SO-060 was deferred, never created).
- This is the maps-to-owner-confidence test so a real human test (PAY-11) is low-risk.

## Constraints (AGENTS.md)
- Banned terms: `humanizer|bypass|undetect|detector|evade`.
- **Stripe TEST mode only — never live.** Automation must NEVER initiate a real charge.
- Do NOT push to `main`.

## Changes required
1. Add `tests/e2e/payment-flow.spec.ts` exercising, against the local dev stack in **Stripe test mode**:
   - **Checkout happy path:** signed-in test user → `/pricing` → click a pack (Quick) → assert redirect to a `checkout.stripe.com` URL (or a mocked checkout route). To complete the loop without a live charge, deliver a **signed test webhook** `checkout.session.completed` to `/api/stripe/webhook` (use the test-mode signing secret, or POST directly to the Functions backend) → assert `/app` balance increments by 10.
   - **Refund clawback:** trigger a `charge.refunded` (admin refund API in test mode, or a signed test webhook) → assert the balance claws back correctly.
   - **401 redirect:** anonymous user clicks buy → redirected to `/sign-in?redirectTo=/pricing`.
2. Prefer signed-test-webhook delivery over depending on the external Stripe CLI, so the test is hermetic in CI. Document any required test env (key NAMES only) in the spec header.
3. Wire the spec into `playwright.config.ts` projects if needed; keep it skippable when test Stripe keys are absent (so CI without secrets still passes).

## Acceptance (machine-checkable)
- [ ] `tests/e2e/payment-flow.spec.ts` exists and passes locally in test mode: checkout→balance+10, refund→clawback, anon→sign-in redirect.
- [ ] The test makes NO live Stripe charge (uses test mode / signed test webhooks).
- [ ] Spec is gracefully skipped (not failed) when test Stripe config is absent.

## Do NOT
- Do NOT use live Stripe keys or initiate a real charge.
- Do NOT touch `main` / deploy.
