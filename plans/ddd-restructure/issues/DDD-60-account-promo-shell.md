# DDD-60: Shell AccountHttpFunctions + PromoHttpFunctions onto Application handlers (strangler replace)

## Context
Per docs/ddd-migration-playbook.md, switch the two consumer-facing HTTP function classes from
calling the old Infrastructure services to invoking the Wave-2 Application handlers in
Application/UseCases/Account, Application/UseCases/BillingSupport, and Application/UseCases/Promo.
Behaviour is unchanged; existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AccountHttpFunctions.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoHttpFunctions.cs`
- Application/UseCases/Account/* (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/BillingSupport/* (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Promo/* (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`

## Constraints
- Inject the Application handlers via DI (already registered in Wave 2). In each migrated method,
  build the Command/Query and call the handler; remove inline DbContext queries on that path.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP the old service classes (`AccountService`, `BillingSupportService`, `PromoService`) and their
  DI registration (other entries still use them). Remove a constructor param only if no remaining
  method in the file uses that old service.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required

### AccountHttpFunctions.cs
Current constructor: `AccountService accountService, BillingSupportService billingSupportService`

1. `GetAccountSummary` (`GET /me`): replace `accountService.GetOrCreateAccountSummaryAsync(...)` with
   `GetAccountSummaryHandler` called with `new GetAccountSummaryQuery(authUser.ExternalAuthUserId, authUser.Email)`.
2. `GetAccountPayments` (`GET /me/payments`): replace `accountService.GetPurchaseHistoryAsync(...)` with
   `GetPurchaseHistoryHandler` called with `new GetPurchaseHistoryQuery(authUser.ExternalAuthUserId, authUser.Email)`.
3. `GetBillingHistory` (`GET /me/billing/history`): replace `accountService.GetBillingHistoryAsync(...)` with
   `GetBillingHistoryHandler` called with `new GetBillingHistoryQuery(authUser.ExternalAuthUserId, authUser.Email)`.
4. `GetBillingSupportRequests` (`GET /billing-support-requests`): replace the two-step
   `GetOrCreateUserAsync` + `billingSupportService.GetForUserAsync(...)` with
   `GetBillingSupportRequestsHandler` called with `new GetBillingSupportRequestsQuery(userId)`.
   The user id must still be resolved first; use `GetOrCreateUserHandler` with
   `new GetOrCreateUserCommand(authUser.ExternalAuthUserId, authUser.Email)` to obtain it.
5. `CreateBillingSupportRequest` (`POST /billing-support-requests`): replace
   `GetOrCreateUserAsync` + `billingSupportService.CreateForUserAsync(...)` with
   `GetOrCreateUserHandler` (to get the user id) then
   `CreateBillingSupportRequestHandler` called with `new CreateBillingSupportRequestCommand(user.Id, createRequest.Type, createRequest.RelatedPaymentIntentId, createRequest.Message, DateTimeOffset.UtcNow)`.
6. `DeleteAccount` (`DELETE /me`): replace `accountService.DeleteAccountAsync(...)` with
   `DeleteAccountHandler` called with `new DeleteAccountCommand(authUser.ExternalAuthUserId)`.
7. Adjust constructor: add the six handlers; drop `AccountService` and `BillingSupportService`
   only if no remaining method in the class still uses them directly.

### PromoHttpFunctions.cs
Current constructor: `PromoService promoService, AccountService accountService`

1. `RedeemPromoCode` (`POST /promo/redeem`) — the `promoService.RedeemAsync(...)` call:
   replace with `RedeemPromoHandler` called with
   `new RedeemPromoCommand(authUser.ExternalAuthUserId, authUser.Email, body.Code, ResolveTrustedClientIp(request), DateTimeOffset.UtcNow)`.
   The `MapRedeemResultAsync` helper currently calls `accountService.GetOrCreateAccountSummaryAsync` on
   success; replace that inner call with `GetAccountSummaryHandler` /
   `new GetAccountSummaryQuery(...)`.
2. `GetPromoStatus` (`GET /promo/status`) — replace `promoService.GetStatusAsync(...)` with
   `GetPromoStatusHandler` called with
   `new GetPromoStatusQuery(authUser.ExternalAuthUserId, authUser.Email, DateTimeOffset.UtcNow)`.
3. Adjust constructor: add `RedeemPromoHandler`, `GetPromoStatusHandler`,
   `GetAccountSummaryHandler`; drop `PromoService` and `AccountService` only if fully unused.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~AccountServiceTests|FullyQualifiedName~PromoApiTests|FullyQualifiedName~PromoServiceTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
