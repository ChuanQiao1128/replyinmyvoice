# Reply In My Voice

Reply In My Voice turns rough drafts into clear, natural replies for teacher messages, sales follow-ups, workplace email, and client/customer responses.

## Local Setup

```bash
npm install
cp .env.example .env.local
npm run prisma:generate
npm run prisma:migrate -- --name init
npm run dev
```

Use Node 22:

```bash
nvm use
```

## Required Services

- Clerk for authentication
- Neon Postgres for Prisma data
- Stripe sandbox for Checkout, Billing Portal, and webhooks
- OpenAI for rewrite generation
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
```

## Database

Runtime uses `DATABASE_URL`. Migrations use `DIRECT_URL`.

Allowed migration commands:

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

## CI/CD

GitHub Actions workflows:

- `.github/workflows/cloudflare-worker.yml`: runs Node build/typecheck/tests and deploys the Cloudflare Worker on pushes to `main`.
- `.github/workflows/dotnet-azure.yml`: builds/tests the .NET backend and deploys the Azure dev App Service on pushes to `main`.

Required Cloudflare/Next secrets and variables are stored in GitHub Actions. Do
not commit `.env.local` or copy secret values into workflow files.
