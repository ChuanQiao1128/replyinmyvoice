# PROMO-10 — Admin UI: /admin/promo-codes (Phase 4, TIER 2)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §7). Deps: PROMO-04.

## Context
A production-grade (not throwaway) admin page to create/list/disable promo codes and view per-code stats. There is no admin page in `app/` today — this is the first one.

## Changes required
1. `app/admin/promo-codes/page.tsx` (+ supporting components / a thin `app/api/admin/promo-codes/*` proxy mirroring `app/api/me/route.ts` that forwards the admin bearer to the C# admin endpoints).
2. Features: list (with `redemptionCount` + derived status), "New code" form (code, displayCode, credits, ttlDays, validFrom/Until, caps), disable/enable toggle, per-code stats (redemptions, distinct users, activation rate, daily curve, IP-hash clusters).
3. **Production states (required, not happy-path only):** non-admin → clear 403/no-permission view; not signed in → redirect to `/sign-in?redirectTo=/admin/promo-codes`; list loading / error / empty; create duplicate code → explicit field error (not 500); create invalid numbers → field validation; disable reflects immediately.

## Acceptance (machine-checkable — Playwright)
- Non-admin → 403 view; admin can create a code; duplicate code → field error; disable → subsequent redeem fails immediately.
- Stats render IP-hash clusters only — NO raw IPs anywhere in the DOM/network.
- Desktop + mobile screenshots clean; `npm run test`/`typecheck` green.

## Constraints / Do NOT
- Admin gating server-side (never client-only). Never render raw IPs. No banned terms. No push/PR/deploy.
