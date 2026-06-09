# RFX-06: Backend ops hardening — pepper-fatal, RowVersion, provider-failure release (FIX-17, FIX-21, FIX-22)

**Tier:** 1 (merged to base) · **Owner:** Codex · **Depends on:** RFX-05
Detailed findings: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#17, #21, #22).

## Context
- `ApiKeyService.ComputeHash` (`backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs` ~33-57): missing `API_KEY_PEPPER` logs once and proceeds with unpeppered SHA-256.
- `RewriteJobProcessor.cs` (~89-96, 133-140): a provider hard-failure rethrows, leaving the attempt `Processing` + quota reserved until the 15-min expiry sweep; redelivery never retries the provider.
- `ApiKey.cs` (~25) and peers: `RowVersion` is a client-managed `Guid` concurrency token (must be bumped by every writer) rather than a DB-generated rowversion.

## Changes required
1. **FIX-17 pepper fatal in prod:** when running in production and `API_KEY_PEPPER` is missing/blank, **throw** at startup or first hash (gated on environment) instead of a once-logged warning. Keep dev/test tolerant. (Do NOT change the hash algorithm in this issue to avoid invalidating live keys; just make absence fatal in prod + leave a clear comment about the future HMAC/rotation option.)
2. **FIX-22 provider-failure release:** on a provider hard-failure in `RewriteJobProcessor`, release the reservation immediately (mark the attempt `Failed`/`Expired`, uncharged) instead of leaving it `Processing` until the sweep; add a bounded provider retry only if it fits the existing pattern, else just release promptly. Preserve the billing invariant (failed = charge 0).
3. **FIX-21 RowVersion:** lowest-risk option — add a focused test/assertion that the concurrency token is bumped on the key write paths, and document the client-managed-Guid contract; OR (if low-risk) migrate the token to a DB-generated `rowversion`. Note the choice in DEVIATIONS (do not destabilize existing optimistic-concurrency tests).

## Acceptance (machine-checkable)
- [ ] xUnit: in a simulated production env a missing pepper makes key hashing/startup throw; in test it does not; a provider hard-failure releases the reservation (attempt terminal + `UsedCount` unchanged, not stuck `Processing`).
- [ ] `cd backend-dotnet && dotnet test` green; `dotnet build` green (if a migration was added for rowversion, no double-cascade FK).
- [ ] Banned-term grep clean; pepper value never logged.

## Do NOT
- Do NOT change `ComputeHash`'s pepper+SHA-256 formula (would invalidate all live keys). Do NOT alter the website rewrite finalize semantics beyond the prompt release fix.
