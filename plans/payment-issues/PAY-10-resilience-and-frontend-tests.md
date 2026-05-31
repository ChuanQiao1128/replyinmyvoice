# PAY-10: Close remaining resilience + frontend-checkout test gaps

**Priority:** P1 · **Owner:** Codex · **Skill:** dotnet-backend-testing + resilience-test-generation + ui-browser-testing · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Backend tests under `backend-dotnet/tests/ReplyInMyVoice.Tests/`; frontend under `tests/`.
- These are the lower-severity-but-high-value gaps from the payment audit (`plans/payment-module-audit.md` §6).

## Constraints (AGENTS.md)
- Banned terms: `humanizer|bypass|undetect|detector|evade`.
- No real Stripe calls. Do NOT push to `main`.

## Changes required (each is a distinct test)
1. **Refund before its grant (out-of-order on the credit side):** seed a `charge.refunded` for a `payment_intent` with NO matching `RewriteCredit` → handled safely (recorded/retryable, no crash, no negative balance). Mirrors the existing checkout-before-user test for the refund direction. (`StripeEventServiceTests.cs`)
2. **API-version pin guard:** flip `StripeConfiguration.ApiVersion` away from the pinned `2025-08-27.basil` and assert `EnsureStripeApiVersionPinned()` throws `stripe_api_version_mismatch`. (`StripeBillingService` is the unit under test.)
3. **True concurrent quota reservation race:** `Task.WhenAll` of two distinct `ReserveAsync` calls on a single remaining slot (file-backed SQLite + WAL, same harness style as the existing duplicate-grant race test) → exactly one `Created`, one `QuotaExceeded`; no over-reservation. (`QuotaServiceTests.cs`)
4. **Worker timeout/cancellation:** provider throws `OperationCanceledException`/`TaskCanceledException` in `RewriteJobProcessor` → reservation released with a timeout `ErrorCode`, quota/credit refunded. (`RewriteJobProcessorTests.cs`)
5. **Frontend checkout flow (vitest or Playwright):** `components/landing/buy-button.tsx` branches — 401 → `/sign-in?redirectTo=/pricing`; success → redirect to Stripe URL; error → inline message. Currently only a static string-contract test (`tests/unit/pricing-auth-visual-system.test.ts`) exists.

## Acceptance (machine-checkable)
- [ ] 5 test groups added; all pass.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run test` green.

## Do NOT
- Do NOT weaken production code to make a test pass.
- Do NOT touch `main` / deploy.
