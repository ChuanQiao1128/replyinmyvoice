# P2-02: Usage aggregation endpoints (summary / series / recent)

**Tier:** 1 (prereq, merged into base) · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §B (USE-01/02/03/05).
- Per-call events ALREADY exist: `ApiKeyUsage` (`Domain/Entities/ApiKeyUsage.cs`: `ApiKeyId, Endpoint, StatusCode, LatencyMs, CostUsdEstimate, CreatedAt`), one row per v1 call, indexed `(ApiKeyId, CreatedAt)`. A user owns many `ApiKey` rows (`UserId`).
- Entra (portal) auth: `FunctionAuthResolver.ResolveUserAsync`. Quota math to reuse: `AccountService.GetOrCreateAccountSummaryAsync` (`AccountService.cs:74-161`).
- Next portal pass-through pattern (forward Entra access token, same-origin): copy `app/api/keys/route.ts` (`getCurrentAccessToken` + `getAzureApiBaseUrl` + `requireSameOrigin`).

## Changes required
1. **New query service** (e.g. `ApiKeyUsageQueryService` in Infrastructure/Services) aggregating `ApiKeyUsage` for a `userId` across all the user's keys. Treat `succeeded = StatusCode in (200,202)`, else `failed`. **Bucket days in the `Pacific/Auckland` time zone** (`TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland")`; this is the business TZ — document it):
   - `GetSummaryAsync`: `{ today, yesterday, monthToDate }` each `{ calls, succeeded, failed }`, plus `last30dCalls`, plus `{ quota, used, remaining, periodEnd }` from the account summary.
   - `GetSeriesAsync(days=30)`: `[{ date:"yyyy-MM-dd", calls, succeeded, failed }]` (zero-filled for empty days).
   - `GetRecentAsync(limit=50, cap 200)`: `[{ createdAt, endpoint, statusCode, latencyMs, keyLast4 }]` newest first.
2. **Functions endpoints** (Entra-authed): `GET api/me/api-usage/summary`, `GET api/me/api-usage/series?days=`, `GET api/me/api-usage/recent?limit=`. Resolve user via `FunctionAuthResolver`; `401` if unauthenticated.
3. **Next pass-through routes** `app/api/me/api-usage/{summary,series,recent}/route.ts` (same-origin, forward Entra token).

## Acceptance (machine-checkable)
- [ ] xUnit: seed `ApiKeyUsage` rows across multiple days/status for user A AND user B → A's summary/series/recent counts are correct and contain **no** B rows (ownership isolation). A row timestamped near NZ midnight buckets into the correct local day.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` green.

## Do NOT
- Do NOT recompute quota differently from `AccountService`. Do NOT drop same-origin on the `/api/me/*` routes.
- Do NOT add a new DB table or daily-rollup table — aggregate on the fly from `ApiKeyUsage`.
