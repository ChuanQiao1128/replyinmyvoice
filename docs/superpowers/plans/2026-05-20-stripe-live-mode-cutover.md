# Stripe Live Mode Cutover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Reply In My Voice from Stripe sandbox billing to live subscription payments without breaking checkout, webhooks, subscription entitlement, monthly quota, or CI/CD deployment.

**Architecture:** Keep the existing Checkout + Billing Portal + webhook flow, but add a live-mode preflight, retry-safe webhook event processing, and an explicit test-to-live customer cutover path. Live Stripe secrets must be supplied through ignored local env, Cloudflare Worker secrets, and GitHub Actions secrets/vars; never through tracked docs.

**Tech Stack:** Next.js App Router, Cloudflare Workers/OpenNext, Stripe Checkout, Stripe Billing Portal, Stripe webhooks, Neon Postgres, Prisma migrations, GitHub Actions.

---

## Context

Current project scan on 2026-05-20 found:

- `app/api/stripe/checkout/route.ts` creates a Stripe customer when the local `User.stripeCustomerId` is empty, then creates a subscription Checkout Session.
- `app/api/stripe/portal/route.ts` opens Stripe Billing Portal for users with a local `stripeCustomerId`.
- `app/api/stripe/webhook/route.ts` handles `checkout.session.completed`, subscription create/update/delete, `invoice.paid`, and `invoice.payment_failed`.
- `lib/quota.ts` treats `active`, `trialing`, and `testing` as paid statuses, and paid users get 40 rewrites per Stripe billing period.
- `prisma/schema.prisma` has `User`, `RewriteUsage`, and `StripeEvent`.
- `.env.local` currently contains Stripe test-mode keys. `local-env.md` is tracked by Git and must not receive live secrets.

Official Stripe launch constraints:

- Live mode requires live API keys, and test-mode Stripe objects such as products/prices/webhook endpoints are not usable in live mode.
- Live webhook endpoints are separate from sandbox webhook endpoints.
- Stripe recommends webhook handlers tolerate delayed, duplicate, and out-of-order events.
- Stripe live secret keys are sensitive and may only be revealable once after creation.
- Customer Portal must be activated/configured in the live Dashboard before customers can self-manage billing.

## Goals

- Accept real recurring payments for `NZD $9/month`.
- Keep the product plan at 40 successful rewrites per billing month.
- Ensure paid users become active after live Checkout payment and webhook delivery.
- Ensure canceled/unpaid/failed-payment states revoke or preserve access according to explicit rules.
- Avoid mixing sandbox customers/subscriptions with live-mode Stripe API calls.
- Make live webhooks retry-safe: if processing fails once, a later Stripe retry must be able to process the same event.
- Update Cloudflare Worker runtime secrets and GitHub Actions secrets/vars without printing secret values.
- Verify checkout, portal, webhook, entitlement, and paid quota behavior end to end.

## Non-Goals

- Do not create a second pricing tier.
- Do not implement annual plans.
- Do not change rewrite quality logic.
- Do not implement Stripe Tax unless the user explicitly decides to enable it.
- Do not do real live charges during automated tests without explicit user approval for a real payment.

## Required User Dashboard Setup

The user must complete or confirm these before the live cutover run:

- Activate the Stripe account for live payments, including required business/KYC information, payout/bank details, and 2FA.
- Confirm the public business name, support email, statement descriptor, and branding in Stripe.
- Create or authorize Codex to create a live recurring product/price:
  - Product name: `Reply In My Voice`
  - Price: `NZD 9.00`
  - Billing interval: monthly
  - Quantity: 1
- Create or authorize Codex to create a live webhook endpoint:
  - URL: `https://replyinmyvoice.com/api/stripe/webhook`
  - Events:
    - `checkout.session.completed`
    - `customer.subscription.created`
    - `customer.subscription.updated`
    - `customer.subscription.deleted`
    - `invoice.paid`
    - `invoice.payment_failed`
- Activate and configure Stripe Customer Portal in live mode:
  - Allow payment method update.
  - Allow subscription cancellation.
  - Show invoice history.
  - Set terms/privacy links if Stripe asks for them.
- Decide whether taxes are handled manually or through Stripe Tax. If Stripe Tax is enabled, add a separate implementation plan.

## Secret Input Contract

