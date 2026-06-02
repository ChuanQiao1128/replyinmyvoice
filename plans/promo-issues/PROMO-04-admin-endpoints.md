# PROMO-04 — Admin promo endpoints + audit (Phase 1, TIER 1)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §7). Deps: PROMO-01 (PROMO-02 for stats shape).

## Context
Let the owner self-serve promo codes. Reuse the existing admin auth (`AdminAccess.RequireAdminAsync` → `ADMIN_EMAILS`) and audit (`AdminAuditLog`) patterns. Backend endpoints only here; the admin UI page is PROMO-10.

## Changes required
1. `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs` (or a new `PromoAdminService.cs`): methods to create / list / get-detail-with-stats / edit / disable / enable promo codes. Create validates: unique normalized `Code`, numeric rules mirroring the CHECK constraints, `ValidUntil>ValidFrom`; store both normalized `Code` and original `DisplayCode`. **Every mutation writes `AdminAuditLog`** with admin `oid`+email, `promoCodeId`, action, and a changed-fields summary.
2. `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoAdminHttpFunctions.cs` (or extend `AdminHttpFunctions.cs`): `POST admin/promo-codes`, `GET admin/promo-codes`, `GET admin/promo-codes/{id}`, `PATCH admin/promo-codes/{id}`, `POST admin/promo-codes/{id}/disable`, `POST admin/promo-codes/{id}/enable` — all gated by `RequireAdminAsync` → `403` for non-admins.
3. Stats (detail endpoint): total redemptions, distinct users, activation rate (redeemers whose linked `RewriteCredit.AmountConsumed>0`), daily curve, and IP-hash CLUSTERS only (never raw IPs).

## Acceptance (machine-checkable)
- Integration tests: non-admin → `403`; admin create → row + audit log; duplicate code → explicit `400` (not 500); disable → `IsActive=false`; stats payload contains activation rate and exposes only IP hashes.
- `dotnet test` green.

## Constraints / Do NOT
- Reuse `RequireAdminAsync` + `AdminAuditLog`; audit EVERY mutation.
- Stats must NOT expose raw IPs — hashes/clusters only.
- No secret values in tracked files; no banned terms; no push/PR/deploy.
