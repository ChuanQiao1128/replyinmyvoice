# Manual Setup

These steps are dashboard-only or final-cutover tasks. They should not block local development.

## Clerk

- Add `https://replyinmyvoice.com` as an allowed production origin.
- Add the deployed Worker preview URL after deployment if Clerk requires it for testing.
- Confirm redirect URLs:
  - `/sign-in`
  - `/sign-up`
  - `/app`
- Launch check on 2026-05-18: Clerk API was reachable, but `replyinmyvoice.com` was not observed in the `/domains` API response. Verify the formal domain/origin in the Clerk dashboard before or during real-account testing.
- Clerk DNS verification records were added in Cloudflare on 2026-05-21:
  - `clerk.replyinmyvoice.com -> frontend-api.clerk.services`
  - `accounts.replyinmyvoice.com -> accounts.clerk.services`
  - `clkmail.replyinmyvoice.com -> mail.4rlognk6y6o0.clerk.services`
  - `clk._domainkey.replyinmyvoice.com -> dkim1.4rlognk6y6o0.clerk.services`
  - `clk2._domainkey.replyinmyvoice.com -> dkim2.4rlognk6y6o0.clerk.services`
- These records must stay DNS-only in Cloudflare. If Clerk still shows them as unverified, run verification again in the Clerk dashboard after DNS propagation.

## Stripe

- Current sandbox price should verify as `unit_amount=900`, `currency=nzd`, `interval=month`.
- User-facing copy should display `NZD $9/month`.
- Webhook endpoint currently points to:
  - `https://replyinmyvoice.com/api/stripe/webhook`
- Required events implemented in code:
  - `checkout.session.completed`
  - `customer.subscription.created`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
- Additional events implemented in code and configured in the Stripe sandbox webhook on 2026-05-18:
  - `invoice.paid`
  - `invoice.payment_failed`
- Before live mode, create or confirm a live recurring price and update `STRIPE_PRICE_ID`.

## Cloudflare Variables

Set production runtime variables/secrets for the Worker:

- `NEXT_PUBLIC_APP_URL`
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`
- `CLERK_SECRET_KEY`
- `NEXT_PUBLIC_CLERK_SIGN_IN_URL`
- `NEXT_PUBLIC_CLERK_SIGN_UP_URL`
- `NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL`
- `NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL`
- `DATABASE_URL`
- `DIRECT_URL`
- `OPENAI_API_KEY`
- `OPENAI_MODEL`
- `OPENAI_MODEL_PRIMARY`
- `OPENAI_MODEL_REPAIR`
- `OPENAI_MODEL_ESCALATION`
- `OPENAI_MODEL_CHEAP_STRUCTURED`
- `OPENAI_MODEL_MID_WRITER`
- `OPENAI_MODEL_STRONG_ESCALATION`
- `NATURALNESS_THRESHOLD`
- `MAX_ESCALATIONS`
- `OPENAI_PRICE_CHEAP_INPUT_PER_1M`
- `OPENAI_PRICE_CHEAP_OUTPUT_PER_1M`
- `OPENAI_PRICE_MID_INPUT_PER_1M`
- `OPENAI_PRICE_MID_OUTPUT_PER_1M`
- `OPENAI_PRICE_STRONG_INPUT_PER_1M`
- `OPENAI_PRICE_STRONG_OUTPUT_PER_1M`
- `OPENAI_TIMEOUT_SEC`
- `STRIPE_SECRET_KEY`
- `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`
- `STRIPE_PRICE_ID`
- `STRIPE_WEBHOOK_SECRET`
- `WRITING_SIGNAL_PROVIDER`
- `SAPLING_API_KEY`
- `WRITING_SIGNAL_TIMEOUT_SEC`
- `REWRITE_LEARNING_LOG_ENABLED`
- `REWRITE_COST_LOG_ENABLED`
- `ADMIN_EMAILS`
- `ADMIN_CLERK_USER_IDS`
- `ADMIN_ALLOW_RAW_REWRITE_TEXT`
- `SAPLING_PRICE_PER_1000_CHARS_USD`
- `ADMIN_NZD_PER_USD`
- `STRIPE_TIMEOUT_SEC` optional, defaults to 25 seconds
- `LAUNCH_CONFIRMED`
- `EVAL_MAX_PROMPT_ITERATIONS`
- `EVAL_MAX_WALLCLOCK_MINUTES`

Deploy with:

```bash
npm run cf:deploy
```

The deploy script uses `--keep-vars` so dashboard variables are preserved.

Launch update on 2026-05-18:

- Worker `replyinmyvoice-app` has the required runtime secret names configured.
- `NODE_ENV` was corrected to `production` in Worker secrets.
- Secret values were not printed or committed.

## Internal Admin Dashboard

- Route: `https://replyinmyvoice.com/admin`
- Entry point: signed-in admins see an `Admin` button in the `/app` header.
- Non-admin users do not see the entry and cannot access `/admin` directly.
- Admin access is controlled by:
  - `ADMIN_EMAILS`
  - `ADMIN_CLERK_USER_IDS`
