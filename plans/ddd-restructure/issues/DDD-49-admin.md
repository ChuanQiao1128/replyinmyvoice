# DDD-49: Migrate Admin use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the Admin use-cases from
Infrastructure/Services/AdminService.cs into Application handlers that depend on repository
interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP the old
service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

AdminService.cs (1406L) exposes nine public use-cases:
- `GetUsersAsync` — paginated/filtered list of users with subscription and credit summary
- `GetUserDetailAsync` — full admin detail for a single user (usage periods, credits, payments,
  billing support requests, reconciliation summary)
- `GetStatsAsync` — aggregate platform statistics (user counts, revenue, usage)
- `GetBillingSupportQueueAsync` — list open billing support requests for admin review
- `ResolveBillingSupportRequestAsync` — mark a billing support request as resolved with audit
- `WriteAccountingRevenueCsvAsync` — stream a revenue CSV report to a provided output stream
- `GrantCreditsAsync` — manually grant rewrite credits to a user with audit; returns `AdminCreditGrantServiceResult`
- `DeleteUserAsync` — admin-initiated user deletion with audit; returns `AdminDeleteUserServiceResult`
- `SetUserSuspensionAsync` — suspend or unsuspend a user account; returns `AdminSuspensionServiceResult` *(also present)*
- `IssueRefundAsync` — initiate a Stripe refund on behalf of admin with review-threshold check; returns `AdminRefundServiceResult` *(also present)*

AdminService.cs is 1406L. It is acceptable to migrate the PRIMARY use-cases first:
`GetUsersAsync`, `GetUserDetailAsync`, `GetStatsAsync`, `GrantCreditsAsync`, and `DeleteUserAsync`.
Leave a `// TODO(DDD): remaining AdminService use-cases (GetBillingSupportQueue, ResolveBillingSupport, WriteAccountingRevenueCsv, SetUserSuspension, IssueRefund)`
note in the handler folder if the full migration is too large for one Codex pass — never break the
build; the old service remains the fallback.

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old AdminService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests (including `AdminRouteMetadataTests`) stay green,
  assertions unchanged.
- `GrantCreditsAsync` and `DeleteUserAsync` commit state — use IUnitOfWork.
- `WriteAccountingRevenueCsvAsync` streams to a provided output stream — the handler signature
  should accept a `Stream` parameter; relational + SQLite paths must both be preserved via a
  focused repository method.
- `IssueRefundAsync` calls Stripe — use `IStripeBillingClient` (from DDD-46) for the refund call;
  define `IAdminRefundAuditRepository` if a separate audit-trail write is needed.
- `IssueRefundAsync` has refund-review threshold checks (constants `RefundReviewCountThreshold` /
  `RefundReviewAmountThreshold`) — preserve in the handler or a Domain value object.

## Changes required
1. `Application/UseCases/Admin/*.cs` — command/query + handler per use-case (primary pass):
   - `GetAdminUsersQuery.cs` + `GetAdminUsersHandler.cs`
   - `GetAdminUserDetailQuery.cs` + `GetAdminUserDetailHandler.cs`
   - `GetAdminStatsQuery.cs` + `GetAdminStatsHandler.cs`
   - `GrantCreditsCommand.cs` + `GrantCreditsHandler.cs`
   - `DeleteAdminUserCommand.cs` + `DeleteAdminUserHandler.cs`
   - (If not all fit in one pass, leave `// TODO(DDD): remaining AdminService use-cases` stubs for
     GetBillingSupportQueue / ResolveBillingSupport / WriteAccountingRevenueCsv / SetUserSuspension / IssueRefund)
2. `Application/Abstractions/IAdminUserRepository.cs` — interface for admin user-list, user-detail,
   stats, and credit/deletion mutations.
3. `Application/Abstractions/IAdminStatsRepository.cs` — interface for aggregate stats projections.
4. Extend `IAppUserRepository` in Application/Abstractions if new admin-specific methods are needed.
5. Extend Infrastructure/Repositories with matching implementations; register in
   ServiceCollectionExtensions.cs.
6. `Application/Common/*.cs` — DTO types for admin user list/detail/stats responses and mutation
   result types (`AdminCreditGrantResultDto`, `AdminDeleteUserResultDto`).
7. `ServiceCollectionExtensions.cs` — register all new handlers + repositories with `AddScoped`.
8. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/AdminUseCaseTests.cs` — cover the
   primary handlers: user list pagination/filter, user detail, stats, grant-credits (success +
   user-not-found), delete-user (success + forbidden + not-found); SQLite in-memory.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~AdminUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Admin/GrantCreditsHandler.cs`

## DO NOT
- Do NOT delete or edit AdminService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
