# P2-11: API key rotation flow + per-key 30-day usage in the keys table

**Tier:** 2 · **Owner:** Codex · **Depends on:** P2-02, P2-07

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §F (OPS-02/03).
- Key service: `ApiKeyService` (`Infrastructure/Services/ApiKeyService.cs` — `GenerateAsync`, `ListAsync`, `RevokeAsync`, `ComputeHash`). Keys endpoints: `ApiKeyHttpFunctions` (`/api/keys`, `/api/keys/{id}`). Per-call data: `ApiKeyUsage` (`(ApiKeyId, CreatedAt)`). Frontend: `components/developers/api-keys-panel.tsx` (now inside the Keys tab from P2-07).

## Changes required
1. **Rotate** (backend): `ApiKeyService.RotateAsync(userId, keyId)` = create a NEW key with the same `Name` (returns plaintext ONCE) AND set `RevokedAt` on the old key; ownership-checked. Expose `POST /api/keys/{id}/rotate` in `ApiKeyHttpFunctions` (Entra-authed) + Next pass-through `app/api/keys/[id]/rotate/route.ts`.
2. **Per-key analytics** (backend): extend the keys list (or add `GET /api/keys/{id}/usage`) with a **last-30-day call count + status mix** per key, computed from `ApiKeyUsage` (reuse the P2-02 aggregation, filtered by `keyId`).
3. **Frontend**: a "Rotate" action in the keys table (show-once new plaintext, same UX as create) and a per-key 30-day call count column/badge.

## Acceptance (machine-checkable)
- [ ] xUnit: `RotateAsync` creates a new active key and revokes the old (old key hash now resolves to revoked → `401`); per-key usage count is correct and ownership-isolated.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` + `npm run test` green (update pinned copy tests).

## Do NOT
- Do NOT print/log plaintext or the pepper. Do NOT weaken `ApiKeyAuthResolver` (revoked keys must still 401).
- Do NOT change the hashing scheme.
