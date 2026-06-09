# PROMO-06 — Free baseline 3→0 + consistency migration (Phase 3, TIER 1) — RISK CHECKPOINT

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §16.1, §4, D2/D15). Deps: PROMO-02, PROMO-03.

## Context
Flip new-user free quota from 3 to 0 so trial rewrites come ONLY from redeeming a code. This is the top regression risk — display, `/api/me`, `ReserveAsync`, and existing DB rows must all agree.

## Changes required
1. **First, in code, determine which value `QuotaService.ReserveAsync` trusts** for the free period: the `GetUsagePlan` constant or the persisted `UsagePeriod.QuotaLimit`. Document the finding in the PR description. (`AccountService.GetOrCreateAccountSummaryAsync` computes `periodRemaining` from the constant.)
2. `AccountService.GetUsagePlan` — free `QuotaLimit` `3 → 0`, backed by config `FREE_BASELINE_REWRITES` (default 0) for reversibility.
3. EF data migration: `UPDATE UsagePeriods SET QuotaLimit=0, UpdatedAt=<now> WHERE PeriodKey='free:lifetime'` (zero existing free rows per D15). Forward-only; additive/safe.
4. Extend `AccountService.DeleteAccountAsync` to also anonymize `PromoCodeRedemption` for the erased user (null `RedeemIpHash`; keep the row).

## Acceptance (machine-checkable — the §16.1 five assertions, as tests)
- new user → `/api/me` `remaining == 0`;
- existing free user (seeded period limit 3) after migration → `remaining == 0`;
- `ReserveAsync` no longer allows a 3rd free rewrite when no credit exists;
- with a PROMO credit present → `remaining == promoRemaining`;
- PROMO credit exhausted → exhausted/paywall state true.
- account-erase test covers `PromoCodeRedemption`.
- `dotnet test` green.

## Constraints / Do NOT
- Do NOT touch paid quota (90) or consumption logic.
- Migration is forward-only; NO `migrate reset`/`--force-reset`/drops.
- No banned terms. No push/PR/deploy.
