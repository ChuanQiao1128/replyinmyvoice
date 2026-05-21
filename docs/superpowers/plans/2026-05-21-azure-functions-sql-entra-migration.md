# Azure Functions SQL Entra Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Clerk and Neon in the production runtime with Microsoft Entra External ID, Azure Functions/.NET APIs, Azure SQL, and Azure Service Bus while keeping Cloudflare as the public frontend edge.

**Architecture:** Cloudflare serves the frontend shell and static/public pages. Microsoft Entra External ID handles customer sign-in with Google federation. Azure Functions/.NET owns API routes, Stripe webhooks, quota/accounting, admin APIs, rewrite job intake, Azure SQL persistence, and Service Bus queue/worker processing.

**Tech Stack:** Cloudflare frontend, Microsoft Entra External ID, Google OAuth federation, Azure Functions, .NET, Azure SQL, Azure Service Bus, Application Insights, Stripe, OpenAI, Sapling, GitHub Actions.

---

## Scope

This plan is a migration target for the next long development run.

In scope:

- Remove Clerk from the production auth path.
- Remove Neon from the production database path.
- Keep Cloudflare as the public frontend and DNS layer.
- Add Microsoft Entra External ID sign-in with Google.
- Add Azure Functions/.NET API surface.
- Use Azure SQL as the source of truth for users, subscriptions, quota, attempts, costs, learning samples, and Stripe events.
- Use Azure Service Bus for asynchronous rewrite jobs.
- Preserve existing product behavior: free quota, paid monthly quota, Stripe checkout/portal/webhook, Naturalness Check, adaptive rewrite quality gates, admin cost dashboard.

Out of scope for the first migration:

- Facebook and Apple login.
- Full Azure App Service migration.
- Deleting existing Cloudflare Worker/Neon data before Azure smoke tests pass.
- Stripe live-mode financial cutover unless the user explicitly requests it after the migration is stable.

## Required User Inputs

Put real values in `/Users/qc/Desktop/CloudFlare/.env.local` or the local environment manager. Do not paste secrets into chat and do not commit them.

Azure subscription/resource values:

```env
AZURE_SUBSCRIPTION_ID=
AZURE_TENANT_ID=
AZURE_LOCATION=australiaeast
AZURE_RESOURCE_GROUP=replyinmyvoice-dev-rg
```

Azure Functions values:

```env
AZURE_FUNCTION_APP_NAME=replyinmyvoice-api-dev
AZURE_FUNCTION_STORAGE_ACCOUNT_NAME=
AZURE_APPLICATION_INSIGHTS_NAME=replyinmyvoice-ai-dev
```

Azure SQL values:

```env
AZURE_SQL_SERVER_NAME=replyinmyvoice-sql-dev
AZURE_SQL_DATABASE_NAME=replyinmyvoice-db-dev
AZURE_SQL_ADMIN_USER=
AZURE_SQL_ADMIN_PASSWORD=
AZURE_SQL_CONNECTION_STRING=
```

Azure Service Bus values:

```env
AZURE_SERVICE_BUS_NAMESPACE=
AZURE_SERVICE_BUS_QUEUE_NAME=reply-rewrite-jobs
AZURE_SERVICE_BUS_CONNECTION_STRING=
```

Microsoft Entra External ID values:

```env
AZURE_EXTERNAL_ID_TENANT_ID=
AZURE_EXTERNAL_ID_TENANT_SUBDOMAIN=
AZURE_EXTERNAL_ID_AUTHORITY=
AZURE_EXTERNAL_ID_FRONTEND_CLIENT_ID=
AZURE_EXTERNAL_ID_API_CLIENT_ID=
AZURE_EXTERNAL_ID_API_AUDIENCE=
AZURE_EXTERNAL_ID_API_SCOPE=
AZURE_EXTERNAL_ID_WELL_KNOWN_URL=
AZURE_EXTERNAL_ID_SIGN_IN_FLOW_NAME=
```

Google federation values:

```env
GOOGLE_CLIENT_ID_FOR_ENTRA=
GOOGLE_CLIENT_SECRET_FOR_ENTRA=
```

