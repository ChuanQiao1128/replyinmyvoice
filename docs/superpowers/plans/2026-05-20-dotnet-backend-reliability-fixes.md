# .NET Backend Reliability Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hardening the C#/.NET async rewrite backend so queue publish failures, duplicate queue delivery, duplicate idempotency keys, disposed DbContext reuse, and expired reservations cannot corrupt attempt state or quota accounting.

**Architecture:** Keep the API + database reservation + Azure Service Bus + worker design. Add missing reliability boundaries: safe `DbContext` factory behavior, request hash conflict handling, publish recovery/outbox behavior, atomic worker claim, and scheduled reservation cleanup. Preserve the Next.js/Cloudflare production app while improving the parallel `.NET` backend.

**Tech Stack:** ASP.NET Core, EF Core, Azure SQL, Azure Service Bus, xUnit, background worker/WebJob, GitHub Actions/Azure deployment.

---

## Context

The target backend architecture is:

```text
Frontend -> POST /api/rewrite -> RewriteAttempt + UsageReservation -> Service Bus -> Worker -> Provider -> finalize/release
```

This is the right direction for the resume-level backend claim:

```text
failure-safe metered SaaS backend for AI rewrite workflows under retries, duplicate clicks, provider failures, queue redelivery, and concurrency.
```

Current review surfaced reliability gaps that should be fixed before treating the `.NET` backend as interview/demo-ready.

## Current Risks To Fix

### P1: Scoped DbContext Factory Can Return A Disposed Context

Observed risk:

- `Func<AppDbContext>` is registered in infrastructure.
- Some services use `await using` around the factory result.
- If the factory returns the current scoped `DbContext`, manually disposing it can break later calls in the same scope or worker processing loop.

Desired behavior:

- Use `IDbContextFactory<AppDbContext>` or a wrapper that creates a fresh context per operation.
- Do not manually dispose a scoped `AppDbContext` owned by DI.
- Worker-safe services should not reuse a disposed context across messages.

### P1: No Durable Outbox Or Republish Path After Reservation Commit

Observed risk:

- API creates `Pending` `RewriteAttempt` and `UsageReservation` in the database.
- Then it publishes the job to Service Bus.
- If DB commit succeeds but Service Bus publish fails, the attempt can remain pending and never be processed.
- Retrying the same idempotency key may return the existing pending attempt without republishing.

Desired behavior:

- Either implement a transactional outbox table and dispatcher, or add a safe republish path for existing pending attempts.
- The job publish must be recoverable without double-reserving or double-charging.

Recommended MVP fix:

- Add `RewriteOutboxMessage` table with unique `RewriteAttemptId`.
- In the same transaction that creates the attempt/reservation, insert an outbox row.
- API may try immediate publish after commit.
- A dispatcher/worker marks outbox rows as published only after Service Bus send succeeds.
- Existing pending attempts with unpublished outbox rows can be safely dispatched later.

### P1: Worker Needs Atomic Claim

Observed risk:

- Duplicate Service Bus deliveries can cause two workers to see the same pending attempt.
- Both may call the provider.
- Finalization may prevent double charge, but provider calls and state transitions can duplicate.

Desired behavior:

- Add `MarkProcessingAsync(attemptId)` or equivalent.
- It must atomically transition only `Pending -> Processing`.
- Only the worker that successfully claims the attempt calls the provider.
- If attempt is already `Processing`, `Succeeded`, `Failed`, `Released`, or `Expired`, the worker must skip provider calls.

### P2: Idempotency Key With Different Body Should Return 409

Observed risk:

- Same user + same idempotency key can return an existing attempt even if the request body differs.

Desired behavior:

- Store canonical `RequestHash`.
- If an existing attempt has the same `(UserId, IdempotencyKey)` but different `RequestHash`, return `409 Conflict`.
- Do not publish a job.
- Do not create or release quota.

### P2: Expired Reservations Need An Actual Runner

Observed risk:

- Release-expired code exists, but may not be scheduled.
- Worker crash, publish failure, or unrecoverable queue issue can leave `ReservedCount` inflated.

