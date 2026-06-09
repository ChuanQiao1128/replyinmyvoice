# API-01: ApiKeyService — generate / hash / list / revoke (+ Last4 column)

**Tier:** 1 (prereq) · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — read §Data Model and §"Key format & hashing".
- The `ApiKey` entity already exists with a unique `KeyHash` index but has ZERO attached logic:
  - `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKey.cs` (fields incl. `KeyHash`, `Name`, `RateLimitPerMinute=60`, `ExpiresAt`, `RevokedAt`, `LastUsedAt`, `RowVersion`).
  - EF mapping + `KeyHash` unique index: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs` (~line 338).
- DI registration: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`.
- Service style to mirror (constructor-injected `Func<AppDbContext>` factory): `RewriteRequestService.cs`, `AccountService.cs`.

## Constraints (AGENTS.md + SPEC)
- Banned terms (CI grep on app/components/public/lib): `humanizer|bypass|undetect|detector|evade`.
- No secret values in tracked files; read `API_KEY_PEPPER` from env at RUNTIME inside the service, never at import. A placeholder may appear only in a scoped `*.example` file.
- Do NOT push or touch `main`.

## Changes required
1. **Add nullable `Last4` column** to `ApiKey` (`ApiKey.cs`), map it in `AppDbContext`, and generate an EF migration (`dotnet ef migrations add AddApiKeyLast4` in the Infrastructure project). `Last4` = last 4 chars of the plaintext, for masked display; it is NOT a secret.
2. **New `ApiKeyService`** at `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs`:
   - `static string ComputeHash(string plaintext)` — lowercase hex SHA-256 of `API_KEY_PEPPER ‖ plaintext` (pepper read from env at runtime; if unset, hash plaintext alone and log a warning once).
   - `Task<(Guid Id, string Plaintext)> GenerateAsync(Guid userId, string name, CancellationToken)` — plaintext = `"rmv_live_" + base62(32 CSPRNG bytes)`; persist `ApiKey { UserId, Name, KeyHash=ComputeHash(plaintext), Last4=last4(plaintext) }`; return the plaintext ONCE (never persisted).
   - `Task<IReadOnlyList<ApiKeySummary>> ListAsync(Guid userId, CancellationToken)` — rows for the user, each `{ Id, Name, MaskedKey = "rmv_live_••••" + Last4, LastUsedAt, CreatedAt, RevokedAt }`. NEVER returns plaintext or hash.
   - `Task<bool> RevokeAsync(Guid userId, Guid keyId, CancellationToken)` — set `RevokedAt=now` only if the key belongs to `userId`; return false if not found / not owned.
3. **Register** `ApiKeyService` in `ServiceCollectionExtensions`.

## Acceptance (machine-checkable)
- [ ] New xUnit tests in `backend-dotnet/tests/ReplyInMyVoice.Tests/`:
  - `GenerateAsync` returns a `rmv_live_`-prefixed plaintext; the stored row's `KeyHash != plaintext` AND `KeyHash == ApiKeyService.ComputeHash(plaintext)`; `Last4` == last 4 chars of plaintext.
  - `ComputeHash` is deterministic for the same input.
  - `ListAsync` never exposes plaintext or hash; `MaskedKey` ends with the stored `Last4`.
  - `RevokeAsync` sets `RevokedAt` for the owner and returns `false` for a non-owner key id.
- [ ] `cd backend-dotnet && dotnet test` is green (existing tests still pass).
- [ ] `dotnet build` green (new migration compiles).

## Do NOT
- Do NOT implement the bearer-token auth resolver (API-02) or any HTTP endpoint (API-03/04/06).
- Do NOT change existing quota/rewrite logic.
- Do NOT print/log the plaintext or the pepper value.
