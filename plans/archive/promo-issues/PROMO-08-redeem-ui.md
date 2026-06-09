# PROMO-08 — Redeem UI: card + /app empty-state branching + Turnstile widget (Phase 4, TIER 2)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §13, copy in the spec's copy section). Deps: PROMO-07, PROMO-06.

## Context
After free baseline = 0, a signed-in user with no redemption must see a "Redeem your code" card (NOT the buy-paywall). After redeeming, the workspace shows trial credits.

## Changes required
1. `components/app/redeem-code-card.tsx` — code input + Cloudflare Turnstile widget (`NEXT_PUBLIC_TURNSTILE_SITE_KEY`; dev = test site key) + Redeem button; calls `/api/promo/redeem`; inline states: success ("3 rewrites unlocked — expire in N days"), `invalid_code`, `code_expired`, `already_redeemed`, `code_exhausted`, `ip_velocity`.
2. `app/app/page.tsx` — branch the empty state on the `/api/me` `promo` block: `!hasRedeemed && remaining==0` → redeem card; `remaining>0` → workspace; `hasRedeemed && remaining==0` → existing paywall. Map PROMO source → "Trial rewrites" label in the quota line.
3. On success, re-fetch `/api/me` (no full reload).

## Acceptance (machine-checkable — Playwright)
- New signed-in user sees the redeem card, not the paywall.
- Redeem success → workspace shows "N of 3 trial rewrites" + expiry; invalid/expired/already show inline errors.
- Exhausted trial → buy-paywall.
- Mobile viewport: no overflow; Turnstile widget renders.
- `npm run test`/`typecheck` green.

## Constraints / Do NOT
- Do NOT print the universal code anywhere in the UI (intent filter). No banned terms. No push/PR/deploy.