Desired behavior:

- Add a background cleanup loop in the worker or API hosted service.
- It periodically releases expired pending reservations.
- It must be idempotent and concurrency-safe.

### Current Workspace Hygiene Issue

Observed risk:

- Temporary files exist:
  - `Program 2.cs`
  - `OpenAiRewriteProvider 2.cs`
  - `ServiceCollectionExtensions 2.cs`
  - worker `Program 2.cs`
  - `ServiceBusRewriteWorker 2.cs`
- These should not remain in the source tree.

Desired behavior:

- Inspect each `* 2.cs` file.
- If it contains intended fixes, merge them into the canonical file.
- Delete the temporary file.
- Verify solution build.

## Files To Inspect

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
- EF Core model and configuration files under `backend-dotnet/src/ReplyInMyVoice.Infrastructure`
- Rewrite services under `backend-dotnet/src/ReplyInMyVoice.Infrastructure` and/or application service projects
- xUnit tests under `backend-dotnet/tests`

## Task 1: Clean Workspace And Merge Intended `* 2.cs` Changes

**Files:**
- Modify canonical files listed above.
- Delete temporary `* 2.cs` files after merging required changes.

- [ ] **Step 1: Diff canonical files against temporary files**

Run:

```bash
diff -u "backend-dotnet/src/ReplyInMyVoice.Api/Program.cs" "backend-dotnet/src/ReplyInMyVoice.Api/Program 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs" "backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider.cs" "backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs" "backend-dotnet/src/ReplyInMyVoice.Worker/Program 2.cs" || true
diff -u "backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker.cs" "backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker 2.cs" || true
```

Expected: identify whether temporary files contain intended fixes or accidental duplicates.

- [ ] **Step 2: Merge only intended changes into canonical files**

Rules:

- keep one canonical file per class;
- preserve compileable namespace/class names;
- do not keep duplicate top-level `Program` files;
- do not keep duplicate worker services;
- do not delete user work blindly; inspect before merging.

- [ ] **Step 3: Delete temporary files**

After merging:

```bash
rm "backend-dotnet/src/ReplyInMyVoice.Api/Program 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiRewriteProvider 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Worker/Program 2.cs"
rm "backend-dotnet/src/ReplyInMyVoice.Worker/ServiceBusRewriteWorker 2.cs"
```

- [ ] **Step 4: Build**

Run:

```bash
dotnet build backend-dotnet/ReplyInMyVoice.sln
```

Expected: build either passes or fails only with real implementation errors that should be fixed in later tasks.

## Task 2: Replace Risky DbContext Factory Registration

**Files:**
- Modify: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`
- Modify services that currently inject `Func<AppDbContext>` if needed.
- Test: relevant xUnit service tests.

- [ ] **Step 1: Locate current factory usage**

Run:

```bash
rg -n "Func<AppDbContext>|IDbContextFactory|CreateDbContext|await using.*DbContext|AddDbContext" backend-dotnet/src backend-dotnet/tests
```

- [ ] **Step 2: Write a failing test for multiple factory calls**

Add or update an infrastructure test that:

- resolves the service provider;
- calls the database factory twice;
- disposes the first context;
- verifies the second operation still works.

The test should fail if the factory returns the same scoped context instance.

- [ ] **Step 3: Use `AddDbContextFactory<AppDbContext>`**

Register:

```csharp
services.AddDbContextFactory<AppDbContext>(options =>
{
    // existing SQL Server or SQLite configuration
});
```

Then inject `IDbContextFactory<AppDbContext>` where a service needs to create and dispose contexts manually.

- [ ] **Step 4: Avoid disposing scoped DbContext**

If a service receives `AppDbContext` directly from DI, do not wrap it in `await using`.

If a service receives `IDbContextFactory<AppDbContext>`, create a fresh context per operation:

```csharp
await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
```

- [ ] **Step 5: Verify**

Run:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter DbContext
dotnet build backend-dotnet/ReplyInMyVoice.sln
```

