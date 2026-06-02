# PROMO-05 — Concurrency & security test suite (Phase 2, TIER 2)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §9 matrix, §16.2). Deps: PROMO-02, PROMO-03, PROMO-04.

## Context
Phase-2 gate: lock the adversarial/parallel cases that single-component unit tests don't cover. Backend (xUnit + SQLite/retrying execution strategy).

## Changes required
- Add a focused test class (e.g. `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`) covering:
  - same user double-click redeem → exactly one credit;
  - global cap=1 with N parallel users → exactly one success, rest `code_exhausted`;
  - expired / disabled code → no credit granted;
  - missing/mismatched proxy secret → service treats IP as untrusted AND (prod-config) fails closed;
  - same IP across many accounts → blocked at 5, flagged from 2;
  - `ValidUntil` boundary inclusive.

## Acceptance (machine-checkable)
- All §16.2 cases present and green; `dotnet test backend-dotnet/...` full suite green.
- Global-cap test actually runs the redeems in parallel (Task.WhenAll) and asserts exact count.

## Constraints / Do NOT
- Do NOT weaken production code to make a test pass; tests adapt to the real atomic/idempotent design.
- No `@ts-ignore`/`eslint-disable`/loosened configs. No banned terms. No push/PR/deploy.
