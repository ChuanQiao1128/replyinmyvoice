# Reply In My Voice — .NET / Azure backend

Production backend for the rewrite product: a layered (Clean Architecture) C# solution
deployed to Azure (Azure SQL + Azure Functions + Service Bus + Application Insights).
The Next.js frontend is a thin UI that proxies to this backend; all business logic lives here.

## Architecture

```
                 ┌─────────────────────────────────────────────┐
   HTTP  ──────► │  ReplyInMyVoice.Functions  (Azure Functions) │  ◄── deployed HTTP surface
                 └───────────────────┬─────────────────────────┘
   Service Bus ─► ReplyInMyVoice.Worker ──┐                     (auth, request shaping)
                 └──────────────────────┐ │
                                        ▼ ▼
                 ┌─────────────────────────────────────────────┐
                 │  ReplyInMyVoice.Application                  │  use-cases (Command/Query + Handler),
                 │   UseCases/* · Abstractions/* (35 ports)     │  orchestration, no IO
                 └───────────────────┬─────────────────────────┘
                                     │ depends only on ▼
                 ┌─────────────────────────────────────────────┐
                 │  ReplyInMyVoice.Domain                       │  entities, enums, quality/quota
                 │   (zero ProjectReferences, zero packages)    │  state machines — pure logic
                 └─────────────────────────────────────────────┘
                                     ▲ implemented by
                 ┌─────────────────────────────────────────────┐
                 │  ReplyInMyVoice.Infrastructure               │  EF Core + Azure SQL, Stripe,
                 │   Data · Migrations · Repositories ·         │  Service Bus, provider adapters,
                 │   Providers · Queueing · Resilience          │  outbox, circuit breaker
                 └─────────────────────────────────────────────┘
```

**The dependency rule is enforced by the compiler, not by convention.** `Domain` references
nothing; `Application` references only `Domain`; EF Core / Stripe.net / `Azure.Messaging.ServiceBus`
appear **only** in `Infrastructure`. Adapters are bound to ports in a single composition root
(`Infrastructure/ServiceCollectionExtensions.cs` → `AddReplyInMyVoiceInfrastructure`), shared by all hosts.

## Projects

| Project | Role |
|---|---|
| `src/ReplyInMyVoice.Domain` | Entities, enums, contracts, rewrite-quality + quota state machines. Pure, no IO. |
| `src/ReplyInMyVoice.Application` | Use-cases by feature (`Rewrite`, `Quota`, `StripeEvent`, `Billing`, `WebhookOutbox`, `StripeReconciliation`, `ApiKey`, `Admin`, …) as Command/Query records + Handlers over `Abstractions/` ports. |
| `src/ReplyInMyVoice.Infrastructure` | EF Core `AppDbContext` + migrations, repositories, external providers, Service Bus queueing, transactional outbox, provider circuit breaker. |
| `src/ReplyInMyVoice.Functions` | Azure Functions isolated worker — the deployed HTTP surface (Entra JWT + API-key auth, rate limiting, HTTP hardening middleware). |
| `src/ReplyInMyVoice.Worker` | Background Service Bus consumer (async rewrite finalize, retries, idempotent redelivery). |
| `tests/ReplyInMyVoice.Tests` | xUnit suite — quota races, Stripe webhook replay, outbox dispatch, circuit breaker, HTTP hardening. |
| `tools/ReplyInMyVoice.Eval` | Offline rewrite-quality evaluation harness (engine is a swappable black box). |

## Engineering highlights

- **Quota correctness under concurrency** — reservations run at `IsolationLevel.Serializable`
  with retry, and `UnitOfWork` nests `BeginTransactionAsync` *inside*
  `CreateExecutionStrategy().ExecuteAsync` (the correct way to combine `EnableRetryOnFailure`
  with explicit transactions).
- **Layered, DB-enforced idempotency** — `(UserId, IdempotencyKey)` unique index with request-hash
  conflict → 409; Stripe `event id` as PK + claim-lock; filtered-unique `StripeEventId` on credits
  so a replayed webhook physically cannot double-grant.
- **Transactional outbox + competing-consumer lease** (`LockedUntil`/`RowVersion`), with a
  concurrent double-dispatch test.
- **Provider resilience** — circuit breaker held in a singleton registry (survives
  `IHttpClientFactory` handler rotation) + retry/timeout `DelegatingHandler`.
- **Real auth** — full Entra JWT validation (signature/issuer/audience/lifetime); B2B API keys are
  CSPRNG-generated, peppered-SHA-256 hashed at rest (never stored plaintext); per-key rate limiting;
  SSRF protection on customer-supplied webhook URLs.
- **Behavior-gated CI** — a dedicated job spins up `mssql/server:2022` to run EF migrations on a real
  SQL Server before deploy; post-deploy health + `commitSha == github.sha` version gate.

## Build & test

```bash
cd backend-dotnet
dotnet restore
dotnet build
dotnet test          # full xUnit suite
```

> The rewrite engine itself is intentionally a **swappable black box** behind
> `IRewriteEngineClient` (frozen result-shape + contract tests), so the model/provider can be
> replaced without touching the rest of the system.