Expected: tests and build pass.

## Task 3: Add RequestHash Conflict Handling

**Files:**
- Modify rewrite request service.
- Modify `RewriteAttempt` model/configuration only if `RequestHash` is missing or not persisted.
- Test: rewrite idempotency tests.

- [ ] **Step 1: Confirm `RequestHash` storage**

Run:

```bash
rg -n "RequestHash|IdempotencyKey|RewriteAttempt" backend-dotnet/src backend-dotnet/tests
```

- [ ] **Step 2: Write failing test**

Test case:

```text
Given same user and same X-Idempotency-Key
When first request body is A
And second request body is B
Then second request returns 409 Conflict
And quota reservation count does not change
And no new job is published
```

- [ ] **Step 3: Canonicalize request before hashing**

Use a deterministic hash input:

- trim string fields;
- include `messageToReplyTo`, `roughDraftReply`, `audience`, `purpose`, `whatHappened`, `factsToPreserve`, `tone`;
- serialize with stable property ordering;
- hash with SHA-256.

- [ ] **Step 4: Enforce conflict**

In the idempotency lookup path:

```csharp
if (existingAttempt.RequestHash != requestHash)
{
    return RewriteRequestResult.Conflict("idempotency_key_reused_with_different_payload");
}
```

- [ ] **Step 5: Verify**

Run:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Idempotency
```

Expected: conflict test passes and existing same-body retry tests still pass.

## Task 4: Add Atomic Worker Claim

**Files:**
- Modify rewrite attempt repository/service.
- Modify worker processor.
- Test: worker redelivery/concurrency tests.

- [ ] **Step 1: Write failing duplicate-delivery test**

Test case:

```text
Given one Pending attempt
When two worker processors receive the same AttemptId concurrently
Then only one MarkProcessingAsync returns true
And the provider is called exactly once
And final usage is charged once
```

- [ ] **Step 2: Implement `MarkProcessingAsync`**

Preferred behavior:

```csharp
Task<bool> MarkProcessingAsync(Guid attemptId, CancellationToken cancellationToken);
```

It must atomically update:

```sql
UPDATE RewriteAttempts
SET Status = 'Processing', StartedAt = SYSUTCDATETIME()
WHERE Id = @attemptId AND Status = 'Pending'
```

Return true only if one row changed.

With EF Core, use `ExecuteUpdateAsync` if available, or attach entity with concurrency token and handle `DbUpdateConcurrencyException`.

- [ ] **Step 3: Worker must skip if claim fails**

Worker flow:

```text
load message attempt id
call MarkProcessingAsync
if false, complete/skip message without provider call
if true, call provider
finalize or release
```

- [ ] **Step 4: Verify**

Run:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Worker
```

Expected: duplicate delivery test passes.

## Task 5: Implement Transactional Outbox For Azure Service Bus Publishing

**Files:**
- Create: `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/OutboxMessageStatus.cs`
- Create: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/OutboxMessage.cs`
- Modify: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`
- Modify: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`
- Modify: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteRequestService.cs`
- Modify: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Queueing/AzureServiceBusRewriteJobPublisher.cs`
- Create: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/OutboxDispatcherService.cs`
- Create: `backend-dotnet/src/ReplyInMyVoice.Worker/OutboxDispatcherWorker.cs`
- Modify: `backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs`
- Add migration under `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/`
- Test: `backend-dotnet/tests/ReplyInMyVoice.Tests/OutboxDispatcherTests.cs`
- Test: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteRequestServiceTests.cs`
- Test: `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`

### Azure Design

The Azure integration should work like this:

```text
POST /api/rewrite
  -> Azure SQL transaction
     - RewriteAttempt
     - UsageReservation
     - UsagePeriod.ReservedCount + 1
     - OutboxMessage(RewriteJobCreated)
  -> 202 Accepted

Continuous WebJob / Worker process
  -> OutboxDispatcherWorker polls Azure SQL due pending rows
  -> claims a row with LockedUntil + LockedBy
  -> sends Azure Service Bus message
  -> marks outbox row Sent

