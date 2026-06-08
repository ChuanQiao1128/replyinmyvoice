# DDD Migration Playbook

This playbook is the standard strangler recipe for moving one bounded context into the DDD target architecture. Use it for each later context migration after Wave 1.

The goal is not to rewrite the whole backend at once. The goal is to migrate one use-case slice, leave the tree green, and give the next issue a clean point to continue.

## Layer Rules

```text
Domain <- Application <- Infrastructure
                 ^
                 |
            Presentation
```

- Domain owns entities, value objects, enums, and domain services. It has no Infrastructure dependency.
- Application owns use-case handlers, repository interfaces, `IUnitOfWork`, result types, and DTOs.
- Infrastructure owns EF Core, provider clients, queue clients, notification clients, repository implementations, and UnitOfWork implementation.
- Presentation hosts, including Functions, API, and Worker, should become thin shells that authenticate, parse input, call Application, and map the result to the host response.

## Standard Strangler Steps

### 1. Pick One Bounded Context Slice

Choose the smallest useful use-case pair or workflow. Prefer a command/query pair with clear tests, for example create/get, update/get, or enqueue/status.

Define:

- the entry points that currently handle the use case;
- the existing service or inline logic that will be strangled;
- the Domain entities involved;
- the current behavior tests that must remain green;
- the old service that must stay in place until cleanup.

### 2. Define Application Interfaces

Add only the repository interfaces the slice needs under `ReplyInMyVoice.Application/Abstractions`.

Rules:

- methods use Domain entities, primitives, and `CancellationToken`;
- interfaces do not reference EF Core, `AppDbContext`, provider clients, queue clients, or Infrastructure types;
- add `IUnitOfWork.SaveChangesAsync(CancellationToken ct = default)` if the slice commits state;
- keep the methods narrow and add more only when a later use case needs them.

### 3. Implement Infrastructure Repositories

Implement the Application interfaces under `ReplyInMyVoice.Infrastructure/Repositories`.

Rules:

- implementations are backed by `AppDbContext`;
- `UnitOfWork` delegates to `AppDbContext.SaveChangesAsync`;
- register each interface and implementation in `AddReplyInMyVoiceInfrastructure`;
- leave existing `Infrastructure/Services/*` classes untouched.

### 4. Add Application Handlers

Move orchestration into `ReplyInMyVoice.Application/UseCases/<Context>`.

Use the CQRS-lite shape:

- `<Action><Context>Command` plus handler for commands;
- `Get<Context>Query` plus handler for reads;
- `Application/Common` result and DTO types where host responses need stable mapping.

The handler may coordinate repositories, `IUnitOfWork`, and Domain behavior. It must not use `AppDbContext` directly.

### 5. Shell The Entry Point

Update the relevant Presentation entry point after the handler is registered.

The shell should:

- authenticate and authorize the caller;
- validate and parse request input;
- build the Application command or query;
- invoke the handler;
- map the Application result to the existing HTTP, timer, or worker response contract.

Keep behavior unchanged unless the issue brief explicitly says otherwise. Existing API tests should pass without weakening assertions.

### 6. Clean Up Later

Do not delete the old service in the same issue that proves the new path unless the issue is a cleanup issue.

Cleanup happens after the entry point has been switched and tests prove there are no remaining callers. The cleanup issue should remove the old service, obsolete tests, and dead registrations together.

## Worked Example: Wave-1 Rewrite Create/Get

The Wave-1 Rewrite create/get migration is the template for later contexts.

| Step | Issue | Rewrite example |
|---|---|---|
| Define interfaces | DDD-11 | `IAppUserRepository`, `IRewriteAttemptRepository`, `IUsagePeriodRepository`, `IUsageReservationRepository`, `IRewriteCreditRepository`, `IOutboxMessageRepository`, and `IUnitOfWork` were added for the create/get slice |
| Implement Infrastructure | DDD-12 | Matching repositories and `UnitOfWork` were added under `Infrastructure/Repositories` and registered in `AddReplyInMyVoiceInfrastructure` |
| Add Application handlers | DDD-20 | `CreateRewriteAttemptHandler` and `GetRewriteAttemptHandler` moved create/get orchestration into `Application/UseCases/Rewrite` |
| Keep old service live | DDD-20 | `RewriteRequestService` remained in place so the old path continued to compile and run |
| Shell entry point | DDD-21 | `RewriteHttpFunctions` create/get routes are the shell target: build `CreateRewriteAttemptCommand` or `GetRewriteAttemptQuery`, call the handler, and preserve the existing response contract |
| Later cleanup | Wave 2+ | Delete the strangled create/get parts of the old service path only after no entry point or test needs them |

The important part is the order. DDD-20 did not edit `RewriteHttpFunctions`, and DDD-21 should not delete `RewriteRequestService`. That separation keeps each issue small and independently mergeable.

## Copy This Pattern For The Next Context

For each new bounded context, write the issue as:

1. name the exact use-case slice;
2. list the old entry points and old service path;
3. add only the Application interfaces needed by that slice;
4. implement those interfaces in Infrastructure and DI;
5. add the Application command/query handlers and focused tests;
6. switch the entry point to a thin shell;
7. defer deletion to a cleanup issue.

## Acceptance Checklist For Context Migrations

- Application does not reference Infrastructure or EF Core.
- Domain remains free of Infrastructure and provider dependencies.
- Infrastructure implements Application interfaces and owns `AppDbContext` usage.
- Presentation does not contain business orchestration for the migrated use case.
- Old service remains until a cleanup issue removes it.
- Behavior tests remain green and assertions are not weakened.
- No database schema change is introduced unless a separate issue explicitly requires it.
