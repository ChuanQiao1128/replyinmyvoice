# DDD-40: Migrate Account use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the Account use-cases from
Infrastructure/Services/AccountService.cs into Application handlers that depend on repository
interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP the old
service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

AccountService.cs (663L) exposes seven public use-cases:
- `GetOrCreateUserAsync` — find or create an AppUser from external auth identity
- `FindUserAsync` — look up an AppUser by external auth user-id
- `GetOrCreateAccountSummaryAsync` — build the full AccountSummary projection (usage, credits, promos)
- `GetPurchaseHistoryAsync` — paginated list of payments for a user
- `HasPaidApiEntitlementAsync` — boolean entitlement check for the API scope
- `GetBillingHistoryAsync` — list combined subscription and one-time billing history items
- `DeleteAccountAsync` — soft/hard delete user data including Stripe subscription cancellation

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old AccountService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests for AccountService stay green, assertions unchanged.
- `GetOrCreateUserAsync` and `DeleteAccountAsync` both commit state — use IUnitOfWork.
- `DeleteAccountAsync` accepts an optional Stripe cancellation side-effect; the handler should
  accept a delegate/interface for that rather than calling StripeBillingService directly.

## Changes required
1. `Application/UseCases/Account/*.cs` — command/query + handler per use-case:
   - `GetOrCreateUserCommand.cs` + `GetOrCreateUserHandler.cs`
   - `FindUserQuery.cs` + `FindUserHandler.cs`
   - `GetAccountSummaryQuery.cs` + `GetAccountSummaryHandler.cs`
   - `GetPurchaseHistoryQuery.cs` + `GetPurchaseHistoryHandler.cs`
   - `HasPaidApiEntitlementQuery.cs` + `HasPaidApiEntitlementHandler.cs`
   - `GetBillingHistoryQuery.cs` + `GetBillingHistoryHandler.cs`
   - `DeleteAccountCommand.cs` + `DeleteAccountHandler.cs`
2. Extend Application/Abstractions with any new repository interfaces needed (e.g.
   `IRewriteCreditRepository` extension methods, `IStripeSubscriptionCancellationService` interface
   for the optional Stripe side-effect in DeleteAccount).
3. Extend Infrastructure/Repositories with matching implementations; register in
   ServiceCollectionExtensions.cs.
4. `Application/Common/*.cs` — DTO types for AccountSummary, PurchaseHistory, BillingHistory
   projections returned by the query handlers.
5. `ServiceCollectionExtensions.cs` — register the new handlers (+ any new repositories) with `AddScoped`.
6. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/AccountUseCaseTests.cs` — cover all
   handlers using SQLite in-memory, reusing the existing test harness patterns.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~AccountUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Account/GetOrCreateUserHandler.cs`

## DO NOT
- Do NOT delete or edit AccountService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