ServiceBusRewriteWorker
  -> receives AttemptId from Azure Service Bus
  -> RewriteJobProcessor claims attempt
  -> provider call
  -> finalize or release reservation
```

The API must not publish directly to Service Bus after the reservation transaction. Azure SQL is the durable handoff point. Azure Service Bus is the async delivery mechanism after the outbox row exists.

Recommended Service Bus message settings:

```text
MessageId: OutboxMessage.Id as N-format string
CorrelationId: RewriteAttemptId as N-format string
Subject: RewriteJobCreated
ContentType: application/json
Body: { "attemptId": "..." }
ApplicationProperties["attemptId"]: RewriteAttemptId
```

If Azure Service Bus duplicate detection is enabled for the queue, the stable `MessageId` provides an extra safety layer. The database outbox and worker idempotency must still be correct without relying on Service Bus duplicate detection.

- [ ] **Step 1: Write failing tests for outbox creation and no direct publish**

Add tests proving:

```text
ReserveAsync creates RewriteAttempt, UsageReservation, and OutboxMessage in one transaction.
RewriteRequestService.CreateAttemptAsync does not call IRewriteJobPublisher directly.
Duplicate idempotency key does not create a second OutboxMessage.
Quota exceeded does not create an OutboxMessage.
```

Run:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter "Outbox|RewriteRequestService|QuotaService"
```

Expected before implementation: tests fail because `OutboxMessage` does not exist and `RewriteRequestService` still publishes directly.

- [ ] **Step 2: Add `OutboxMessageStatus` enum**

Create `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/OutboxMessageStatus.cs`:

```csharp
namespace ReplyInMyVoice.Domain.Enums;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3
}
```

- [ ] **Step 3: Add `OutboxMessage` entity**

Create `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/OutboxMessage.cs`:

```csharp
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string MessageType { get; set; }
    public required string PayloadJson { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 10;
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LockedBy { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public string? CorrelationId { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
```

First message type:

```text
RewriteJobCreated
```

Payload:

```json
{ "attemptId": "..." }
```

- [ ] **Step 4: Configure EF Core schema**

Modify `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`:

```csharp
public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
```

Configure:

```csharp
modelBuilder.Entity<OutboxMessage>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
    entity.HasIndex(x => new { x.Status, x.LockedUntil });
    entity.Property(x => x.MessageType).HasMaxLength(160);
    entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
    entity.Property(x => x.LockedBy).HasMaxLength(160);
    entity.Property(x => x.CorrelationId).HasMaxLength(160);
    entity.Property(x => x.LastError).HasMaxLength(1000);
    entity.Property(x => x.RowVersion).IsConcurrencyToken();
});
```

Generate migration after the buildable source tree is restored:

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
dotnet ef migrations add AddOutboxMessages \
  --project src/ReplyInMyVoice.Infrastructure \
  --startup-project src/ReplyInMyVoice.Api
```

- [ ] **Step 5: Insert outbox row inside the quota transaction**

Modify `QuotaService.ReserveAsync` so the same serializable transaction creates:

```text
RewriteAttempt
UsageReservation
UsagePeriod.ReservedCount += 1
OutboxMessage
```

Outbox values:

```text
MessageType = "RewriteJobCreated"
PayloadJson = JsonSerializer.Serialize(new RewriteJob(attempt.Id))
Status = Pending
NextAttemptAt = now
AttemptCount = 0
MaxAttempts = 10
CorrelationId = attempt.Id.ToString("N")
```

Invariant:

```text
No Created attempt with ReservedCount +1 should exist without a corresponding Pending outbox message.
```

- [ ] **Step 6: Remove direct publish from `RewriteRequestService`**

Modify `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteRequestService.cs`:

- remove `IRewriteJobPublisher jobPublisher` from the constructor;
- remove:

```csharp
await jobPublisher.PublishAsync(new RewriteJob(result.AttemptId), cancellationToken);
```

`RewriteRequestService` should only create or return the attempt state. Publishing is owned by the dispatcher.

- [ ] **Step 7: Implement outbox claim and state transitions**

Create `OutboxDispatcherService` methods with deterministic, testable behavior:

```csharp
Task<IReadOnlyList<OutboxMessage>> ClaimDueMessagesAsync(
    string lockedBy,
    DateTimeOffset now,
    int batchSize,
    TimeSpan lockDuration,
    CancellationToken cancellationToken);

