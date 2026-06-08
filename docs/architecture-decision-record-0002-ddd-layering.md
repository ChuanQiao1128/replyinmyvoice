# ADR-0002 - DDD Layering And Strangler Migration Playbook

- **Status:** Accepted - 2026-06-09
- **Deciders:** Project owner; supervised DDD restructure delivery wave
- **Extends:** `docs/architecture-decision-record.md` and `plans/ddd-restructure/REQUIREMENT.md`
- **Scope of this record:** backend project layering and the standard migration pattern. This is docs-only; it does not change code, projects, schemas, deployment targets, billing, or secrets.
- **Review method:** `system-spec-synthesis` skill, following the source facts in the DDD restructure requirement and Wave-1 issue briefs.

---

## 1. Context

ADR-0001 made C# on Azure the backend of record while retaining the Next.js frontend as the presentation and thin proxy layer. The DDD restructure wave keeps that platform decision and tightens the backend structure so later work can move one bounded context at a time without breaking the integration branch.

The current backend already has a pure Domain project and an Infrastructure project, but use-case orchestration still exists in several places:

| Concern | Current issue |
|---|---|
| Application orchestration | Missing dedicated Application project before Wave 1 |
| Persistence access | `AppDbContext` is used directly by entry points and services |
| HTTP entry points | Azure Functions and the ASP.NET Core API repeat endpoint logic |
| Infrastructure services | Some services mix orchestration with database, provider, queue, and notification work |

The target is classic DDD layering with a CQRS-lite Application layer. Each pull request must remain independently mergeable into `delivery/ddd-restructure`, so the migration cannot be a single large rewrite.

## 2. Decision

### 2.1 Target Layering

The accepted dependency direction is:

```text
Presentation
  ReplyInMyVoice.Functions   single production HTTP/timer entry, thin shells
  ReplyInMyVoice.Api         local/integration-test host, thin shells
  ReplyInMyVoice.Worker      background host, thin shells
        |
        v
Application
  ReplyInMyVoice.Application
  UseCases/                  command and query handlers
  Abstractions/              repository interfaces and IUnitOfWork
  Common/                    result types and DTOs
        |
        v
Domain
  ReplyInMyVoice.Domain
  entities, value objects, enums, RewriteEngine, quality gates
        ^
        |
Infrastructure
  ReplyInMyVoice.Infrastructure
  Data/                      AppDbContext, the UnitOfWork substrate
  Repositories/              repository and UnitOfWork implementations
  Providers/Queueing/etc.    external integration implementations
```

The dependency rule is `Domain <- Application <- Infrastructure`. Presentation depends on Application and wires Infrastructure only at composition roots.

### 2.2 Key Decisions

**D1 - Single production entry is Azure Functions.** The production deploy target does not change. `ReplyInMyVoice.Api` remains useful for local and integration-test hosting, but both hosts should call the same Application handlers once a context is migrated.

**D2 - Repository interfaces live in Application.** `Application/Abstractions` owns interfaces such as `IRewriteAttemptRepository` and `IUnitOfWork`. `Infrastructure/Repositories` implements them against `AppDbContext`. Interfaces are added only for the bounded context being migrated.

**D3 - Use a strangler migration.** For each bounded context, add Application handlers and repository implementations first, leave the old service in place, switch the entry point to the new handler, then delete the old service only in a later cleanup issue.

**D4 - No database schema change for the DDD restructure.** The wave reorganizes code layers. It does not add EF migrations or alter Azure SQL schema.

**D5 - Tests move with behavior.** Existing behavior contracts stay green. Tests may be relocated or adjusted to follow the new layer, but they must not be removed just to make a diff pass.

### 2.3 Migration Sequencing Across Waves

Wave 1 establishes the foundation and proves the migration recipe on one small bounded-context slice:

| Issue | Role in the sequence |
|---|---|
| DDD-10 | Add the `ReplyInMyVoice.Application` project skeleton |
| DDD-11 | Define Application repository interfaces and `IUnitOfWork` for the Rewrite create/get path |
| DDD-12 | Implement those interfaces in Infrastructure and register them in DI |
| DDD-20 | Move Rewrite create/get orchestration into Application handlers |
| DDD-21 | Shell the Azure Functions create/get entry points onto those handlers |
| DDD-30 | Record this ADR and publish the reusable migration playbook |

Wave 2 and later waves migrate the remaining bounded contexts using the same recipe: Account, Quota, Promo, Billing, Stripe, ApiKey, Admin, BillingSupport, Webhook/Outbox, and the rewrite job processor. Cleanup waves then remove old strangled services and any dead entry-point logic once there are no remaining callers.

## 3. Consequences

### 3.1 Benefits

- Business use cases move into one Application layer instead of being repeated across Functions, API, Worker, and Infrastructure services.
- The Domain project remains pure and stable while Application coordinates use cases with explicit interfaces.
- Infrastructure remains the only layer that knows EF Core, provider clients, queue clients, and notification clients.
- The same Application handler can serve Azure Functions, the local API host, integration tests, and future worker shells.
- The integration branch stays green because each context is migrated by add-then-replace steps.

### 3.2 Costs And Tradeoffs

- There is temporary duplication while old services remain after the entry point is switched.
- Repository interfaces may look narrow at first because they are added only when a migrated use case needs them.
- Cleanup work is mandatory. Without later deletion passes, the project would keep both old and new paths longer than intended.
- Some integration tests will need to track the Application layer as entry points are thinned.

### 3.3 Guardrails

- Do not change production entry target, database schema, billing mode, secrets, DNS, or deploy flow as part of DDD layering.
- Do not add speculative repository methods for aggregates that are not in the current context.
- Do not move provider, EF Core, queue, or notification implementation details into Application or Domain.
- Do not delete old services in the same issue that adds the new handler unless the issue brief explicitly says it is a cleanup issue.

## 4. Alternatives

- **Big-bang backend rewrite.** Rejected because it would touch too many contexts at once, make failures hard to isolate, and risk breaking the integration branch for a long period.
- **Keep `AppDbContext` in entry points and services.** Rejected because direct persistence access is the layering problem this wave is meant to remove.
- **Put repository interfaces in Domain.** Rejected because persistence ports belong to the Application use-case boundary; Domain should stay focused on entities, value objects, enums, and domain services.
- **Make `ReplyInMyVoice.Api` the production entry.** Rejected because the accepted production target is Azure Functions. The API host remains local and test support.
- **Generate repositories for every aggregate up front.** Rejected because it adds unused surface area. Interfaces and implementations are created only when a migrated bounded context needs them.

## 5. References

- `docs/architecture-decision-record.md`
- `plans/ddd-restructure/REQUIREMENT.md`
- `plans/ddd-restructure/issues/DDD-11-repository-interfaces.md`
- `plans/ddd-restructure/issues/DDD-12-repository-impl-di.md`
- `plans/ddd-restructure/issues/DDD-20-rewrite-usecase-migration.md`
- `plans/ddd-restructure/issues/DDD-21-functions-rewrite-shell.md`
- `docs/ddd-migration-playbook.md`