- Raw rewrite text is hidden by default. Keep `ADMIN_ALLOW_RAW_REWRITE_TEXT=false` unless debugging an approved internal case.
- Cost values are estimates for product/pricing decisions, not accounting-grade invoices.
- `SAPLING_PRICE_PER_1000_CHARS_USD` controls Sapling cost estimates.
- `ADMIN_NZD_PER_USD` optionally converts stored USD estimates for display.

## Database Runtime Note

Prisma remains the source of truth for schema, migrations, and generated types.
Runtime database access in the Worker uses the Neon serverless driver directly.

Reason: the Prisma generated client path for the JS engine currently emits a
query compiler WASM artifact that does not run correctly in the OpenNext
workerd preview for this app. The direct Neon runtime path was verified with
the Worker preview DB smoke test, while keeping Prisma migrations intact.

Migration commands still use `DIRECT_URL`; runtime requests use `DATABASE_URL`.

## Domain Cutover

Previous guardrail:

```env
LAUNCH_CONFIRMED=false
```

Current launch phase authorization:

```env
LAUNCH_CONFIRMED=true
```

Formal domain cutover is authorized for this phase. Keep the existing Cloudflare Pages project available for rollback.

Cutover result on 2026-05-18:

- `replyinmyvoice.com` is attached to Worker `replyinmyvoice-app`.
- Cloudflare Pages project `replyinmyvoice` was not deleted.
- The apex Pages custom domain was removed because it was still serving the holding page after Worker custom-domain attach.
- `www.replyinmyvoice.com` remained on Pages at the time of the cutover check.

Deployment verification on 2026-05-20:

- Worker `replyinmyvoice-app` deployed successfully with version `ee305ed6-632e-487e-b12d-805b17bc00af`.
- Worker preview URL verified at `https://replyinmyvoice-app.qc1128qc.workers.dev`.
- Apex domain `https://replyinmyvoice.com` returned the Worker app.
- `/pricing` returned 200.
- `/app` redirected signed-out users to `/sign-in`.
- `/api/health/db` returned `{"ok":true}`.
- `/api/rewrite` returned 401 for a signed-out request with the correct Origin header.

WWW cutover on 2026-05-20:

- The user explicitly requested `www.replyinmyvoice.com` to serve the same Worker app as the apex domain.
- The Pages custom domain for `www.replyinmyvoice.com` was removed from project `replyinmyvoice`.
- The old `www.replyinmyvoice.com -> replyinmyvoice.pages.dev` CNAME was deleted.
- `wrangler.jsonc` now declares both `replyinmyvoice.com` and `www.replyinmyvoice.com` as Worker custom domains.
- Worker `replyinmyvoice-app` deployed successfully with version `a9e17a94-1fc0-440a-b7a7-1036f3063e3b`.
- `www.replyinmyvoice.com` returned 200 for `/`, 307 to `/sign-in` for `/app`, `{"ok":true}` for `/api/health/db`, and 401 for signed-out `/api/rewrite`.

Rollback DNS record captured before cutover:

```text
type=CNAME
name=replyinmyvoice.com
content=replyinmyvoice.pages.dev
proxied=true
ttl=1
```

Rollback path:

1. Detach the Worker custom domain for `replyinmyvoice.com`.
2. Recreate the CNAME record above.
3. Confirm the Cloudflare Pages project `replyinmyvoice` still serves the holding page.

Final cutover checklist:

- Worker preview URL works.
- `/` works.
- `/pricing` works.
- `/sign-in` works.
- `/app` auth gate works.
- Unauthenticated `/api/rewrite` rejects requests.
- `/api/stripe/webhook` responds and validates signed events.
- A DB smoke test passes in Worker preview.
- Stripe live mode checklist is complete.
- Clerk production origins are configured.

Only then switch `LAUNCH_CONFIRMED=true` and attach `replyinmyvoice.com` to the verified Worker.

## Real Account Checkout Note

Real signed-in account testing on 2026-05-18 verified:

- Clerk sign-in through the formal domain.
- Free quota: 3 successful rewrites.
- Hard paywall/API quota response after free quota: 402.
- Stripe sandbox Checkout session creation.
- Stripe webhook DB update from an active sandbox subscription.
- Paid rewrite after subscription activation.

The hosted Stripe Checkout browser page itself triggered Stripe/hCaptcha agent
verification in headless automation. The backend paid-path verification used
Stripe sandbox API subscription creation for the same real Clerk user/customer
so webhook handling, DB subscription state, and paid rewrite behavior were still
verified end to end.
