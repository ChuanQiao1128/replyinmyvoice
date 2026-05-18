# .NET Azure Next Phase Development Brief

Date: 2026-05-18

## Purpose

Build the next version of Reply In My Voice as a C#/.NET backend that supports the resume positioning:

```text
C#/.NET metered SaaS backend for failure-safe AI rewrite workflows.
```

The technical focus is not the writing product alone. The focus is correctness under real backend failure modes:

- retries
- duplicate submissions
- network disconnects
- provider timeouts and malformed provider responses
- quota exhaustion
- concurrent requests
- Stripe webhook replay
- delayed subscription state updates

## Current State

The current working product is implemented as:

- Next.js App Router
- TypeScript
- Cloudflare Workers/OpenNext
- Neon Postgres
- Prisma schema/migrations
- Clerk
- Stripe sandbox
- OpenAI
- Sapling Naturalness Check

Current code already has useful behavior to preserve:

- server-side quota enforcement
- usage charged only after successful rewrite
- provider failure does not consume usage
- Stripe webhook signature verification
- `StripeEvent` idempotency
- Naturalness Check fallback when Sapling is unavailable
- docs and evaluation notes for rewrite quality

The next phase should either build a parallel .NET backend or migrate the backend slice-by-slice. Do not delete the working Next.js/Cloudflare app until the .NET backend has feature and failure-mode parity.

## Resume-Aligned Deliverables

The implementation should make these resume bullets truthful:

```text
Designed an ASP.NET Core Web API with clear service boundaries for rewrite orchestration, quota accounting, Stripe billing entitlements, provider adapters, authentication, webhook processing, and standardized ProblemDetails error handling.

Engineered an idempotent rewrite pipeline using request keys, usage reservations, EF Core transactions, row-version concurrency checks, and attempt state tracking so retries, network failures, duplicate submissions, provider errors, and concurrent requests do not double-charge or incorrectly consume quota.

Built a custom AI Agent Studio with reusable Claude Code and Codex skills for requirements-to-system planning, resilience test generation, and state-machine/data-model review, routing planning-heavy work to Claude and code/test execution loops to Codex to improve delivery speed and failure-mode coverage.
```

## Recommended Architecture

### Phase 1: Keep It Proveable

Use:

- ASP.NET Core Web API
- EF Core
- Azure SQL Database
- xUnit
- Stripe sandbox
- OpenAI provider adapter
- Sapling provider adapter
- Application Insights
- GitHub Actions
- Azure App Service

Do not introduce Azure Service Bus in the first implementation unless the synchronous API becomes hard to keep reliable. The first goal is to prove the quota, idempotency, transaction, and failure tests.

### Phase 2: Add Async Resilience

Add only after Phase 1 passes:

- Azure Service Bus queue
- background worker or Azure Function
- dead-letter queue handling
- bounded retry policy
- job status polling endpoint

This makes sense if rewrite latency, provider instability, or concurrency pressure justifies moving provider calls out of the API request path.

## Core API Shape

### Rewrite Request

```http
POST /api/rewrite
X-Idempotency-Key: <uuid>
Content-Type: application/json
```

Request body:

```json
{
  "messageToReplyTo": "optional incoming message",
  "roughDraftReply": "required draft",
  "audience": "optional",
  "purpose": "optional",
  "whatHappened": "optional",
  "factsToPreserve": "optional",
  "tone": "warm"
}
```

Response outcomes:

- `200 OK` with existing or newly completed result when synchronous processing succeeds.
- `202 Accepted` if Phase 2 async worker is enabled and the attempt is still processing.
- `400 Bad Request` for validation errors. No usage charge.
- `401 Unauthorized` for auth failures. No usage charge.
- `402 Payment Required` when no quota is available.
- `409 Conflict` if the same idempotency key is pending and the API chooses not to block.
- `422 Unprocessable Entity` if quality gates reject all candidates. No usage charge.
- `500/502/504` for provider/server failures. No usage charge.

### Attempt Lookup

```http
GET /api/rewrite-attempts/{attemptId}
```

Used by the frontend after network disconnect, page refresh, async processing, or duplicate click handling.

## Data Model Targets

### User

Minimum fields:

- `Id`
- `ExternalAuthUserId`
- `Email`
- `StripeCustomerId`
- `StripeSubscriptionId`
- `SubscriptionStatus`
- `CurrentPeriodEnd`
- `CreatedAt`
- `UpdatedAt`

### UsagePeriod

