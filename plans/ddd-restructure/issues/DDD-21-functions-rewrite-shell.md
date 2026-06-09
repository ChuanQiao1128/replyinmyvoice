# DDD-21: Shell the rewrite create/get Functions onto the Application handlers

## Context
Switch the Functions rewrite create/get entry points to call the DDD-20 Application handlers,
removing the inline `AppDbContext` access on that path. This proves the "entry point becomes a thin
shell" half of the migration pattern. All other functions are left untouched.
Read first: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`
(382 lines), the DDD-20 handlers, `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`
(behaviour contract — 1436 lines).

## Constraints
- Change ONLY the create-attempt + get-attempt handlers inside `RewriteHttpFunctions`.
- Behaviour/response contract is unchanged — `RewriteApiTests` must stay green and their
  assertions must NOT be modified.
- Inject the Application handler via DI (registered in DDD-20).
- Do NOT touch `V1RewriteHttpFunctions` (later wave).

## Changes required
1. `RewriteHttpFunctions.cs` — the create/get handlers build the DDD-20 Command/Query and invoke
   the handler; remove the inline DbContext query/SaveChanges on those two paths only.
2. If the constructor needs the handler injected, update the constructor and confirm DI is already
   registered (by DDD-20).

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteApiTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change `RewriteApiTests` assertions (behaviour is unchanged).
- Do NOT touch other function files or `V1RewriteHttpFunctions`.
- Do NOT delete `RewriteRequestService` (it is strangled in a later wave, not here).
- Do NOT push, open a PR, or touch main.
