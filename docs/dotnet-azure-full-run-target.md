# .NET Azure Full Autonomous Run Target

Date: 2026-05-19

## Purpose

This document defines the next long autonomous development run for **Reply In My Voice**.

The run is not a planning-only pass and should not be split into "first stage" and "later stage" implementation. Once the user explicitly starts the long run, the agent should continue until the full C#/.NET Azure backend target is implemented, verified, deployed, or blocked by one of the stop conditions below.

This document is the target for the next autonomous run. It is not evidence that the work is already complete.

## Primary Goal

Build and deploy a production-shaped C#/.NET backend for Reply In My Voice that proves the resume-level technical claim:

```text
A failure-safe metered SaaS backend where rewrite requests remain correct under retries, duplicate clicks, provider failures, network disconnects, concurrent requests, Stripe webhook replay, delayed subscription updates, queue redelivery, and Azure deployment/runtime failures.
```

The strongest technical value is not "ASP.NET Core layered architecture" by itself. The strongest value is that the system keeps quota, attempt state, billing entitlement, and rewrite results correct when real failure modes happen.

## Required Architecture Target

The long run should build the .NET/Azure backend using:

- ASP.NET Core Web API
- EF Core
- Azure SQL Database
- Azure Service Bus
- Background worker or WebJob for queued rewrite processing
- Azure App Service
- Application Insights
- Stripe sandbox billing and webhook processing
- Clerk-compatible production authentication boundary
- OpenAI provider adapter
- Sapling or writing-signal provider adapter where applicable
- GitHub Actions CI/CD preparation
- xUnit tests for correctness and resilience

Do not remove or break the existing Next.js/Cloudflare implementation unless the user explicitly asks for a migration/cutover. The .NET backend can be built as a parallel backend until feature and failure-mode parity is proven.

## Non-Negotiable Backend Invariants

The backend must enforce these rules server-side:

- A successful user-visible rewrite consumes quota at most once.
- Validation errors do not consume quota.
- Authentication failures do not consume quota.
- Payment or entitlement failures do not consume quota.
- OpenAI failures do not consume quota.
- Sapling/writing-signal failures do not consume quota.
- JSON parse failures do not consume quota.
- Server failures do not consume quota unless a successful rewrite result was already committed.
- Duplicate requests with the same user and idempotency key do not double-charge.
- A retry after network disconnect can return the already completed result.
- Two concurrent requests cannot let a user exceed quota.
- Stripe webhook replay does not process the same Stripe event twice.
- Queue redelivery does not double-finalize usage.
- Pending reservations can expire or be released safely.

## Required Data Model

The implementation must include durable persistence for at least:

- `AppUser`
- `UsagePeriod`
- `RewriteAttempt`
- `UsageReservation`
- `StripeEvent`

The data model must support:

- unique user identity mapping from the auth provider
- unique `(UserId, IdempotencyKey)` for rewrite attempts
- unique `(UserId, PeriodKey)` for quota periods
- unique Stripe event IDs
- optimistic concurrency or equivalent transaction protection for usage periods
- attempt status tracking
- reservation status tracking
- persisted request data or enough durable job payload to complete queued rewrites after API response, network disconnect, or worker restart
- persisted result data so idempotent retries can return the same rewrite result without calling providers again

## Required Rewrite Flow

The rewrite API must follow this shape:

1. Authenticate the user.
2. Validate request input.
3. Require and validate `X-Idempotency-Key`.
4. Load or create the local user record.
5. Resolve entitlement and quota period.
6. In a short database transaction:
   - return existing attempt result if the idempotency key already succeeded
   - return pending/processing state if the idempotency key already exists but is not complete
   - reserve one quota unit if quota is available
   - create a durable rewrite attempt and reservation
7. Commit the transaction before calling OpenAI/Sapling.
8. Publish a rewrite job to Azure Service Bus or process through the configured async worker path.
9. Worker processes the job with bounded retries and provider timeouts.
10. On provider success, finalize the reservation and mark the attempt `Succeeded` in a short transaction.
11. On provider failure, malformed JSON, timeout, quality-gate failure, or server failure before success, release the reservation and mark the attempt failed without consuming quota.
12. Idempotent retry returns the existing succeeded result or current processing state.

The provider call must not hold a long database lock.

## Required Stripe Flow

Stripe billing must include:

- Checkout creation for authenticated users.
- Billing portal creation for authenticated users with a Stripe customer.
- Webhook raw body reading.
- Stripe signature verification in production.
- Local/test fallback only outside production.
- Durable `StripeEvent` storage by Stripe event ID.
- Idempotent replay handling.
- Subscription status synchronization into the local database.
- Entitlement calculation from local database state, not client-side state.

Webhook events to support:

- `checkout.session.completed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`

## Required Azure Flow

The long run must attempt to complete Azure end to end:

- Confirm Azure CLI login and subscription selection.
- Provision or verify the configured resource group.
- Provision or verify Azure App Service Plan.
- Provision or verify Azure App Service.
- Provision or verify Azure SQL Database.
- Provision or verify Azure Service Bus namespace and rewrite queue.
- Provision or verify Key Vault or App Service secret/app-setting storage.
- Provision or verify Application Insights.
- Apply EF Core migrations to Azure SQL.
- Deploy the ASP.NET Core API to Azure App Service.
- Deploy the worker as a WebJob or equivalent App Service background process.
- Configure App Service connection strings and app settings without printing secret values.
- Verify the remote `/health` endpoint.
- Verify at least one remote auth/validation failure path.
- Verify queue-backed rewrite processing where credentials and providers allow it.