The user should put live values only in `.env.local`, not `local-env.md`.

Required values:

```env
STRIPE_SECRET_KEY=sk_live_...
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=pk_live_...
STRIPE_PRICE_ID=price_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_LIVE_CUTOVER_APPROVED=true
```

Optional if Codex is asked to create live Stripe objects by API:

```env
STRIPE_CREATE_LIVE_OBJECTS=true
```

Do not print these values. Preflight may print only redacted status such as `live`, `present`, `missing`, or object metadata like currency/amount/interval.

## Current System

Checkout flow:

```text
Paywall / Upgrade button
  -> POST /api/stripe/checkout
  -> get or create User
  -> reuse User.stripeCustomerId if present
  -> create Stripe Checkout Session with STRIPE_PRICE_ID
  -> redirect to hosted Stripe Checkout
```

Webhook flow:

```text
POST /api/stripe/webhook
  -> verify Stripe-Signature with STRIPE_WEBHOOK_SECRET
  -> insert StripeEvent id
  -> if insert succeeded, handle event
  -> update User subscription fields
```

Quota flow:

```text
User.subscriptionStatus active/trialing/testing
  -> paid quota 40
  -> periodKey paid:${subscriptionId}:${currentPeriodEnd}
  -> successful rewrite increments RewriteUsage
```

## Findings To Fix Before Live

- [P1] `app/api/stripe/webhook/route.ts` marks a Stripe event as processed before the subscription update succeeds. If `handleStripeEvent` throws after insertion, Stripe retries will hit the existing event id and skip processing. Add event status fields and retry failed events.
- [P1] Existing sandbox `User.stripeCustomerId` values cannot be reused with live keys. Add a cutover script or migration step that resets sandbox Stripe fields before live launch, or add mode-aware customer fields.
- [P1] The current Next app has no Stripe route/webhook tests. Add tests for checkout customer creation, live/test mode mismatch, webhook idempotency, failed webhook retry, and subscription entitlement update.
- [P1] Cloudflare Worker secrets and GitHub Actions secrets/vars must both be updated. `npm run cf:deploy -- --keep-vars` preserves existing Worker secrets; it does not replace sandbox Stripe secrets by itself.
- [P2] Billing Portal live configuration is dashboard-dependent. Add preflight that calls Stripe enough to detect missing portal configuration before customers see a runtime failure.
- [P2] `testing` is treated as a paid status in `lib/quota.ts`. Before public launch, verify no real users remain in `testing`, or add an admin-only guard so `testing` cannot leak as a production entitlement.

## Proposed Architecture

Live mode should be deployed through a controlled cutover:

```text
1. User provides live Stripe env values in .env.local
2. Run local live preflight without printing secrets
3. Verify live product/price amount/currency/interval
4. Verify live webhook endpoint and signing secret
5. Verify live Customer Portal configuration
6. Add retry-safe StripeEvent processing
7. Reset or mode-separate sandbox customer/subscription fields
8. Update Cloudflare Worker secrets
9. Update GitHub Actions secrets/vars
10. Deploy
11. Smoke test live checkout creation and webhook signature rejection
12. User performs one explicit real checkout if approved
13. Verify DB subscriptionStatus and paid quota
```

## Data Model

Modify `StripeEvent`:

```prisma
model StripeEvent {
  id             String    @id
  type           String
  status         String    @default("processing")
  processedAt    DateTime?
  failedAt       DateTime?
  lastError      String?
  attemptCount   Int       @default(0)
  stripeMode     String    @default("unknown")
  createdAt      DateTime  @default(now())
  updatedAt      DateTime  @updatedAt

  @@index([status])
  @@index([type])
  @@index([createdAt])
}
```

Cutover handling for existing users:

Option A, recommended for current MVP:

```sql
UPDATE "User"
SET
  "stripeCustomerId" = NULL,
  "stripeSubscriptionId" = NULL,
  "stripePriceId" = NULL,
  "subscriptionStatus" = 'inactive',
  "currentPeriodEnd" = NULL,
  "updatedAt" = now()
WHERE "stripeCustomerId" LIKE 'cus_%'
  OR "stripeSubscriptionId" IS NOT NULL
  OR "subscriptionStatus" IN ('active', 'trialing', 'testing');
```

This avoids live Stripe trying to reuse test customers. Run only after user approval because it removes sandbox paid access.

