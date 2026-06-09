# DDD-45: Migrate PromoAdmin use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the PromoAdmin use-cases from
Infrastructure/Services/PromoAdminService.cs into Application handlers that depend on repository
interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP the old
service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

PromoAdminService.cs (722L) exposes seven public use-cases:
- `ListPromoCodesAsync` — paginated/filtered list of promo codes with stats
- `GetPromoCodeDetailAsync` — full detail for a single promo code including redemption history
- `CreatePromoCodeAsync` — create a new promo code with validation; returns `AdminPromoMutationResult`
- `UpdatePromoCodeAsync` — update fields on an existing promo code; returns `AdminPromoMutationResult`
- `SetPromoCodeActiveAsync` — toggle the active flag on a promo code; returns `AdminPromoMutationResult`
- `ArchivePromoCodeAsync` — soft-archive a promo code; returns `AdminPromoMutationResult`
- `RestorePromoCodeAsync` — restore a previously archived promo code; returns `AdminPromoMutationResult`

PromoAdminService.cs is 722L. It is acceptable to migrate the PRIMARY use-cases (`CreatePromoCodeAsync`,
`UpdatePromoCodeAsync`, `ListPromoCodesAsync`, `GetPromoCodeDetailAsync`) in the first Codex pass and
leave a `// TODO(DDD): remaining PromoAdminService use-cases (SetActive/Archive/Restore)` note in the
handler folder if the full migration is too large for one pass — never break the build; the old service
remains the fallback.

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old PromoAdminService untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests for PromoAdminService stay green, assertions unchanged.
- Mutation handlers (Create/Update/SetActive/Archive/Restore) commit state — use IUnitOfWork.
- The audit-trail append (`AddAudit`) must be preserved in each mutation handler.
- `AdminPromoMutationResult` and its `AdminPromoResultKind` enum should be represented as
  Application/Common DTOs, not re-using the Infrastructure record directly.

## Changes required
1. `Application/UseCases/PromoAdmin/*.cs` — command/query + handler per use-case:
   - `ListPromoCodesQuery.cs` + `ListPromoCodesHandler.cs`
   - `GetPromoCodeDetailQuery.cs` + `GetPromoCodeDetailHandler.cs`
   - `CreatePromoCodeCommand.cs` + `CreatePromoCodeHandler.cs`
   - `UpdatePromoCodeCommand.cs` + `UpdatePromoCodeHandler.cs`
   - `SetPromoCodeActiveCommand.cs` + `SetPromoCodeActiveHandler.cs`
   - `ArchivePromoCodeCommand.cs` + `ArchivePromoCodeHandler.cs`
   - `RestorePromoCodeCommand.cs` + `RestorePromoCodeHandler.cs`
   - (If not all fit in one pass, leave a `// TODO(DDD): remaining PromoAdminService use-cases` stub)
2. `Application/Abstractions/IPromoAdminRepository.cs` — interface for promo-code admin queries
   and mutations; extend `IPromoCodeRepository` (from DDD-44) if appropriate, or create a
   separate interface for admin projections.
3. Extend Infrastructure/Repositories with matching implementations; register in
   ServiceCollectionExtensions.cs.
4. `Application/Common/*.cs` — DTO types for admin promo list/detail responses and the
   `AdminPromoMutationResultDto` returned by mutation handlers.
5. `ServiceCollectionExtensions.cs` — register all new handlers + repositories with `AddScoped`.
6. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/PromoAdminUseCaseTests.cs` — cover
   create/update/list/detail handlers; include duplicate-code and not-found paths; SQLite in-memory.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~PromoAdminUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/PromoAdmin/CreatePromoCodeHandler.cs`

## DO NOT
- Do NOT delete or edit PromoAdminService or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
