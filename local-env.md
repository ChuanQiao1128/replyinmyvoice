# Reply In My Voice Local Environment

This file tracks the local and production environment setup for `replyinmyvoice.com`.

Do not paste secret values into this markdown file. Put secret values only in `.env.local` locally and in the production platform environment variables.

## Local Files

- Local env file: `/Users/qc/Desktop/CloudFlare/.env.local`
- Requirements file: `/Users/qc/Desktop/CloudFlare/replyinmyvoice_requirements.md`
- Domain: `replyinmyvoice.com`
- Cloudflare Pages project: `replyinmyvoice`

## Current `.env.local` Variables

### App

```env
NEXT_PUBLIC_APP_URL=https://replyinmyvoice.com
NODE_ENV=development
```

Keep `NEXT_PUBLIC_APP_URL` as `https://replyinmyvoice.com` for production-like testing. For pure local callback testing, it can temporarily become `http://localhost:3000`.

### Clerk

```env
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=your_clerk_publishable_key
CLERK_SECRET_KEY=your_clerk_secret_key
NEXT_PUBLIC_CLERK_SIGN_IN_URL=/sign-in
NEXT_PUBLIC_CLERK_SIGN_UP_URL=/sign-up
NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL=/app
NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL=/app
```

Where to get values:

- Clerk Dashboard -> your app -> API keys.
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` is public.
- `CLERK_SECRET_KEY` is secret.

Dashboard setup needed:

- Add local origin: `http://localhost:3000`
- Add production origin: `https://replyinmyvoice.com`
- Confirm redirect URLs:
  - `/sign-in`
  - `/sign-up`
  - `/app`

### Neon Postgres

```env
DATABASE_URL=pooled_neon_connection_string
DIRECT_URL=direct_neon_connection_string
```

Where to get values:

- Neon Dashboard -> project -> Connection details.
- `DATABASE_URL` should be the pooled connection string if available.
- `DIRECT_URL` should be the direct connection string for Prisma migrations.

### OpenAI

```env
OPENAI_API_KEY=your_openai_secret_key
OPENAI_MODEL=gpt-4o-mini
OPENAI_TIMEOUT_SEC=25
```

Where to get values:

- OpenAI Platform -> API keys.
- Keep `OPENAI_MODEL=gpt-4o-mini` unless we decide to change models later.
- Use `OPENAI_TIMEOUT_SEC=25` to avoid hanging provider calls.

### Sapling Writing Signal

Purpose: before/after third-party writing signal for reply naturalness. This is a reference signal only, not a guarantee and not the sole product success metric.

```env
WRITING_SIGNAL_PROVIDER=sapling
SAPLING_API_KEY=your_sapling_api_key
WRITING_SIGNAL_TIMEOUT_SEC=10
```

Where to get values:

- Sapling dashboard / API settings.
- Put the Sapling API key only in `.env.local` and production environment variables.

Current product decisions:

- Show the before/after signal to users.
- Run the signal for free users too.
- One user Rewrite request counts as one usage attempt, even if the server tries multiple bounded internal optimization strategies for quality.
- User-facing label: `Naturalness Check`.
- Show AI-like signal percentage. Lower is better after rewrite.
- Use reference-only wording, no pass/fail language, no guarantee, and no detector-bypass framing.
- Lowering the AI-like signal is a core product goal; during development, keep testing and changing prompts/strategies if the reduction is not satisfactory.

### Stripe

```env
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=your_stripe_publishable_key
STRIPE_SECRET_KEY=your_stripe_secret_key

STRIPE_PRICE_ID=your_stripe_recurring_price_id
STRIPE_WEBHOOK_SECRET=your_stripe_webhook_signing_secret
```

Where to get values:

- Stripe Dashboard -> Developers -> API keys.
- Create one subscription product: `NZD $9/month` for the current sandbox/MVP setup.
- Copy the recurring price ID into `STRIPE_PRICE_ID`.
- After deployment, create a webhook endpoint for:
  - `checkout.session.completed`
  - `customer.subscription.created`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
- Copy the webhook signing secret into `STRIPE_WEBHOOK_SECRET`.

### Local Subscription Bypass

```env
ALLOW_DEV_SUBSCRIPTION_BYPASS=false
```

Set to `true` only for local development when testing the rewrite workspace before Stripe is fully configured. The app must ignore this in production.

## Production Variables

The same variables must be added to the production deployment environment.

If deploying to Cloudflare, add them under:

Cloudflare Dashboard -> Workers & Pages -> target project/worker -> Settings -> Environment variables

