# Resume Alignment Gap Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining gaps between the current Reply In My Voice implementation and the resume claims around C#/.NET backend reliability, transactional quota accounting, Azure Service Bus resilience, Stripe webhook safety, reusable AI Agent Studio skills, and production deployment evidence.

**Architecture:** Keep the live Next.js/Cloudflare product stable while hardening the parallel ASP.NET Core/Azure backend. The `.NET` backend should use Azure SQL as the transactional source of truth, a transactional outbox for reliable Service Bus publishing, a continuous worker/WebJob for outbox dispatch and queue processing, and explicit state transitions for rewrite attempts, reservations, outbox messages, and Stripe events.

**Tech Stack:** ASP.NET Core, EF Core, Azure SQL, Azure Service Bus, Azure App Service continuous WebJob, xUnit, Stripe sandbox, OpenAI/Sapling provider adapters, Next.js/Cloudflare, Codex/Claude reusable skills.

---

## Purpose

This is the master checklist for the next long development run. It records what must be implemented or verified before the strongest resume wording is fully supported by code, tests, and deployment evidence.

Detailed child plans:

- `docs/superpowers/plans/2026-05-20-dotnet-backend-reliability-fixes.md`
- `docs/superpowers/plans/2026-05-20-clean-final-quality-gate.md`

This file should be updated again if the user adds more required changes before the long run starts.

## Resume Claims Being Aligned

Target claims:

1. ASP.NET Core Web API with clear service boundaries for HTTP contracts, quota accounting, billing events, provider adapters, authentication, and ProblemDetails error handling.
2. Idempotent rewrite-request pipeline using request keys, usage reservations, EF Core transactions, row-version/concurrency checks, Azure Service Bus jobs, and attempt state tracking.
3. Transactional outbox-backed queue publishing so a database commit cannot lose a rewrite job when Service Bus publish fails.
4. Stripe billing integration with raw-body webhook signature verification, idempotent event storage, retry-safe subscription updates, and database-backed entitlement synchronization.
5. Reusable AI Agent Studio / Claude Code / Codex workflow for requirements planning, resilience tests, state-machine/data-model review, build-fix loops, and deployment preparation.
6. Deployment evidence through GitHub CI/CD, Cloudflare production app verification, and Azure dev backend verification.

## Current Reality Snapshot

### What Is Already True

- The live product is deployed on `https://replyinmyvoice.com` through Next.js/Cloudflare.
- A parallel `.NET` backend exists under `backend-dotnet/`.
- The `.NET` backend includes `AppUser`, `UsagePeriod`, `RewriteAttempt`, `UsageReservation`, and `StripeEvent`.
- The `.NET` backend has quota reservation, finalization, release, provider-failure, malformed-JSON, and Stripe webhook replay tests.
- Azure dev deployment evidence exists in `docs/dotnet-azure-full-run-result.md`.
- Reusable local skills exist under `agent-skills/` and `/Users/qc/.codex/skills/`.

### What Is Not Safe To Claim Yet

- The `.NET` backend is not currently build-clean because duplicate `* 2.cs` files are present.
- The live production website is not currently served by the `.NET` backend; it is served by the Next.js/Cloudflare Worker.
- Queue publishing is not yet protected by a transactional outbox.
- Same idempotency key with a different request body is not yet rejected with `409`.
- Worker claim is not yet strictly atomic.
- Expired reservation cleanup exists as a method, but no scheduled runner is confirmed.
- Stripe webhook event storage is idempotent, but partial failure between event insert and entitlement sync is not fully retry-safe.
- AI Agent Studio needs a clearer artifact/runbook if the resume says “custom AI Agent Studio.”
- The rewrite engine still needs a clean-final gate for internal-analysis/meta-language leakage.

## Long Run Priority Order

Do the work in this order. The first two are blocking because they affect every later `.NET` backend claim.

1. Restore `.NET` buildability by merging/removing `* 2.cs` files.
2. Implement transactional outbox for Azure Service Bus publishing.
3. Add atomic worker claim.
4. Add idempotency-key request-hash conflict handling.
5. Add expired reservation cleanup runner.
6. Make Stripe webhook processing retry-safe across partial failures.
7. Verify and redeploy Azure dev backend.
8. Package AI Agent Studio evidence.
9. Implement clean-final rewrite quality gate and redeploy Cloudflare app.
10. Re-check resume wording against actual deployment architecture.

## Task 1: Restore `.NET` Buildability

**Why this comes first:** Outbox work must not start while duplicate temporary files are being compiled. Otherwise errors from unfinished files and outbox changes will be mixed.

