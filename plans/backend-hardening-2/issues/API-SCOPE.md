## Context

You are implementing issue **API-SCOPE** (finding #14) on the integration branch `delivery/backend-hardening-2` (base it on that branch; NEVER `main`). Backend is .NET 8 / Azure Functions at `/Users/qc/Desktop/CloudFlare/backend-dotnet`.

`ApiKey.Scope` is persisted but never enforced. Enforce it at the V1 boundary, mirroring the already-shipped `IsTest` pattern, with **default = full** so existing keys are unaffected.

Read these first (every anchor is real):
- `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKey.cs:12` — `Scope` default is `"[]"` (JSON array). Do NOT change this default.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs` — `ResolveAsync` builds `ApiKeyAuthResult` at line 58; the record is at line 74 (`record ApiKeyAuthResult(Guid? UserId, Guid? ApiKeyId, int RateLimitPerMinute, bool IsTest = false)`). This is the carrier to extend, exactly like `IsTest`.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` — `V1SubmitRewrite` (line 59), `V1GetUsage` (line 378). `IsTest` is enforced at lines 152 and 443. The error helper is `FunctionHttpResults.Problem(...)` (used at line 250) which emits an `{ error: { code, message } }` envelope; tests assert via `error/code`.
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/ApiKey/GenerateApiKeyHandler.cs:17-26` — never sets `Scope`; `RotateApiKeyHandler.cs:41` copies it forward. Leave both as-is.
- Tests to extend: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyAuthResolverTests.cs` (resolver unit, seeds `new ApiKey{...}` directly — add a `scope` param to `SeedApiKeyAsync`), and `backend-dotnet/tests/ReplyInMyVoice.Tests/V1RewriteRateLimitTests.cs` (full WebApplicationFactory HTTP driver via `PostV1RewriteAsync`; `SeedApiKeyUserAsync` builds `ApiKey` rows).

## Constraints

- Base branch = `delivery/backend-hardening-2`. NEVER push, open a PR, or touch `main`.
- Banned substrings anywhere (incl. comments/identifiers): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change the `IRewriteEngineClient` contract, `ResultJson` shape, or the engine error-code set.
- No EF migration: the `Scope` column has no max-length config and full-vs-restricted is decided at parse time, not in schema. Do NOT add a migration.
- Keep the existing 799 tests green; every behavioral change adds xUnit tests.
- No secret values in tracked files; validate env at runtime in the handler (unchanged here).
- **Default-full is mandatory**: `"[]"`, `null`, empty, or whitespace `Scope` MUST be treated as full access (every current key + the entity default). Only a **non-empty** explicit JSON array restricts; access is denied only when a required scope is absent from a non-empty array.

## Changes required

1. Add a small Application-layer helper `backend-dotnet/src/ReplyInMyVoice.Application/Common/ApiKeyScopes.cs`:
   - A const for the rewrite scope, e.g. `public const string Rewrite = "rewrite";` (define `Usage`/`UsageRead` only if you also gate `V1GetUsage`; otherwise keep one).
   - `static IReadOnlySet<string> Parse(string? scopeJson)` — returns an **empty set** for null/whitespace/`"[]"`/unparseable JSON (treated as FULL by the caller). For a valid non-empty JSON string array, returns the trimmed, case-insensitive set of values. Never throws on bad JSON (catch `JsonException`, return empty).
   - `static bool Allows(IReadOnlySet<string> granted, string required)` — returns `true` when `granted` is empty (full) OR `granted` contains `required` (OrdinalIgnoreCase).
2. `ApiKeyAuthResolver.cs`: parse `apiKey.Scope` via `ApiKeyScopes.Parse` and carry it on `ApiKeyAuthResult`. Extend the record to `ApiKeyAuthResult(Guid? UserId, Guid? ApiKeyId, int RateLimitPerMinute, bool IsTest = false, IReadOnlySet<string>? Scopes = null)` (default `null` ⇒ treat as full). Set it on the success return at line 58. The unauthenticated/early returns (lines 33, 44) keep the default.
3. `V1RewriteHttpFunctions.cs`:
   - In `V1SubmitRewrite` (line 59), after the `auth.UserId is null` check (line 73) and before rate-limiting, reject when the key lacks the rewrite scope: `if (!ApiKeyScopes.Allows(auth.Scopes ?? EmptyFullSet, ApiKeyScopes.Rewrite)) return Error("insufficient_scope", "This API key does not have the required scope.", StatusCodes.Status403Forbidden);` (route through the existing `Error`/`CompleteAsync` helpers so usage is recorded consistently). Place the check so NO `RewriteAttempt`/reservation/outbox row is created on denial.
   - Mirror the same guard in `V1GetUsage` (line 378) ONLY if you defined a usage scope; if you keep a single `rewrite` scope, gate just submit and leave usage ungated (document the choice in the PR/commit). Pick one and make the tests match.
   - Treat a `null` `auth.Scopes` as full (existing keys).
4. Tests:
   - `ApiKeyAuthResolverTests.cs`: add a `scope` parameter to `SeedApiKeyAsync` and add facts: (a) `Scope = "[]"` ⇒ `ResolveAsync().Scopes` is empty/full; (b) `Scope = "[\"rewrite\"]"` ⇒ parsed set contains `rewrite`; (c) a restricted scope NOT containing rewrite parses to a non-empty set lacking it.
   - `V1RewriteRateLimitTests.cs`: add a fact seeding a live key with `Scope = "[\"usage\"]"` (no rewrite) ⇒ `POST /api/v1/rewrite` returns 403 with `error.code = "insufficient_scope"` and `RewriteAttempts/UsageReservations/OutboxMessages` counts are 0; add a fact that a default `Scope = "[]"` key still returns 202 (allow path). Reuse `PostV1RewriteAsync`/`AssertErrorCodeAsync`.

## Acceptance

- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiKeyAuthResolverTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~V1RewriteRateLimitTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `cd backend-dotnet && ! grep -RniE "humanizer|bypass|undetect|detector|evade" src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs src/ReplyInMyVoice.Application/Common`

## DO NOT

- Do NOT change the `ApiKey.Scope` entity default (`"[]"`), `GenerateApiKeyCommand`, or the generate/rotate API surface.
- Do NOT add an EF migration or alter the `Scope` column.
- Do NOT make `"[]"`/null/empty scopes deny access — that would break every existing key.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, the engine error-code set, sandbox/`IsTest` behavior, or rate limiting.
- Do NOT introduce banned terms. Do NOT push, open a PR, or merge to `main` — work only on `delivery/backend-hardening-2`.