Task MarkSentAsync(Guid outboxMessageId, DateTimeOffset now, CancellationToken cancellationToken);

Task MarkFailedAttemptAsync(
    Guid outboxMessageId,
    string safeError,
    DateTimeOffset now,
    CancellationToken cancellationToken);
```

Claim query:

```text
Status = Pending
NextAttemptAt <= now
LockedUntil IS NULL OR LockedUntil < now
```

Claim side effects:

```text
Status = Processing
LockedUntil = now + 30 seconds
LockedBy = worker instance id
LastAttemptAt = now
RowVersion = new Guid
```

Failure side effects:

```text
AttemptCount += 1
LastError = sanitized error
LastAttemptAt = now
LockedBy = null
LockedUntil = null
if AttemptCount >= MaxAttempts:
  Status = Failed
else:
  Status = Pending
  NextAttemptAt = now + min(5 minutes, 2^AttemptCount seconds)
```

Sent side effects:

```text
Status = Sent
SentAt = now
LockedBy = null
LockedUntil = null
```

- [ ] **Step 8: Implement `OutboxDispatcherWorker`**

Create `backend-dotnet/src/ReplyInMyVoice.Worker/OutboxDispatcherWorker.cs`.

Behavior:

```text
On startup:
  if Service Bus connection string is missing, log warning and idle
Loop:
  claim due pending outbox rows from Azure SQL
  publish each RewriteJobCreated payload to Azure Service Bus
  mark Sent on success
  mark Pending/Failed with exponential backoff on failure
  delay for configured polling interval
```

Suggested config:

```env
OUTBOX_DISPATCH_INTERVAL_SECONDS=5
OUTBOX_DISPATCH_BATCH_SIZE=10
OUTBOX_LOCK_SECONDS=30
OUTBOX_MAX_ATTEMPTS=10
```

Do not print Service Bus connection strings or payload secrets in logs.

- [ ] **Step 9: Register dispatcher in worker app**

Modify `backend-dotnet/src/ReplyInMyVoice.Worker/Program.cs`:

```csharp
builder.Services.AddHostedService<ServiceBusRewriteWorker>();
builder.Services.AddHostedService<OutboxDispatcherWorker>();
```

The worker app should run both:

```text
OutboxDispatcherWorker -> Azure SQL to Azure Service Bus
ServiceBusRewriteWorker -> Azure Service Bus to RewriteJobProcessor
```

- [ ] **Step 10: Add dispatcher tests**

Add `backend-dotnet/tests/ReplyInMyVoice.Tests/OutboxDispatcherTests.cs`.

Required coverage:

```text
dispatcher sends pending due message and marks Sent
dispatcher failure increments AttemptCount and sets NextAttemptAt
dispatcher marks Failed after MaxAttempts
locked message is not claimed by another dispatcher
expired lock can be claimed again
Sent message is not resent
Failed message is not resent automatically
```

Use fake `IRewriteJobPublisher`; do not hit live Azure Service Bus in tests.

- [ ] **Step 11: Verify local backend**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare
dotnet build backend-dotnet/ReplyInMyVoice.sln
dotnet test backend-dotnet/ReplyInMyVoice.sln
dotnet publish backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj -c Release
dotnet publish backend-dotnet/src/ReplyInMyVoice.Worker/ReplyInMyVoice.Worker.csproj -c Release
```

Expected:

- build passes;
- xUnit tests pass;
- API publish passes;
- Worker publish passes.

- [ ] **Step 12: Verify Azure deployment path**

After local verification:

