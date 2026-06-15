## Context

You are implementing issue **STRUCT-03** of the backend hardening wave on a .NET 8 / Azure Functions backend. Wave spec: `/Users/qc/Desktop/CloudFlare/plans/backend-hardening-2/SPEC.md` (section STRUCT-03). Goal: remove all inline `AppDbContext` access from the V1 public-API HTTP handler and the API-key auth resolver, routing it through Application-layer repository ports.

Read these first (every claim below is anchored in them):
- `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` — injects `AppDbContext db` (ctor `:24-31`); inline EF at user lookup (`:129-132`, `:463-466`), sandbox create `CreateSandboxAttemptAsync` (`:550-601`), usage write `TryWriteApiKeyUsageAsync` (`:722-753`). Five `TODO(DDD)-64` markers.
- `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs` — static class taking `AppDbContext db`; reads `db.ApiKeys` by `KeyHash`, sets `LastUsedAt`, saves. Called only from V1RewriteHttpFunctions (`:67, :280, :386`).
- `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IApiKeyUsageRepository.cs` (read-only today), `IApiKeyRepository.cs`, `IAppUserRepository.cs` (`GetByIdAsync` exists), `IRewriteAttemptRepository.cs` (`AddAsync` + `GetByUserIdAndIdempotencyKeyAsync` exist), `IUnitOfWork.cs`.
- `/Users/qc/Desktop/CloudFlare/backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/{ApiKeyUsageRepository,ApiKeyRepository,RewriteAttemptRepository,AppUserRepository}.cs` — pattern: repos only stage (`db.X.AddAsync`/tracked mutation); persistence via `IUnitOfWork.SaveChangesAsync`. DI in `Infrastructure/ServiceCollectionExtensions.cs:85-106`.
- Tests: `/Users/qc/Desktop/CloudFlare/backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyAuthResolverTests.cs` (7 tests call the static resolver with `AppDbContext`), `V1RewriteRateLimitTests.cs` (real DI host), `RewriteApiTests.cs`.

## Constraints

- Base branch `delivery/backend-hardening-2`. Worker must NEVER push, open a PR, or touch `main`.
- Do NOT change the `IRewriteEngineClient` contract, `ResultJson` shape, or the V1 error-code set / HTTP envelopes — behavior must be byte-for-byte preserved (frozen black box, HARD-01).
- Banned substrings anywhere (CI grep guard, halt on match): `humanizer|bypass|undetect|detector|evade`.
- No secret values in tracked files; validate env at runtime in the handler, not at import.
- No EF migration, no schema change, no new index. Keep the existing 799 tests green and ADD tests.
- Repos stage only; commit through `IUnitOfWork.SaveChangesAsync`. Preserve the existing best-effort semantics: the `LastUsedAt` write and the `ApiKeyUsage` write are best-effort and must NOT fail the request on `DbUpdateException` (see resolver `:48-56` and `:737-752`).
- Keep dual-provider behavior intact (SQLite tests + Azure SQL prod); do not regress the sandbox `IgnoreQueryFilters()` read path (`:561-566`).

## Changes required

