# API-02: ApiKeyAuthResolver — Bearer rmv_live_ → user id

**Tier:** 1 (prereq) · **Owner:** Codex · **Depends on:** API-01

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §Proposed Architecture, §Security and Privacy.
- Existing auth resolver to mirror in shape (Entra only): `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs` (`ResolveUserAsync`, `ResolveBearerToken`).
- Reuse `ApiKeyService.ComputeHash` (from API-01) for hashing.
- `ApiKey` lookup target: `AppDbContext.ApiKeys`, unique `KeyHash` index.

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`.
- No secret values in tracked files; pepper read at runtime (via `ApiKeyService.ComputeHash`).
- Do NOT push or touch `main`.

## Changes required
1. **New `ApiKeyAuthResolver`** at `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs`:
   - `Task<Guid?> ResolveUserIdAsync(HttpRequest request, AppDbContext db, DateTimeOffset now, CancellationToken)`:
     1. extract `Authorization: Bearer <token>`; if missing OR not prefixed `rmv_live_` → return `null`.
     2. `hash = ApiKeyService.ComputeHash(token)`; find the `ApiKey` where `KeyHash == hash`.
     3. return `null` if: not found, `RevokedAt != null`, or (`ExpiresAt != null && ExpiresAt <= now`).
     4. on success: best-effort set `LastUsedAt = now` (save), and return `UserId`.
   - Keep it dependency-light (static helper or simple class) so HTTP functions can call it like `FunctionAuthResolver`.

## Acceptance (machine-checkable)
- [ ] New xUnit tests: valid active key → its `UserId`; unknown token → `null`; revoked key → `null`; expired key (`ExpiresAt` in the past) → `null`; missing `Authorization` → `null`; a non-`rmv_live_` token → `null`.
- [ ] On a successful resolve, the row's `LastUsedAt` is updated.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT wire it into any endpoint yet (API-03/04/09 do that).
- Do NOT modify `FunctionAuthResolver` (the Entra path) or the website rewrite flow.
