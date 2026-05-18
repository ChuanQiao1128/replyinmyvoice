# .NET Azure Full Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and deploy a complete ASP.NET Core/Azure backend for Reply In My Voice with idempotent rewrite attempts, usage reservations, Stripe webhook replay safety, Service Bus-backed rewrite processing, and CI/CD-ready Azure deployment.

**Architecture:** Add a parallel `.NET` solution under `backend-dotnet/` without deleting the current Next.js/Cloudflare product. The API accepts authenticated rewrite requests, reserves quota transactionally, enqueues a Service Bus job, and exposes attempt status/result endpoints. A worker processes jobs, calls provider adapters, finalizes or releases reservations, and records safe failure states.

**Tech Stack:** ASP.NET Core 8, EF Core 8, Azure SQL, Azure Service Bus, Azure App Service, Azure Key Vault-ready configuration, Application Insights, xUnit, GitHub Actions, Stripe, OpenAI, Sapling.

---

## Files And Responsibilities

- `backend-dotnet/ReplyInMyVoice.sln`: .NET solution.
- `backend-dotnet/src/ReplyInMyVoice.Domain`: entities, enums, DTO-like contracts, domain helpers.
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure`: EF Core DbContext, repositories, quota service, provider adapters, Stripe event service, Service Bus publisher.
- `backend-dotnet/src/ReplyInMyVoice.Api`: HTTP API, ProblemDetails errors, health checks, auth placeholder/JWT boundary, rewrite and webhook endpoints.
- `backend-dotnet/src/ReplyInMyVoice.Worker`: Service Bus processor for rewrite jobs.
- `backend-dotnet/tests/ReplyInMyVoice.Tests`: xUnit tests for idempotency, quota, provider failure, concurrency, webhook replay, and worker finalization.
- `.github/workflows/dotnet-azure.yml`: build/test/deploy workflow.
- `infra/azure/`: Azure CLI/Bicep deployment scripts if needed.
- `docs/dotnet-azure-next-phase.md`: update with final architecture and verification results.

## Task 1: Scaffold Solution

- [ ] Create solution and projects.
- [ ] Add package references for ASP.NET Core, EF Core SQL Server/SQLite, Azure Service Bus, Stripe, xUnit, FluentAssertions, Moq or NSubstitute.
- [ ] Add project references.
- [ ] Run `dotnet build` and confirm the empty solution builds.

## Task 2: Domain Model And DbContext

- [ ] Write failing tests for unique idempotency key, quota period uniqueness, and Stripe event uniqueness.
- [ ] Implement entities: `AppUser`, `UsagePeriod`, `RewriteAttempt`, `UsageReservation`, `StripeEvent`.
- [ ] Implement enums: `RewriteAttemptStatus`, `UsageReservationStatus`, `SubscriptionStatus`.
- [ ] Implement `AppDbContext` with indexes and concurrency tokens.
- [ ] Run tests and build.

## Task 3: Quota Reservation State Machine

- [ ] Write failing tests for successful reservation, quota exhaustion, duplicate idempotency key, provider failure release, and successful finalization.
- [ ] Implement `QuotaService` with transactional reserve/finalize/release methods.
- [ ] Ensure provider calls are outside DB transactions.
- [ ] Run tests and build.

## Task 4: Rewrite API And Attempt Lookup

- [ ] Write failing API-level tests for missing idempotency key, invalid request, unauthenticated request boundary, accepted attempt, duplicate pending attempt, and succeeded retry.
- [ ] Implement `POST /api/rewrite` with `X-Idempotency-Key`.
- [ ] Implement `GET /api/rewrite-attempts/{attemptId}`.
- [ ] Implement ProblemDetails responses.
- [ ] Run tests and build.

## Task 5: Provider Adapters And Worker

- [ ] Write failing tests for OpenAI failure release, Sapling timeout unavailable signal, malformed provider JSON, and worker successful finalization.
- [ ] Implement provider interfaces.
- [ ] Implement deterministic local fake providers for tests/dev.
- [ ] Implement OpenAI/Sapling adapter shells reading from environment.
- [ ] Implement Service Bus job publisher and worker processor.
- [ ] Run tests and build.

## Task 6: Stripe Webhook Replay Safety

- [ ] Write failing tests for duplicate Stripe event, invalid signature, subscription active/trialing update, and subscription deleted/inactive update.
- [ ] Implement raw-body webhook endpoint.
- [ ] Store processed `StripeEvent` IDs idempotently.
- [ ] Update local entitlement state.
- [ ] Run tests and build.

## Task 7: Azure Configuration And CI/CD

- [ ] Add appsettings templates without secrets.
- [ ] Add Azure resource provisioning script for Resource Group, App Service Plan, Web App, Azure SQL, Key Vault, Application Insights, Service Bus namespace, and queue.
- [ ] Add GitHub Actions workflow: restore, build, test, publish, deploy.
- [ ] Add deployment smoke test against `/health`.
- [ ] Run local build/test before provisioning.

## Task 8: Azure Provision And Deploy

- [ ] Load Azure values from `.env.local` without printing secrets.
- [ ] Confirm Azure CLI subscription context.
- [ ] Create/update Azure resources.
- [ ] Apply database migration.
- [ ] Deploy API and worker.
- [ ] Configure App Service settings.
- [ ] Run remote smoke tests.

## Definition Of Done

- `dotnet build backend-dotnet/ReplyInMyVoice.sln` passes.
- `dotnet test backend-dotnet/ReplyInMyVoice.sln` passes.
- Duplicate idempotency key does not double-charge.
- Provider failures release reservations.
- Concurrent requests cannot exceed quota.
- Stripe event replay is idempotent.
- Service Bus worker can finalize or release attempts.
- GitHub Actions workflow is present.
- Azure resource script is present and documented.
- Azure deployment is completed or any external blocker is documented with exact next action.
