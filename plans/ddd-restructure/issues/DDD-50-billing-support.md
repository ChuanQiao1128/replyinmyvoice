# DDD-50: Migrate BillingSupport use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the BillingSupport use-cases from
Infrastructure/Services/BillingSupportService.cs into Application handlers that depend on
repository interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP
the old service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/BillingSupportService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

BillingSupportService.cs exposes two public use-cases:
- `CreateForUserAsync` — submit a billing support request for a user (with duplicate-open-request
  guard and rate limiting); returns a `BillingSupportRequestServiceResult` discriminated union
- `GetForUserAsync` — list all billing support requests for a user, ordered by date

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old BillingSupportService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests stay green, assertions unchanged.
- `CreateForUserAsync` commits state — use IUnitOfWork. The duplicate-open guard (checking for an
  existing open request) must be preserved within the same transaction.
- `BillingSupportRequestServiceResult` and its `BillingSupportRequestResultKind` enum should be
  represented as Application/Common DTOs, not re-using the Infrastructure record directly.
- `GetForUserAsync` is read-only — no IUnitOfWork needed.

## Changes required
1. `Application/UseCases/BillingSupport/*.cs` — command/query + handler per use-case:
   - `CreateBillingSupportRequestCommand.cs` + `CreateBillingSupportRequestHandler.cs`
   - `GetBillingSupportRequestsQuery.cs` + `GetBillingSupportRequestsHandler.cs`
2. `Application/Abstractions/IBillingSupportRepository.cs` — interface for creating a billing
   support request, querying existing open requests (duplicate guard), and listing requests by user.
3. Extend Infrastructure/Repositories with a `BillingSupportRepository` implementation; register in
   ServiceCollectionExtensions.cs.
4. `Application/Common/*.cs` — `BillingSupportRequestResultDto` (mirrors
   `BillingSupportRequestServiceResult` / `BillingSupportRequestResultKind`) and
   `BillingSupportRequestResponseDto` (mirrors `BillingSupportRequestResponse`) returned by handlers.
5. `ServiceCollectionExtensions.cs` — register the new handlers + repository with `AddScoped`.
6. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/BillingSupportUseCaseTests.cs` — cover
   both handlers: successful create, duplicate-open-request guard (InvalidRequest path), list
   (empty and non-empty); SQLite in-memory.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~BillingSupportUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/BillingSupport/CreateBillingSupportRequestHandler.cs`

## DO NOT
- Do NOT delete or edit BillingSupportService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
