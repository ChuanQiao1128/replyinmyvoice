# .NET Azure Full Run Result

Date: 2026-05-19

## Summary

The long autonomous .NET/Azure backend run completed against the dev Azure environment.

The original deployed dev API was:

```text
https://replyinmyvoice-api-dev.azurewebsites.net
```

That Windows B1 App Service backend was deleted on 2026-05-20 to stop fixed monthly run-rate cost. The current low-cost Azure dev backend is:

```text
https://replyinmyvoice-func-dev.azurewebsites.net
```

No Stripe live-mode action, real charge, or production-domain cutover was performed.

## Implemented

- Added a parallel ASP.NET Core 8 backend under `backend-dotnet/`.
- Added EF Core domain persistence for:
  - `AppUser`
  - `UsagePeriod`
  - `RewriteAttempt`
  - `UsageReservation`
  - `StripeEvent`
- Added durable rewrite attempt request storage through `RewriteAttempt.RequestJson`.
- Added quota reservation/finalization/release logic.
- Added expired reservation cleanup.
- Added idempotent rewrite request handling through `(UserId, IdempotencyKey)`.
- Added worker-side idempotency so Service Bus redelivery does not call the provider again after success.
- Added provider failure and malformed provider JSON release paths so failed rewrites do not consume quota.
- Added Azure Service Bus publisher and continuous WebJob worker.
- Added OpenAI and deterministic rewrite provider adapters.
- Added production-safe auth boundary:
  - header-based auth is allowed only in Development/Testing or when `ALLOW_HEADER_AUTH=true`
  - App Service was restored to `ALLOW_HEADER_AUTH=false` after smoke testing
  - Clerk-compatible JWT configuration is supported through `CLERK_JWT_ISSUER`, `CLERK_ISSUER`, or derived issuer from the Clerk publishable key
- Added Stripe checkout and billing portal endpoints.
- Added raw-body Stripe webhook handling with production signature verification.
- Added idempotent Stripe event storage.
- Added Stripe subscription entitlement synchronization for subscription webhook events.
- Added Azure scripts:
  - `infra/azure/provision.sh`
  - `infra/azure/migrate.sh`
  - `infra/azure/deploy.sh`
  - `infra/azure/read-env.sh`
- Added GitHub Actions CI/CD workflow at `.github/workflows/dotnet-azure.yml`.
- Configured GitHub Actions Azure OIDC deployment prerequisites:
  - Azure app registration
  - service principal
  - resource-group Contributor assignment
  - GitHub `main` federated credential
  - GitHub Actions Azure secrets and variables

## Azure Result

Verified dev resources:

- Azure resource group exists.
- Azure App Service Plan exists.
- Azure App Service exists and runs the API.
- Azure SQL Database exists.
- EF Core migrations applied successfully.
- Azure Service Bus namespace exists.
- Azure Service Bus rewrite queue exists.
- Application Insights exists.
- App Service app settings and connection strings are configured without printing secret values.
- API and Worker were deployed as one App Service package, with the Worker under a continuous WebJob path.

## Remote Smoke Tests

Passed:

- `GET /health` returned HTTP `200`.
- `POST /api/rewrite` without auth returned HTTP `401`.
- `POST /api/stripe/webhook` without Stripe signature returned HTTP `400` in production.
- A temporary dev-only queue-backed rewrite smoke test was run with `ALLOW_HEADER_AUTH=true`.
- The remote rewrite attempt moved from `Pending` to `Processing` to `Succeeded`.
- Service Bus queue ended with `0` active messages and `0` dead-letter messages.
- `ALLOW_HEADER_AUTH` was restored to `false` and App Service was restarted.
- Header-only rewrite after reset returned HTTP `401`.

## Local Verification

Passed:

```bash
dotnet build backend-dotnet/ReplyInMyVoice.sln
dotnet test backend-dotnet/ReplyInMyVoice.sln
dotnet publish backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj -c Release
dotnet publish backend-dotnet/src/ReplyInMyVoice.Worker/ReplyInMyVoice.Worker.csproj -c Release
bash -n infra/azure/*.sh
```

