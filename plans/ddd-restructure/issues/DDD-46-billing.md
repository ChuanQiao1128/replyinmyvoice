# DDD-46: Migrate Billing use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the Billing use-cases from
Infrastructure/Services/StripeBillingService.cs and Infrastructure/Services/TaxTurnoverService.cs
into Application handlers that depend on repository interfaces, following the Wave-1 Rewrite
template (Application/UseCases/Rewrite). KEEP both old services in place (strangler
add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs,
backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/TaxTurnoverService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

StripeBillingService.cs (494L) exposes five public use-cases:
- `CreateCheckoutSessionUrlAsync` — create a Stripe Checkout session and return the redirect URL
- `CreatePortalSessionUrlAsync` — create a Stripe Customer Portal session URL
- `CancelSubscriptionAsync` — cancel a user's active Stripe subscription
- `RefundPaymentAsync` — issue a refund for a payment intent; returns `StripeRefundResult`
- `ListPaidPaymentIntentsAsync` — list successful payment intents for a user from Stripe

TaxTurnoverService.cs exposes one public use-case:
- `GetRollingTwelveMonthReportAsync` — compute a rolling 12-month NZ GST turnover report from
  local payment records, with optional warning notification side-effect

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old StripeBillingService and TaxTurnoverService untouched. No DB schema change / no new
  EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests stay green, assertions unchanged.
- Stripe API calls (Checkout, Portal, Cancel, Refund, ListPaymentIntents) are Infrastructure
  concerns. Define `IStripeBillingClient` (already present in StripeBillingService.cs as an
  internal interface — promote it to Application/Abstractions) so handlers call the interface,
  not Stripe SDK types directly.
- `GetRollingTwelveMonthReportAsync` reads from local DB only and triggers an optional
  notification side-effect; define `ITaxTurnoverNotifier` in Application/Abstractions for the
  notification side-effect; the Infrastructure implementation wraps the existing notification sender.
- `CreateCheckoutSessionUrlAsync` and `CreatePortalSessionUrlAsync` may upsert AppUser data as a
  side-effect — preserve this via `IAppUserRepository` (already in Application/Abstractions).

## Changes required
1. `Application/UseCases/Billing/*.cs` — command/query + handler per use-case:
   - `CreateCheckoutSessionCommand.cs` + `CreateCheckoutSessionHandler.cs`
   - `CreatePortalSessionQuery.cs` + `CreatePortalSessionHandler.cs`
   - `CancelSubscriptionCommand.cs` + `CancelSubscriptionHandler.cs`
   - `RefundPaymentCommand.cs` + `RefundPaymentHandler.cs`
   - `ListPaidPaymentsQuery.cs` + `ListPaidPaymentsHandler.cs`
   - `GetTaxTurnoverReportQuery.cs` + `GetTaxTurnoverReportHandler.cs`
2. `Application/Abstractions/IStripeBillingClient.cs` — promote the existing `IStripeBillingClient`
   interface (currently in StripeBillingService.cs) to Application/Abstractions; Infrastructure
   wires `StripeBillingClient` as the implementation.
3. `Application/Abstractions/ITaxTurnoverNotifier.cs` — interface for the optional warning
   notification side-effect.
4. Extend Infrastructure/Repositories/implementations for any new DB query needs (e.g. payment
   records for turnover report); register in ServiceCollectionExtensions.cs.
5. `Application/Common/*.cs` — DTO types for checkout/portal URLs, refund result, payment list
   items, and tax turnover report returned by handlers.
6. `ServiceCollectionExtensions.cs` — register all new handlers, repositories, and clients with
   `AddScoped` / `AddTransient` as appropriate.
7. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/BillingUseCaseTests.cs` — cover all
   handlers using fakes for `IStripeBillingClient` and `ITaxTurnoverNotifier`; include refund
   not-found and cancel-no-subscription paths; SQLite in-memory for DB queries.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~BillingUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Billing/CreateCheckoutSessionHandler.cs`

## DO NOT
- Do NOT delete or edit StripeBillingService, TaxTurnoverService, or any other existing services
  (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
