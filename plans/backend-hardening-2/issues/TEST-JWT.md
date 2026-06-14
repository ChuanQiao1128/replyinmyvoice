## Context

- Repo root: `/Users/qc/Desktop/CloudFlare`. Base branch: `delivery/backend-hardening-2` (NEVER `main`).
- Read first:
  - `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs` — `ValidateBearerTokenAsync` (lines 118-172) builds `TokenValidationParameters` (lines 147-157) and calls `new JwtSecurityTokenHandler().ValidateToken(...)` (lines 161-164). Signing keys come from a `private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>` (line 15) via `GetOrAdd` (lines 131-135) and `await manager.GetConfigurationAsync(...)` (line 140). `ResolveUserAsync` (lines 17-53) is the public entry; the bearer path runs only after header-auth (lines 22-26) and `request.HttpContext.User` (lines 28-35) both miss.
  - `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs` — 11 existing tests; all inject `ClaimsPrincipal` onto `request.HttpContext.User` or call pure helpers; none signs a JWT or hits `ValidateToken`. `CreateRequest` (lines 205-214) and `BuildConfiguration` (lines 216-220) are the helpers to reuse.
  - `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/AdminAccess.cs:12` — one of 28 call sites of `ResolveUserAsync`; they all use the existing `(request, configuration, cancellationToken)` form and must keep compiling unchanged.
- `ConfigurationManager<OpenIdConnectConfiguration>` implements `IConfigurationManager<OpenIdConnectConfiguration>` from `Microsoft.IdentityModel.Protocols` 7.1.2 (already referenced in `src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj`, along with `System.IdentityModel.Tokens.Jwt` 7.1.2). The test project references the Functions project. `GetConfigurationAsync(CancellationToken)` returns an `OpenIdConnectConfiguration` whose `.SigningKeys` and `.Issuer` are read at lines 140/150/152.

## Constraints

- Banned substrings anywhere (CI grep guard, halt on match): `humanizer | bypass | undetect | detector | evade`. Do not introduce them in names, comments, or strings.
- Do NOT change `IRewriteEngineClient` / rewrite-engine contract.
- Keep all 799 existing tests green; this is additive. The 28 production callers of `ResolveUserAsync` must remain source-compatible — make the seam an OPTIONAL parameter (defaulting to the real cached manager) or a separate internal overload, never a breaking signature change.
- Do NOT relax any production validation rule: `ValidateIssuerSigningKey`, `ValidateIssuer`/`ValidIssuers`, `ValidateAudience`/`ValidAudiences`, `ValidateLifetime`, and `ClockSkew = 2 min` stay exactly as-is. The seam only replaces WHERE signing keys come from, not WHETHER they are checked.
- No real network/metadata calls in tests; no secret values in tracked files. The RSA key is generated in-test.

## Changes required

1. In `FunctionAuthResolver.cs`, add an injectable OIDC-config seam without breaking callers. Preferred shape: add an optional trailing parameter `IConfigurationManager<OpenIdConnectConfiguration>? configurationManagerOverride = null` to `ResolveUserAsync` and thread it to `ValidateBearerTokenAsync`. When the override is non-null, use it for `GetConfigurationAsync(...)` instead of the cached `ConfigurationManagers.GetOrAdd(...)`; when null, behavior is byte-for-byte the current production path (still keyed by metadata address, same caching). Keep the existing 3-arg call form valid for all 28 callers.
   - Alternative acceptable shape if the optional param is awkward: keep the public method unchanged and add an `internal static Task<ClaimsPrincipal?> ValidateBearerTokenAsync(string token, IConfiguration configuration, IConfigurationManager<OpenIdConnectConfiguration> configManager, CancellationToken)` overload plus `[assembly: InternalsVisibleTo("ReplyInMyVoice.Tests")]`. The optional-parameter route is preferred because it needs no InternalsVisibleTo.
2. In `FunctionAuthResolverTests.cs`, add a private test helper that: (a) generates an in-test RSA key (`RSA.Create(2048)`), wraps the PUBLIC key as an `RsaSecurityKey`, and builds a stub `IConfigurationManager<OpenIdConnectConfiguration>` (or a real `ConfigurationManager` fed a static `OpenIdConnectConfiguration`) exposing that key as a `SigningKey` plus a matching `Issuer`; (b) mints a signed JWT with `JwtSecurityTokenHandler.CreateEncodedJwt` / `SecurityTokenDescriptor` using configurable issuer/audience/expiry/signing-key and an `oid` claim. Configure `BuildConfiguration` with matching `ENTRA_AUTHORITY` + audience so the production validation parameters are populated.
3. Add the three negative-path `[Fact]` tests + one positive control, all calling `ResolveUserAsync` (bearer path, empty `HttpContext.User`) with the seam injected:
   - `ResolveUserAsync_rejects_token_signed_with_wrong_key` → token signed by a DIFFERENT RSA key than the config-manager publishes → `null`.
   - `ResolveUserAsync_rejects_token_with_wrong_audience` → `aud` not in configured audiences → `null`.
   - `ResolveUserAsync_rejects_expired_token` → `exp` ~10 min in the past (beyond 2-min skew) → `null`.
   - `ResolveUserAsync_accepts_valid_signed_token` (positive control) → correctly signed, in-audience, unexpired, `oid` present → non-null with expected `ExternalAuthUserId`.

## Acceptance

- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~FunctionAuthResolverTests` passes and shows the 4 new tests (3 negative + 1 positive control) plus the 11 existing ones.
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` is fully green (>= 803 tests; no regressions to the existing 799).
- `grep -RniE "humanizer|bypass|undetect|detector|evade" backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs || true` prints nothing.
- `grep -RnE "ResolveUserAsync\(" backend-dotnet/src/ReplyInMyVoice.Functions | grep -v configurationManagerOverride` still shows the 28 production call sites compiling unchanged (no caller edits required).

## DO NOT

- Do NOT push, open a PR, merge, or touch `main`/`master`. Commit only on the issue worktree branch off `delivery/backend-hardening-2`.
- Do NOT change `IRewriteEngineClient`, `ResultJson`, or the engine error-code set.
- Do NOT weaken any token-validation flag or the 2-minute `ClockSkew`; do NOT make a negative test pass by disabling a check.
- Do NOT make a breaking signature change to `ResolveUserAsync` or edit the 28 call sites.
- Do NOT add real network calls, live metadata fetches, or secret values to tracked files.
- Do NOT touch files outside the two in scope.