Frontend runtime values:

```env
NEXT_PUBLIC_AZURE_API_BASE_URL=
NEXT_PUBLIC_ENTRA_AUTHORITY=
NEXT_PUBLIC_ENTRA_CLIENT_ID=
NEXT_PUBLIC_ENTRA_API_SCOPE=
```

## Dashboard Preparation

### Microsoft Entra External ID

- [ ] Create or confirm an External ID external tenant for Reply In My Voice.
- [ ] Record the tenant ID and tenant subdomain.
- [ ] Create a frontend app registration for the Cloudflare-hosted app shell.
- [ ] Create an API app registration for the Azure Functions API.
- [ ] Expose an API scope from the API app registration.
- [ ] Add the frontend redirect URI:

```text
https://replyinmyvoice.com/auth/callback
```

- [ ] Add the local redirect URI for development:

```text
http://localhost:3000/auth/callback
```

- [ ] Create a sign-up/sign-in user flow.
- [ ] Add the frontend application to that user flow.
- [ ] Ensure issued tokens include stable subject, email, and name/profile claims where available.

### Google Cloud Console

- [ ] Create a Google OAuth web application for Entra External ID federation.
- [ ] Configure the OAuth consent screen with app name, support email, and authorized domain:

```text
replyinmyvoice.com
```

- [ ] Add the exact redirect URI shown by Entra External ID when configuring Google as an identity provider.
- [ ] Store the generated client ID and client secret in `.env.local`.

Important:

```text
Do not reuse the old Clerk Google OAuth redirect URI.
The Google redirect URI must be the one shown by Entra External ID for Google federation.
```

### Stripe

- [ ] Keep the current Stripe webhook until Azure API smoke tests pass.
- [ ] After Azure Functions webhook is deployed, create or update the Stripe webhook endpoint to the Azure API webhook URL.
- [ ] Store the new webhook secret in `.env.local`.

## Task 1: Migration Preflight

**Files:**

- Modify: `/Users/qc/Desktop/CloudFlare/docs/preflight-report.md`
- Modify: `/Users/qc/Desktop/CloudFlare/docs/manual-setup.md`

- [ ] Verify Azure CLI account:

```bash
az account show --query "{subscriptionId:id, tenantId:tenantId, name:name}" -o table
```

- [ ] Verify required local variable names are present without printing secret values.
- [ ] Verify current Git status and branch.
- [ ] Verify existing Cloudflare frontend deployment still serves `replyinmyvoice.com`.
- [ ] Write findings to `docs/preflight-report.md`.

## Task 2: Azure SQL Data Model

**Files:**

- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Domain/Entities/UserAccount.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Domain/Entities/AuthIdentity.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Domain/Entities/Subscription.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteAttempt.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Domain/Entities/UsageReservation.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Domain/Entities/UsagePeriod.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Domain/Entities/StripeEvent.cs`
- Create or modify: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`

- [ ] Define Azure SQL/EF Core models with stable internal `UserAccount.Id`.
- [ ] Use `AuthIdentity` to map `provider = entra_external_id` and stable Entra subject to `UserAccount`.
- [ ] Keep business tables linked to internal `UserAccount.Id`, not email.
- [ ] Add uniqueness constraints:

```text
AuthIdentity(provider, providerSubject)
UserAccount(email) where appropriate
UsagePeriod(userId, periodKey)
StripeEvent(eventId)
RewriteAttempt(userId, idempotencyKey)
```

- [ ] Add indexes needed for quota, Stripe, admin, and learning dashboards.
- [ ] Add EF Core migration.

## Task 3: Azure Functions API

**Files:**

- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/Program.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteFunctions.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeFunctions.cs`
- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminFunctions.cs`

- [ ] Add JWT bearer validation against Entra External ID metadata.
- [ ] Add user upsert from Entra claims.
- [ ] Implement `/api/me`.
- [ ] Implement `/api/rewrite` request intake with quota reservation.
- [ ] Implement `/api/rewrite/{attemptId}` status lookup.
- [ ] Implement Stripe checkout/portal/webhook endpoints.
- [ ] Implement admin endpoints with email/subject allowlist.
- [ ] Configure CORS for:

