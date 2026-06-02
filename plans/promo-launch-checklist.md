# Promo Launch Checklist

PROMO-12 final verification before the wave deploy stage. This checklist does
not authorize deploys; it maps the five launch-gating checkpoints from
`plans/promo-code-trial-spec.md` §18 to tests that lock each behavior.

## Acceptance Commands

Run all of these before the supervised deploy stage:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln
npm run test
npm run typecheck
npm run test:e2e
npm run smoke:promo-preview
restricted-term grep from AGENTS.md over app components public lib
```

`npm run smoke:promo-preview` runs OpenNext Cloudflare preview locally with
Cloudflare Turnstile test keys, a local Azure mock, and smoke calls to `/`,
`/pricing`, `/sign-in`, `/app`, and `/api/promo/redeem`.

## Gate Mapping

| Launch gate | Locked by passing tests |
| --- | --- |
| Free-baseline cutover: display, `/api/me`, `ReserveAsync`, and DB agree at zero until promo credits exist. | `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs` -> `GetOrCreateAccountSummary_creates_user_and_reports_free_usage`, `GetOrCreateAccountSummary_ignores_old_free_period_limit_after_cutover`, `GetUsagePlan_uses_free_baseline_configuration_override`, `AccountSummaryIncludesPromoBlockAndTrialCreditLabel`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs` -> `ReserveAsync_rejects_free_rewrite_without_credit_when_old_period_has_two_used`, `ReserveAsync_uses_valid_credit_when_period_quota_is_full_and_success_keeps_credit_consumed`, `ReserveAsync_returns_quota_exceeded_when_period_full_and_no_usable_credit`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FreeBaselineMigrationTests.cs` -> `FreeBaselineZeroMigration_updates_existing_free_lifetime_periods`; `tests/e2e/promo-full-loop.spec.ts` -> `signup, login, redeem, trial rewrites, and paywall`; `tests/e2e/promo-redeem-ui.spec.ts` -> redeem card, trial quota, and exhausted-paywall cases. |
| Global-cap race: atomic cap under parallel redeem load. | `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs` -> `Global_cap_one_with_parallel_users_grants_one_success_and_exhausts_the_rest`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs` -> `RedeemAsync_global_cap_allows_exactly_n_parallel_redemptions`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs` -> `Redeem_exhausted_code_returns_code_exhausted`. |
| Proxy trusted IP: proxy secret and trusted client IP handling fail closed where required. | `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs` -> `Redeem_api_treats_client_ip_as_untrusted_when_proxy_secret_is_missing_or_mismatched`, `RedeemAsync_in_production_without_trusted_proxy_ip_fails_closed_without_credit`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs` -> `Redeem_ignores_client_forwarded_for_without_proxy_secret`; `tests/unit/promo-redeem-route.test.ts` -> `verifies Turnstile and forwards auth, trusted IP, and proxy secret`, `returns server_config in production when the proxy secret is missing`; `npm run smoke:promo-preview` -> confirms preview redeem reaches the mock backend only after forwarding the trusted client IP and proxy secret. |
| Turnstile env: test keys in local/preview and server-config failure when prod secret is missing. | `tests/unit/promo-redeem-route.test.ts` -> `returns invalid_captcha when the token is missing`, `returns invalid_captcha when Turnstile rejects the token`, `returns server_config in production when the Turnstile secret is missing`; `tests/unit/auth-signup-routes.test.ts` -> `rejects listed email domains before calling Entra`, `rejects missing Turnstile tokens before calling Entra`, `rejects failed Turnstile checks before calling Entra`, `rejects missing Turnstile config before calling Entra`; `tests/e2e/auth-gate.spec.ts` -> signup Turnstile widget and token submission cases; `tests/e2e/promo-full-loop.spec.ts` -> signup Turnstile and redeem Turnstile token flow; `npm run smoke:promo-preview` -> verifies Turnstile siteverify on Worker preview before redeem is forwarded. |
| Admin auth / audit: non-admin blocked and every promo mutation audited. | `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminAccessTests.cs` -> `AdminPing_NonAdminForbidden`, admin allowlist cases; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminAuditLogTests.cs` -> `AdminAuditLogPersists`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs` -> `AdminPromoCreate_NonAdminGetsForbidden`, `AdminPromoCreate_AddsRowAndAuditLog`, `AdminPromoCreate_DuplicateCodeReturnsBadRequest`, `AdminPromoDisable_SetsInactiveAndAudits`, `AdminPromoDetail_StatsExposeActivationRateAndHashClustersOnly`; `tests/e2e/admin-promo-codes.spec.ts` -> signed-out redirect, non-admin view, admin create/duplicate/stats/disable flow; `tests/unit/admin-promo-codes.test.ts` and `tests/unit/admin-promo-proxy-route.test.ts` -> client validation, sanitized stats, and proxy mutation forwarding. |

## Launch Notes

- Preview smoke must use Cloudflare Turnstile test keys. The production
  domain-locked key is not used for localhost or Worker preview.
- Do not run deploy commands from PROMO-12. The supervised wave deploy stage
  performs cutover after all issue checks are green.
- Do not change Stripe live/cutover settings, DNS, or `LAUNCH_CONFIRMED` in this
  issue.