Option B, later hardening:

```prisma
stripeMode String @default("test")
```

Then store separate live/test customer state. This is more flexible but not required for first live launch.

## API And Job Contracts

No public API shape change is required.

Add internal Stripe mode preflight:

```ts
type StripeLivePreflightResult = {
  secretKeyMode: "live" | "test" | "unknown" | "missing";
  publishableKeyMode: "live" | "test" | "unknown" | "missing";
  price: {
    id: string;
    livemode: boolean;
    currency: string;
    unitAmount: number | null;
    recurringInterval: string | null;
  };
  webhookEndpoint: {
    url: string;
    enabledEvents: string[];
    liveMode: boolean;
  } | null;
  portalReady: boolean;
  blockers: string[];
};
```

Expected live preflight:

```text
secretKeyMode = live
publishableKeyMode = live
price.livemode = true
price.currency = nzd
price.unitAmount = 900
price.recurringInterval = month
webhookEndpoint.url = https://replyinmyvoice.com/api/stripe/webhook
portalReady = true
blockers = []
```

## State And Error Handling

Webhook event lifecycle:

```text
new event -> processing
processing -> processed after successful subscription sync
processing -> failed if subscription sync throws
failed -> processing on later Stripe retry
processed -> skipped on duplicate delivery
```

Rules:

- Duplicate processed events return 200 and do not mutate state.
- Failed events are allowed to retry.
- Unknown events return 200 without mutation.
- Invalid signature returns 400.
- Webhook handler must not require event ordering. If an invoice event arrives first, retrieve the subscription by id and sync from Stripe.

Subscription entitlement:

- `active` and `trialing` continue to count as paid.
- `canceled`, `incomplete_expired`, `unpaid`, and deleted subscriptions should remove paid access.
- `past_due` behavior must be explicitly decided:
  - MVP recommended: not paid unless Stripe still reports `active`.
  - If the user wants a grace period, add that as a separate rule and test.

## Security And Privacy

- Never put live Stripe keys in `local-env.md`; it is tracked by Git.
- Never print live secret values in terminal output, docs, commits, or final messages.
- Do not source `.env.local` for `npm run cf:deploy`; the repo README warns this can break OpenNext through `NODE_ENV`.
- Use a dedicated script to push only approved secret names to Cloudflare and GitHub.
- Keep webhook signature verification required in production.
- Use the live webhook secret for the live endpoint; sandbox webhook secret is not valid for live events.

## Rollout Plan

### Task 1: Add Live Stripe Preflight

**Files:**
- Create: `scripts/stripe-live-preflight.ts`
- Modify: `package.json`

- [ ] Add a script that reads Stripe env vars from process env and prints only modes/status, not values.
- [ ] Retrieve `STRIPE_PRICE_ID` from Stripe API and verify `livemode`, `currency`, `unit_amount`, and `recurring.interval`.
- [ ] List live webhook endpoints and verify `https://replyinmyvoice.com/api/stripe/webhook` exists with required events.
- [ ] Attempt a Billing Portal configuration check or create a harmless portal session only for a known test customer if available.
- [ ] Add npm script:

```json
"stripe:live-preflight": "tsx scripts/stripe-live-preflight.ts"
```

### Task 2: Make Stripe Webhook Event Processing Retry-Safe

**Files:**
- Modify: `prisma/schema.prisma`
- Create: `prisma/migrations/<timestamp>_stripe_event_status/migration.sql`
- Modify: `app/api/stripe/webhook/route.ts`
- Test: `tests/unit/stripe-webhook.test.ts`

- [ ] Add `status`, `processedAt`, `failedAt`, `lastError`, `attemptCount`, `stripeMode`, and `updatedAt` to `StripeEvent`.
- [ ] Replace `markEventProcessed` with `beginStripeEventProcessing`, `markStripeEventProcessed`, and `markStripeEventFailed`.
- [ ] Let failed events be retried on later Stripe deliveries.
- [ ] Add tests:
  - duplicate processed event is skipped
  - failed event can be retried and processed
  - invalid signature returns 400
  - subscription update writes `subscriptionStatus`, `stripeSubscriptionId`, `stripePriceId`, and `currentPeriodEnd`

### Task 3: Add Test-To-Live Customer Cutover

