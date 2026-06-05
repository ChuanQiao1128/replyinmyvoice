# P2-06: Scheduled-jobs audit + 30-day API-attempt retention purge

**Tier:** 2 · **Owner:** Codex · **Depends on:** P2-04

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §D (OPS-05) + `SPEC.md` §Security (bounded 30-day retention).
- The async path persists input/output on `RewriteAttempt` (`RequestJson`, `ResultJson`). The public promise is a **bounded 30-day retention** for API-originated attempts — but there is no purge job. Timer functions live under `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/` (grep `[TimerTrigger]`).

## Changes required
1. **Audit:** grep all `[TimerTrigger]` functions and confirm each business timer is actually scheduled: payment grace-expiry (P2-04), Stripe reconciliation, credit-expiry reminder (if present). Record findings in a new doc `plans/rewrite-api-v1/scheduled-jobs.md` (one row per timer: name, cron, what it does, file).
2. **New purge:** a testable service method that deletes or nulls `RequestJson`/`ResultJson` (and finalizes status if needed) for **API-originated** `RewriteAttempt`s older than 30 days, plus a daily `RetentionPurgeFunction` `[TimerTrigger]` that calls it. Keep the function thin; logic in a service for unit-testability. (If attempts don't carry a source flag distinguishing API vs website, purge by age across all terminal attempts — note this decision in DEVIATIONS and the doc.)

## Acceptance (machine-checkable)
- [ ] xUnit: the purge method clears payloads on attempts older than 30 days and leaves newer ones intact (and does not touch non-terminal attempts).
- [ ] `cd backend-dotnet && dotnet test` green; `dotnet build` green.
- [ ] `plans/rewrite-api-v1/scheduled-jobs.md` exists and lists every timer with its cron.

## Do NOT
- Do NOT delete whole `RewriteAttempt` rows needed for idempotency within the window; only purge payloads past 30 days per the spec.
- Do NOT change quota/billing behavior.
