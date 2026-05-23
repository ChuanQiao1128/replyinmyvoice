# Autonomous Overnight Run Charter — Product Pivot

- **Date:** 2026-05-24 · **Authorized by:** owner (chat) · **Supervisor:** Claude Code (planner) → Codex (implementer)
- **Source of truth for WHAT to build:** `plans/product-redesign-prd.md` (v0.2)
- **Scope (owner choice):** Phase 0 → Phase 1 → Phase 2 (as far as safely possible).
- **Merge policy (owner choice):** auto-merge to `main` on green checks.

## Workspace & collision rule
- Work in the `feat/students-v2` worktree (`/Users/qc/Desktop/CloudFlare-funnel`) + child branches.
- Merge to `main` via `gh pr merge` (remote) only — never `git checkout main` in this worktree, so the working tree stays on `feat/students-v2` and does not collide with the overnight supervisor operating on the main checkout.
- First merge lands the whole v2 redesign on main; CI **skips deploy**, so main-merge ≠ prod.

## Hard gates (do NOT cross unattended)
1. **Live Stripe Price/Product creation → GATED.** Build + verify in Stripe **TEST mode**; queue final numbers for owner morning confirm. (Unless owner replies with locked numbers.)
2. No real Stripe charges (first live txn is owner's).
3. No edits to `STRIPE_PRICE_ID`, `STRIPE_WEBHOOK_SECRET`, `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`.
4. No prod deploy (`cf:deploy`, DNS, custom-domain). No `npm publish`.
5. Never print/commit secrets. Banned-term grep (`humanizer|bypass|undetect|detector|evade`) must stay clean. Provider/eval spend ≤ NZ$20.
6. If "keep main clean" is received → switch to PR-only (no auto-merge to main).

## Self-fix loop (per chunk)
1. Send Codex a self-contained brief (sandbox: workspace-write for code/copy; full-access only for prisma migrate / Stripe TEST / gh).
2. Codex runs autonomously (approval=never), returns diff.
3. Review vs PRD + gates. Validate: `npm run lint && npm run typecheck && npm run test && npm run build` + banned-grep.
4. Green → commit; auto-merge to main per policy. Red → corrected brief, retry ≤2×, then mark `blocked` and move on.
5. Append outcome to `plans/decisions-log.md`; keep running report in `plans/overnight-run-report.md`.

## Phase briefs (outline)
- **Phase 0 — copy/repositioning (no Stripe/schema):** reply-decision positioning across hero/use-cases/trust/closing/faq; delete dead `components/landing/pricing.tsx`; `/students` H1 → "Write the message you're nervous to send." + boundary line + free-quota value-anchor; user-facing "Naturalness Check" → "Tone check" (keep internal `writing-signal` names); leave price NUMBERS unchanged this phase. Validate + merge.
- **Phase 1 — revenue core (no live Stripe):** Prisma `RewriteCredit` + `Referral` + `User.planTier/referralCode` + migration; quota service → tier + credit ledger + reservation; tier-from-priceId map (env placeholders); checkout supports subscription + one-time; webhook grants credits idempotently (TEST mode); split `/pricing` (monthly vs one-time) + situational post-copy paywall + per-source quota meter. Verify in Stripe TEST. Live price creation = GATED queue.
- **Phase 2 — funnel & decision layer:** scenario-first entry + micro-flows; progressive 2+1 input; output redesign (Ready-to-send + Facts-preserved primary; Why/Tone/Before-you-send secondary); task-first onboarding. Verify with Playwright/preview.

## Morning report
`plans/overnight-run-report.md`: what merged, what's blocked, the queued Stripe numbers for confirm, and any decisions needing owner input.
