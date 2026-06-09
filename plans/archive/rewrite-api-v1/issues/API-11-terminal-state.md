# API-11: Terminal-state guarantee for API attempts (poll never hangs forever)

**Tier:** 2 · **Owner:** Codex · **Depends on:** API-03

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §State and Error Handling.
- `QuotaService.ReleaseExpiredReservationsAsync` (`QuotaService.cs:345`) sweeps `Pending` reservations past their TTL → drives the attempt to `Expired` (uncharged). The worker (`RewriteJobProcessor`) sets `Failed` on provider failure/timeout (uncharged).
- Reservation TTL = 15 min (`RewriteRequestService.cs`: `TimeSpan.FromMinutes(15)`).

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets. No push/touch `main`.
- Do NOT alter the worker's core engine logic — this is verification + (only if a real gap exists) a minimal safety fix.

## Changes required
1. Verify that an API-originated attempt that never completes (worker never runs, or provider times out) reaches a TERMINAL state (`Failed`/`Expired`, uncharged) via the existing TTL sweep + worker release paths, so `GET /api/v1/rewrite/{id}` eventually returns `failed` rather than `processing` forever.
2. Add tests proving it. If a real gap exists (e.g. the sweep isn't reachable for the API path), make the minimal fix.

## Acceptance (machine-checkable)
- [ ] xUnit: a `Pending` attempt past its TTL, after `ReleaseExpiredReservationsAsync(now)`, is `Expired`, its reservation released (`ReservedCount` decremented, `UsedCount` unchanged).
- [ ] A provider-failure path leaves the attempt `Failed` and uncharged (assert it holds for an API-originated attempt).
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT change the engine or finalize/charge semantics. Do NOT extend the TTL.
