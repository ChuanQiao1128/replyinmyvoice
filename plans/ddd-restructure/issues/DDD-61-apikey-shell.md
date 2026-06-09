# DDD-61: Shell ApiKeyHttpFunctions + ApiUsageHttpFunctions onto Application handlers (strangler replace)

## Context
Per docs/ddd-migration-playbook.md, switch the two API-key HTTP function classes from calling the
old Infrastructure services to invoking the Wave-2 Application handlers in
Application/UseCases/ApiKey. Behaviour is unchanged; existing tests stay green with assertions
unmodified. Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiUsageHttpFunctions.cs`
- Application/UseCases/ApiKey/* (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/GetOrCreateUserCommand.cs + GetOrCreateUserHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyHttpFunctionsTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyUsageQueryServiceTests.cs`

## Constraints
- Inject the Application handlers via DI (already registered in Wave 2). In each migrated method,
  build the Command/Query and call the handler; remove inline DbContext queries on that path.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP the old service classes (`ApiKeyService`, `ApiKeyUsageQueryService`, `AccountService`) and
  their DI registration. Remove a constructor param only if no remaining method in the file uses it.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required

### ApiKeyHttpFunctions.cs
Current constructor: `AccountService accountService, ApiKeyService apiKeyService`

Several endpoints first obtain `user.Id` via `accountService.GetOrCreateUserAsync`. Replace all
such calls with `GetOrCreateUserHandler` called with
`new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email)`.

1. `CreateApiKey` (`POST /keys`): replace `accountService.GetOrCreateUserAsync` + `apiKeyService.GenerateAsync(...)`
   with `GetOrCreateUserHandler` (for the user id) then `GenerateApiKeyHandler` called with
   `new GenerateApiKeyCommand(user.Id, name, isTest)`.
2. `ListApiKeys` (`GET /keys`): replace `GetOrCreateUserAsync` + `apiKeyService.ListAsync(...)` with
   `GetOrCreateUserHandler` then `ListApiKeysHandler` called with `new ListApiKeysQuery(user.Id)`.
3. `RotateApiKey` (`POST /keys/{id}/rotate`): replace `GetOrCreateUserAsync` + `apiKeyService.RotateAsync(...)` with
   `GetOrCreateUserHandler` then `RotateApiKeyHandler` called with
   `new RotateApiKeyCommand(user.Id, id)`.
4. `RevokeApiKey` (`DELETE /keys/{id}`): replace `GetOrCreateUserAsync` + `apiKeyService.RevokeAsync(...)` with
   `GetOrCreateUserHandler` then `RevokeApiKeyHandler` called with
   `new RevokeApiKeyCommand(user.Id, id)`.
5. `SetApiKeyWebhook` (`POST /keys/{id}/webhook`): replace `GetOrCreateUserAsync` + `apiKeyService.SetWebhookAsync(...)` with
   `GetOrCreateUserHandler` then `SetApiKeyWebhookHandler` called with
   `new SetApiKeyWebhookCommand(user.Id, id, webhookUrl)`.
   The existing `ApiKeyService.TryNormalizeWebhookUrl` static validation call is NOT migrated —
   keep calling it as a static helper before invoking the handler.
6. `ClearApiKeyWebhook` (`DELETE /keys/{id}/webhook`): replace `GetOrCreateUserAsync` + `apiKeyService.ClearWebhookAsync(...)` with
   `GetOrCreateUserHandler` then `ClearApiKeyWebhookHandler` called with
   `new ClearApiKeyWebhookCommand(user.Id, id)`.
7. Adjust constructor: add `GetOrCreateUserHandler`, `GenerateApiKeyHandler`, `ListApiKeysHandler`,
   `RotateApiKeyHandler`, `RevokeApiKeyHandler`, `SetApiKeyWebhookHandler`,
   `ClearApiKeyWebhookHandler`; drop `AccountService` and `ApiKeyService` only if fully unused.

### ApiUsageHttpFunctions.cs
Current constructor: `AccountService accountService, ApiKeyUsageQueryService apiKeyUsageQueryService`

1. `GetApiUsageSummary` (`GET /me/api-usage/summary`): replace
   `apiKeyUsageQueryService.GetSummaryAsync(authUser.ExternalAuthUserId, authUser.Email, ...)` with
   `GetApiUsageSummaryHandler` called with
   `new GetApiUsageSummaryQuery(authUser.ExternalAuthUserId, authUser.Email, DateTimeOffset.UtcNow)`.
2. `GetApiUsageSeries` (`GET /me/api-usage/series`): replace
   `accountService.GetOrCreateAccountSummaryAsync` + `apiKeyUsageQueryService.GetSeriesAsync(account.Id, ...)` with
   `GetApiUsageSeriesHandler` called with
   `new GetApiUsageSeriesQuery(accountId, DateTimeOffset.UtcNow, days)`.
   The account id must be resolved first; use `GetAccountSummaryHandler` /
   `new GetAccountSummaryQuery(authUser.ExternalAuthUserId, authUser.Email)` to obtain it.
3. `GetApiUsageRecent` (`GET /me/api-usage/recent`): replace
   `accountService.GetOrCreateAccountSummaryAsync` + `apiKeyUsageQueryService.GetRecentAsync(account.Id, ...)` with
   `GetApiUsageRecentHandler` called with
   `new GetApiUsageRecentQuery(accountId, DateTimeOffset.UtcNow, limit)`.
   Resolve the account id the same way as above.
4. Adjust constructor: add `GetApiUsageSummaryHandler`, `GetApiUsageSeriesHandler`,
   `GetApiUsageRecentHandler`, `GetAccountSummaryHandler`; drop `AccountService` and
   `ApiKeyUsageQueryService` only if fully unused.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~ApiKeyServiceTests|FullyQualifiedName~ApiKeyHttpFunctionsTests|FullyQualifiedName~ApiKeyUsageQueryServiceTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