**Files to inspect:**

- `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`
- `backend-dotnet/src/ReplyInMyVoice.Api/Program 2.cs`
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions 2.cs`
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider.cs`
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider 2.cs`
- `backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs`
- `backend-dotnet/src/ReplyInMyVoice.Worker/Program 2.cs`
- `backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker.cs`
- `backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker 2.cs`

- [ ] **Step 1: Diff temporary files against canonical files**

```bash
diff -u "backend-dotnet/src/ReplyInMyVoice.Api/Program.cs" "backend-dotnet/src/ReplyInMyVoice.Api/Program 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs" "backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider.cs" "backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs" "backend-dotnet/src/ReplyInMyVoice.Worker/Program 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker.cs" "backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker 2.cs" || true
```

- [ ] **Step 2: Merge intended changes**

Known likely intended changes:

- `Program 2.cs` appears to include Clerk-compatible JWT auth, Stripe checkout/portal/webhook endpoints, and production-safe header auth.
- `ServiceCollectionExtensions 2.cs` appears to fix `Func<AppDbContext>` so it creates a fresh context instead of returning the scoped one.
- `OpenAiRewriteProvider 2.cs` appears to be the intended provider implementation.

Merge intentionally. Keep one canonical file per class.

- [ ] **Step 3: Delete temporary duplicates**

```bash
rm "backend-dotnet/src/ReplyInMyVoice.Api/Program 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Worker/Program 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker 2.cs"
```

- [ ] **Step 4: Verify build baseline**

```bash
dotnet build backend-dotnet/ReplyInMyVoice.sln
```

Expected: build passes, or only real implementation errors remain.

## Task 2: Implement Transactional Outbox With Azure Service Bus

**Why this matters:** The current API writes `RewriteAttempt` and `UsageReservation` in Azure SQL, then publishes to Service Bus after the DB commit. If Service Bus publish fails after the DB commit, the rewrite job can be lost while quota remains reserved. Transactional outbox fixes that.

**Target flow:**

```text
POST /api/rewrite
  -> Azure SQL transaction
     - create RewriteAttempt
     - create UsageReservation
     - increment UsagePeriod.ReservedCount
     - create OutboxMessage(RewriteJobCreated)
  -> return 202 Accepted

OutboxDispatcherWorker
  -> poll due pending OutboxMessages from Azure SQL
  -> claim row using Processing + LockedUntil + LockedBy
  -> publish Azure Service Bus message
  -> mark Sent or schedule retry with backoff

ServiceBusRewriteWorker
  -> receive AttemptId from Azure Service Bus
  -> RewriteJobProcessor
  -> atomic attempt claim
  -> provider call
  -> finalize or release reservation
```

**Azure-specific design:**

- Azure SQL is the durable outbox store.
- Azure Service Bus is the delivery mechanism after the outbox row exists.
- Worker runs as the existing continuous WebJob / worker process in Azure App Service.
- Dispatcher should be safe if multiple worker instances run.
- Tests must use fakes and local SQLite; they must not hit live Azure Service Bus.
- Azure Service Bus duplicate detection can be enabled as an extra defense, but correctness must not rely on it.

**Outbox state machine:**

```text
Pending -> Processing: dispatcher claims due unlocked message
Processing -> Sent: Service Bus send succeeds
Processing -> Pending: send fails and attempts remain
Processing -> Failed: send fails and AttemptCount reaches MaxAttempts
Processing -> Pending: lock expires and another dispatcher reclaims
Sent -> Sent: never resend
Failed -> Failed: no automatic resend; manual reset only
```

**Outbox entity:**

```text
Id: Guid
MessageType: string
PayloadJson: string
Status: Pending | Processing | Sent | Failed
CreatedAt: DateTimeOffset
NextAttemptAt: DateTimeOffset
AttemptCount: int
MaxAttempts: int
LockedUntil: DateTimeOffset?
LockedBy: string?
SentAt: DateTimeOffset?
LastAttemptAt: DateTimeOffset?
LastError: string?
CorrelationId: string?
RowVersion: Guid
```

**Service Bus message:**

```text
MessageId = OutboxMessage.Id
CorrelationId = RewriteAttemptId
Subject = RewriteJobCreated
ContentType = application/json
Body = { "attemptId": "..." }
ApplicationProperties["attemptId"] = RewriteAttemptId
```

- [ ] **Step 1: Add outbox enum/entity and EF schema**

Create:

- `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/OutboxMessageStatus.cs`
- `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/OutboxMessage.cs`

Modify:

- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`

