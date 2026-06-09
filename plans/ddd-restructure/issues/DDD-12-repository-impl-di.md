# DDD-12: Infrastructure repository + UnitOfWork implementations + DI wiring

## Context
Implement the DDD-11 repository interfaces against the existing `AppDbContext`. Infrastructure
starts referencing Application (it implements Application's interfaces — the classic DDD dependency
direction). The old services are NOT touched (strangler: old path stays live).
Read first: the `Application/Abstractions/*.cs` produced by DDD-11;
`backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`;
`backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs` (registration
pattern, lines 31-92, incl. the `Func<AppDbContext>` factory).

## Constraints
- Add a `ProjectReference` from `ReplyInMyVoice.Infrastructure.csproj` to `ReplyInMyVoice.Application`.
- Implementations use `AppDbContext` (existing DbSets). Either inject the scoped `AppDbContext` or
  use the existing `Func<AppDbContext>` factory pattern — match what neighbouring services do.
- `UnitOfWork` wraps `AppDbContext.SaveChangesAsync`.
- Do NOT modify any existing `Services/*` class (strangler — the old path must keep working).

## Changes required
1. `Infrastructure/Repositories/<Aggregate>Repository.cs` — one per DDD-11 interface, backed by the
   matching `AppDbContext` DbSet.
2. `Infrastructure/Repositories/UnitOfWork.cs` — implements `IUnitOfWork`, delegates to
   `AppDbContext.SaveChangesAsync`.
3. `ServiceCollectionExtensions.cs` — register each interface→implementation (`AddScoped`) next to
   the existing service registrations inside `AddReplyInMyVoiceInfrastructure`.
4. `ReplyInMyVoice.Infrastructure.csproj` — add the Application ProjectReference.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `grep -q "ReplyInMyVoice.Application" backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj`
- `ls backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/UnitOfWork.cs`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT modify any existing `Infrastructure/Services/*` (old path must remain).
- Do NOT modify `Functions`, `Api`, or `Worker`.
- Do NOT push, open a PR, or touch main.
