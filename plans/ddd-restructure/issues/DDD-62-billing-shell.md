# DDD-62: Shell BillingHttpFunctions + StripeWebhookFunction onto Application handlers (strangler replace)

## Context
Per docs/ddd-migration-playbook.md, switch the two billing-related HTTP function classes from
calling the old Infrastructure services to invoking the Wave-2 Application handlers in
Application/UseCases/Billing and Application/UseCases/StripeEvent. Behaviour is unchanged;
existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/BillingHttpFunctions.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeWebhookFunction.cs`
- Application/UseCases/Billing/* (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/StripeEvent/ProcessStripeWebhookCommand.cs +
  ProcessStripeWebhookHandler.cs (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`

## Constraints
- Inject the Application handlers via DI (already registered in Wave 2). In each migrated method,
  build the Command/Query and call the handler; remove inline DbContext queries on that path.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP the old service classes (`IStripeBillingService`, `StripeEventService`) and their DI
  registration. Remove a constructor param only if no remaining method in the file uses it.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required

### BillingHttpFunctions.cs
Current constructor: `IStripeBillingService billingService, ILogger<BillingHttpFunctions> logger,
  ICheckoutVelocityLimiter? checkoutVelocityLimiter = null`

1. `CreateCheckoutSession` (`POST /stripe/checkout`): replace
   `billingService.CreateCheckoutSessionUrlAsync(authUser.ExternalAuthUserId, authUser.Email, sku, ...)` with
   `CreateCheckoutSessionHandler` called with
   `new CreateCheckoutSessionCommand(authUser.ExternalAuthUserId, authUser.Email, sku)`.
   All existing rate-limit and SKU-validation logic before the handler call is preserved unchanged.
   All existing catch blocks and their error-mapping logic are preserved unchanged — wrap the
   handler call in the same try/catch pattern already present.
2. `CreateBillingPortalSession` (`POST /stripe/portal`): replace
   `billingService.CreatePortalSessionUrlAsync(authUser.ExternalAuthUserId, ...)` with
   `CreatePortalSessionHandler` called with
   `new CreatePortalSessionQuery(authUser.ExternalAuthUserId)`.
   Preserve existing catch blocks.
3. Adjust constructor: add `CreateCheckoutSessionHandler`, `CreatePortalSessionHandler`; drop
   `IStripeBillingService` only if no remaining method in the class still uses it directly
   (the `IsKnownSku` static helper call references `StripeBillingService` but as a static method —
   check whether that still compiles without the instance field before removing the field).

### StripeWebhookFunction.cs
Current constructor: `StripeEventService stripeEventService, ILogger<StripeWebhookFunction> logger`

1. `Run` (`POST /stripe/webhook`): the entire signature-verification, JSON-parsing, and logging
   preamble is kept unchanged. Replace only the call
   `stripeEventService.ProcessWebhookEventAsync(eventId, eventType, rawBody, DateTimeOffset.UtcNow, ...)` with
   `ProcessStripeWebhookHandler` called with
   `new ProcessStripeWebhookCommand(new StripeWebhookPayloadDto(eventId, eventType, rawBody), DateTimeOffset.UtcNow)`.
   The handler return value maps to the same `{ received = true, processed }` response.
2. Adjust constructor: add `ProcessStripeWebhookHandler`; drop `StripeEventService` only if fully
   unused after the swap.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~StripeBillingServiceTests|FullyQualifiedName~StripeBillingApiTests|FullyQualifiedName~StripeWebhookApiTests|FullyQualifiedName~StripeEventServiceTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