Required indexes:

```text
Status + NextAttemptAt
Status + LockedUntil
```

Required field limits:

```text
MessageType max 160
LockedBy max 160
CorrelationId max 160
LastError max 1000
RowVersion concurrency token
```

- [ ] **Step 2: Create migration**

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
dotnet ef migrations add AddOutboxMessages \
  --project src/ReplyInMyVoice.Infrastructure \
  --startup-project src/ReplyInMyVoice.Api
```

- [ ] **Step 3: Insert outbox row in quota transaction**

Modify `QuotaService.ReserveAsync` so a new rewrite attempt creates:

```text
RewriteAttempt
UsageReservation
UsagePeriod.ReservedCount += 1
OutboxMessage(MessageType = RewriteJobCreated)
```

all inside the same DB transaction.

- [ ] **Step 4: Remove direct Service Bus publish from API path**

Modify `RewriteRequestService`:

- remove `IRewriteJobPublisher` from the constructor;
- remove direct `PublishAsync` after `ReserveAsync`;
- return the attempt state only.

- [ ] **Step 5: Add dispatcher service and worker**

Create:

- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/OutboxDispatcherService.cs`
- `backend-dotnet/src/ReplyInMyVoice.Worker/OutboxDispatcherWorker.cs`

Dispatcher must:

- poll only due pending rows;
- use `LockedUntil` and `LockedBy`;
- send to Azure Service Bus through `IRewriteJobPublisher`;
- mark `Sent` only after send succeeds;
- schedule exponential backoff after failures;
- mark `Failed` after `MaxAttempts`;
- idle without crashing if Service Bus connection string is missing.

- [ ] **Step 6: Register dispatcher in worker**

Modify `backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs`:

```csharp
builder.Services.AddHostedService<ServiceBusRewriteWorker>();
builder.Services.AddHostedService<OutboxDispatcherWorker>();
```

- [ ] **Step 7: Add outbox tests**

Required tests:

```text
ReserveAsync creates OutboxMessage with attempt/reservation
duplicate idempotency key does not create second OutboxMessage
quota exceeded does not create OutboxMessage
dispatcher sends pending due message and marks Sent
dispatcher failure increments AttemptCount and sets NextAttemptAt
dispatcher marks Failed after MaxAttempts
locked message is not claimed by another dispatcher
expired lock can be reclaimed
Sent message is not resent
```

