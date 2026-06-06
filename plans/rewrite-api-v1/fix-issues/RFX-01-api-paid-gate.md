# RFX-01: Enforce paid-plan gate on the public API (FIX-01)

**Tier:** 1 (prereq, merged to base) · **Owner:** Codex · **Depends on:** none
Detailed finding: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#1). Confirmed by BOTH reviewers + a live E2E test where a FREE-tier account's key successfully called the API.

## Context
- SPEC decision 5: "No free tier for API. A key only works when the owning account already has paid quota. Pay first, then call." This is NOT enforced today.
- Submit path: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` (~115-178) loads the plan + reserves quota with no plan-tier check.
- Plan/entitlement source: `AccountService.GetUsagePlan` / `GetOrCreateAccountSummaryAsync` (scope "free"|"paid"), `QuotaService` (period + usable `RewriteCredit`). Auth: `ApiKeyAuthResolver` returns `IsTest`. `ApiKey.PlanTier`/`MonthlyQuota` are currently dead fields.

## Changes required
1. In the **live** (`!auth.IsTest`) submit path, BEFORE reserving quota, reject accounts with **no paid entitlement** → `402` with code `api_requires_paid_plan` and a clear message. "Paid entitlement" = an active/trialing/testing subscription **OR** at least one usable (non-expired, remaining>0) purchased `RewriteCredit`. A pure free-baseline account (scope `free`, no credits) must be rejected. Do this with no quota reservation (uncharged).
2. **Sandbox** (`auth.IsTest`) keys are exempt (they are stubbed, no real quota) — unchanged.
3. Make the decision a small testable helper on `AccountService`/`QuotaService` (e.g. `HasPaidApiEntitlement(userId)`); decide PlanTier/MonthlyQuota: either wire `ApiKey.PlanTier` into this check or remove the dead fields — note the choice in DEVIATIONS.

## Acceptance (machine-checkable)
- [ ] xUnit: a free-baseline account's live key → `402 api_requires_paid_plan`, no reservation/charge; an active-subscription account → proceeds (202); an account with a usable purchased credit → proceeds; a sandbox `rmv_test_` key → still works.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` green (if any TS error-copy/types touched).
- [ ] Banned-term grep clean.

## Do NOT
- Do NOT change the website (same-origin) rewrite path's free-quota behavior — this gate is for the **public API** path only. Do NOT charge on rejection. Do NOT break sandbox keys.
