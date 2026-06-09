# DDD-20: Migrate Rewrite create/get use-cases to Application handlers (strangler PATTERN)

## Context
This is the **pattern template** issue — every later context migration copies it. Move the Rewrite
"create attempt" + "get attempt" use-cases into Application handlers that depend on the DDD-11/12
repository interfaces instead of `AppDbContext`. **Keep** `Infrastructure/Services/RewriteRequestService`
untouched (strangler add-then-replace; the entry switch is DDD-21; deleting the old service is a
later wave). Scope is deliberately the SMALL create/get path, not the 583-line ProcessRewriteJob.
Read first: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteRequestService.cs`
(create logic), `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`
(how create/get currently work, incl. the get-by-id user filter), `Application/Abstractions/*`
(DDD-11 interfaces).

## Constraints
- Handler behaviour must be equivalent to current create/get (the existing `RewriteApiTests` must
  stay green WITHOUT changing their assertions).
- Handlers depend on the repository interfaces + `IUnitOfWork`, never on `AppDbContext` directly.
- CQRS-lite shape: `CreateRewriteAttemptCommand` + handler; `GetRewriteAttemptQuery` + handler;
  return Application `Common` result/DTO types.
- Leave `RewriteRequestService` in place (do not delete or edit it).

## Changes required
1. `Application/UseCases/Rewrite/CreateRewriteAttemptCommand.cs` + `...Handler.cs` — replicate the
   create + quota-reservation logic of `RewriteRequestService`, via repositories + UoW.
2. `Application/UseCases/Rewrite/GetRewriteAttemptQuery.cs` + `...Handler.cs` — fetch an attempt by
   id under the caller's user constraint.
3. `Application/Common/*.cs` — minimal result/DTO types the handlers return.
4. `ServiceCollectionExtensions.cs` — register the new handlers (`AddScoped`).
5. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteUseCaseTests.cs` — cover both
   handlers (SQLite in-memory, reuse the existing test harness patterns).

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0 (full suite incl. RewriteApiTests)
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/CreateRewriteAttemptHandler.cs`

## DO NOT
- Do NOT delete or edit `RewriteRequestService` or any other existing service.
- Do NOT change the Functions/Api entry points (DDD-21 handles Functions).
- Do NOT migrate `ProcessRewriteJob` (the 583-line rewrite orchestration — later wave).
- Do NOT push, open a PR, or touch main.
