# .NET Azure Full Run Result

Date: 2026-05-19

## Summary

The long autonomous .NET/Azure backend run completed against the dev Azure environment.

The deployed dev API is:

```text
https://replyinmyvoice-api-dev.azurewebsites.net
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

The current Azure dev backend is suitable for proving the resume backend claims around idempotency, usage reservations, retry safety, webhook replay safety, Service Bus processing, Azure SQL migrations, and App Service deployment.

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