Run:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Outbox
```

## Task 3: Add Atomic Rewrite Attempt Claim

**Problem:** `MarkProcessingAsync` currently sets `Pending -> Processing` but does not return whether this worker actually claimed the attempt.

- [ ] **Step 1: Change claim contract**

```csharp
Task<bool> MarkProcessingAsync(Guid attemptId, DateTimeOffset now, CancellationToken cancellationToken);
```

- [ ] **Step 2: Make transition atomic**

Only one worker can update:

```text
Pending -> Processing
```

All other states return `false`.

- [ ] **Step 3: Worker skips provider if claim fails**

`RewriteJobProcessor` must not call OpenAI if `MarkProcessingAsync` returns `false`.

- [ ] **Step 4: Add duplicate queue delivery test**

Expected:

```text
two processors receive same AttemptId
provider called exactly once
UsedCount increments once
```

## Task 4: Add Idempotency RequestHash Conflict

**Problem:** The same `X-Idempotency-Key` currently returns the existing attempt even if the body changed.

- [ ] Compare incoming request hash with existing `RewriteAttempt.RequestHash`.
- [ ] Return `409 Conflict` for same key with different body.
- [ ] Do not create outbox message.
- [ ] Do not create or release quota.
- [ ] Add API/service tests for this path.

## Task 5: Add Expired Reservation Cleanup Runner

**Problem:** `ReleaseExpiredReservationsAsync` exists, but no actual runner is confirmed.

- [ ] Add hosted cleanup loop in worker or API.
- [ ] Config:

```env
RESERVATION_CLEANUP_INTERVAL_SECONDS=300
```

- [ ] Cleanup must be idempotent.
- [ ] Repeated cleanup must not decrement `ReservedCount` twice.
- [ ] Add tests for expired pending reservation cleanup.

## Task 6: Make Stripe Webhook Processing Retry-Safe

**Problem:** `StripeEvent` is inserted before entitlement sync. If sync fails, retry may be skipped because the event already exists.

- [ ] Add Stripe event processing status:

```text
Pending
Processed
Failed
```

- [ ] Mark `Processed` only after entitlement sync succeeds.
- [ ] Allow retry for `Pending` or `Failed`.
- [ ] Keep duplicate `Processed` events idempotent.
- [ ] Add tests:

```text
duplicate processed event is skipped
sync failure leaves event retryable
retry after failure updates entitlement once
invalid signature still returns 400
```

## Task 7: Verify Azure Dev Backend End To End

After local tests pass:

- [ ] Apply EF migration to Azure SQL.
- [ ] Deploy API and continuous WebJob worker.
- [ ] Confirm `OutboxMessages` table exists in Azure SQL.
- [ ] Create a remote rewrite request.
- [ ] Confirm:

```text
RewriteAttempt = Pending
OutboxMessage = Pending
OutboxMessage = Sent
Service Bus message processed
RewriteAttempt = Processing
RewriteAttempt = Succeeded or Failed
UsageReservation = Finalized or Released
ReservedCount is correct
UsedCount is correct
```

- [ ] Confirm Service Bus queue has no unexpected active/dead-letter messages after smoke.
- [ ] Confirm `/health` returns 200.
- [ ] Confirm unauthenticated rewrite returns 401.
- [ ] Confirm invalid Stripe webhook signature returns 400.

## Task 8: Package AI Agent Studio Evidence

Current skill evidence exists, but the “custom AI Agent Studio” claim needs a clearer artifact.

- [ ] Create or update a studio README/runbook.
- [ ] Document Claude vs Codex routing:

```text
Claude: heavy planning, requirements-to-system plan, architecture review
Codex: code edits, tests, build-fix loops, deployment commands
```

- [ ] Include reusable skills:

```text
system-spec-synthesis / requirements-to-system-plan
resilience-test-generation
state-machine-modeling
data-module-review
claude-heavy-planning-handoff
```

- [ ] Add example prompts and outputs.
- [ ] Link skills to actual project artifacts and tests.

## Task 9: Implement Clean-Final Rewrite Gate

This is the frontend/product-quality track. It should not block `.NET` outbox work, but it must be completed before claiming the rewrite quality loop is robust.

Detailed plan:

```text
docs/superpowers/plans/2026-05-20-clean-final-quality-gate.md
```

Required:

- [ ] reject internal/meta/reviewer language such as `is referenced`;
- [ ] add Priya billing regression;
- [ ] rerun focused eval;
- [ ] deploy Cloudflare production app;
- [ ] smoke `https://replyinmyvoice.com`.

## Verification Commands

### .NET Backend

```bash
dotnet build backend-dotnet/ReplyInMyVoice.sln
dotnet test backend-dotnet/ReplyInMyVoice.sln
dotnet publish backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj -c Release
dotnet publish backend-dotnet/src/ReplyInMyVoice.Worker/ReplyInMyVoice.Worker.csproj -c Release
```

### Next.js/Cloudflare If Rewrite Quality Code Changes

```bash
npm run lint
npm run typecheck
npm test
npm run build
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

### GitHub/Azure/Cloudflare

- [ ] Push to GitHub after local verification.
- [ ] Wait for GitHub Actions to pass.
- [ ] Deploy Azure dev backend if `.NET` backend changed.
- [ ] Deploy Cloudflare app if Next.js rewrite code changed.
- [ ] Run remote smoke tests.

## Stop Conditions

Stop and ask the user only if:

- Azure permission denies required resource creation, migration, or deployment.
- GitHub push or repository settings are denied.
- Required secrets are missing or invalid.
- A real Stripe live-mode payment, real charge, or production billing action is required.
- Continuing would expose, print, or commit secrets.

Do not stop for ordinary build errors, test failures, migration errors, Azure CLI syntax issues, Service Bus errors, provider/model errors, queue publish errors, or eval failures during an approved long run. Fix and continue.

## Resume Wording Gate

Only use the strongest resume wording after these are true:

- `.NET` build/test/publish pass from the current checkout.
- Azure SQL migration is applied.
- Outbox dispatcher works in Azure dev.
- Service Bus worker processes the outbox-published job.
- Request hash conflict returns `409`.
- Duplicate queue delivery does not call provider twice.
- Expired reservation cleanup is actually scheduled.
- Stripe webhook partial failure can retry safely.
- Agent Studio workflow has a visible artifact/runbook.

Until then, use conservative wording:

```text
Built and hardened a parallel ASP.NET Core/Azure backend for failure-safe AI rewrite workflows, including usage reservations, idempotent attempts, Stripe webhook replay handling, and Service Bus worker processing.
```
