# RFX-02: Usage accounting consistency + bounds + sandbox isolation (FIX-10, FIX-12, FIX-14)

**Tier:** 1 (merged to base) · **Owner:** Codex · **Depends on:** RFX-01
Detailed findings: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#10, #12, #14). Suspect B confirmed by both reviewers.

## Context
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs` (~79-167): `Used` comes only from `UsagePeriod`, while `Quota`/`Remaining` add credit remaining → `Quota − Used ≠ Remaining` (observed `quota=2, used=1, remaining=2`).
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiUsageHttpFunctions.cs` (~51) + `ApiKeyUsageQueryService.cs` (~75, 134-144): `?days=` is unbounded and series/summary load all rows with no DB-side date bound.
- `V1RewriteHttpFunctions.cs` (~278-301): the poll guard only blocks test→live (`auth.IsTest && !IsSandboxAttempt`), so a LIVE key can read a sandbox attempt of the same user.

## Changes required
1. **FIX-10 usage math:** make the usage numbers self-consistent — define `remaining = max(0, quota − used − reserved)` and ensure `used` accounts for the same pools as `quota` (period + consumed credits), OR return an explicit per-source breakdown plus a coherent total. Add a test asserting `quota − used − reserved == remaining` for free, paid, and credit-funded cases.
2. **FIX-12 bounds:** cap `days` (e.g. 1..90; reject/`400` or clamp invalid) and push the date filter into the DB query (`WHERE CreatedAt >= windowStart`) so summary/series/recent never load the full table.
3. **FIX-14 sandbox isolation:** make the attempt-read env check symmetric — `if (attempt is null || IsSandboxAttempt(attempt) != auth.IsTest) return 404;` so a live key cannot read sandbox attempts and vice-versa.

## Acceptance (machine-checkable)
- [ ] xUnit: usage invariant `quota − used − reserved == remaining` holds across free/paid/credit; `days` out of range is clamped/400 and the query is date-bounded; a live key gets `404` for a sandbox attempt id and a test key `404` for a live attempt id.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` green.
- [ ] Banned-term grep clean.

## Do NOT
- Do NOT change the website usage-display contract in a breaking way without updating its tests. Do NOT alter the billing charge invariant.