Current xUnit result:

```text
25 passed, 0 failed
```

## Tested Failure Modes

Covered by automated tests:

- validation error does not create usage or attempts
- unauthenticated request does not create usage or attempts
- duplicate idempotency key does not create a second reservation
- retry after success returns the same result
- provider failure releases reservation and does not consume quota
- malformed provider JSON releases reservation and does not consume quota
- finalization is idempotent
- distinct requests cannot exceed the available quota slot
- expired reservation cleanup releases quota
- queue redelivery after success does not call provider again
- duplicate Stripe event is processed once
- production webhook rejects missing signature
- subscription webhook updates entitlement state
- checkout and portal HTTP boundaries are covered without calling Stripe from tests

## Intentional Non-Goals

Not done in this run:

- No Stripe live-mode setup.
- No real payment or real charge.
- No production-domain cutover.
- No Cloudflare Pages or DNS cutover.
- No deletion of the existing Next.js/Cloudflare implementation.

## Follow-Up Notes

The GitHub Actions deployment path is configured but will be fully proven when a workflow runs on `main`.

The current Azure dev backend is suitable for proving the resume backend claims around idempotency, usage reservations, retry safety, webhook replay safety, Service Bus processing, Azure SQL migrations, and Azure Functions deployment.

## 2026-05-20 Reliability Gap Closure

Additional backend reliability work was implemented and deployed to the same Azure dev App Service.

Implemented:

- Added `OutboxMessage` persistence and EF migration `AddOutboxAndStripeEventStatus`.
- Moved rewrite queue publishing from request-time direct Service Bus publish to a transactional outbox inserted in the same transaction as `RewriteAttempt` and `UsageReservation`.
- Added `OutboxDispatcherWorker` for due-message dispatch, lock ownership, retry count, and exponential backoff.
- Added atomic `Pending -> Processing` claim behavior so duplicate Service Bus deliveries do not call the rewrite provider twice.
- Added idempotency conflict handling when the same idempotency key is reused with a different request hash.
- Added `ExpiredReservationCleanupWorker` so stale pending reservations are released.
- Changed Stripe webhook event handling from "mark processed before sync" to `Processing -> Processed/Failed`, allowing failed sync attempts to be retried.

Verification:

- .NET tests: 34 passed.
- Azure SQL migration applied successfully to `replyinmyvoice-db-dev`.
- Azure App Service zip deploy completed successfully.
- Remote health check passed at `https://replyinmyvoice-api-dev.azurewebsites.net/health`.

## 2026-05-21 Azure Functions Cost Optimization

The costly Windows B1 App Service runtime was replaced with an Azure Functions consumption runtime while preserving the Azure SQL, Service Bus, outbox, and quota reservation architecture.

Implemented:

- Deleted `replyinmyvoice-api-dev`.
- Deleted the empty `replyinmyvoice-plan-dev` Windows B1 App Service Plan.
- Added `ReplyInMyVoice.Functions` as a .NET 8 isolated worker Function App project.
- Added HTTP trigger functions for health, rewrite creation, rewrite polling, billing, and Stripe webhook handling.
- Added Service Bus trigger function for rewrite job processing.
- Added timer trigger functions for outbox dispatch and expired reservation cleanup.
- Added Azure Functions provision/deploy scripts.
- Updated `.github/workflows/dotnet-azure.yml` to publish and deploy the Function App instead of an App Service/WebJob package.
- Registered `Microsoft.Storage` because Azure Functions requires a storage account.
- Created Function App `replyinmyvoice-func-dev` on Linux consumption plan `Y1`.
- Reused `replyinmyvoice-ai-dev` for Application Insights and deleted the duplicate auto-created `replyinmyvoice-func-dev` Application Insights component.

Current retained Azure resources:

