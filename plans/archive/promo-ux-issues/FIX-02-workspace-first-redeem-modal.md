TASK: Make /app workspace-first — always show the rewrite tool; turn the redeem-code card into a button + modal; replace full-page paywalls with in-workspace nudges.

CONTEXT
- Repo: /Users/qc/Desktop/CloudFlare, branch feat/promo-ux-fix (off current main). Spec: plans/promo-ux-fix-plan.md (read it).
- BUG: app/app/page.tsx branches on selectAppExperience(...) (lib/promo-app-state.ts) and renders a FULL-PAGE <RedeemCodeCard/> for new users (0 credits, not redeemed), and full-page <PaywallCard/> when out of credits. After login the user should see the PRODUCT WORKSPACE first; redeeming must be a BUTTON on the workspace, not a page takeover.
- Relevant files: app/app/page.tsx, components/app/rewrite-workspace.tsx, components/app/subscription-status.tsx, components/app/redeem-code-card.tsx, components/app/paywall-card.tsx, lib/promo-app-state.ts. The redeem card already does POST /api/promo/redeem + a Cloudflare Turnstile widget; KEEP that logic.

CHANGES REQUIRED
1. app/app/page.tsx: ALWAYS render <RewriteWorkspace> (remove the early returns that render full-page redeem / free-paywall / paid-paywall). Pass the workspace what it needs: promo state (hasRedeemed, trialRemaining, trialExpiresAt), paid flag, usage (remaining/quota/exhausted/sources), and derived flags like `outOfCredits` and `canRedeem` (= not paid && (!hasRedeemed || trialRemaining===0)).
2. components/app/redeem-code-card.tsx: refactor so it can render INSIDE a modal/dialog (a controlled `open`/`onClose` API), reusing the existing redeem POST + Turnstile widget. On successful redemption, refresh account state (router.refresh() or refetch /api/me) so new credits appear WITHOUT a full page reload, then close the modal.
3. components/app/rewrite-workspace.tsx (+ subscription-status.tsx): 
   - Add a "Redeem code" button near the quota/status (opens the redeem modal). Show it whenever `canRedeem`.
   - When `remaining === 0`: render an inline nudge in the results/status area — "You have 0 rewrites. Redeem a trial code or buy a pack." — with the Redeem button + a <Link href="/pricing">Buy rewrites</Link>. For paid users out of monthly quota, reuse the existing paid paywall copy as the nudge (link to billing/portal).
   - The Rewrite action stays gated by the existing server quota (402); do not pre-disable the whole tool — never replace the page.
4. lib/promo-app-state.ts: repurpose selectAppExperience from a page-level gate into an in-workspace banner state (e.g. return "needsRedeem" | "needsBuy" | "ok"), or remove its page-gating use entirely. KEEP labelForQuotaSource and trialExpiryLabel unchanged.
5. Tests: update tests/unit/promo-app-state*.test.ts and tests/unit/workspace-copy*.test.ts (and any other test asserting the old full-page redeem/paywall experience) to the new workspace-first / modal model. Keep contract tests green.

ACCEPTANCE (Playwright where feasible + unit)
- New signed-in user (0 credits, not redeemed) lands on the WORKSPACE (rewrite textareas/controls visible), NOT a full-page redeem card; sees a "Redeem code" button and a 0-credits nudge.
- Clicking "Redeem code" opens a modal containing the code input + Turnstile; on a valid code the workspace credit count updates without a hard reload and the modal closes.
- Out-of-credits paid user sees an in-workspace buy/manage nudge (no full-page takeover).
- `npm run typecheck` + `npm run test` pass (updated contract tests); banned-term grep clean.

DO NOT
- No backend, EF migration, /api/promo/redeem, /api/me, or secret changes (frontend-only). Do not break the existing rewrite flow or server-side quota enforcement. No banned terms. Never push / PR / merge / deploy.
