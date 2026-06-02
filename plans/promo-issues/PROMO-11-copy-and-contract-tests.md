# PROMO-11 — Copy changes + contract-test updates (Phase 4, TIER 2)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read the copy/interaction section + §4 grounding). Deps: PROMO-08.

## Context
De-emphasize "free", emphasize "redeem a trial code". A family of source-string contract tests pins this copy and GATES the prod deploy (`npm run test` must pass for `cf:deploy`), so they MUST be updated in lockstep.

## Changes required
1. Update copy (exact strings owner-tunable; keep banned-term-free and brand-aligned "reply in my voice"):
   - `components/landing/hero.tsx` (the "3 free" stat), `components/landing/closing-cta.tsx`, `components/landing/pricing-v2.tsx`, `app/pricing/page.tsx`, `components/site-footer.tsx`, `app/developers/page.tsx`, `components/auth/google-oauth-card.tsx` sign-up highlights, `app/app/page.tsx` quota label.
2. Update the contract tests to the new strings: `tests/unit/pricing-auth-visual-system.test.ts` and `tests/unit/workspace-copy.test.ts` (and any other test asserting the old "3 free"/free-tier strings).

## Acceptance (machine-checkable)
- `npm run test` green (updated contract tests pass; no stale "3 free"/"free tier" assertions left dangling).
- `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` → no user-facing hits.
- `npm run typecheck` green.

## Constraints / Do NOT
- Do NOT leave any contract test asserting removed copy (would block deploy). No banned terms. No push/PR/deploy.