Tracks committed and reserved usage for a free lifetime period or paid billing period.

Fields:

- `Id`
- `UserId`
- `PeriodKey`
- `QuotaLimit`
- `UsedCount`
- `ReservedCount`
- `PeriodStart`
- `PeriodEnd`
- `RowVersion`
- `CreatedAt`
- `UpdatedAt`

Constraints:

- unique `(UserId, PeriodKey)`
- index `(UserId, PeriodKey)`
- `RowVersion` concurrency token

### RewriteAttempt

Tracks one user-visible rewrite request.

Fields:

- `Id`
- `UserId`
- `IdempotencyKey`
- `RequestHash`
- `Status`
- `ResultJson`
- `ErrorCode`
- `ErrorMessage`
- `CreatedAt`
- `CompletedAt`
- `ExpiresAt`
- `RowVersion`

Statuses:

- `Pending`
- `Processing`
- `Succeeded`
- `Failed`
- `Released`
- `Expired`

Constraints:

- unique `(UserId, IdempotencyKey)`
- index `(UserId, CreatedAt)`
- index `(Status, ExpiresAt)`

### UsageReservation

Tracks a temporary quota hold that can be finalized or released.

Fields:

- `Id`
- `UserId`
- `UsagePeriodId`
- `RewriteAttemptId`
- `Status`
- `ExpiresAt`
- `CreatedAt`
- `FinalizedAt`
- `ReleasedAt`
- `RowVersion`

Statuses:

- `Pending`
- `Finalized`
- `Released`
- `Expired`

Constraints:

- unique `(RewriteAttemptId)`
- index `(UserId, Status)`
- index `(Status, ExpiresAt)`

### StripeEvent

Stores processed external billing events.

Fields:

- `EventId`
- `Type`
- `ProcessedAt`
- `CreatedAt`

Constraints:

- primary key or unique index on `EventId`

## Rewrite Accounting Rules

The system should guarantee:

1. A successful rewrite consumes quota at most once.
2. Validation, auth, payment, provider, parsing, quality-gate, and server failures do not consume quota.
3. Reusing the same `X-Idempotency-Key` returns the same attempt state or result.
4. Concurrent requests cannot exceed quota.
5. Pending reservations expire and can be released.
6. Stripe webhook replays do not corrupt subscription state.

The preferred transaction flow:

```text
1. Start short transaction.
2. Validate entitlement and quota.
3. Insert or load RewriteAttempt by (UserId, IdempotencyKey).
4. If attempt already succeeded, return stored result.
5. If attempt is pending/processing, return current state.
6. Create UsageReservation if there is quota.
7. Increment ReservedCount, not UsedCount.
8. Commit transaction.
9. Call OpenAI/Sapling outside the transaction.
10. On success, finalize reservation: UsedCount +1, ReservedCount -1, Attempt = Succeeded.
11. On failure, release reservation: ReservedCount -1, Attempt = Failed/Released.
```

Provider calls must never run inside a long database transaction.

## Required Tests

Use xUnit with provider mocks and database-backed integration tests.

Minimum failure-mode coverage:

- validation error does not consume quota
- unauthenticated request does not consume quota
- exhausted free quota returns `402`
- OpenAI failure releases reservation and does not consume quota
- Sapling timeout returns rewrite with unavailable signal when rewrite itself succeeds
- provider JSON parse failure releases reservation and does not consume quota
- duplicate idempotency key does not double-charge
- duplicate idempotency key after success returns the same result
- duplicate idempotency key while pending returns processing/conflict state
- two concurrent requests with one quota remaining allow only one reservation
- expired reservation can be released
- duplicate Stripe event is processed once
- invalid Stripe webhook signature returns `400`
- subscription update changes database entitlement state once

## AI Agent Studio Skills

Create reusable skills in the Studio, not one-off project scripts.

Recommended Studio location:

```text
/Users/qc/Desktop/AI-Agent-Studio/
  skills/
    requirements-to-system-plan/
      SKILL.md
      output-schema.md
      examples/
    resilience-test-writer/
      SKILL.md
      dotnet-profile.md
      examples/
    state-machine-and-data-model-review/
      SKILL.md
      review-checklist.md
      examples/
  profiles/
    claude-code.md
    codex.md
  projects/
    reply-in-my-voice/
      project-context.md
      skill-runs/
```

If the Studio repo/folder does not exist yet, create it separately from `/Users/qc/Desktop/CloudFlare`. Keep generic skills outside the product repo so they can be reused on future projects.

