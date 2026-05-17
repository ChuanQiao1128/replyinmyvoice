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
- `OPENAI_TIMEOUT_SEC`
- `STRIPE_SECRET_KEY`
- `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`
- `STRIPE_PRICE_ID`
- `STRIPE_WEBHOOK_SECRET`
- `WRITING_SIGNAL_PROVIDER`
- `SAPLING_API_KEY`
- `WRITING_SIGNAL_TIMEOUT_SEC`
- `LAUNCH_CONFIRMED`
- `EVAL_MAX_PROMPT_ITERATIONS`
- `EVAL_MAX_WALLCLOCK_MINUTES`

Deploy with:

```bash
npm run cf:deploy
```

The deploy script uses `--keep-vars` so dashboard variables are preserved.

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