1. **`IApiKeyUsageRepository`**: add `Task AddAsync(ApiKeyUsage usage, CancellationToken ct = default);`. Implement in `ApiKeyUsageRepository` as `await db.ApiKeyUsages.AddAsync(usage, ct);` (stage only).
2. **`IApiKeyRepository`**: add a by-hash lookup + last-used write. Add `Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);` (tracked) and `void TouchLastUsed(ApiKey apiKey, DateTimeOffset now);` (sets `apiKey.LastUsedAt = now` on the already-tracked entity). Implement both in `ApiKeyRepository` following the existing tracked-query pattern.
3. **`ApiKeyAuthResolver`**: convert from a static class to an injectable scoped service (e.g. `sealed class ApiKeyAuthResolver(IApiKeyRepository apiKeys, IUnitOfWork unitOfWork)`), removing the `using ReplyInMyVoice.Infrastructure.Data;` import and the `AppDbContext` parameter. Keep `ResolveAsync` / `ResolveUserIdAsync` signatures otherwise identical (drop the `AppDbContext db` arg). Internally: `GetByKeyHashAsync`, validate revoked/expired exactly as today, `TouchLastUsed` + `unitOfWork.SaveChangesAsync` wrapped in the same best-effort try/catch (on `DbUpdateException`, swallow — do not block a valid key). Register it `AddScoped<ApiKeyAuthResolver>()` in the Functions DI (find where Functions register services; if registration lives in `Infrastructure/ServiceCollectionExtensions.cs` add it there).
4. **`V1RewriteHttpFunctions`**: change the primary constructor to inject `ApiKeyAuthResolver authResolver`, `IAppUserRepository appUsers`, `IRewriteAttemptRepository rewriteAttempts`, `IApiKeyUsageRepository apiKeyUsages`, `IUnitOfWork unitOfWork` and REMOVE `AppDbContext db`. Replace the 3 `ApiKeyAuthResolver.ResolveAsync(request, db, now, ct)` call sites with `authResolver.ResolveAsync(request, now, ct)`. Replace user lookups (`:129-132`, `:463-466`) with `appUsers.GetByIdAsync(...)` (use a no-tracking variant only if one exists; otherwise `GetByIdAsync` is acceptable — read-only here). Reimplement `CreateSandboxAttemptAsync` using `rewriteAttempts.GetByUserIdAndIdempotencyKeyAsync(...)` for the dedupe read (note: it must still ignore query filters for the sandbox key — if the existing repo method does not ignore filters, add a dedicated `GetByUserIdAndIdempotencyKeyIgnoringFiltersAsync` to `IRewriteAttemptRepository` rather than reaching into `db`) and `rewriteAttempts.AddAsync(...)` + `unitOfWork.SaveChangesAsync(...)` for the insert. Reimplement `TryWriteApiKeyUsageAsync` using `apiKeyUsages.AddAsync(...)` + `unitOfWork.SaveChangesAsync(...)`, keeping the swallow-all best-effort catch.
5. **Remove all five `TODO(DDD)-64` / `DDD-64` comment markers** from `V1RewriteHttpFunctions.cs`.
6. **Update `ApiKeyAuthResolverTests`**: construct the resolver via the new service (build it from a real scoped provider or `new ApiKeyAuthResolver(new ApiKeyRepository(db), new UnitOfWork(db))`) and drop the `db` argument from the `ResolveAsync`/`ResolveUserIdAsync` calls. Keep all 7 assertions (LastUsedAt set on valid/test key; unchanged on revoked/expired/unknown/missing-header/unknown-prefix).
7. **Add `tests/ReplyInMyVoice.Tests/V1RewriteRepositoryRoutingTests.cs`**: a test that drives `V1SubmitRewrite` (live key) and asserts a `RewriteAttempt` row and an `ApiKeyUsage` row are persisted via the handler (proving the repository path writes), plus a sandbox (`rmv_test_`) path asserting the sandbox attempt is created and idempotency-reuse returns the same attempt id. Reuse the DI host fixture pattern from `V1RewriteRateLimitTests.cs`.

## Acceptance

- `cd backend-dotnet && grep -rn "AppDbContext" src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs` returns nothing
- `cd backend-dotnet && ! grep -rn "DDD-64" src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`
- `cd backend-dotnet && grep -n "AddAsync" src/ReplyInMyVoice.Application/Abstractions/IApiKeyUsageRepository.cs`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~V1RewriteRepositoryRoutingTests"`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~ApiKeyAuthResolverTests|FullyQualifiedName~V1RewriteRateLimitTests|FullyQualifiedName~RewriteApiTests"`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `grep -RniE "humanizer|bypass|undetect|detector|evade" backend-dotnet/src/ReplyInMyVoice.Functions backend-dotnet/src/ReplyInMyVoice.Infrastructure backend-dotnet/src/ReplyInMyVoice.Application || true`

## DO NOT

- Do NOT change `IRewriteEngineClient`, `ResultJson`, V1 error codes, or any HTTP response body/status (frozen black box).
- Do NOT add an EF migration, change the schema, or add an index.
- Do NOT touch validation literals, STRUCT-02 shared-validation work, `ApiKey.Scope` enforcement (API-SCOPE), or any file outside the 8 in scope.
- Do NOT introduce banned substrings `humanizer|bypass|undetect|detector|evade` anywhere.
- Do NOT print, commit, or summarize secret values; validate env at runtime only.
- Do NOT push, open a PR, merge, or operate on `main` — work only on `delivery/backend-hardening-2`.