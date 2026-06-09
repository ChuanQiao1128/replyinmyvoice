# DDD-43: Migrate ApiKey use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the ApiKey use-cases from
Infrastructure/Services/ApiKeyService.cs and Infrastructure/Services/ApiKeyUsageQueryService.cs
into Application handlers that depend on repository interfaces, following the Wave-1 Rewrite
template (Application/UseCases/Rewrite). KEEP both old services in place (strangler
add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs,
backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyUsageQueryService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

ApiKeyService.cs exposes six public use-cases:
- `GenerateAsync` — create a new API key, hash it, persist, return the plaintext once
- `ListAsync` — list a user's API keys with last-used metadata
- `RotateAsync` — replace an existing key with a new one atomically; returns new plaintext or null if not found
- `RevokeAsync` — soft-delete a key; returns bool for not-found
- `SetWebhookAsync` — set (or clear via `ClearWebhookAsync`) a webhook URL on a key
- `ClearWebhookAsync` — clear the webhook URL from a key

ApiKeyUsageQueryService.cs exposes three public use-cases:
- `GetSummaryAsync` — aggregated usage stats for a key over a window (calls, succeeded, failed)
- `GetSeriesAsync` — per-day usage time-series for a key over a window
- `GetRecentAsync` — paged list of recent API calls for a key

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old ApiKeyService and ApiKeyUsageQueryService untouched. No DB schema change / no new
  EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests stay green, assertions unchanged.
- `GenerateAsync` and `RotateAsync` commit state — use IUnitOfWork. The static `ComputeHash`
  helper may be kept as a Domain or Application static method (no Infrastructure dependency).
- The webhook URL validation logic (`TryNormalizeWebhookUrl`) must live in Application or Domain,
  not embedded in a repository implementation.
- Usage query handlers are read-only — no IUnitOfWork needed.

## Changes required
1. `Application/UseCases/ApiKey/*.cs` — command/query + handler per use-case:
   - `GenerateApiKeyCommand.cs` + `GenerateApiKeyHandler.cs`
   - `ListApiKeysQuery.cs` + `ListApiKeysHandler.cs`
   - `RotateApiKeyCommand.cs` + `RotateApiKeyHandler.cs`
   - `RevokeApiKeyCommand.cs` + `RevokeApiKeyHandler.cs`
   - `SetApiKeyWebhookCommand.cs` + `SetApiKeyWebhookHandler.cs`
   - `ClearApiKeyWebhookCommand.cs` + `ClearApiKeyWebhookHandler.cs`
   - `GetApiUsageSummaryQuery.cs` + `GetApiUsageSummaryHandler.cs`
   - `GetApiUsageSeriesQuery.cs` + `GetApiUsageSeriesHandler.cs`
   - `GetApiUsageRecentQuery.cs` + `GetApiUsageRecentHandler.cs`
2. `Application/Abstractions/IApiKeyRepository.cs` — repository interface for key CRUD and
   webhook operations; `IApiKeyUsageRepository.cs` — interface for usage query projections.
3. Extend Infrastructure/Repositories with matching implementations; register in
   ServiceCollectionExtensions.cs.
4. `Application/Common/*.cs` — DTO types for ApiKey projections and usage responses returned by
   handlers (mirrors `ApiKeySummary`, `ApiUsageSummaryResponse`, `ApiUsageSeriesPoint`,
   `ApiUsageRecentItem`).
5. `ServiceCollectionExtensions.cs` — register all new handlers and repositories with `AddScoped`.
6. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/ApiKeyUseCaseTests.cs` — cover all
   handlers using SQLite in-memory; include rotate/revoke not-found paths and usage window clamping.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiKeyUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/ApiKey/GenerateApiKeyHandler.cs`

## DO NOT
- Do NOT delete or edit ApiKeyService, ApiKeyUsageQueryService, or any other existing services
  (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