If a resource already exists, reuse it. Do not create duplicate resources just because a prior command partially succeeded.

## Required CI/CD Flow

The repository must include GitHub Actions or equivalent CI/CD preparation that can:

- restore .NET dependencies
- build the solution
- run xUnit tests
- publish the API
- publish/package the worker
- deploy to Azure App Service when required Azure/GitHub credentials are configured

If GitHub OIDC, service principal, repository secrets, or dashboard-only setup is blocked by permissions, document the exact remaining manual setup. Continue completing all local and Azure CLI work that can be completed safely.

## Required Tests

The long run must add or preserve tests for:

- validation error does not consume quota
- unauthenticated request does not consume quota
- provider failure does not consume quota
- malformed provider JSON does not consume quota
- Sapling/writing-signal timeout does not consume quota if the rewrite fails
- duplicate idempotency key does not double-charge
- retry after success returns the same result
- two concurrent requests with one quota remaining allow only one reservation/finalization
- queue redelivery does not double-finalize usage
- expired/pending reservation can be released
- duplicate Stripe event is processed once
- invalid Stripe signature is rejected in production
- subscription update changes entitlement state correctly

## Required Verification Commands

Before reporting completion, run and report the result of:

```bash
dotnet build backend-dotnet/ReplyInMyVoice.sln
dotnet test backend-dotnet/ReplyInMyVoice.sln
dotnet publish backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj -c Release
dotnet publish backend-dotnet/src/ReplyInMyVoice.Worker/ReplyInMyVoice.Worker.csproj -c Release
bash -n infra/azure/*.sh
```

If frontend code is changed during the run, also run the relevant existing Next.js verification commands from `package.json`.

## Remote Completion Criteria

The run is complete only when one of these is true:

1. Full success:
   - local build passes
   - local tests pass
   - API publish passes
   - worker publish passes
   - Azure resources are verified
   - EF Core migrations are applied to Azure SQL
   - Azure App Service deployment succeeds
   - worker/WebJob deployment succeeds
   - remote `/health` succeeds
   - remote failure-path smoke test succeeds
   - remaining manual items, if any, are documented

2. Hard blocker:
   - one of the stop conditions below is hit
   - the blocker is documented with exact command/context
   - all non-blocked local work has still been completed and verified

Do not stop only because of ordinary build errors, test failures, package restore issues, Azure CLI syntax issues, migration errors, App Service deployment errors, Service Bus setup errors, or worker packaging problems. Investigate, fix, and continue.

## Stop Conditions

Stop and ask the user only if:

- Azure permission denies resource creation, migration, or deployment and no CLI workaround is available.
- GitHub push, GitHub Actions setup, or repository configuration is denied by permissions.
- A required secret is missing, invalid, or cannot be derived from existing local configuration.
- A real paid/live-mode financial action is required.
- A production domain cutover is required.
- Continuing would expose, print, commit, or otherwise leak secrets.
- The user explicitly interrupts or pauses the run.

If a dashboard-only step is required, document it and continue with everything else that can be done locally or through CLI.

## Secret Handling

Never print, quote, summarize, commit, or expose:

- `.env.local` secret values
- Azure SQL passwords
- Service Bus connection strings
- Stripe secret keys or webhook secrets
- OpenAI API keys
- Sapling API keys
- Clerk secret keys
- Cloudflare API tokens
- Key Vault secret values

Commands may check whether values are present, but output must be redacted or limited to boolean/status information.

## Agent Studio And Skill Requirement

This run should demonstrate the project's reusable AI Agent Studio workflow.

Use project or Codex/Claude skills where they match the task:

- `system-spec-synthesis` for turning requirements into implementation-ready system plans.
- `resilience-test-generation` for retry, timeout, quota, webhook replay, Service Bus redelivery, and provider failure tests.
- `state-machine-modeling` for rewrite attempt, usage reservation, subscription, webhook, and queue job state transitions.
- `data-module-review` for EF Core models, migrations, indexes, transactions, and persistence invariants.
- `claude-heavy-planning-handoff` for broad planning that spans Azure/backend/auth/billing/queue architecture.

Planning-heavy tasks may be routed to Claude Code. Code edits, test execution, build-fix loops, local verification, Azure CLI execution, and deployment work are suitable for Codex.

## Resume Alignment

When the run is complete, these resume claims should be truthful:

```text
Designed an ASP.NET Core Web API with clear service boundaries for rewrite orchestration, quota accounting, Stripe billing entitlements, provider adapters, authentication, webhook processing, and standardized ProblemDetails error handling.

Engineered an idempotent rewrite pipeline using request keys, usage reservations, EF Core transactions, row-version concurrency checks, Azure Service Bus jobs, and attempt state tracking so retries, network failures, duplicate submissions, provider errors, queue redelivery, and concurrent requests do not double-charge or incorrectly consume quota.

Built a custom AI Agent Studio with reusable Claude Code and Codex skills for requirements-to-system planning, resilience test generation, state-machine/data-model review, and build-fix-deploy loops, routing planning-heavy work to Claude and code/test/deployment execution to Codex.
```

Do not claim any of these as complete until the implementation, tests, and deployment evidence support them.
