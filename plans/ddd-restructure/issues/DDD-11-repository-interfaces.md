# DDD-11: Application repository interfaces + IUnitOfWork

## Context
Define the persistence abstractions in the Application layer that the Rewrite pattern migration
(DDD-20) will consume. Classic DDD: interfaces live in `Application/Abstractions`, implementations
in `Infrastructure` (DDD-12). This issue defines ONLY what the Rewrite create/get use-cases plus
their core aggregates need — NOT all 25 aggregates (later waves add more on demand).
Read first: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/{AppUser,RewriteAttempt,UsageReservation,RewriteCredit,UsagePeriod}.cs`
(aggregate shapes), `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`
(existing DbSets + query patterns), `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteRequestService.cs`
(which queries the create path actually uses).

## Constraints
- Pure interfaces + the minimal parameter/return types only. NO implementation, NO EF Core
  dependency (Application must not reference EF Core or Infrastructure).
- Interface methods take Domain entities / primitives / `CancellationToken` and return `Task`.
- Naming: `I<Aggregate>Repository`; `IUnitOfWork.SaveChangesAsync(CancellationToken ct = default)`.
- Define ONLY the methods DDD-20 needs (smallest set). Do not speculatively design unused methods.

## Changes required
1. `Application/Abstractions/IUnitOfWork.cs` — `Task<int> SaveChangesAsync(CancellationToken ct = default);`
2. `Application/Abstractions/IAppUserRepository.cs` — methods the create-attempt path needs
   (e.g. get-by-id; mirror what `RewriteRequestService` reads about the user).
3. `Application/Abstractions/IRewriteAttemptRepository.cs` — `AddAsync`, get-by-id (with the
   caller/user constraint used by the get path), and whatever create/get needs.
4. Add `IUsageReservationRepository` / `IRewriteCreditRepository` / `IUsagePeriodRepository`
   ONLY if the DDD-20 create path's quota reservation actually touches them directly; otherwise
   omit and leave for a later wave. Determine this by reading `RewriteRequestService` and the
   `QuotaService` create path it calls.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IUnitOfWork.cs`
- `ls backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/I*Repository.cs` lists ≥1 file
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT write implementations (DDD-12 does).
- Do NOT reference EF Core or the Infrastructure project from Application.
- Do NOT define interfaces for all 25 aggregates — only what the pattern needs.
- Do NOT push, open a PR, or touch main.
