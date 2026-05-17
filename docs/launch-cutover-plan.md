# Launch Cutover And Quality Plan

Date: 2026-05-18

## Goal

Move ReplyInMyVoice from the verified `workers.dev` MVP to a `replyinmyvoice.com` launch candidate, while keeping Stripe in sandbox mode and improving the core Naturalness Check results until the internal quality target is met.

## Required Reading For The Next Long Run

Read these files before executing:

- `/Users/qc/Desktop/CloudFlare/AGENTS.md`
- `/Users/qc/Desktop/CloudFlare/replyinmyvoice_requirements.md`
- `/Users/qc/Desktop/CloudFlare/docs/preflight-report.md`
- `/Users/qc/Desktop/CloudFlare/docs/manual-setup.md`
- `/Users/qc/Desktop/CloudFlare/docs/optimization-notes.md`
- `/Users/qc/Desktop/CloudFlare/docs/launch-cutover-plan.md`
- `/Users/qc/Desktop/CloudFlare/package.json`
- `/Users/qc/Desktop/CloudFlare/wrangler.jsonc`
- `/Users/qc/Desktop/CloudFlare/prisma/schema.prisma`

`.env.local` may be inspected only for variable names and presence. Do not print secret values.

## Hard Constraints

- Work only in `/Users/qc/Desktop/CloudFlare`.
- `LAUNCH_CONFIRMED=true` means formal domain cutover is authorized for this phase.
- Stripe must stay in sandbox mode. Do not request or switch to live Stripe keys or live prices.
- Do not delete the existing Cloudflare Pages project.
- Do not remove the ability to roll back to the current holding page.
- Keep using the independent Worker name `replyinmyvoice-app`.
- Commit and push after each completed phase.
- If a dashboard-only action blocks progress, document it in `docs/manual-setup.md` and continue with everything else that can be completed.
- Never print, summarize, commit, or expose secret values.

## Phase 1: Launch Preflight

Run a fresh preflight before changing deployment state.

Required checks:

- Confirm current branch and git status.
- Confirm GitHub remote and push access.
- Confirm `LAUNCH_CONFIRMED=true` is present.
- Confirm Cloudflare token still supports Worker, Zone, and DNS/custom-domain operations.
- Confirm current Worker URL is reachable.
- Confirm `.gitignore` still protects secret and build output paths.
- Run:
  - `npm run lint`
  - `npm run typecheck`
  - `npm run test`
  - `npm run test:e2e`
  - `npm run build`
  - `npm run cf:build`
- Run Worker smoke against `workers.dev`.
- Update `docs/preflight-report.md`.

Commit message:

```bash
git commit -m "docs: update launch preflight"
```

## Phase 2: Clerk And Stripe Dashboard Configuration Check

Check everything that can be checked through local files or APIs.

Clerk expected origins and redirects:

- `https://replyinmyvoice.com`
- `https://replyinmyvoice-app.qc1128qc.workers.dev`
- `/sign-in`
- `/sign-up`
- `/app`

Stripe remains sandbox.

Stripe webhook events that code supports:

- `checkout.session.completed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`
- `invoice.paid`
- `invoice.payment_failed`

If the dashboard is missing invoice events or domain/origin settings, write the exact manual steps to `docs/manual-setup.md`.

Commit message:

```bash
git commit -m "docs: record dashboard launch checks"
```

## Phase 3: Formal Domain Cutover

Cutover is authorized only because `LAUNCH_CONFIRMED=true`.

Execution order:

1. Deploy the latest app to `replyinmyvoice-app`.
2. Smoke test `https://replyinmyvoice-app.qc1128qc.workers.dev`.
3. Attach or route `replyinmyvoice.com` to the verified Worker.
4. Do not delete the old Pages project.
5. Preserve rollback instructions in `docs/manual-setup.md`.

Required formal-domain smoke:

- `https://replyinmyvoice.com/` returns 200.
- `https://replyinmyvoice.com/pricing` returns 200.
- `https://replyinmyvoice.com/sign-in` returns 200.
- Signed-out `/app` redirects to sign-in.
- Unauthenticated `POST /api/rewrite` returns 401.
- `GET /api/stripe/webhook` returns 200.
- `GET /api/health/db` returns 200.

Commit message:

```bash
git commit -m "docs: record domain cutover"
```

## Phase 4: Real Account Full-Path Test

Use a real test account. Do not print credentials.

Required flow:

- Register a new test user.
- Sign in.
- Open `/app`.
- Complete one rewrite.
- Confirm usage is counted after the successful rewrite.
- Use the three free lifetime rewrites.
- Confirm the fourth rewrite is blocked with paywall behavior.
- Start Stripe sandbox checkout.
- Complete sandbox checkout.
- Confirm webhook updates subscription state to active or trialing.
- Confirm paid quota changes to 100 for the billing period.
- Confirm a paid rewrite succeeds.

Record results in `docs/preflight-report.md`.

If a dashboard-only setting blocks the flow, document it in `docs/manual-setup.md`.

Commit message:

```bash
git commit -m "docs: record account flow verification"
```

## Phase 5: Naturalness Optimization To Target

Internal target:

- Average AI-like signal reduction of at least 30 points.
- Most evaluated samples rewrite to below 50%.

Execution approach:

- Keep user-facing language as `Naturalness Check`, `writing signal`, and `AI-like signal`.
- Use representative teacher, sales, client, and workplace email samples.
- Analyze failures by sample type.
- Try prompt and strategy changes until the target is met.
- Keep production cost bounded per request:
  - 1 draft writing-signal call
  - up to 2 OpenAI rewrite attempts
  - up to 2 rewrite writing-signal calls
- Save measured rounds and final strategy in `docs/optimization-notes.md`.
- When the target is met, commit the production strategy.

Commit message:

```bash
git commit -m "feat: improve naturalness strategy"
```

## Phase 6: GitHub And Final Verification

Run final verification:

- `npm run lint`
- `npm run typecheck`
- `npm run test`
- `npm run test:e2e`
- `npm run build`
- `npm run cf:build`
- Banned-term scan over `app`, `components`, `public`, and source `lib` paths.
- Worker smoke.
- Formal-domain smoke.

Then:

- Commit any final docs.
- Push `codex/autonomous-mvp`.
- Report latest commit hash.
- Confirm whether branch should be merged to `main` or opened as a PR.

## Done Criteria

This phase is complete only when:

- `replyinmyvoice.com` serves the new app.
- The existing Pages project is preserved for rollback.
- Clerk sign-in/sign-up works on the formal domain.
- Stripe sandbox checkout and webhook flow works.
- Free and paid quota behavior is verified with a real test account.
- Naturalness internal target is met and documented.
- GitHub branch is clean and pushed.
