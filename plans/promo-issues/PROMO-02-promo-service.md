# PROMO-02 — PromoService: redeem + status (Phase 1, TIER 1)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §6, §8, §9, §10). Deps: PROMO-01.

## Context
Core redemption logic. A valid redemption grants a `RewriteCredit{Source="PROMO"}` — the EXACT shape `AdminService.GrantCreditsAsync` already creates — so the existing `QuotaService` consumption/paywall path is reused unchanged.

## Changes required
1. `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`:
   - `RedeemAsync(externalAuthUserId, email, rawCode, trustedClientIp?, now, ct)` and `GetStatusAsync(...)`.
   - Normalize code (trim, UPPER, strip spaces/dashes). Use `GetOrCreateUser` (mirror `AccountService`).
   - **Serializable transaction** via the existing `ExecuteInTransactionAsync` pattern (see `StripeEventService`).
   - **Global cap = atomic conditional UPDATE** `RedemptionCount=RedemptionCount+1 WHERE Id=@id AND IsActive AND now BETWEEN ValidFrom AND ValidUntil AND (MaxRedemptionsGlobal IS NULL OR RedemptionCount<MaxRedemptionsGlobal)`; rows-affected==0 ⇒ re-read to disambiguate. NEVER read-count-then-write.
   - **Idempotency:** check existing `(PromoCodeId,UserId)`; on the unique-index `DbUpdateException`, roll back → `AlreadyRedeemed` (no second grant).
   - Grant `RewriteCredit{UserId, Source="PROMO", AmountGranted=code.CreditsGranted, AmountConsumed=0, GrantedAt=now, ExpiresAt=now.AddDays(code.GrantTtlDays)}`; insert `PromoCodeRedemption{...RewriteCreditId, CodeSnapshot, RedeemIpHash, Applied}`.
   - IP: `HashIp` = salted SHA-256 using `PROMO_IP_HASH_SALT`; DB velocity = count Applied redemptions with same `RedeemIpHash` in last 24h; **hard-block at `PROMO_IP_VELOCITY_MAX_24H` (default 5), flag (log) from `PROMO_IP_VELOCITY_FLAG_FROM` (default 2)**.
   - **Fail-closed:** if running in prod and the trusted-IP path lacks a valid proxy secret, return a server-config error rather than silently skipping IP defense (the HTTP layer in PROMO-03 supplies/validates the secret; design the service so a null trustedIp under prod config is treated as misconfig, not "skip").
   - Result type with kinds: Success, InvalidCode, Expired, AlreadyRedeemed, CapReached, IpVelocityBlocked, (ServerConfig).

## Acceptance (machine-checkable, xUnit + SQLite)
- Happy redeem grants exactly `CreditsGranted` with `ExpiresAt≈now+GrantTtlDays`.
- Second redeem (same user+code) → `AlreadyRedeemed`, balance unchanged, exactly one credit row.
- Concurrent double-redeem (retrying execution strategy + unique index) → exactly one grant.
- Expired / inactive / not-yet-valid gates return the right kind.
- **Global cap N with N+ parallel redeems → exactly N Applied** (atomic update proven under load).
- IP velocity: blocks at 5, flags at 2.
- `dotnet test` green.

## Constraints / Do NOT
- Redemption MUST reuse `RewriteCredit{Source="PROMO"}` — do NOT add a separate trial-consumption path.
- Global cap MUST be the atomic conditional UPDATE — never check-then-write.
- Store only salted IP hashes; never store/log raw IP, code values, or secrets.
- No `migrate reset`/drops; no Stripe/DNS/LAUNCH_CONFIRMED; no banned terms; no push/PR/deploy.