```text
apply EF migration to Azure SQL
deploy API + continuous WebJob worker to Azure App Service
confirm OutboxMessages table exists
create one rewrite request
confirm Pending outbox row becomes Sent
confirm Service Bus queue receives/processes message
confirm RewriteAttempt moves Pending -> Processing -> Succeeded or Failed
confirm Service Bus active/dead-letter counts are clean
```

Do not switch Stripe live mode or make real charges.

## Task 6: Add Expired Reservation Cleanup Runner

**Files:**
- Modify worker hosted services or add cleanup hosted service.
- Test: expiration cleanup tests.

- [ ] **Step 1: Write failing cleanup test**

Test case:

```text
Given a Pending reservation whose ExpiresAt is in the past
When cleanup runs
Then reservation becomes Expired or Released
And UsagePeriod.ReservedCount decreases by 1
And UsedCount does not change
And running cleanup again does not decrement again
```

- [ ] **Step 2: Implement cleanup runner**

Add a hosted service or worker loop:

```text
every N minutes:
  ReleaseExpiredReservationsAsync(now)
```

Make the interval configurable:

```env
ReservationCleanupIntervalSeconds=300
```

- [ ] **Step 3: Ensure idempotency**

Cleanup must update only rows still in `Pending` or `Processing` with expired timestamps and must not alter `Finalized`, `Released`, or `Expired` rows.

- [ ] **Step 4: Verify**

Run:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Reservation
```

Expected: cleanup tests pass.

## Task 7: Full Verification And Documentation

**Files:**
- Modify: `docs/dotnet-azure-full-run-target.md`
- Modify: `docs/dotnet-azure-full-run-result.md`
- Modify: `docs/dotnet-azure-next-phase.md` if architecture changes.

- [ ] **Step 1: Run local backend verification**

Run:

```bash
dotnet build backend-dotnet/ReplyInMyVoice.sln
dotnet test backend-dotnet/ReplyInMyVoice.sln
dotnet publish backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj -c Release
dotnet publish backend-dotnet/src/ReplyInMyVoice.Worker/ReplyInMyVoice.Worker.csproj -c Release
```

- [ ] **Step 2: Update docs**

Document:

- DbContext factory fix;
- request hash conflict behavior;
- atomic worker claim;
- outbox/republish recovery behavior;
- reservation cleanup runner;
- any remaining Azure deployment blockers.

- [ ] **Step 3: Stage only intended `.NET` files**

Do not stage unrelated Next.js rewrite-quality work unless the user explicitly combines the runs.

```bash
git status --short
git add backend-dotnet docs/dotnet-azure-full-run-target.md docs/dotnet-azure-full-run-result.md docs/dotnet-azure-next-phase.md docs/superpowers/plans/2026-05-20-dotnet-backend-reliability-fixes.md
git status --short
```

- [ ] **Step 4: Commit and push after verification**

```bash
git commit -m "Harden dotnet rewrite queue reliability"
git push origin main
```

Only deploy Azure if the user starts the `.NET` backend long run or explicitly asks to deploy the `.NET` backend.

## Stop Conditions

Do not stop for:

- compile errors;
- test failures;
- migration errors;
- Service Bus local mock issues;
- ordinary Azure CLI command syntax issues.

Stop and ask the user only if:

- a required secret is missing or invalid;
- Azure permission denies required resource access;
- GitHub push permission is denied;
- fixing the issue would expose or commit secrets;
- a real live-mode Stripe payment or production billing action is required.

## Completion Criteria

This `.NET` reliability pass is complete only when:

- temporary `* 2.cs` files are removed or intentionally ignored with documented reason;
- DbContext factory risk is eliminated;
- same idempotency key with different payload returns `409`;
- queue publish after DB commit is recoverable through outbox or safe republish;
- duplicate Service Bus delivery cannot call provider twice for the same attempt;
- expired reservation cleanup runs and is idempotent;
- required xUnit tests pass;
- `dotnet build`, `dotnet test`, API publish, and worker publish pass;
- docs are updated.
