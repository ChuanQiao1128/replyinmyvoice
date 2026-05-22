# M4-012 Landing/Header/Footer Design Brief

Date: 2026-05-22

## Scope

Refresh only the public landing page, shared header, and shared footer for Reply In My Voice. The work preserves the product positioning line:

```text
Replies that still sound like you.
```

This pass does not change auth, billing, rewrite, quota, API, telemetry, webhook, provider, secret, or infrastructure behavior.

## Design Decisions

- Palette: keep the existing ink, paper, clay, sage, and gold identity, then add mint and sky section bands so the page no longer reads as one warm beige surface.
- Typography: keep the repo's system sans stack and existing hierarchy. Tighten the hero from oversized marketing scale to a confident product page scale that works beside the workflow demo.
- Layout: keep a max-width 6xl grid, shorten the hero so the next section can appear sooner, and use alternating full-width bands for trust, use cases, how-it-works, pricing, FAQ, CTA, and footer rhythm.
- Shape and surfaces: keep every card/panel at `rounded-lg` or lower, which maps to 8px or less. Remove the nested pricing card structure and keep repeated use-case/trust/step items as standalone cards.
- Motion: use only existing hover/focus transitions. No new animation framework or heavy motion layer.
- Assets: treat the interactive reply workflow demo as the primary product visual. No abstract stock imagery, decorative blobs, or fabricated screenshots were added.
- Navigation: keep existing routes and auth/session behavior. The header now reserves space better on mobile by shortening the signed-out CTA to "Start" below the small breakpoint.

## Draft PR 200 Handling

The task allowed reuse of draft PR #200 only after review. This Codex invocation was explicitly forbidden from using `git` or `gh`; public lookup did not expose reviewable PR content. No draft branch was merged or copied wholesale. The refresh was implemented from the current checked-out source and the M4-012 brief.

## Files In Scope

- `components/site-header.tsx`
- `components/site-footer.tsx`
- `components/landing/hero.tsx`
- `components/landing/interactive-demo.tsx`
- `components/landing/trust-panel.tsx`
- `components/landing/use-cases.tsx`
- `components/landing/how-it-works.tsx`
- `components/landing/pricing.tsx`
- `components/landing/faq.tsx`
- `components/landing/closing-cta.tsx`
- `tailwind.config.ts`

## Final Self-Critique

- Philosophy alignment: 8/10. The page now feels more like a practical reply workflow product than a generic AI landing page, while keeping the core positioning intact.
- Visual hierarchy: 8/10. The hero leads with the product promise, then the workflow demo and trust proof points carry the next read. The shorter hero improves first-viewport rhythm.
- Craft quality: 8/10. Section bands, shadows, and surface weights are more consistent, and the pricing area no longer nests cards. Browser verification caught and fixed a mobile horizontal-overflow issue in the hero/workflow grid.
- Functionality: 8/10. Existing links, CTAs, sample switching, and product claims are preserved. No app, billing, auth, or provider logic changed.
- Originality: 7/10. The page uses a custom workflow preview and restrained product palette. It is intentionally conservative for launch readiness rather than highly experimental.

## Browser Verification

- Local route: `http://localhost:3001/`
- Desktop viewport: 1440x1100, HTTP 200, no console errors, no failed requests, no horizontal overflow.
- Mobile viewport: 390x1000, HTTP 200, no console errors, no failed requests, no horizontal overflow after the hero/workflow grid fix.
- Screenshot artifacts during the run: `/tmp/m4-012-desktop-fixed.png` and `/tmp/m4-012-mobile-fixed.png`.
