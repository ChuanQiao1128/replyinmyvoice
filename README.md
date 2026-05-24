# Reply In My Voice

Reply In My Voice turns rough drafts into clear, natural replies for teacher messages, sales follow-ups, workplace email, and client/customer responses.

## Local Setup

```bash
npm install
cp .env.example .env.local
npm run dev
```

Use Node 22:

```bash
nvm use
```

## Required Services

- Microsoft Entra External ID with Google sign-in for authentication
- Azure Functions for public backend API routes
- Azure SQL for account, quota, rewrite attempts, Stripe events, and operational data
- Azure Service Bus for queued rewrite processing
- Stripe sandbox for Checkout, Billing Portal, and webhooks
- DeepSeek/OpenAI-compatible chat completions for rewrite generation
- Sapling for Naturalness Check
- Cloudflare Workers/OpenNext for deployment

## Key Commands

```bash
npm run lint
npm run typecheck
npm run test
npm run test:e2e
npm run build
npm run cf:build
npm run cf:preview
npm run cf:deploy
npm run eval:naturalness
npm run eval:scenarios
```

## Rewrite Strategy

The public `/api/rewrite` route is a Cloudflare/Next compatibility proxy to the
.NET Azure Functions backend. The C# backend owns rewrite attempts, quota
reservation/finalization, Service Bus processing, model calls, and Sapling
Naturalness Check gating. If the bounded workflow cannot produce a fact-safe
rewrite under the configured quality bar, the backend marks the attempt failed,
releases the reservation, and the proxy returns a no-charge quality-failure
response.

## Database

Public runtime data is served by Azure SQL through Azure Functions.

EF Core migration commands:

```bash
dotnet ef database update \
  --project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj \
  --startup-project backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj \
  --context AppDbContext
```

Legacy Prisma/Neon files remain for historical tests and cleanup work only; they
are not the public account/quota/rewrite runtime path. Historical Prisma
migration commands:

```bash
npx prisma generate
npx prisma migrate dev --name init
npx prisma migrate deploy
```

Do not run destructive reset commands without explicit approval.

## Stripe Webhook

The app handles:

- `checkout.session.completed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`
- `invoice.paid`
- `invoice.payment_failed`

Production webhook verification requires `STRIPE_WEBHOOK_SECRET`.

## Cloudflare

This app targets Cloudflare Workers through OpenNext:

- Worker name: `replyinmyvoice-app`
- Config: `wrangler.jsonc`
- Runtime output: `.open-next/worker.js`
- Deploy command: `npm run cf:deploy`
- Current Worker URL: `https://replyinmyvoice-app.qc1128qc.workers.dev`
- Production domain: `https://replyinmyvoice.com`

`LAUNCH_CONFIRMED=false` means the existing `replyinmyvoice.com` holding page and DNS must not be changed.

Do not `source .env.local` before running `npm run cf:deploy`; exporting
`NODE_ENV` from `.env.local` can break OpenNext's production build. Pass only
the Cloudflare auth variables to the deploy process, or use GitHub Actions.

The Worker entry point is the root `worker.js` wrapper so the Cloudflare cron
handler can run scheduled LearningOps. Keep Wrangler minification enabled, and
do not enable broad `find_additional_modules` or root-level WASM rules; those
make Wrangler attach unrelated repo files and Prisma/Next WASM artifacts,
causing the Worker package to exceed Cloudflare size limits.

## CI/CD

GitHub Actions workflows:

- `.github/workflows/cloudflare-worker.yml`: runs Node build/typecheck/tests and deploys the Cloudflare Worker on pushes to `main`.
- `.github/workflows/dotnet-azure.yml`: builds/tests the .NET backend and deploys the Azure dev Function App on pushes to `main`.

Required Cloudflare/Next secrets and variables are stored in GitHub Actions. Do
not commit `.env.local` or copy secret values into workflow files.
