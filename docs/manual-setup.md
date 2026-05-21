# Manual Setup

These steps are dashboard-only or final-cutover tasks. They should not block local development.

## Azure Functions / Azure SQL / Entra External ID Migration

The next backend/auth target is to move production runtime identity and data away from Clerk/Neon and onto:

```text
Cloudflare frontend
+ Microsoft Entra External ID customer auth
+ Google social sign-in through Entra
+ Azure Functions / .NET API
+ Azure SQL
+ Azure Service Bus
```

App Service is not required for this target unless Azure Functions proves unsuitable.

Preparation values should go in:

```text
/Users/qc/Desktop/CloudFlare/.env.local
```

Use:

```text
/Users/qc/Desktop/CloudFlare/local-env.md
```

only for notes and explanations. Do not put secret values in committed docs.

Required values to prepare:

```env
AZURE_SUBSCRIPTION_ID=
AZURE_TENANT_ID=
AZURE_LOCATION=australiaeast
AZURE_RESOURCE_GROUP=replyinmyvoice-dev-rg

AZURE_FUNCTION_APP_NAME=replyinmyvoice-api-dev
AZURE_FUNCTION_STORAGE_ACCOUNT_NAME=
AZURE_APPLICATION_INSIGHTS_NAME=replyinmyvoice-ai-dev

AZURE_SQL_SERVER_NAME=replyinmyvoice-sql-dev
AZURE_SQL_DATABASE_NAME=replyinmyvoice-db-dev
AZURE_SQL_ADMIN_USER=
AZURE_SQL_ADMIN_PASSWORD=
AZURE_SQL_CONNECTION_STRING=

AZURE_SERVICE_BUS_NAMESPACE=
AZURE_SERVICE_BUS_QUEUE_NAME=reply-rewrite-jobs
AZURE_SERVICE_BUS_CONNECTION_STRING=

AZURE_EXTERNAL_ID_TENANT_ID=
AZURE_EXTERNAL_ID_TENANT_SUBDOMAIN=
AZURE_EXTERNAL_ID_AUTHORITY=
AZURE_EXTERNAL_ID_FRONTEND_CLIENT_ID=
AZURE_EXTERNAL_ID_API_CLIENT_ID=
AZURE_EXTERNAL_ID_API_AUDIENCE=
AZURE_EXTERNAL_ID_API_SCOPE=
AZURE_EXTERNAL_ID_WELL_KNOWN_URL=
AZURE_EXTERNAL_ID_SIGN_IN_FLOW_NAME=

GOOGLE_CLIENT_ID_FOR_ENTRA=
GOOGLE_CLIENT_SECRET_FOR_ENTRA=

NEXT_PUBLIC_AZURE_API_BASE_URL=
NEXT_PUBLIC_ENTRA_AUTHORITY=
NEXT_PUBLIC_ENTRA_CLIENT_ID=
NEXT_PUBLIC_ENTRA_API_SCOPE=
```

Dashboard steps:

1. Create or confirm a Microsoft Entra External ID external tenant.
2. Create a frontend app registration for `replyinmyvoice.com`.
3. Create an API app registration for the Azure Functions backend and expose one API scope.
4. Create a sign-up/sign-in user flow and attach the frontend app.
5. In Google Cloud Console, create a Google OAuth web app for Entra federation.
6. Add `replyinmyvoice.com` to Google OAuth consent screen authorized domains if required.
7. Add the exact redirect URI shown by Entra External ID when configuring Google federation.
8. Copy the Google client id/secret into `.env.local`.
9. Add the Google client id/secret to Entra External ID identity providers.
10. Select Google in the Entra user flow.

Important:

```text
Do not reuse old Clerk redirect URLs.
Do not paste secrets into chat.
Do not block the first migration on Facebook/Apple login.
Finish Google first, then add additional social providers later.
```

## Clerk

**Clerk auth has been removed.** See plans/clerk-dns-cleanup.md for the post-cutover DNS cleanup steps the user runs manually after 7 days of verified Entra-only operation.

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

## Clerk removal — rollback notes (M1-012)

The legacy `CLERK_*` env variables (`ADMIN_CLERK_USER_IDS`, `CLERK_SECRET_KEY` fallback) were removed in M1-012. The User model's `clerkUserId` field is still in the Prisma schema and will be renamed to `entra_user_id` in M1-007. If rollback is needed, restore the legacy env names from commit ba8127a.