For non-interactive Wrangler deployment from Codex/local shell, also keep these local-only deploy variables in `.env.local`:

```env
CLOUDFLARE_ACCOUNT_ID=c86c61e9f248099c824247dfd0a12098
CLOUDFLARE_API_TOKEN=your_scoped_cloudflare_api_token
LAUNCH_CONFIRMED=false
EVAL_MAX_PROMPT_ITERATIONS=5
EVAL_MAX_WALLCLOCK_MINUTES=60
```

## Still Needed From User

- None for local development.
- Stripe sandbox price is currently NZD 9/month and has been accepted for testing.
- Cloudflare token now passes Wrangler, Pages, Workers, Zone, and DNS records read checks.
- `LAUNCH_CONFIRMED=false` is intentionally set for safe autonomous development. The code agent must not change `replyinmyvoice.com` DNS records or the existing Pages custom domain while it is false.
- Evaluation limits are intentionally set to avoid unbounded OpenAI/Sapling optimization loops.

## Future .NET / Azure Phase Inputs

The next-phase planning document is:

- `/Users/qc/Desktop/CloudFlare/docs/dotnet-azure-next-phase.md`

Do not paste Azure, Stripe, OpenAI, Sapling, Clerk, or database secret values into this markdown file. Record only variable names and setup notes here.

When the .NET/Azure phase starts, ask the user for these non-secret decisions first:

```env
AZURE_SUBSCRIPTION_ID=subscription_id_goes_in_local_env_only
AZURE_TENANT_ID=tenant_id_goes_in_local_env_only
AZURE_LOCATION=preferred_azure_region
AZURE_RESOURCE_GROUP=resource_group_name
AZURE_BUDGET_LIMIT=monthly_budget_confirmation
AZURE_ALLOW_PAID_RESOURCES=false
```

If deployment automation is approved, collect or configure these through Azure CLI login, GitHub OIDC, Azure Key Vault, or GitHub Secrets rather than storing plaintext secrets in docs:

```env
AZURE_APP_SERVICE_NAME=app_service_name
AZURE_APP_SERVICE_PLAN_NAME=app_service_plan_name
AZURE_SQL_SERVER_NAME=sql_server_name
AZURE_SQL_DATABASE_NAME=sql_database_name
AZURE_KEY_VAULT_NAME=key_vault_name
AZURE_APPLICATION_INSIGHTS_NAME=application_insights_name
AZURE_SERVICE_BUS_NAMESPACE=optional_service_bus_namespace
AZURE_SERVICE_BUS_QUEUE=optional_service_bus_queue
```

The existing Neon database should remain untouched until the .NET/Azure backend has a verified migration path. For a resume-aligned Azure backend, prefer Azure SQL Database plus EF Core for the new implementation.

## How To Prepare Each Key

Fill values in `/Users/qc/Desktop/CloudFlare/.env.local`. Do not paste secret values into this markdown file or into chat.

### 1. OpenAI

Purpose: powers the rewrite API.

Website:

- https://platform.openai.com/api-keys

Steps:

1. Log in to OpenAI Platform.
2. Open API keys.
3. Create a new secret key.
4. Name it `replyinmyvoice`.
5. Copy it once and paste it into `.env.local`.

Fill:

```env
OPENAI_API_KEY=your_openai_secret_key
OPENAI_MODEL=gpt-4o-mini
```

Notes:

- The full OpenAI secret key is only shown when created. If you lose it, create a new one.
- Make sure the OpenAI project has billing/credits enabled before testing real rewrites.

### 2. Clerk

Purpose: sign-in, sign-up, authenticated `/app`, and authenticated API calls.

Website:

- https://dashboard.clerk.com

Steps:

1. Create a new Clerk application named `Reply In My Voice`.
2. Choose Email as the primary sign-in method. Google can be added later.
3. Go to API keys.
4. Copy the publishable key and secret key.
5. Add local and production URLs in allowed origins / redirect URLs.

Fill:

```env
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=your_clerk_publishable_key
CLERK_SECRET_KEY=your_clerk_secret_key
NEXT_PUBLIC_CLERK_SIGN_IN_URL=/sign-in
NEXT_PUBLIC_CLERK_SIGN_UP_URL=/sign-up
NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL=/app
NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL=/app
```

Configure URLs:

```txt
http://localhost:3000
https://replyinmyvoice.com
https://www.replyinmyvoice.com
```

### 3. Neon Postgres

Purpose: stores user subscription state from Stripe.

Website:

- https://console.neon.tech

Steps:

