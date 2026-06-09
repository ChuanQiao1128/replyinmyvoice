# RFX-03: Atomic rate limit + Idempotency-Key validation + RequestId index (FIX-04, FIX-05, FIX-08)

**Tier:** 1 (merged to base; contains a migration) · **Owner:** Codex · **Depends on:** RFX-02
Detailed findings: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#4, #5, #8).

## Context
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`: rate limit counts `ApiKeyUsage` rows over the last minute (~590-638) and enforces at ~78-83, but the usage row is written AFTER the response in `TryWriteApiKeyUsageAsync` (~689-719) inside a swallow-all `catch{}` → concurrent bursts pass and the limiter degrades open under load. The `Idempotency-Key` header (~126) is stored unvalidated but `RewriteAttempt.IdempotencyKey` is capped at 120 chars (`AppDbContext.cs:85`) → overlong keys throw at SaveChanges (500).
- `AppDbContext.cs` (~378-383): `ApiKeyUsage` has no index on `RequestId`, which the webhook enqueuer + lookups query.

## Changes required
1. **FIX-04 atomic rate limit:** replace the COUNT-over-best-effort-rows limiter with an atomic fixed-window counter (e.g. a dedicated counter row updated with a single atomic `UPDATE`/`INSERT ... ON CONFLICT`-style op, or a per-key window record incremented in one statement) checked-and-incremented BEFORE processing. Do NOT depend on the post-response usage write for limiting. If the limiter's own write fails, fail **closed** (`429`/`503`), don't silently allow.
2. **FIX-05 Idempotency-Key validation:** validate the header length up-front; if it exceeds the stored column limit (120) return `400 invalid_request` (do not 500). Keep the auto-generated key path. (Keep OpenAPI's documented limit consistent — RFX-08 aligns the spec.)
3. **FIX-08 index:** add an index on `ApiKeyUsage.RequestId` (+ EF migration). Consider whether `(RequestId)` should be unique given one row per submit — if safe, make it unique; otherwise plain index (note the choice).

## Acceptance (machine-checkable)
- [ ] xUnit: exceeding the per-minute limit returns `429` even under concurrent submits and even if a usage write fails (fail-closed); an over-length `Idempotency-Key` returns `400` not `500`; the new index exists in the migration.
- [ ] `cd backend-dotnet && dotnet test` green; `dotnet build` green (migration compiles).
- [ ] Banned-term grep clean.

## Do NOT
- Do NOT introduce two cascade-path FKs in the migration (Azure SQL rejects them — see CROSS-REVIEW #11). Do NOT weaken the billing invariant.
