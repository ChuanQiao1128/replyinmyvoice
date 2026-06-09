# DDD-68: Shell Api/Program.cs — stripe + promo + admin + billing-support endpoints (part 2, strangler replace)

## Context
Per docs/ddd-migration-playbook.md, this is the second half of the `ReplyInMyVoice.Api/Program.cs`
strangler replace (1559 lines, split to avoid a collision with DDD-67).

**DEPENDS ON DDD-67 BEING MERGED FIRST.** Do not start this issue until DDD-67 is merged into the
integration branch; the two issues edit the same file and will conflict if interleaved.

This issue covers the following endpoint groups ONLY:

- `/api/promo/redeem` (POST) — redeem promo code
- `/api/promo/status` (GET) — get promo status
- `/api/stripe/checkout` (POST) — create checkout session
- `/api/stripe/portal` (POST) — create billing portal session
- `/api/stripe/webhook` (POST) — process Stripe webhook

Any other routes in Program.cs that were not covered by DDD-67 or listed above should receive a
`// TODO(DDD): not yet shelled — DDD-68` comment if they still call old services directly.
Behaviour is unchanged; existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` (current state after DDD-67 merge)
- Application/UseCases/Promo/RedeemPromoCommand.cs + RedeemPromoHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Promo/GetPromoStatusQuery.cs + GetPromoStatusHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/GetAccountSummaryQuery.cs + GetAccountSummaryHandler.cs
  (via `git show origin/delivery/ddd-restructure:...` — may already be injected by DDD-67)
- Application/UseCases/Billing/CreateCheckoutSessionCommand.cs + CreateCheckoutSessionHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Billing/CreatePortalSessionQuery.cs + CreatePortalSessionHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/StripeEvent/ProcessStripeWebhookCommand.cs + ProcessStripeWebhookHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`

## Constraints
- In Program.cs Minimal API, add handlers as route lambda parameters (same pattern as DDD-67).
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP `PromoService`, `AccountService`, `IStripeBillingService`, `StripeEventService` registrations.
  Keep the existing helper functions (`ResolveTrustedPromoClientIp`, `ReadPromoRedeemRequestAsync`,
  `MapPromoRedeemResultAsync`, `ReadCheckoutSessionRequestAsync`, `SetRetryAfter`,
  `IsBillingProviderFailure`) — refactor their internal calls to use handlers where they previously
  called old service instances, or keep them as-is if they are pure computation helpers.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required (endpoint by endpoint)

1. `POST /api/promo/redeem`: replace `promoService.RedeemAsync(externalUserId, email, code, ip, now, ...)` with
   `RedeemPromoHandler` called with
   `new RedeemPromoCommand(externalUserId, email, request.Code, ResolveTrustedPromoClientIp(httpRequest, configuration), DateTimeOffset.UtcNow)`.
   The `MapPromoRedeemResultAsync` helper currently calls
   `accountService.GetOrCreateAccountSummaryAsync(...)` on success — replace that inner call with
   `GetAccountSummaryHandler` / `new GetAccountSummaryQuery(externalUserId, email)`.
   Add `RedeemPromoHandler`, `GetAccountSummaryHandler` (if not already injected) as lambda parameters.

2. `GET /api/promo/status`: replace `promoService.GetStatusAsync(externalUserId, email, now, ...)` with
   `GetPromoStatusHandler` called with
   `new GetPromoStatusQuery(externalUserId, email, DateTimeOffset.UtcNow)`.
   Add `GetPromoStatusHandler` as a lambda parameter.

3. `POST /api/stripe/checkout`: replace `billingService.CreateCheckoutSessionUrlAsync(externalUserId, email, sku, ...)` with
   `CreateCheckoutSessionHandler` called with
   `new CreateCheckoutSessionCommand(externalUserId, email, sku)`.
   All rate-limit and SKU-validation logic before the handler call is preserved unchanged.
   All existing catch blocks are preserved unchanged — wrap the handler call inside the same
   try/catch structure already present.
   Add `CreateCheckoutSessionHandler` as a lambda parameter; keep `ICheckoutVelocityLimiter`
   parameter as-is (it is not migrated).

4. `POST /api/stripe/portal`: replace `billingService.CreatePortalSessionUrlAsync(externalUserId, ...)` with
   `CreatePortalSessionHandler` called with `new CreatePortalSessionQuery(externalUserId)`.
   Preserve existing catch blocks.
   Add `CreatePortalSessionHandler` as a lambda parameter.

5. `POST /api/stripe/webhook`: the entire signature-verification and JSON-parsing preamble is kept
   unchanged. Replace `stripeEventService.ProcessWebhookEventAsync(eventId, eventType, rawBody, now, ...)` with
   `ProcessStripeWebhookHandler` called with
   `new ProcessStripeWebhookCommand(new StripeWebhookPayloadDto(eventId, eventType, rawBody), DateTimeOffset.UtcNow)`.
   Map the handler return value to the same `{ received = true, processed }` response.
   Add `ProcessStripeWebhookHandler` as a lambda parameter; drop `StripeEventService` parameter only
   if no remaining route in the file uses it.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~PromoApiTests|FullyQualifiedName~PromoServiceTests|FullyQualifiedName~StripeBillingApiTests|FullyQualifiedName~StripeWebhookApiTests|FullyQualifiedName~StripeEventServiceTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT start this issue before DDD-67 is merged — this issue edits the same Program.cs.
- Do NOT touch the rewrite / account / V1 endpoints already migrated by DDD-67.
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
