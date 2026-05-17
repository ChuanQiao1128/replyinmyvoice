# Manual Setup

These steps are dashboard-only or final-cutover tasks. They should not block local development.

## Clerk

- Add `https://replyinmyvoice.com` as an allowed production origin.
- Add the deployed Worker preview URL after deployment if Clerk requires it for testing.
- Confirm redirect URLs:
  - `/sign-in`
  - `/sign-up`
  - `/app`

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
- Additional events implemented in code and recommended in the Stripe dashboard:
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

## Domain Cutover

Current guardrail:

```env
LAUNCH_CONFIRMED=false
```

Do not change `replyinmyvoice.com` DNS or the existing Pages custom domain while this remains false.

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
