# PROMO-12 — End-to-end + Worker-preview smoke + launch checklist (Phase 4, TIER 2)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §15, §16, §18). Deps: ALL prior (PROMO-01..11).

## Context
Final verification before the (separately-scripted) auto-deploy. Prove the whole loop and lock the 5 launch-gating checkpoints. Deploy itself is NOT done in this issue (handled by the wave's deploy stage).

## Changes required
1. Playwright e2e (against local/preview with Cloudflare Turnstile TEST keys): signup (Turnstile + disposable-email reject) → login → redeem (test code) → 3 trial rewrites → consume → paywall; admin page create/disable/stats.
2. A scripted **Worker-preview smoke** (opennextjs-cloudflare preview) hitting `/`, `/pricing`, `/sign-in`, `/app`, `/api/promo/redeem` (invalid/expired/already), and confirming IP forwarding + Turnstile verify work end-to-end on preview.
3. A `plans/promo-launch-checklist.md` mapping each of the 5 launch-gating checkpoints (free-baseline cutover, global-cap race, proxy trusted-IP fail-closed, Turnstile env, admin auth/audit) to the test(s) that lock it, all green.

## Acceptance (machine-checkable)
- `dotnet test` + `npm run test` + `npm run typecheck` all green; banned-term grep clean.
- Playwright e2e suite passes; preview smoke script exits 0.
- The 5 checkpoints each reference a passing test in the checklist.

## Constraints / Do NOT
- Preview uses Turnstile TEST keys (the real key is domain-locked to replyinmyvoice.com). 
- Do NOT deploy or merge to main from this issue — the wave's deploy stage handles cutover. No banned terms. No push/PR/deploy.