### Skill 1: requirements-to-system-plan

Primary executor: Claude.

Codex role: scan repo files and summarize discovered implementation facts.

Inputs:

- `AGENTS.md`
- requirements markdown
- existing repo file map
- target stack profile

Outputs:

- API endpoints
- DTOs
- service boundaries
- database models
- state list
- error states
- acceptance tests
- deployment assumptions

Use this before implementation begins.

### Skill 2: resilience-test-writer

Primary executor: Codex.

Claude role: generate the failure matrix and review test gaps.

Inputs:

- system plan
- existing test project
- provider interfaces
- target framework profile

Outputs:

- xUnit tests
- mocked provider failures
- idempotency tests
- concurrency tests
- quota accounting tests
- webhook replay tests when external events exist

Use this before or during implementation so failure behavior drives the design.

### Skill 3: state-machine-and-data-model-review

Primary executor: Claude.

Codex role: verify the result against EF Core models, migrations, and tests.

Inputs:

- EF Core models
- migration files
- rewrite flow
- billing flow
- quota rules

Outputs:

- state transition table
- invalid transition list
- missing status warnings
- unique constraint recommendations
- index recommendations
- row-version/concurrency recommendations
- required regression tests

Use this before finalizing schema and before claiming the resume bullet is true.

### Optional Later Skill: cloud-readiness-review

This is useful, but it is not part of the first Studio milestone.

Primary executor: Codex.

Claude role: write deployment runbook and summarize manual blockers.

Automatable checks:

- `dotnet build`
- `dotnet test`
- secret scan
- required environment variable names
- health endpoint
- CORS origins
- migration command
- logging and telemetry config
- GitHub Actions workflow
- Azure App Service settings
- Key Vault references

Manual blockers:

- Azure subscription creation
- billing approval
- domain/DNS cutover
- Stripe live-mode approval
- Clerk dashboard setup

Do not describe this as fully automated cloud deployment unless those manual blockers have actually been automated or completed.

## Secret And Local Environment Rules

Never put secret values in markdown, git commits, or chat.

Local secret values should go only in:

```text
/Users/qc/Desktop/CloudFlare/.env.local
```

Production secret values should go in:

- Azure App Service configuration
- Azure Key Vault
- GitHub Secrets or OIDC-backed deployment config

This document records names and locations only.

## Future Inputs To Request From User

Ask for these only when the .NET/Azure build or deployment phase actually needs them.

Azure identity and deployment:

- Azure subscription ID
- Azure tenant ID
- preferred Azure region
- resource group name
- budget limit confirmation
- whether paid resource creation is allowed
- Azure CLI login or service principal/OIDC setup

Azure resource names:

- App Service name
- App Service Plan name
- Azure SQL server name
- Azure SQL database name
- Key Vault name
- Application Insights name
- optional Service Bus namespace and queue name

Application secrets:

- OpenAI API key
- Sapling API key
- Stripe secret key
- Stripe webhook signing secret
- Stripe price ID
- Clerk configuration if the .NET backend handles auth directly

Do not ask the user to paste secret values into markdown. Use `.env.local`, Azure Key Vault, or GitHub Secrets.

## Suggested Implementation Order

1. Create a separate `.NET` backend project folder or branch.
2. Add ASP.NET Core Web API skeleton with health endpoint and ProblemDetails.
3. Add EF Core models for `User`, `UsagePeriod`, `RewriteAttempt`, `UsageReservation`, and `StripeEvent`.
4. Write failing xUnit tests for quota, idempotency, concurrency, and provider failures.
5. Implement quota reservation and finalization services.
6. Implement provider adapter interfaces and mocked providers.
7. Implement rewrite endpoint with `X-Idempotency-Key`.
8. Implement Stripe webhook endpoint with raw body signature verification and event idempotency.
9. Add Application Insights logging and correlation IDs.
10. Run full test suite and fix gaps.
11. Deploy to Azure App Service only after local tests pass.
12. Add Azure Service Bus only if async processing is needed after the synchronous version is correct.

## Definition Of Done

The next phase is complete when:

- `dotnet build` passes
- `dotnet test` passes
- idempotency tests pass
- quota failure tests pass
- concurrency tests pass
- provider failure tests pass
- Stripe webhook replay tests pass
- Azure App Service deployment smoke test passes, if deployment is in scope
- no secret values are committed or written to markdown
- the three resume bullets above are backed by code, tests, and docs