1. Create a Neon project named `replyinmyvoice`.
2. Choose the free tier if available.
3. Open Connection details.
4. Select Prisma or Postgres connection strings.
5. Copy both pooled and direct connection strings.

Fill:

```env
DATABASE_URL=pooled_neon_connection_string
DIRECT_URL=direct_neon_connection_string
```

Recommended:

- Use the pooled connection string for `DATABASE_URL`.
- Use the direct connection string for `DIRECT_URL`, which Prisma migrations need.

### 4. Stripe

Purpose: `NZD $9/month` checkout, billing portal, webhook subscription updates for the current sandbox/MVP setup.

Website:

- https://dashboard.stripe.com

Steps:

1. Start in test mode first.
2. Go to Developers -> API keys.
3. Copy the publishable key and secret key.
4. Create a Product named `Reply In My Voice`.
5. Create a recurring monthly Price for `NZD $9/month`.
6. Copy the Price ID.
7. After the production URL is deployed, create a webhook endpoint.

Fill:

```env
STRIPE_SECRET_KEY=your_stripe_secret_key
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=your_stripe_publishable_key
STRIPE_PRICE_ID=your_stripe_recurring_price_id
STRIPE_WEBHOOK_SECRET=your_stripe_webhook_signing_secret
```

Webhook endpoint after deploy:

```txt
https://replyinmyvoice.com/api/stripe/webhook
```

Webhook events:

```txt
checkout.session.completed
customer.subscription.created
customer.subscription.updated
customer.subscription.deleted
```

Notes:

- Test keys usually start with `sk_test_` and `pk_test_`.
- Live keys usually start with `sk_live_` and `pk_live_`.
- The webhook signing secret is separate from API keys and belongs to a specific webhook endpoint.

### 5. Cloudflare

Purpose: production deployment for `replyinmyvoice.com`.

Website:

- https://dash.cloudflare.com

Current status:

- Domain is already registered: `replyinmyvoice.com`
- Pages project already exists: `replyinmyvoice`
- Holding page is already live.
- GitHub remote is configured: `git@github.com:ChuanQiao1128/replyinmyvoice.git`
- GitHub SSH auth has been verified locally.

After the app is built, production env vars must be added under:

```txt
Cloudflare Dashboard
Workers & Pages
replyinmyvoice
Settings
Environment variables
```

Add the same variables as `.env.local`, but use production/live values where relevant.

Wrangler deployment token:

1. Open Cloudflare Dashboard -> My Profile -> API Tokens.
2. Create a custom token for local/Codex deployment.
3. Scope it to the `replyinmyvoice.com` zone and the account that owns `replyinmyvoice`.
4. Grant the minimum deployment permissions needed for the chosen Cloudflare target:
   - Account: Workers Scripts Edit
   - Account: Cloudflare Pages Edit
   - Account: Account Settings Read
   - Zone: Zone Read
   - Zone: DNS Records Read/Edit if automated DNS changes are needed
5. Paste the token only into `.env.local` as `CLOUDFLARE_API_TOKEN`.

### 6. GitHub

Purpose: source control and optional Cloudflare Pages Git integration.

Website:

- https://github.com/new

Current status:

- Local Git repository is initialized.
- `origin` is set to `git@github.com:ChuanQiao1128/replyinmyvoice.git`.
- The remote appears reachable over SSH and currently has no branch heads.

Expected URL shape:

```txt
git remote add origin git@github.com:ChuanQiao1128/replyinmyvoice.git
```

## Setup Priority

Minimum to build locally:

1. No real keys required at first.
2. Empty `.env.local` values are okay while code is being built.
3. Build must not fail just because secrets are empty.

Minimum to test real rewrite:

1. OpenAI key.
2. Clerk keys.
3. `ALLOW_DEV_SUBSCRIPTION_BYPASS=true` for local-only testing before Stripe is ready.

Minimum to test paid flow:

1. Clerk keys.
2. Neon connection strings.
3. Stripe test keys.
4. Stripe test price ID.
5. Stripe webhook secret.

Minimum to launch:

1. OpenAI key with billing enabled.
2. Clerk production app/keys and allowed URLs.
3. Neon production database.
4. Stripe live product, live price, live API keys, live webhook secret.
5. Cloudflare production environment variables.
6. Cloudflare API token if Codex/local shell should deploy non-interactively.

## Notes

- Never commit `.env.local`.
- It is okay for `.env.local` to contain empty values while we build the app. The code must validate secrets at runtime, not during build.
- The current public holding page is already deployed at `https://replyinmyvoice.com`.
