# API-06: Key-management endpoints — POST/GET/DELETE /api/keys (Entra-authed)

**Tier:** 1 (prereq) · **Owner:** Codex · **Depends on:** API-01

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §"Key management".
- Entra auth resolver: `FunctionAuthResolver.ResolveUserAsync` (`backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`).
- `ApiKeyService` (API-01): `GenerateAsync`, `ListAsync`, `RevokeAsync`.
- `AccountService.GetOrCreateUserAsync(externalId, email, ct)` resolves the Entra user → `AppUser`.
- Next proxy pattern (same-origin + Entra token) to mirror: `app/api/me/route.ts` + `lib/azure-api.ts`.

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets. No push/touch `main`.
- These are account-management endpoints → **Entra session auth** (NOT API key).

## Changes required
1. **New `ApiKeyHttpFunctions`** (`backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`):
   - `POST /api/keys` (Entra) — body `{ name }` → `GenerateAsync` → `201 { id, name, key: <plaintext>, createdAt }` (plaintext ONCE).
   - `GET /api/keys` (Entra) — `ListAsync` → `200 [{ id, name, maskedKey, lastUsedAt, createdAt, revokedAt }]`.
   - `DELETE /api/keys/{id:guid}` (Entra) — `RevokeAsync(userId, id)` → `204`; not owned/not found → `404`.
2. **Next proxy routes**: `app/api/keys/route.ts` (POST/GET) and `app/api/keys/[id]/route.ts` (DELETE), same-origin + Entra token forwarding (mirror `app/api/me/route.ts`).

## Acceptance (machine-checkable)
- [ ] xUnit: create returns a plaintext key once; list returns masked keys (no plaintext); revoke sets `RevokedAt`; revoking another user's key → `404`.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` + `npm run test` green.

## Do NOT
- Do NOT key these endpoints by API key (they are Entra/session). Do NOT touch the v1 rewrite endpoints.