```text
https://replyinmyvoice.com
https://www.replyinmyvoice.com
http://localhost:3000
```

## Task 4: Azure Service Bus Worker

**Files:**

- Create: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteWorkerFunctions.cs`
- Modify: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`

- [ ] Publish rewrite jobs to Azure Service Bus after quota reservation.
- [ ] Process Service Bus messages by `attemptId`.
- [ ] Add atomic `Pending -> Processing` claim.
- [ ] Finalize usage on success.
- [ ] Release usage on provider failure, timeout, bad JSON, or quality failure.
- [ ] Keep idempotent behavior for duplicate queue delivery.

## Task 5: Frontend Auth Replacement

**Files:**

- Remove or stop using: `/Users/qc/Desktop/CloudFlare/app/sign-in/[[...sign-in]]/page.tsx`
- Remove or stop using: `/Users/qc/Desktop/CloudFlare/app/sign-up/[[...sign-up]]/page.tsx`
- Modify: `/Users/qc/Desktop/CloudFlare/app/layout.tsx`
- Modify: `/Users/qc/Desktop/CloudFlare/app/app/page.tsx`
- Modify: `/Users/qc/Desktop/CloudFlare/components/site-header.tsx`
- Modify: `/Users/qc/Desktop/CloudFlare/components/app/rewrite-workspace.tsx`
- Create: `/Users/qc/Desktop/CloudFlare/lib/entra-auth.ts`
- Create: `/Users/qc/Desktop/CloudFlare/components/auth/entra-google-sign-in.tsx`

- [ ] Remove Clerk provider and Clerk middleware from active runtime.
- [ ] Add Entra External ID browser login.
- [ ] Store/access tokens only through the chosen frontend auth helper.
- [ ] Attach access token to Azure API requests.
- [ ] Replace Clerk user calls with `/api/me` from Azure API.
- [ ] Ensure signed-out users are routed to the new sign-in UI.
- [ ] Ensure Google sign-in is the primary visible option.

## Task 6: Stripe And Quota Preservation

**Files:**

- Modify: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/BillingService.cs`
- Modify: `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`
- Modify: `/Users/qc/Desktop/CloudFlare/backend-dotnet/tests/ReplyInMyVoice.Tests/`

- [ ] Preserve 3 free lifetime successful rewrites.
- [ ] Preserve 40 paid successful rewrites/month.
- [ ] Do not charge validation, auth, payment, provider, quality, or server failures.
- [ ] Stripe webhook processing remains idempotent by event id.
- [ ] Stripe customer and subscription mapping use internal user id plus email metadata.

## Task 7: Deployment

**Files:**

- Create or modify: `/Users/qc/Desktop/CloudFlare/.github/workflows/azure-functions.yml`
- Modify: `/Users/qc/Desktop/CloudFlare/wrangler.jsonc`
- Modify: `/Users/qc/Desktop/CloudFlare/docs/manual-setup.md`

- [ ] Build and test frontend.
- [ ] Build and test Azure Functions.
- [ ] Deploy Azure Functions.
- [ ] Apply EF Core migrations to Azure SQL.
- [ ] Set Azure Function app settings without printing values.
- [ ] Deploy Cloudflare frontend with new API/auth environment.
- [ ] Smoke test:

```text
GET /
GET /pricing
Google sign-in starts
GET Azure /api/me with token
POST Azure /api/rewrite with token
Stripe webhook signature verification
Admin access for ADMIN_EMAILS only
```

## Completion Criteria

- Clerk is no longer required for sign-in/sign-up.
- Neon is no longer required for production runtime data.
- Google sign-in works through Entra External ID.
- Azure Functions validates Entra tokens.
- Azure SQL stores user, usage, subscription, rewrite, cost, and learning data.
- Azure Service Bus processes rewrite jobs idempotently.
- Stripe checkout/portal/webhook works against the Azure backend.
- Admin dashboard remains protected and usable.
- Character limits, quality gates, no-charge failure behavior, and Naturalness Check remain intact.
- GitHub push and production deployment complete only after build/test/smoke checks pass.
