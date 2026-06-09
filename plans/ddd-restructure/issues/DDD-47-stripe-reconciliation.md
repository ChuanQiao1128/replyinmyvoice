# DDD-47: Migrate StripeReconciliation use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the StripeReconciliation use-cases from
Infrastructure/Services/StripeReconciliationService.cs into Application handlers that depend on
repository interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP
the old service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeReconciliationService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

StripeReconciliationService.cs exposes one public use-case:
- `ReconcileAsync` — compare local purchase-grant records against Stripe payment intents, compute
  discrepancies, send alerts for any mismatches, and return a `StripeReconciliationReport`

This is a query-style use-case (read + external call + alert side-effect; no DB writes).

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old StripeReconciliationService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests stay green, assertions unchanged.
- Stripe payment-intent listing is an Infrastructure concern — reuse or extend `IStripeBillingClient`
  (defined in DDD-46) for `ListPaidPaymentIntentsAsync`; alternatively define a focused
  `IStripePaymentReconciliationClient` in Application/Abstractions if DDD-46 is not yet merged
  (the interface already exists as an internal in StripeReconciliationService.cs — promote it).
- Alert dispatch is an Infrastructure concern — define `IStripeReconciliationAlerter` in
  Application/Abstractions (already exists internally in the service — promote it).
- Local purchase-grant snapshot loading reads from DB via a repository; define or extend an
  `IPaymentGrantRepository` interface in Application/Abstractions.

## Changes required
1. `Application/UseCases/StripeReconciliation/ReconcileStripeCommand.cs` + `ReconcileStripeHandler.cs`
   — replicates the full `ReconcileAsync` orchestration: load local grants, call Stripe for paid
   intents, diff them, alert on discrepancies, return a `StripeReconciliationReportDto`.
2. `Application/Abstractions/IStripePaymentReconciliationClient.cs` — promote the existing internal
   interface; Infrastructure wires the existing Stripe-backed implementation.
3. `Application/Abstractions/IStripeReconciliationAlerter.cs` — promote the existing internal
   interface; Infrastructure wires the existing notification implementation.
4. `Application/Abstractions/IPaymentGrantRepository.cs` — interface for loading local
   purchase-grant snapshots used in reconciliation.
5. Extend Infrastructure/Repositories with a `PaymentGrantRepository` implementation; register in
   ServiceCollectionExtensions.cs.
6. `Application/Common/*.cs` — `StripeReconciliationReportDto` and `StripeReconciliationDiscrepancyDto`
   (mirrors the existing record types) returned by the handler.
7. `ServiceCollectionExtensions.cs` — register `ReconcileStripeHandler` + new repositories/clients
   with `AddScoped`.
8. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeReconciliationUseCaseTests.cs` —
   cover: no discrepancies, paid-but-no-grant, grant-but-no-payment, amount-mismatch paths; use
   fakes for `IStripePaymentReconciliationClient` and `IStripeReconciliationAlerter`.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~StripeReconciliationUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeReconciliation/ReconcileStripeHandler.cs`

## DO NOT
- Do NOT delete or edit StripeReconciliationService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
