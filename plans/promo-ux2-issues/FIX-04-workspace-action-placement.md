TASK: Move the redeem/buy actions out of the rewrite OUTPUT box into the top status bar; keep the output area clean.

CONTEXT
- Repo: /Users/qc/Desktop/CloudFlare, branch feat/promo-ux-fix-2 (off current main). Spec: plans/promo-ux-fix-2-plan.md.
- BUG (design): `components/app/rewrite-workspace.tsx` (~L282-312) renders an "out of credits" box INSIDE the right-hand output column ("IN YOUR VOICE") with `Redeem code` / `Buy rewrites` / `Manage billing` buttons. Account-level actions don't belong in the output/results area.
- `components/app/subscription-status.tsx` (top status bar) already renders `Redeem code` (when `canRedeem`) and a button labelled `Upgrade` (free) / `Manage billing` (paid) that calls `openCheckout()` / `openBillingPortal()`.

CHANGES REQUIRED
1. `components/app/subscription-status.tsx`: for FREE users rename the `Upgrade` button label to **`Buy rewrites`** (keep the same `openCheckout()` action + icon). Keep `Redeem code` (when `canRedeem`) and the paid `Manage billing`. Net: top bar = `Redeem code` + `Buy rewrites` (free) / `Manage billing` (paid).
2. `components/app/rewrite-workspace.tsx`: in the out-of-credits sub-component (the box shown in the output column), REMOVE the `Redeem code` / `Buy rewrites` / `Manage billing` buttons. Keep a short, button-less hint line (e.g. "You're out of rewrites — use Redeem code or Buy rewrites in the bar above."). Do not render account-action buttons in the output column. The Rewrite action stays gated by the server 402.
3. Update any unit/contract test (e.g. tests/unit/workspace-copy*.test.ts) that asserts the old output-box buttons or the `Upgrade` label, to match the new layout/labels. Keep contract tests green.

ACCEPTANCE (Playwright where feasible + unit)
- Out-of-credits user: the TOP bar shows `Redeem code` + `Buy rewrites`; the output column shows only a text hint (NO Redeem/Buy/Manage buttons).
- `Buy rewrites` in the top bar opens checkout (same as the old `Upgrade`).
- `npm run typecheck` + `npm run test` green; banned-term grep clean.

DO NOT
- No backend change. Don't break the redeem modal trigger (`Redeem code` still opens the modal) or quota enforcement. No banned terms. Never push/PR/merge/deploy.