```text
Azure SQL Basic: replyinmyvoice-db-dev
Azure Service Bus Basic: replyinmyvoice-sb-dev
Key Vault: replyinmyvoice-kv-dev
Application Insights: replyinmyvoice-ai-dev
Storage Account: replyinmyvoicefuncdev
Function App: replyinmyvoice-func-dev
Linux Dynamic Consumption Plan: AustraliaEastLinuxDynamicPlan
```

Verification:

```text
dotnet build ReplyInMyVoice.sln: passed
dotnet test ReplyInMyVoice.sln: 34 passed
GET https://replyinmyvoice-func-dev.azurewebsites.net/api/health: passed
Remote rewrite smoke: Pending -> Processing -> Succeeded
```

Cost posture:

```text
Windows B1 App Service fixed run-rate removed.
Azure Functions consumption runtime expected to be near zero at low traffic.
Azure SQL Basic remains the main predictable Azure dev cost.
```

## 2026-05-23 Production Backend Cutover Completion

The live Cloudflare Worker now treats Azure Functions as the backend runtime and Azure SQL as the account/quota database for public app flows.

Implemented:

- Added Azure `/api/me` account summary backed by EF Core/Azure SQL.
- Added Azure Functions `/api/me` and `/api/health/db`.
- Added Entra Bearer-token validation in Functions; production header-only auth is disabled.
- Updated Cloudflare `/app` to read account, quota, and billing state from Azure instead of Neon.
- Updated rewrite workspace, checkout, portal, DB health, and Stripe webhook paths to call or proxy Azure Functions.
- Disabled the unfinished API-key management runtime path until it has an Azure-backed data model.
- Deployed Azure Functions to `https://replyinmyvoice-func-dev.azurewebsites.net`.
- Deployed Cloudflare Worker `replyinmyvoice-app` to `replyinmyvoice.com` and `www.replyinmyvoice.com`.

Verification:

```text
dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore: 47 passed
npm run lint: passed
npm run typecheck: passed
npm run test: 48 files / 313 tests passed
dotnet publish API/Worker/Functions Release: passed
PATH=/usr/local/bin:$PATH NEXT_DIST_DIR=.next-build npm run build: passed
PATH=/usr/local/bin:$PATH npm run cf:build: passed
infra/azure/functions-provision.sh: passed
infra/azure/migrate.sh: database already up to date
infra/azure/functions-deploy.sh: passed
PATH=/usr/local/bin:$PATH npm run cf:deploy: deployed version 19086619-cd41-48e9-bb8d-d64deaa76dd1
```

Remote smoke:

```text
GET Azure /api/health: 200
GET Azure /api/health/db: 200, {"ok":true,"database":"azure-sql"}
GET Azure /api/me without auth: 401
POST Azure /api/rewrite without auth: 401
OPTIONS Azure /api/rewrite from replyinmyvoice.com: 204 with authorization/content-type/x-idempotency-key allowed
GET replyinmyvoice.com/: 200
GET replyinmyvoice.com/pricing: 200
GET replyinmyvoice.com/app without auth: 307 to /sign-in
GET replyinmyvoice.com/api/health/db: 200, Azure SQL health
POST replyinmyvoice.com/api/rewrite without session: 401
GET replyinmyvoice.com/api/stripe/webhook: 200, backend azure-functions
```

Runtime settings confirmed:

```text
ALLOW_HEADER_AUTH=false
OPENAI_BASE_URL=https://api.deepseek.com
NEXT_PUBLIC_ENTRA_API_SCOPE=api://1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32/access_as_user
ENTRA_API_AUDIENCE=api://1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32
```

Limitations:

- A live signed-in Entra browser rewrite was not run from this Codex session because no authenticated user access token was available.
- Stripe dashboard endpoint settings were not changed directly; the existing Cloudflare webhook route now proxies events to Azure Functions for compatibility.
- The repository still contains legacy Prisma/Neon helper modules and tests, but public Cloudflare app routes no longer call them.
