# DDD-63: Shell AdminHttpFunctions onto Application handlers (strangler replace)

## Context
Per docs/ddd-migration-playbook.md, switch `AdminHttpFunctions.cs` (943 lines) from constructing
`AdminService` and `PromoAdminService` inline to injecting and invoking the Wave-2 Application
handlers in Application/UseCases/Admin and Application/UseCases/PromoAdmin. Behaviour is
unchanged; existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

**Size note:** AdminHttpFunctions.cs is 943 lines. Complete the primary admin + promo-admin
endpoints first. If any endpoint group requires referencing a handler that was NOT migrated in
Wave 2 (see `RemainingAdminServiceUseCasesTodo.cs` — the remaining use-cases for
`GetBillingSupportQueue`, `ResolveBillingSupport`, `WriteAccountingRevenueCsv`,
`SetUserSuspension`, and `IssueRefund` were explicitly deferred), leave those endpoints calling
the old service and add a `// TODO(DDD): remaining AdminService use-case — DDD-63` comment. Do
NOT remove the `_adminService` field while any endpoint still uses it.

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`
- Application/UseCases/Admin/* (via `git show origin/delivery/ddd-restructure:...`)
  Note: `RemainingAdminServiceUseCasesTodo.cs` lists which use-cases are NOT yet in Application.
- Application/UseCases/PromoAdmin/* (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRouteMetadataTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminDeleteUserTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminCreditAdjustTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminAuditLogTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRefundTests.cs`

## Constraints
- Inject the Application handlers via DI (already registered in Wave 2). In each migrated method,
  build the Command/Query and call the handler; remove inline service calls on that path.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP `AdminService`, `PromoAdminService`, and their DI registration; the class currently builds
  them in its constructor — change to DI injection of handlers for migrated endpoints, but keep
  the old fields active while any endpoint still needs them.
- For endpoints whose handler does NOT yet exist in Application (see deferred list above), leave
  the old service call in place and add a `// TODO(DDD): remaining AdminService use-case — DDD-63`
  comment. The build must not break.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required

### AdminHttpFunctions.cs — endpoints with handlers available in Wave 2

**Admin/user endpoints (use `_adminService` / Admin handlers):**
1. `ListUsers` (`GET /console/users`): replace `_adminService.GetUsersAsync(page, pageSize, ...)` with
   `GetAdminUsersHandler` called with `new GetAdminUsersQuery(page, pageSize)`.
2. `GetUserDetail` (`GET /console/users/{userId}`): replace `_adminService.GetUserDetailAsync(parsedUserId, ...)` with
   `GetAdminUserDetailHandler` called with `new GetAdminUserDetailQuery(parsedUserId)`.
3. `GetStats` (`GET /console/stats`): replace `_adminService.GetStatsAsync(...)` with
   `GetAdminStatsHandler` called with `new GetAdminStatsQuery()`.
4. `DeleteUser` (`DELETE /console/users/{userId}`): replace `_adminService.DeleteUserAsync(...)` with
   `DeleteAdminUserHandler` called with
   `new DeleteAdminUserCommand(admin.ExternalAuthUserId, admin.Email, parsedUserId, DateTimeOffset.UtcNow)`.
5. `GrantCredits` (`POST /console/users/{userId}/credits`): replace `_adminService.GrantCreditsAsync(...)` with
   `GrantCreditsHandler` called with
   `new GrantCreditsCommand(admin.ExternalAuthUserId, admin.Email, parsedUserId, grantRequest.Amount, grantRequest.Reason, DateTimeOffset.UtcNow)`.

**PromoAdmin endpoints (use `_promoAdminService` / PromoAdmin handlers):**
6. `CreatePromoCode` (`POST /console/promo-codes`): replace `_promoAdminService.CreatePromoCodeAsync(...)` with
   `CreatePromoCodeHandler` called with
   `new CreatePromoCodeCommand(admin.ExternalAuthUserId, admin.Email, createRequest.Code, createRequest.Description, createRequest.CreditsGranted, createRequest.GrantTtlDays, createRequest.ValidFrom, createRequest.ValidUntil, createRequest.MaxRedemptionsGlobal, createRequest.MaxRedemptionsPerUser, DateTimeOffset.UtcNow)`.
7. `ListPromoCodes` (`GET /console/promo-codes`): replace `_promoAdminService.ListPromoCodesAsync(...)` with
   `ListPromoCodesHandler` called with `new ListPromoCodesQuery(DateTimeOffset.UtcNow)`.
8. `GetPromoCodeDetail` (`GET /console/promo-codes/{promoCodeId}`): replace `_promoAdminService.GetPromoCodeDetailAsync(...)` with
   `GetPromoCodeDetailHandler` called with
   `new GetPromoCodeDetailQuery(parsedPromoCodeId, DateTimeOffset.UtcNow)`.
9. `UpdatePromoCode` (`PATCH /console/promo-codes/{promoCodeId}`): replace `_promoAdminService.UpdatePromoCodeAsync(...)` with
   `UpdatePromoCodeHandler` called with
   `new UpdatePromoCodeCommand(admin.ExternalAuthUserId, admin.Email, parsedPromoCodeId, updateRequest.*, DateTimeOffset.UtcNow)`.
10. `SetPromoCodeActiveAsync` (shared helper for Disable/Enable): replace `_promoAdminService.SetPromoCodeActiveAsync(...)` with
    `SetPromoCodeActiveHandler` called with
    `new SetPromoCodeActiveCommand(admin.ExternalAuthUserId, admin.Email, parsedPromoCodeId, isActive, DateTimeOffset.UtcNow)`.
11. `ArchiveOrRestorePromoCodeAsync` (shared helper for Archive/Restore): replace with
    `ArchivePromoCodeHandler` / `new ArchivePromoCodeCommand(...)` or
    `RestorePromoCodeHandler` / `new RestorePromoCodeCommand(...)` as appropriate.

**Deferred endpoints — leave calling old service with TODO comment:**
- `ListBillingSupportRequests` (`GET /console/billing-support-requests`): `_adminService.GetBillingSupportQueueAsync`
- `ResolveBillingSupportRequest` (`POST /console/billing-support-requests/{id}/resolve`): `_adminService.ResolveBillingSupportRequestAsync`
- `ExportAccountingRevenueCsv` (`GET /console/accounting/revenue.csv`): `_adminService.WriteAccountingRevenueCsvAsync`
- `SetUserSuspension` (`POST /console/users/{userId}/suspension`): `_adminService.SetUserSuspensionAsync`
- `IssueRefund` (`POST /console/users/{userId}/refund`): `_adminService.IssueRefundAsync`

**Constructor change:**
Replace the current hand-wired constructor (which creates `AdminService` + `PromoAdminService` from
`Func<AppDbContext>`) with standard constructor injection of the handlers listed above, plus
`IConfiguration configuration` for `AdminAccess`. Keep `_adminService` and `_promoAdminService`
fields (or equivalent) active while the deferred endpoints still need them; those fields can
simply be injected as the old service types via DI rather than constructed inline.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~AdminRouteMetadataTests|FullyQualifiedName~AdminDeleteUserTests|FullyQualifiedName~AdminCreditAdjustTests|FullyQualifiedName~AdminAuditLogTests|FullyQualifiedName~AdminPromoTests|FullyQualifiedName~AdminRefundTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
