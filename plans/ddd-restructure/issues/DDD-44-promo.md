# DDD-44: Migrate Promo use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the Promo use-cases from
Infrastructure/Services/PromoService.cs into Application handlers that depend on repository
interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP the old
service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

PromoService.cs exposes two public use-cases:
- `RedeemAsync` — redeem a promo code for a user (with IP velocity check, atomic increment,
  credit grant, and retry logic); returns a `PromoRedeemResult` discriminated union
- `GetStatusAsync` — return the redemption status of a promo code for a given user

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old PromoService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests for PromoService stay green, assertions unchanged.
- `RedeemAsync` commits state and has retry logic with optimistic-concurrency semantics — the
  handler must preserve the atomic-increment + retry pattern using IUnitOfWork.
- IP velocity defence (`PromoIpDefense`) is an Application-layer concern: the `RedeemPromoCommand`
  should carry an optional IP hash; the handler applies the rate-check via a repository count query.
- `GetOrCreateUserAsync` called inside `RedeemAsync` should be fulfilled via `IAppUserRepository`
  (already in Application/Abstractions).
- The `PromoRedeemResult` result type (including the discriminated `PromoRedeemResultKind` enum)
  should be represented as an Application/Common DTO, not re-using the Infrastructure record.

## Changes required
1. `Application/UseCases/Promo/*.cs` — command/query + handler per use-case:
   - `RedeemPromoCommand.cs` + `RedeemPromoHandler.cs`
   - `GetPromoStatusQuery.cs` + `GetPromoStatusHandler.cs`
2. `Application/Abstractions/IPromoCodeRepository.cs` — interface for promo-code lookup, redemption
   record query, and redemption count query (IP velocity); mirror existing style.
3. Extend Infrastructure/Repositories with a `PromoCodeRepository` implementation; register in
   ServiceCollectionExtensions.cs.
4. `Application/Common/*.cs` — `PromoRedeemResultDto` (mirrors `PromoRedeemResult` / `PromoRedeemResultKind`)
   and `PromoStatusDto` (mirrors `PromoStatusResult`) returned by the handlers.
5. `ServiceCollectionExtensions.cs` — register the new handlers + repository with `AddScoped`.
6. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/PromoUseCaseTests.cs` — cover both
   handlers: success redemption, cap-reached, already-redeemed, expired, IP-velocity-blocked, and
   get-status (redeemed / not-redeemed) paths; use SQLite in-memory.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~PromoUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Promo/RedeemPromoHandler.cs`

## DO NOT
- Do NOT delete or edit PromoService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