**Files:**
- Create: `scripts/stripe-live-cutover-reset.ts`
- Modify: `package.json`
- Test: `tests/unit/stripe-live-cutover.test.ts`

- [ ] Add a dry-run mode that counts users with Stripe/test entitlement fields.
- [ ] Add an apply mode gated by `STRIPE_LIVE_CUTOVER_APPROVED=true`.
- [ ] Reset sandbox Stripe fields for users before live launch.
- [ ] Do not delete users or rewrite usage history.
- [ ] Add npm scripts:

```json
"stripe:live-cutover:dry-run": "tsx scripts/stripe-live-cutover-reset.ts --dry-run",
"stripe:live-cutover:apply": "tsx scripts/stripe-live-cutover-reset.ts --apply"
```

### Task 4: Sync Live Secrets To Deployment Targets

**Files:**
- Create: `scripts/sync-live-stripe-secrets.ts`
- Modify: `docs/manual-setup.md`

- [ ] Add Cloudflare secret sync for:
  - `STRIPE_SECRET_KEY`
  - `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`
  - `STRIPE_PRICE_ID`
  - `STRIPE_WEBHOOK_SECRET`
- [ ] Add GitHub secret/var sync instructions:
  - GitHub secrets: `STRIPE_SECRET_KEY`, `STRIPE_PRICE_ID`, `STRIPE_WEBHOOK_SECRET`
  - GitHub vars: `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`
- [ ] The script must never echo values. It may print only `updated <name>`.

### Task 5: Verify Checkout And Portal Boundaries

**Files:**
- Test: `tests/unit/stripe-checkout.test.ts`
- Test: `tests/unit/stripe-portal.test.ts`

- [ ] Mock Stripe API calls and verify Checkout uses the live price id from env.
- [ ] Verify Checkout does not proceed if live mode is expected but a test key is loaded.
- [ ] Verify Portal returns a clean app error if Stripe says portal is not configured.
- [ ] Verify `requireSameOrigin` still protects checkout and portal routes.

### Task 6: Run Verification And Deploy

Run:

```bash
npm run prisma:generate
npm run typecheck
npm run test
npm run build
npm run cf:build
npm run stripe:live-preflight
npm run stripe:live-cutover:dry-run
```

After user approves live reset:

```bash
npm run stripe:live-cutover:apply
npm run cf:deploy
```

Smoke:

```bash
curl -I https://replyinmyvoice.com/
curl -I https://www.replyinmyvoice.com/
curl -sS https://replyinmyvoice.com/api/health/db
curl -sS -X POST https://replyinmyvoice.com/api/rewrite \
  -H "Origin: https://replyinmyvoice.com" \
  -H "Content-Type: application/json" \
  --data '{"roughDraftReply":"Hello, this is a test draft.","tone":"warm"}'
```

Manual live-payment smoke, only after user explicitly approves a real payment:

```text
1. Sign in as a real user.
2. Exhaust or bypass free quota only if needed to show paywall.
3. Click Upgrade.
4. Complete a real Stripe Checkout payment.
5. Confirm Stripe dashboard shows live subscription.
6. Confirm webhook delivery succeeded.
7. Confirm Neon User.subscriptionStatus is active or trialing.
8. Confirm /app shows 40 paid rewrites for the billing period.
9. Open Billing Portal and confirm cancellation/payment-method pages work.
```

## Verification Plan

Automated checks must pass:

- `npm run typecheck`
- `npm run test`
- `npm run build`
- `npm run cf:build`
- `npm run stripe:live-preflight`

Database checks:

- `StripeEvent` records use `processing`, `processed`, or `failed`.
- A failed event can be retried.
- Sandbox Stripe customer/subscription fields are cleared or mode-separated before live API calls.

Live dashboard checks:

- Product/price is live, NZD 900 monthly.
- Webhook endpoint is live and has required events.
- Webhook delivery succeeds after checkout.
- Customer Portal opens from `/app`.

## Open Questions

- Should `past_due` users keep access for a grace period, or lose paid access until payment recovers?
- Should Stripe Tax be enabled now, or should tax handling stay manual for first launch?
- Should existing sandbox paid/test users be reset to inactive at cutover, or should the user manually choose which test account remains upgraded?
- Should the first live payment be made by the user only, or can Codex guide the browser while the user confirms card/payment details?
