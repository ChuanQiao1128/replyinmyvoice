# Skill Run Log

This append-only log records evidence that the reusable project skills were actually opened, followed, smoke-tested, or used during Reply In My Voice development.

Source of truth for future entries:

```text
AGENTS.md -> Codex And Claude Code Skill Policy -> Skill run log rule
```

Tracked skills:

```text
system-spec-synthesis
resilience-test-generation
state-machine-modeling
data-module-review
dotnet-backend-testing
ui-browser-testing
cloud-architecture-cost-review
claude-heavy-planning-handoff
```

## Logging Rules

- Add one entry per skill per run.
- Log future usage when an agent opens, follows, smoke-tests, or uses a tracked skill.
- If a tracked skill should have applied but was missed, add a gap entry and describe the correction.
- Do not log secrets, `.env.local` values, API tokens, private keys, credential file contents, or raw private user text.
- Do not backfill guessed historical runs. Historical entries are allowed only when backed by an existing artifact.
- Use absolute dates.

## Entry Template

```text
### YYYY-MM-DD - <skill-name> - <short task label>

- Agent: Codex | Claude Code | Other
- Trigger: <why this skill applied>
- Action: <opened/followed/smoke-tested/used/gap>
- Output artifacts: <files, docs, tests, plans, or command output references>
- Verification evidence: <commands run, tests passed, doc section, or result summary>
- Limitations: <anything not proven, unavailable, skipped, or no-charge/no-secret note>
```

## Entries

### 2026-06-09 - state-machine-modeling - CLEAN-12 webhook dispatcher cleanup

- Agent: Codex worker
- Trigger: GitHub issue #669 removes an obsolete webhook dispatcher class after reviewing webhook delivery lifecycle coverage.
- Action: Opened and followed the skill. State list: `Pending`, `InProgress`, `Delivered`, `Failed`. Event list: due delivery claimed, send succeeds, send returns a retryable HTTP status, send/setup fails, required delivery data is missing, max attempts reached. Transition table: due `Pending` or expired `InProgress` -> `InProgress` on claim; `InProgress` -> `Delivered` on success; `InProgress` -> `Pending` with backoff on retryable failure; `InProgress` -> `Failed` when max attempts or required-data failure terminalizes the delivery. Invariants: one claimed delivery is sent once per lease, locks clear after terminal or retryable outcomes, exhausted deliveries do not reschedule, and the surviving sender adapter/HTTP sender registrations remain live.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/HttpWebhookDeliverySender.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~WebhookOutboxUseCaseTests"` passed 9/9; full `dotnet test ReplyInMyVoice.sln -c Release` passed 651/651; source check found no dispatcher class in `backend-dotnet/src`.
- Limitations: No schema, migration, queue runtime, push, PR, or deployment action was performed.

### 2026-06-09 - resilience-test-generation - CLEAN-12 webhook retry and sender coverage

- Agent: Codex worker
- Trigger: GitHub issue #669 requires deleting an obsolete webhook dispatcher test only after preserving retry/backoff, endpoint-safety, signature/timestamp, and terminal failure coverage.
- Action: Opened and followed the skill. Critical operation: webhook delivery dispatch and HTTP sender handoff. Dependency boundaries: EF Core SQLite test database, application webhook delivery repository/unit-of-work, legacy infrastructure HTTP sender, and outbound HTTP transport. Failure matrix covered: retryable HTTP failure reschedules with backoff; max-attempt failure terminalizes; missing required delivery data terminalizes without sending; concurrent claim prevents duplicate sends; disallowed saved URL fails before the HTTP handler is called.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/HttpWebhookDeliverySenderTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Pre-cleanup focused transfer command `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~WebhookOutboxUseCaseTests|FullyQualifiedName~HttpWebhookDeliverySenderTests"` passed 10/10; final focused outbox command passed 9/9; final full backend suite passed 651/651.
- Limitations: No live webhook endpoint, external network send, production database, push, PR, or deployment action was exercised.

### 2026-06-09 - dotnet-backend-testing - CLEAN-12 backend acceptance gates

- Agent: Codex worker
- Trigger: GitHub issue #669 changes C#/.NET backend source and tests by relocating sender types, deleting obsolete dispatcher coverage, and requiring Release build plus focused and full backend tests.
- Action: Opened and followed the skill; used xUnit, FluentAssertions, EF Core SQLite fixture coverage, deterministic sender fakes, and a local throwing `HttpMessageHandler` to preserve behavior before removing the obsolete service test file.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/HttpWebhookDeliverySender.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/HttpWebhookDeliverySenderTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed after relocation and after deletion; `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~WebhookOutboxUseCaseTests"` passed 9/9; `dotnet test ReplyInMyVoice.sln -c Release` passed 651/651; source checks confirmed the obsolete dispatcher class is absent and `HttpWebhookDeliverySender` remains present.
- Limitations: NuGet advisory metadata lookup emitted NU1900 warnings because package metadata could not be loaded, but all required commands exited 0. Local git commit was attempted but blocked by sandbox permissions on worktree metadata outside this writable root.

### 2026-06-09 - state-machine-modeling - DDD-68 API Program shell

- Agent: Codex worker
- Trigger: GitHub issue #653 changes API entry points for promo redemption/status, Stripe checkout/portal, and Stripe webhook lifecycle handling.
- Action: Opened and followed the skill. State list: promo redemption remains not-redeemed/applied; promo credit remains active until consumed or expired; billing checkout remains unauthenticated/rejected or session-created; billing portal remains customer-missing or session-created; Stripe event remains new/processed/failed/duplicate. Event list: redeem code, get promo status, create checkout session, create portal session, receive webhook, process duplicate webhook. Transition table: valid redeem creates one applied redemption plus one promo credit; invalid/expired/cap/velocity/config cases create no credit; checkout session creation may create/update the local user only after provider success; missing portal customer rejects without provider side effect; valid webhook begins processing and ends processed or failed; duplicate webhook returns `processed=false`. Invariants: API response contracts stay unchanged, promo IP values are hashed before persistence, duplicate Stripe events do not double-apply side effects, old service registrations stay present, and no schema/migration changes occur. Illegal transitions: invalid promo redemption cannot create credits, missing promo hash config cannot create users or credits, failed checkout cannot persist billing state, and processed webhook events cannot be processed again.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~PromoApiTests|FullyQualifiedName~PromoServiceTests|FullyQualifiedName~StripeBillingApiTests|FullyQualifiedName~StripeWebhookApiTests|FullyQualifiedName~StripeEventServiceTests"` passed 69/69 after the Program shell swap; `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: This issue intentionally did not add a transition helper, change persisted state names, alter DDD-67 rewrite/account/V1 routes, push, open a PR, deploy, touch secrets, or change payment mode/config.

### 2026-06-09 - data-module-review - DDD-68 endpoint persistence boundaries

- Agent: Codex worker
- Trigger: GitHub issue #653 reroutes EF-backed promo, account-summary, checkout, portal, and Stripe event persistence through Application handlers.
- Action: Opened and followed the skill; reviewed the route entry points, Application use-case handlers, DI registrations, promo/billing/webhook API tests, and legacy service behavior. Findings: no P1/P2 data defects after preserving promo IP hash/config behavior at the API boundary. Open questions: none for this issue scope. Suggested tests: existing API/service tests covering promo redemption persistence, billing failure no-state behavior, webhook duplicate/idempotency, and full backend regression suite.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused DDD-68 acceptance filter passed 69/69; full backend suite passed 695/695.
- Limitations: No EF model, migration, repository, database index, billing provider config, production data, or service registration cleanup was changed.

### 2026-06-09 - resilience-test-generation - DDD-68 webhook and provider failure gates

- Agent: Codex worker
- Trigger: GitHub issue #653 changes endpoint calls around checkout provider failures, promo config fail-closed behavior, and Stripe webhook duplicate processing.
- Action: Opened and followed the skill. Critical operations: promo redeem, checkout session creation, portal session creation, and Stripe webhook processing. Dependency boundaries: EF persistence, Stripe billing client abstraction, Stripe signature parsing, webhook idempotency records, and promo trusted-IP configuration. Failure matrix covered by existing tests: malformed promo body, missing auth, missing promo hash config, velocity limit, provider timeout, missing portal customer, missing/invalid webhook signature, tampered webhook payload, stale signature, duplicate event, and failed webhook replay behavior.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused DDD-68 filter passed 69/69 and includes no-state assertions for provider/config failures plus duplicate webhook behavior; full backend suite passed 695/695.
- Limitations: No live Stripe endpoint, cloud queue, production database, external network provider, deploy, push, or PR action was exercised.

### 2026-06-09 - dotnet-backend-testing - DDD-68 backend acceptance gates

- Agent: Codex worker
- Trigger: GitHub issue #653 changes C# Minimal API routing and requires `dotnet build`, focused API/service tests, and full backend tests.
- Action: Opened and followed the skill; used the existing xUnit, FluentAssertions, WebApplicationFactory, EF Core SQLite, and deterministic provider fakes as characterization coverage because the issue requires unchanged behavior and unchanged existing assertions.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `docs/skill-run-log.md`.
- Verification evidence: Final verification passed: `dotnet build ReplyInMyVoice.sln -c Release` completed with 0 warnings and 0 errors; focused DDD-68 filter passed 69/69; `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new test assertions were added because this is a behavior-preserving route shell swap. No frontend, browser, deployment, production payment, live provider, secret, push, or PR path was exercised.

### 2026-06-09 - data-module-review - DDD-61 API key Function shell

- Agent: Codex worker
- Trigger: GitHub issue #646 switches API-key and API-usage HTTP Functions from legacy Infrastructure service calls to Application handlers over EF-backed repositories.
- Action: Opened and followed the skill; reviewed the Function entry points, Application API-key/account handlers, repository registration, legacy service response records, and API-key/usage tests. Kept the change at the shell boundary: no schema edits, no migrations, no repository edits, and old service registrations remain.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiUsageHttpFunctions.cs`; test constructor helpers in `ApiKeyHttpFunctionsTests.cs`, `ApiUsageHttpFunctionsTests.cs`, and `ApiInputHardeningTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; focused API-key/service test filter passed 27/27; full backend suite passed 695/695; `git diff --check` passed; changed-file restricted-term scan returned no matches.
- Limitations: This issue intentionally did not remove legacy service classes or DI registration, add migrations, alter repository contracts, push, open a PR, deploy, touch secrets, or change payment wiring.

### 2026-06-09 - dotnet-backend-testing - DDD-61 backend regression gates

- Agent: Codex worker
- Trigger: GitHub issue #646 changes C# Azure Function constructor signatures and backend request handling paths while requiring existing behavior and assertions to remain unchanged.
- Action: Opened and followed the skill; used the existing xUnit/FluentAssertions/EF Core SQLite coverage as the regression gate and changed only direct Function test factories needed for the new handler constructor dependencies.
- Output artifacts: Same DDD-61 Function and test-helper files plus this log entry.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors; `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~ApiKeyServiceTests|FullyQualifiedName~ApiKeyHttpFunctionsTests|FullyQualifiedName~ApiKeyUsageQueryServiceTests"` passed 27/27; `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new test assertions were added because the issue scope is a behavior-preserving shell swap with existing acceptance coverage. No browser, frontend, deployment, production data, payment, or secret path was exercised.

### 2026-06-09 - state-machine-modeling - DDD-41 quota reservation handlers

- Agent: Codex worker
- Trigger: GitHub issue #622 changes and tests quota reservation, rewrite-attempt, and cleanup lifecycles through new Application handlers.
- Action: Opened and followed the skill. State list: `RewriteAttempt` remains `Pending`, `Processing`, `Succeeded`, `Failed`, and `Expired`; `UsageReservation` remains `Pending`, `Finalized`, `Released`, and `Expired`. Event list: reserve quota, duplicate reserve lookup, conflicting reserve lookup, mark processing, finalize success, release failure/cancel path, and release expired cleanup. Transition table: reserve creates pending attempt plus pending reservation; duplicate reserve returns the existing attempt; conflict returns the old attempt with an error code; mark processing moves pending attempt to processing once; finalize moves pending reservation to finalized and attempt to succeeded; release moves pending reservation to released and pending/processing attempt to failed; expired cleanup moves stale pending reservation plus pending/processing attempt to expired. Invariants: terminal attempts are not overwritten by late success, period-backed success moves one reserved count to used count, release/expiry returns reserved period count or credit consumption, and expired cleanup loops in bounded batches. Illegal transitions: finalized/released/expired reservations do not finalize again; failed/expired attempts do not become succeeded; non-pending attempts do not re-enter processing.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Quota/*.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/ReserveQuotaResult.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/QuotaUseCaseTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~QuotaUseCaseTests` passed 9/9; `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~QuotaServiceTests` passed 22/22; `dotnet test ReplyInMyVoice.sln -c Release` passed 641/641.
- Limitations: Entry points were intentionally not switched, and `QuotaService` was not edited per the strangler issue scope.

### 2026-06-09 - data-module-review - DDD-41 quota repository and UoW changes

- Agent: Codex worker
- Trigger: GitHub issue #622 extends EF-backed repositories, `IUnitOfWork`, and quota persistence invariants without a schema change.
- Action: Opened and followed the skill; reviewed `QuotaService`, `AppDbContext` mappings for `UsagePeriod`, `UsageReservation`, `RewriteAttempt`, `RewriteCredit`, and `OutboxMessage`, existing Application repository interfaces, repository implementations, and SQLite integration test patterns. Kept changes additive: narrow lookup methods, expired-reservation batch loading, serializable transaction overloads, and bounded race retry support in Infrastructure.
- Output artifacts: `IRewriteAttemptRepository.cs`; `IUsageReservationRepository.cs`; `IUsagePeriodRepository.cs`; `IRewriteCreditRepository.cs`; `IUnitOfWork.cs`; matching `Infrastructure/Repositories/*` implementations; `QuotaUseCaseTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; focused quota use-case tests passed 9/9; existing quota service tests passed 22/22; full backend tests passed 641/641; no EF Core or `AppDbContext` dependency was introduced into the new quota handlers.
- Limitations: No database migration, model snapshot edit, data backfill, deployment, production data access, payment action, push, or PR action was performed.

### 2026-06-09 - resilience-test-generation - DDD-41 quota retry and cleanup coverage

- Agent: Codex worker
- Trigger: GitHub issue #622 requires reserve race retry behavior and expired-reservation batch cleanup coverage.
- Action: Opened and followed the skill. Critical operation: reserve quota without double-consuming period slots or credits when requests repeat or race. Dependency boundaries: SQLite/EF persistence, outbox row creation, period counters, and credit counters. Failure matrix covered in tests: duplicate request key, conflicting request hash, full quota without credit, credit-backed reservation release, simulated reserve retry path, stale pending cleanup, stale processing cleanup, and late success after expiry.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/QuotaUseCaseTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/UnitOfWork.cs`; quota Application handlers; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~QuotaUseCaseTests` passed 9/9 and asserts final persisted state for attempts, reservations, periods, credits, and outbox messages. Full backend tests passed 641/641.
- Limitations: No live queue, provider, webhook endpoint, cloud service, or production database was contacted. The reserve retry test uses a deterministic unit-of-work shim to prove handler retry wiring; existing service concurrency coverage remains in `QuotaServiceTests`.

### 2026-06-09 - dotnet-backend-testing - DDD-41 quota xUnit coverage

- Agent: Codex worker
- Trigger: GitHub issue #622 adds C#/.NET Application handler tests using SQLite in-memory and requires focused plus full `dotnet test` gates.
- Action: Opened and followed the skill; added `QuotaUseCaseTests` at the EF Core SQLite integration level with hand-written helpers and no new packages. Covered reserve, duplicate/conflict, quota full, credit waterfall, release, finalize, processing claim, expired batch loop, and late-success guard. Added DI registration assertions for the new handlers.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/QuotaUseCaseTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`; quota handler and repository implementation files; `docs/skill-run-log.md`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~QuotaUseCaseTests` passed 9/9; `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~QuotaServiceTests` passed 22/22; `dotnet test ReplyInMyVoice.sln -c Release` passed 641/641.
- Limitations: NuGet package vulnerability metadata lookup emitted `NU1900` warnings because the feed metadata endpoint was unavailable. Local git commit was blocked by sandbox permissions on the parent worktree Git metadata; no push, PR, deploy, live payment action, or secret inspection was performed.

### 2026-06-09 - system-spec-synthesis - DDD-30 ADR and migration playbook

- Agent: Codex worker
- Trigger: GitHub issue #612 asks for an ADR recording target DDD layering and a migration playbook for later bounded-context waves.
- Action: Opened and followed the skill at documentation-spec scale; read `AGENTS.md`, `CLAUDE.md`, issue #612, `docs/architecture-decision-record.md`, the DDD restructure requirement, DDD-30 brief, Wave-1 rewrite migration briefs, and the merged Application/repository/handler files.
- Output artifacts: `docs/architecture-decision-record-0002-ddd-layering.md`; `docs/ddd-migration-playbook.md`; `docs/skill-run-log.md`.
- Verification evidence: `test -f docs/architecture-decision-record-0002-ddd-layering.md` passed; `test -f docs/ddd-migration-playbook.md` passed; `grep -qi "strangler" docs/ddd-migration-playbook.md` passed; both new docs are non-empty; scoped restricted-substring scan over the two new docs printed no matches.
- Limitations: Docs-only issue. No code, csproj, schema, deployment, push, PR, payment, secret, or production branch action was performed.

### 2026-06-09 - data-module-review - DDD-11 Application repository interfaces

- Agent: Codex worker
- Trigger: GitHub issue #608 defines Application-layer persistence abstractions over EF-backed `AppUser`, `RewriteAttempt`, `UsagePeriod`, `UsageReservation`, and `RewriteCredit`.
- Action: Opened and followed the skill; reviewed `AGENTS.md`, `CLAUDE.md`, issue #608, `plans/ddd-restructure/issues/DDD-11-repository-interfaces.md`, the owned entity files, `AppDbContext`, `RewriteRequestService`, `QuotaService`, and the API create/get attempt paths. Ran the data-risk scan at a bounded limit for context.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IUnitOfWork.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IAppUserRepository.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IRewriteAttemptRepository.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IUsagePeriodRepository.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IUsageReservationRepository.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IRewriteCreditRepository.cs`; `docs/skill-run-log.md`.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py backend-dotnet/src --limit 40` completed and surfaced known broad quota/idempotency signals. `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors. `dotnet test ReplyInMyVoice.sln -c Release` passed 616/616 tests. Application source scan found no EF Core or Infrastructure references.
- Limitations: Interfaces only; no repository implementations, migrations, schema changes, transaction behavior changes, DI wiring, provider calls, deploys, pushes, or PR actions were performed.

### 2026-06-09 - state-machine-modeling - DDD-11 rewrite attempt and reservation boundaries

- Agent: Codex worker
- Trigger: GitHub issue #608 exposes repository contracts for `RewriteAttempt` and quota reservation lifecycle aggregates.
- Action: Opened and followed the skill as a lifecycle review. State list: `RewriteAttempt` keeps existing `Pending`, `Processing`, `Succeeded`, `Failed`, and `Expired`; `UsageReservation` keeps existing `Pending`, `Finalized`, `Released`, and `Expired`. Event list: create reservation, idempotent create lookup, owner-scoped get, finalize, release, and expiry cleanup remain existing service-owned events. Transition table and illegal transitions are unchanged by this issue because only repository contracts were added. Invariants checked: attempt result reads stay user-scoped, idempotency lookup stays user-scoped, quota period/credit/reservation mutations are committed through `IUnitOfWork`, and Application abstractions do not expose EF Core types.
- Output artifacts: Same DDD-11 interface files and `docs/skill-run-log.md`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; `dotnet test ReplyInMyVoice.sln -c Release` passed 616/616 tests; `ls backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/I*Repository.cs` listed five repository contracts; Application source scan found no EF Core or Infrastructure references.
- Limitations: No new lifecycle enum, transition helper, persistence implementation, migration, queue behavior, or runtime quota flow changed.

### 2026-06-09 - system-spec-synthesis - DDD-10 Application project skeleton

- Agent: Codex worker
- Trigger: GitHub issue #607 adds the new `ReplyInMyVoice.Application` layer to the target DDD project graph.
- Action: Opened and followed the skill at implementation-checkpoint scale; read `AGENTS.md`, `CLAUDE.md`, issue #607, `plans/ddd-restructure/issues/DDD-10-application-skeleton.md`, `plans/ddd-restructure/REQUIREMENT.md` section 3, `backend-dotnet/ReplyInMyVoice.sln`, and `backend-dotnet/src/ReplyInMyVoice.Domain/ReplyInMyVoice.Domain.csproj`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/ReplyInMyVoice.Application.csproj`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/ApplicationAbstractionsMarker.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/ApplicationAssemblyMarker.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/ApplicationUseCasesMarker.cs`; `backend-dotnet/ReplyInMyVoice.sln`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors; `grep -q "ReplyInMyVoice.Application" backend-dotnet/ReplyInMyVoice.sln` passed; `test -f backend-dotnet/src/ReplyInMyVoice.Application/ReplyInMyVoice.Application.csproj` passed; `dotnet test ReplyInMyVoice.sln -c Release` passed 616/616 tests.
- Limitations: Skeleton only; no use-case, repository, Infrastructure, Api, Functions, Worker, billing, deployment, secret, or production branch changes were made.

### 2026-06-08 - system-spec-synthesis - CORE-593 MCP shared tool contract

- Agent: Codex worker
- Trigger: GitHub issue #593 defines a transport-agnostic MCP tool core, RewriteBackend adapter, async rewrite job contract, and typed request mapping.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, issue #593, `plans/mcp-productization/issues/CORE-shared-tool-core.md`, `plans/mcp-productization/REQUIREMENT.md`, `packages/mcp-server/src/index.ts`, `packages/mcp-server/src/config.ts`, public v1 rewrite routes, and `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteRequest.cs`. Produced an implementation checkpoint plan in-session: two transport-free core modules, no stdio/http server wiring, no C# edits, domain request mapping, stable public outputs, and unit/build/type/test gates.
- Output artifacts: `packages/mcp-server/src/backend/RewriteBackend.ts`; `packages/mcp-server/src/tools/index.ts`; `tests/unit/mcp-core.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused MCP core test first failed on the missing backend module, then passed 6/6 after implementation. `npm --prefix packages/mcp-server run build`, `npm run typecheck`, and `npm run test` passed locally.
- Limitations: The reference artifact named by the issue, `packages/mcp-server/dist/tools/index.js`, was absent before build in this worktree, so implementation used the issue, brief, requirement document, and C# contract as source of truth. No transport wiring, backend changes, deploy, push, PR, payment action, or secret inspection was performed.

### 2026-06-08 - resilience-test-generation - CORE-593 MCP idempotency and polling

- Agent: Codex worker
- Trigger: GitHub issue #593 requires deterministic submit idempotency and bounded async polling to terminal status.
- Action: Opened and followed the skill; identified the critical operation as `rewrite_email` submit plus poll, with dependency boundary at `RewriteBackend`. Added deterministic unit fakes for submit/poll, a stable SHA-256 idempotency assertion over identical typed requests, and polling coverage for working-to-terminal behavior without contacting live services.
- Output artifacts: `packages/mcp-server/src/backend/RewriteBackend.ts`; `packages/mcp-server/src/tools/index.ts`; `tests/unit/mcp-core.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `tests/unit/mcp-core.test.ts` passed 6/6; full `npm run test` passed 473/473. The scoped policy grep over `packages/mcp-server/src` and `tests/unit/mcp-core.test.ts` printed no matches.
- Limitations: No live API, quota, rate-limit, cloud queue, or provider-failure endpoint was contacted. The failed-job path is represented in the core status contract, but live reservation release remains backend-owned and was not retested here.

### 2026-06-08 - state-machine-modeling - CORE-593 MCP rewrite job lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #593 adds a multi-step rewrite attempt lifecycle behind MCP tool calls.
- Action: Opened and followed the skill. State list: submitted attempt id, working, succeeded, failed. Event list: submit accepted, poll reports processing/pending/working, poll reports succeeded, poll reports failed, polling budget exhausted, invalid backend response. Transition table: submit accepted leads to working; working plus nonterminal poll stays working with backoff; working plus succeeded returns final text; working plus failed raises a clear tool error for `rewrite_email`; one-shot result polling returns the normalized state; exhausted polling returns working internally and `rewrite_email` raises a polling-limit error. Invariants: `rewrite_email` returns final rewritten text on success, public output stays minimal, `get_rewrite_result` polls once, and transport wiring remains outside CORE. Illegal transitions: unknown tool names reject, unknown backend status rejects, succeeded without rewritten text cannot be treated as success.
- Output artifacts: `packages/mcp-server/src/backend/RewriteBackend.ts`; `packages/mcp-server/src/tools/index.ts`; `tests/unit/mcp-core.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Unit tests cover exact tool listing, request mapping, default tone, public output keys, working-to-succeeded polling, one-shot result polling, and stable idempotency key generation. `npm --prefix packages/mcp-server run build`, `npm run typecheck`, and `npm run test` passed locally.
- Limitations: No persisted state, database migration, backend enum, queue worker, or deployment lifecycle changed.

### 2026-06-08 - state-machine-modeling - VER-03 deploy version gate

- Agent: Codex worker
- Trigger: GitHub issue #583 changes the Azure Functions deployment lifecycle by adding a post-health package identity gate to `.github/workflows/dotnet-azure.yml`.
- Action: Opened and followed the skill. State list: package upload accepted or tolerated after known config-zip false-failure, health-live, version-matched, terminal gate failure. Event list: config-zip result, package URL changed, `/api/health` returned 200, `/api/version` served expected commit, `/api/version` served empty or mismatched commit, retry window elapsed. Transition table: deploy step success leads to health polling; health 200 leads to version polling; exact commit match leads to deploy success; empty or mismatched commit stays in version polling until timeout; timeout leads to terminal failure. Invariants: liveness must be proven before package identity, package identity must equal the built commit exactly, missing version data cannot pass, Azure auth/resource/migration behavior remains unchanged. Illegal transitions: health-only success cannot mark deploy green, empty version data cannot pass, mismatched old package commit cannot pass. Persistence implications: none. Test checklist: workflow shape, ordering, YAML parse, jq extraction, exact-match success, mismatch failure, missing/empty failure, restricted vocabulary scan.
- Output artifacts: `.github/workflows/dotnet-azure.yml`; `docs/skill-run-log.md`.
- Verification evidence: Machine-checkable VER-03 acceptance command group passed locally, including `/api/version`, `github.sha`, `commitSha`, `::error::`, health-then-version ordering, YAML parse, jq exact extraction/compare self-test, empty/missing fail-closed checks, and restricted vocabulary scan over the workflow.
- Limitations: No live Azure deployment or remote HTTP smoke was run because the issue defines live assertion as CI-only. No secrets, push, PR, or production branch action was performed.

### 2026-06-06 - system-spec-synthesis - GA-04 usage and billing CSV export

- Agent: Codex worker
- Trigger: GitHub issue #550 adds two same-origin export API contracts for developer dashboard usage and billing data.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the GA-04 issue body, `plans/rewrite-api-v1/ga-issues/GA-04-usage-billing-csv-export.md`, sibling `/api/me/*` proxy routes, developer dashboard panels, and unit test patterns. Implementation spec summary: goals are dependency-free CSV exports for existing user-scoped Azure JSON data; non-goals are payment-provider changes, new data stores, existing JSON endpoint changes, and user-id query forwarding; API contracts are `GET /api/me/api-usage/export?limit=` with limit capped at 1000 and `GET /api/me/billing/export`; security requires same-origin checks plus current Entra token forwarding; verification requires serializer, route, UI source, typecheck, unit suite, lint, and restricted-term scan.
- Output artifacts: `lib/csv-export.ts`; `app/api/me/api-usage/export/route.ts`; `app/api/me/billing/export/route.ts`; `tests/unit/developer-export-routes.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused export tests first failed on missing route modules, then passed 8/8 after implementation. `npm run typecheck` passed. `npm run test` passed 436/436.
- Limitations: This was a local implementation spec and verification pass only; no Azure backend, payment-provider, deployment, push, or PR action was run.

### 2026-06-06 - ui-browser-testing - GA-04 developer dashboard export controls

- Agent: Codex worker
- Trigger: GitHub issue #550 adds browser-visible `Export CSV` controls to the Usage and Billing tabs.
- Action: Opened and followed the skill; identified `/developers/keys` Usage and Billing tabs as the affected user-visible flow. Added source-level UI assertions for the new same-origin export links, added a focused Playwright assertion to the existing developer billing spec, and attempted browser execution. Kept controls compact, icon-led, and aligned with the existing dashboard button styling.
- Output artifacts: `components/developers/usage-panel.tsx`; `components/developers/billing-panel.tsx`; `tests/unit/developer-keys-ui.test.ts`; `tests/unit/developer-billing-panel.test.ts`; `tests/e2e/developer-billing.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused UI tests first failed on missing export links, then passed with the export controls present. Combined focused route/UI suite passed 20/20. `npm run typecheck` passed. `npm run test` passed 436/436. `npm run lint` exited 0 with one pre-existing warning in `components/account/account-panel.tsx`. Restricted-term scan over `app`, `components`, `public`, and `lib` returned no matches.
- Limitations: The repo Playwright command for `tests/e2e/developer-billing.spec.ts --project=chromium` hung during multi-server startup and had to be treated as inconclusive; the sandbox denied process termination for the local servers it started. A direct Playwright Chromium launch through the Node REPL failed with macOS sandbox permission errors, so no screenshot inspection was completed in this worker run.

### 2026-06-05 - state-machine-modeling - P2-06 rewrite attempt retention purge

- Agent: Codex worker
- Trigger: GitHub issue #532 changes `RewriteAttempt` lifecycle handling by adding a scheduled purge over attempts with persisted statuses.
- Action: Opened and followed the project skill. State list: `Pending`, `Processing`, `Succeeded`, `Failed`, `Expired`. Event list: rewrite lifecycle events remain owned by existing services; the new internal command is the daily retention purge tick. Transition table: terminal attempts older than 30 days stay in the same status while payload fields are nulled; terminal attempts inside the 30-day window stay unchanged; non-terminal attempts stay unchanged. Invariants: no attempt row deletion, no idempotency key or request hash mutation, no quota or billing mutation, no terminal status rewrite, and no payload scrub for in-flight attempts. Illegal transitions: purge must not touch `Pending` or `Processing`, delete rows, or move attempts between statuses.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RetentionService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RetentionPurgeFunction.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RetentionServiceTests.cs`; `plans/rewrite-api-v1/scheduled-jobs.md`.
- Verification evidence: Focused red run failed because the old 90-day default scrubbed 0 rows; focused green run passed 3/3 retention tests; `cd backend-dotnet && dotnet test` passed 538/538; `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: No new persisted source state was added; source scoping is documented as a P2-06 deviation because `RewriteAttempt` has no API-vs-website source flag.

### 2026-06-05 - data-module-review - P2-06 retention persistence

- Agent: Codex worker
- Trigger: GitHub issue #532 changes EF-backed mutation of `RewriteAttempt.RequestJson`, `RewriteAttempt.ResultJson`, and `RowVersion`.
- Action: Opened and followed the project skill; reviewed `RewriteAttempt`, `AppDbContext` mapping, existing retention service/tests, timer Functions, DI registration, and ran the project data-risk scan with `python3`. Findings: no schema migration is needed because `RequestJson` is already nullable in EF; the main persistence gap is the absent attempt source flag. Open question resolved by brief: purge all terminal attempts by age when source cannot be distinguished. Suggested test: prove old terminal rows scrub payloads while newer and non-terminal rows remain intact.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RetentionService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RetentionServiceTests.cs`; `plans/rewrite-api-v1/scheduled-jobs.md`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py backend-dotnet --limit 80` completed; focused and full backend tests passed; `git diff --check` returned clean.
- Limitations: No production database query plan or migration smoke was run because this change adds no schema/index migration and does not contact production data.

### 2026-06-05 - dotnet-backend-testing - P2-06 retention xUnit coverage

- Agent: Codex worker
- Trigger: GitHub issue #532 requires xUnit coverage for the purge method and backend `dotnet test` / `dotnet build` gates.
- Action: Opened and followed the project skill plus TDD workflow; added the failing service-level xUnit test first, confirmed the red failure, implemented the 30-day terminal-only purge, and ran focused then full backend verification.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RetentionServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RetentionService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RetentionPurgeFunction.cs`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Retention` initially failed on `Expected scrubbed to be 3, but found 0`; after implementation it passed 3/3. `cd backend-dotnet && dotnet test` passed 538/538. `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: NuGet vulnerability metadata lookup emitted `NU1900` warnings because package-feed metadata was unavailable. Local git commit was blocked by sandbox permissions on the parent worktree Git metadata; no push, PR, deploy, live payment action, or secret inspection was performed.

### 2026-06-05 - ui-browser-testing - P2-07 usage dashboard UI

- Agent: Codex worker
- Trigger: GitHub issue #529 changes the browser-visible `/developers/keys` developer portal into a tabbed dashboard with a new Usage tab, responsive summary cards, an SVG chart, remaining quota, and recent calls.
- Action: Opened and followed the project skill; added a failing source-contract test for the dashboard shell, same-origin usage routes, and dependency-free accessible chart; implemented the tab shell and Usage panel; attempted desktop and mobile Playwright verification with a local signed session and mocked same-origin usage responses.
- Output artifacts: `app/developers/keys/page.tsx`; `components/developers/developer-dashboard.tsx`; `components/developers/usage-panel.tsx`; `components/developers/usage-bar-chart.tsx`; `tests/unit/developer-keys-ui.test.ts`.
- Verification evidence: Focused red run failed on the missing dashboard and usage files, then focused green run passed 7/7. `npm run typecheck` passed. `npm run test` passed 416/416. The scoped source policy grep over `app components public lib` returned no matches. `package.json` and `package-lock.json` diffs were empty.
- Limitations: Playwright browser verification could not capture screenshots because Chromium failed to launch in the macOS sandbox while registering its Mach service. Local `npm ci` was blocked by cache ownership, so verification used the existing complete dependency install via a temporary symlink that was removed afterward. Local git commit was blocked because this worktree's Git metadata is outside the writable sandbox. No push, PR, deploy, live payment action, or secret inspection was performed.

### 2026-06-05 - system-spec-synthesis - P2-02 API usage endpoints

- Agent: Codex
- Trigger: GitHub issue #536 adds the portal API usage summary, series, and recent endpoint contracts across Azure Functions and Next pass-through routes.
- Action: Opened and followed the project skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, `plans/rewrite-api-v1/phase2-issues/P2-02-usage-endpoints.md`, and existing account/API-key route patterns before implementation.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiUsageHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyUsageQueryService.cs`; `app/api/me/api-usage/summary/route.ts`; `app/api/me/api-usage/series/route.ts`; `app/api/me/api-usage/recent/route.ts`; focused backend and frontend tests.
- Verification evidence: Focused usage tests passed 3/3; `cd backend-dotnet && dotnet test` passed 536/536; `npm run typecheck` passed; `npm run test` passed 413/413.
- Limitations: No deploy, push, PR, live payment action, production database check, or secret inspection was performed.

### 2026-06-05 - data-module-review - P2-02 API usage aggregation

- Agent: Codex
- Trigger: The issue adds an EF-backed read service over `ApiKeyUsage`, `ApiKey`, `AppUser`, and account usage summary data.
- Action: Opened and followed the project skill; ran the data-risk scan against `backend-dotnet/src`, reviewed the existing indexes and API-key ownership relationship, and kept the change read-only with no schema or migration.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyUsageQueryService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyUsageQueryServiceTests.cs`.
- Verification evidence: The service test seeds two users and multiple keys, verifies ownership isolation, status aggregation, quota values from `AccountService`, recent ordering, and Pacific/Auckland local-day bucketing; full backend suite passed 536/536.
- Limitations: Aggregation is computed on demand from existing event rows; no rollup table, migration, or production query-plan check was added.

### 2026-06-05 - dotnet-backend-testing - P2-02 usage endpoint tests

- Agent: Codex
- Trigger: The issue requires xUnit coverage for C# usage aggregation and adds Azure Functions endpoints.
- Action: Opened and followed the project skill plus TDD workflow; wrote the failing query-service test first, confirmed the red compile failure, implemented the service and Functions endpoints, then added focused Functions tests.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyUsageQueryServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiUsageHttpFunctionsTests.cs`; backend implementation files.
- Verification evidence: Red run failed on missing `ApiKeyUsageQueryService` and response records; focused green run passed 3/3; `cd backend-dotnet && dotnet test` passed 536/536.
- Limitations: NuGet vulnerability metadata lookup emitted `NU1900` warnings because package-feed metadata was unavailable, but restore/build/test completed. Local git commit was blocked by sandbox permissions on the parent worktree Git metadata.

### 2026-06-05 - dotnet-backend-testing - API-05 to API-11 integration merge

- Agent: Codex
- Trigger: The integration merged C# API route changes and xUnit coverage for public v1 rewrite submit, result, usage, rate-limit, idempotency, and terminal-state behavior.
- Action: Opened and followed the project skill; preserved every merged `RewriteApiTests` v1 method, kept distinct test names, combined API-08 per-key settings with API-10 stable hash seeding, and ran the full backend gate.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs`; merge commits in the writable integration Git metadata.
- Verification evidence: `cd backend-dotnet && dotnet test --nologo` passed 532/532.
- Limitations: NuGet vulnerability metadata warnings appeared because the feed lookup was unavailable. Git writes to the original worktree metadata were blocked by the sandbox, so merges were committed using a temporary writable `GIT_DIR` while updating this worktree's files.

### 2026-06-05 - resilience-test-generation - API-05 to API-11 integration merge

- Agent: Codex
- Trigger: The merged branches cover repeated public v1 submit requests, per-key rate limits, idempotency conflict behavior, no-charge terminal states, and usage/reservation invariants.
- Action: Opened and followed the project skill; resolved test unions so duplicate submit, over-limit submit, expired attempts, and provider-failure paths all remain covered without disabling or weakening tests.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`.
- Verification evidence: `dotnet test --nologo` passed 532/532; `npm run test` passed 408/408.
- Limitations: No live cloud queue, payment, AI, writing-signal, or production database endpoint was contacted.

### 2026-06-05 - state-machine-modeling - API-05 to API-11 integration merge

- Agent: Codex
- Trigger: The integration touches public v1 rewrite attempt polling, usage reservation states, idempotent submit state, over-limit rejection, expired attempts, and provider-failure terminal projection.
- Action: Opened and followed the project skill; checked that submit, result polling, usage read projection, rate-limit rejection, duplicate submit, expired attempt, and provider-failure tests all remain present after conflict resolution.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`.
- Verification evidence: V1 test method list includes submit success, rate limit, same-key same-draft idempotency, same-key different-draft conflict, result states, expired result, provider failure, usage summary, and auth rejection cases; full backend test gate passed 532/532.
- Limitations: No new lifecycle enum, transition helper, schema, or migration was added.

### 2026-06-05 - data-module-review - API-05 to API-11 integration merge

- Agent: Codex
- Trigger: The integration reconciles persisted `ApiKeyUsage`, `RewriteAttempt`, `UsageReservation`, `UsagePeriod`, API key lookup, and account usage summary behavior.
- Action: Opened and followed the project skill; preserved API-08's persisted usage window and API-09's usage read path, kept API-10's stable API-key hash seeding, and confirmed no schema or migration conflict was introduced.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: `dotnet test --nologo` passed 532/532 with assertions covering persisted usage rows, reservations, outbox counts, quota counters, and usage summary values.
- Limitations: This was an integration review of existing branch changes; no migration command or production database check was run.

### 2026-06-05 - ui-browser-testing - API-05 to API-11 integration merge

- Agent: Codex
- Trigger: The integration merged browser-visible Next.js routes and developer key-management UI from API-05, API-07, and API-09.
- Action: Opened and followed the project skill; preserved the `/developers/keys` page, developer key panel, header link, public rewrite proxy routes, usage proxy route, and related unit tests.
- Output artifacts: `app/api/v1/rewrite/route.ts`; `app/api/v1/rewrite/[id]/route.ts`; `app/api/v1/usage/route.ts`; `app/developers/keys/page.tsx`; `components/developers/api-keys-panel.tsx`; `components/site-header.tsx`; frontend unit tests.
- Verification evidence: `npm run typecheck` passed; `npm run test` passed 408/408.
- Limitations: Browser screenshots and Playwright E2E were not run because the user-requested gates were typecheck and unit tests for this mechanical integration.

### 2026-06-04 - system-spec-synthesis - API-05 Next public rewrite proxy routes

- Agent: Codex
- Trigger: GitHub issue #507 adds the public Next.js `POST /api/v1/rewrite` and `GET /api/v1/rewrite/{id}` proxy contract for caller-supplied API authorization.
- Action: Opened and followed the system-spec workflow; read `AGENTS.md`, `CLAUDE.md`, the API-05 brief, the v1 API spec, and existing proxy/helper routes before implementing the scoped Next routes.
- Output artifacts: `app/api/v1/rewrite/route.ts`; `app/api/v1/rewrite/[id]/route.ts`; `tests/unit/public-rewrite-api-route.test.ts`.
- Verification evidence: Focused route test first failed on the missing module, then passed 5/5 after implementation; `npm run typecheck` passed; `npm run test` passed 400/400; source policy grep over `app components public lib` returned no matches; `app/api/rewrite/route.ts` diff was empty.
- Limitations: No backend, billing, secret, deployment, push, or PR changes were made. Local git commit was blocked because the worktree Git metadata is outside the writable sandbox.

### 2026-06-04 - system-spec-synthesis - API-03 v1 rewrite submit contract

- Agent: Codex
- Trigger: GitHub issue #505 adds the key-authed `POST /api/v1/rewrite` API contract and async submit behavior.
- Action: Opened and followed the system-spec workflow; read `AGENTS.md`, `CLAUDE.md`, the issue brief, the v1 API spec, and existing Entra submit code before implementing the scoped contract.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; API test-host mirror in `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; focused integration tests in `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Focused API-03 tests passed 4/4; full `dotnet test` under `backend-dotnet` passed 519/519.
- Limitations: This issue only implements submit. The result endpoint, rate limiting, Next.js proxy route, key management UI, and deployment were not changed.

### 2026-06-04 - state-machine-modeling - API-03 rewrite reservation lifecycle

- Agent: Codex
- Trigger: API-03 creates rewrite attempts, usage reservations, and outbox jobs through an async lifecycle.
- Action: Opened and followed the state workflow; kept submit in `Pending` reservation state with existing worker-owned finalization/release transitions and mapped only submit errors for rejected requests.
- Output artifacts: `V1RewriteHttpFunctions.SubmitRewrite`; `/api/v1/rewrite` API test-host route; assertions for `UsageReservationStatus.Pending`, unchanged `UsagePeriod.UsedCount`, and no reservation/outbox on rejects.
- Verification evidence: Focused API-03 tests passed 4/4; full `dotnet test` under `backend-dotnet` passed 519/519.
- Limitations: No new persisted states or transition function were added; the existing `RewriteRequestService` and `QuotaService` lifecycle remains the source of truth.

### 2026-06-04 - data-module-review - API-03 quota and API-key persistence

- Agent: Codex
- Trigger: API-03 reads `AppUser`/`ApiKey`, creates usage reservations through existing services, and writes `ApiKeyUsage` rows.
- Action: Opened and followed the data-module workflow; reviewed `AppDbContext`, API key entities, usage period/reservation entities, `ApiKeyService`, `ApiKeyAuthResolver`, `RewriteRequestService`, and `QuotaService` invariants before code changes.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Tests assert accepted requests create one pending reservation and one usage log while rejected valid-key requests create no attempt/reservation/outbox; full `dotnet test` passed 519/519.
- Limitations: No schema or migration changes were needed. Missing or unknown keys cannot write an `ApiKeyUsage` row because there is no valid key foreign key to attach.

### 2026-06-04 - resilience-test-generation - API-03 reject-before-reserve behavior

- Agent: Codex
- Trigger: API-03 must handle repeated idempotent submit paths, invalid auth, malformed or oversized input, and quota exhaustion without incorrect usage side effects.
- Action: Opened and followed the resilience workflow; designed deterministic HTTP/SQLite tests around auth failure, over-limit input, quota exhaustion, and successful async reservation.
- Output artifacts: Four focused xUnit integration tests in `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Red run failed with `404` before implementation; green run passed 4/4 after adding the route and handler; full `dotnet test` passed 519/519.
- Limitations: No live cloud, payment, AI, email, or production database calls were made. Concurrency stress remains covered by existing quota service tests rather than new API-03 cases.

### 2026-06-04 - dotnet-backend-testing - API-03 v1 rewrite submit integration tests

- Agent: Codex
- Trigger: API-03 requires C#/.NET integration tests for the new key-authed submit endpoint and persisted reservation state.
- Action: Opened and followed the .NET backend testing workflow; wrote failing xUnit/WebApplicationFactory tests first, implemented the Functions endpoint plus API test-host mirror, then ran focused and full backend commands.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Http/FunctionHttpResults.cs`.
- Verification evidence: `dotnet test ReplyInMyVoice.sln --filter V1_rewrite_submit` passed 4/4; `dotnet test` under `backend-dotnet` passed 519/519; whitespace check and scoped prohibited-string guards returned clean.
- Limitations: NuGet vulnerability metadata warnings appeared because the feed was unreachable, but restore/build/test completed from available packages. No push, PR, deploy, live payment action, or secret inspection was performed.

### 2026-06-04 - ui-browser-testing - PARITY-01 pricing surface parity

- Agent: Codex
- Trigger: GitHub issue #495 changed browser-visible pricing UI in the landing pricing block and workspace buy-rewrites dialog.
- Action: Opened and followed the UI/browser testing workflow; added failing unit coverage for unit-price helper text and stronger Value Pack treatment, implemented the scoped UI parity changes, and attempted local browser verification.
- Output artifacts: `components/landing/pricing-v2.tsx`; `components/app/buy-rewrites-dialog.tsx`; `app/globals.css`; `tests/unit/pricing-auth-visual-system.test.ts`; `tests/unit/buy-rewrites-dialog.test.ts`; pricing docs cleanup files.
- Verification evidence: Focused red runs failed on missing unit-price text; focused green runs passed; `npm run typecheck` passed; `npm run test` passed 379/379; source banned-term grep over `app components public lib` returned no matches; fixed-string grep confirmed all three unit-price helper strings in both scoped pricing surfaces.
- Limitations: Playwright Chromium could not launch in this macOS sandbox because the browser process could not register its Mach service, and the local Next dev server showed watcher `EMFILE` warnings with 404 responses during attempted route checks. No screenshot evidence, deploy, push, PR, live payment action, or secret inspection was performed.

### 2026-06-04 - ui-browser-testing - PROMO-ADMIN-A quick-win admin UI

- Agent: Codex
- Trigger: GitHub issue #468 changed the browser-visible `/admin/promo-codes` UI, form hints, card controls, status legend, stats copy, and e2e assertions.
- Action: Opened and followed the UI/browser testing workflow; updated the focused admin promo component unit test first, observed it fail against the old UI, implemented the scoped frontend-only changes, and updated the non-CI Playwright spec assertions for the new stat test ids.
- Output artifacts: `components/admin/promo-codes-admin.tsx`; `tests/unit/admin-promo-codes-component.test.ts`; `tests/e2e/admin-promo-codes.spec.ts`.
- Verification evidence: Focused red run failed on the old legend/placeholder expectations; focused green run passed 2/2; `npm run typecheck` passed; `npm run test` passed 354/354; `npm run build` completed successfully; banned-term grep over `app components public lib` returned no matches.
- Limitations: Playwright e2e and browser screenshots were not run because the issue brief marked e2e as non-CI and the machine-checkable acceptance did not require browser execution. No backend, API, proxy, migration, secret, deploy, push, or PR operation was performed.

### 2026-06-03 - data-module-review - promo branch merge migration reconciliation

- Agent: Codex
- Trigger: Merging `origin/main` into `feat/promo-code-trial` changed EF Core entities, `AppDbContext`, promo redemption persistence, billing support persistence, and migration history.
- Action: Opened and followed the data-module workflow; preserved main's payment/billing entities and promo entities/config, deleted stale promo migrations, reset the snapshot to main, regenerated promo schema and free-baseline data migrations, and reviewed uniqueness, check constraints, FKs, and account erasure behavior.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260603023140_AddPromoCodes.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260603023201_FreeBaselineZero.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.
- Verification evidence: `dotnet ef migrations list` listed all main migrations followed by `20260603023140_AddPromoCodes` and `20260603023201_FreeBaselineZero`; `AddPromoCodes` contains only promo table/index/check/FK DDL; `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 490/490.
- Limitations: EF could not connect to the configured SQL Server while listing applied/pending status, so only the local migration chain order was verified. No database reset, deploy, remote fetch, push, or production operation was run.

### 2026-06-03 - state-machine-modeling - promo redemption and free quota merge

- Agent: Codex
- Trigger: The merge reconciled promo redemption state, subscription downgrade-to-free behavior, and free-baseline quota semantics after `main` added payment lifecycle changes.
- Action: Opened and followed the state-machine workflow; preserved `PromoCodeRedemptionStatus.Applied`, duplicate redemption rejection through `(PromoCodeId, UserId)`, `PastDue` paid entitlement, and updated non-paying subscription test expectations to the promo branch's `FREE_BASELINE_REWRITES=0` default.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; regenerated promo migrations.
- Verification evidence: Focused backend rerun for `FreeBaselineMigrationTests` plus non-paying subscription downgrade tests passed 7/7; full backend suite passed 490/490.
- Limitations: No new persisted state names were added. The change preserves existing payment and promo state models rather than redesigning lifecycle architecture.

### 2026-06-03 - resilience-test-generation - promo/payment merge gates

- Agent: Codex
- Trigger: The reconcile task had to preserve promo idempotency, global-cap, trusted-IP/Turnstile fail-closed behavior, and payment/webhook replay behavior while merging branches.
- Action: Opened and followed the resilience workflow; kept existing promo and payment resilience tests, preserved proxy-secret/IP handling and checkout/webhook helpers, and used failing test evidence before updating stale free-baseline assertions.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 490/490; `npm run test` passed 345/345; the restricted vocabulary scan over `app components public lib` returned no matches.
- Limitations: No live Stripe, Cloudflare, Azure, OpenAI, Sapling, or production database calls were made. EF list attempted local configured SQL connection only to determine applied status and continued without it.

### 2026-06-03 - dotnet-backend-testing - backend merge conflict verification

- Agent: Codex
- Trigger: The merge changed C# API/Functions routes, `AccountService`, EF migrations, and backend xUnit/WebApplicationFactory coverage.
- Action: Opened and followed the .NET backend testing workflow; resolved backend conflicts as unions, regenerated migrations, ran focused failing tests after root-cause investigation, then ran the full backend solution test suite.
- Output artifacts: backend conflict files, regenerated EF migrations, `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`, `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`.
- Verification evidence: Focused rerun passed 7/7; `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 490/490. `NU1900` vulnerability metadata warnings appeared because the NuGet feed was unreachable, but restore/build/test completed.
- Limitations: No remote CI run was started and no push/PR/deploy was performed.

### 2026-06-03 - ui-browser-testing - frontend merge and Playwright config reconciliation

- Agent: Codex
- Trigger: The merge changed frontend routes, account/admin UI contracts, Playwright configuration, and browser-visible promo/payment flows.
- Action: Opened and followed the UI/browser testing workflow; resolved `playwright.config.ts` to preserve both payment/admin and promo E2E server assumptions, then ran frontend typecheck and unit tests.
- Output artifacts: `playwright.config.ts`; `lib/rewrite-eval-cases.ts`; merged frontend/admin/account route files from `origin/main` plus promo branch files.
- Verification evidence: `npm run typecheck` passed; `npm run test` passed 345/345; banned-term grep over `app components public lib` returned no matches.
- Limitations: Playwright browser E2E was not run in this turn because the requested gates were typecheck and unit tests. No browser screenshots, deploy, or remote testing were performed.

### 2026-06-02 - ui-browser-testing - PROMO-12 promo browser and preview checks

- Agent: Codex
- Trigger: PROMO-12 adds Playwright coverage for signup, login, promo redeem, trial rewrite consumption, paywall state, and Worker-preview smoke coverage for browser-visible routes.
- Action: Opened and followed the UI/browser workflow; added a scoped promo full-loop e2e spec, a Worker-preview smoke script, and checklist links from launch gates to browser and route tests.
- Output artifacts: `tests/e2e/promo-full-loop.spec.ts`; `scripts/promo-preview-smoke.ts`; `tests/unit/promo-preview-smoke.test.ts`; `plans/promo-launch-checklist.md`.
- Verification evidence: `PROMO_PREVIEW_PORT=8794 PROMO_AZURE_MOCK_PORT=45940 npm run smoke:promo-preview` passed; `npm run test` passed; focused promo route/unit tests passed. Playwright browser commands were attempted with a local Chromium cache and failed before page execution because Chromium could not register its macOS Mach rendezvous service in this sandbox.
- Limitations: Browser screenshot/flow evidence could not be captured in this worker sandbox due the Chromium launch boundary. No secrets, cookies, or credential values were logged.

### 2026-06-02 - dotnet-backend-testing - PROMO-12 launch checklist backend gate mapping

- Agent: Codex
- Trigger: PROMO-12 requires the launch checklist to map promo trial, cap, proxy, Turnstile, and admin gates to passing backend tests.
- Action: Opened and followed the .NET backend testing workflow; reviewed existing promo, account, quota, proxy, Turnstile, and admin test names and mapped them to the five launch gates without changing backend code.
- Output artifacts: `plans/promo-launch-checklist.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 445 tests; NuGet vulnerability metadata warnings were present, but restore/build/test completed successfully.
- Limitations: No new .NET tests were added for this issue because the dependent PROMO issues already provided the backend lock tests. No secrets were logged.

### 2026-06-02 - resilience-test-generation - PROMO-12 race and preview smoke coverage

- Agent: Codex
- Trigger: PROMO-12 verifies global cap race behavior, proxy trusted-IP fail-closed behavior, Turnstile handling, and repeated promo redeem outcomes before deploy handoff.
- Action: Opened and followed the resilience workflow; connected existing concurrency, route, and admin audit tests to the launch checklist and added preview smoke cases for invalid, expired, and already-used redeem outcomes with IP forwarding verification.
- Output artifacts: `scripts/promo-preview-smoke.ts`; `tests/unit/promo-preview-smoke.test.ts`; `plans/promo-launch-checklist.md`.
- Verification evidence: `PROMO_PREVIEW_PORT=8794 PROMO_AZURE_MOCK_PORT=45940 npm run smoke:promo-preview` passed and observed three Azure mock redeem calls with forwarded client IP; `npm run test` passed; `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed.
- Limitations: The smoke script uses a local Azure mock behind Worker preview and does not deploy or touch production infrastructure. No secrets were logged.

### 2026-06-02 - data-module-review - PROMO-01 promo EF schema

- Agent: Codex
- Trigger: PROMO-01 adds EF Core entities, indexes, concurrency tokens, check constraints, and a migration for promo codes and redemptions.
- Action: Opened and followed the data-module workflow; reviewed uniqueness, FK shape, delete behavior, indexed lookup columns, check constraints, and migration output.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCode.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCodeRedemption.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602080020_AddPromoCodes.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602080020_AddPromoCodes.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.
- Verification evidence: `dotnet build backend-dotnet/ReplyInMyVoice.sln` passed; `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj` passed 401/401; idempotent EF SQL script generation passed and contained the promo tables, checks, unique indexes, and no `RewriteCreditId` FK on promo redemptions.
- Limitations: No local SQL Server target was available in this worker environment, so database update was verified through EF script generation plus SQLite schema tests. No secrets were logged.

### 2026-06-02 - state-machine-modeling - PROMO-01 redemption status

- Agent: Codex
- Trigger: PROMO-01 adds persisted promo redemption status with `Applied` and `Reversed` values.
- Action: Opened and followed the state workflow at schema scope; kept the issue to persisted states only and used the unique `(PromoCodeId, UserId)` index as the duplicate-apply backstop.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCodeRedemption.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoCodeSchemaTests.cs`.
- Verification evidence: Focused `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter PromoCodeSchemaTests --no-restore` passed 4/4; full backend test command passed 401/401.
- Limitations: PROMO-01 intentionally adds no redemption service, transition function, reversal behavior, quota behavior, or consumption logic. No secrets were logged.

### 2026-06-02 - dotnet-backend-testing - PROMO-01 SQLite schema tests

- Agent: Codex
- Trigger: PROMO-01 requires xUnit SQLite tests for unique promo code, unique redemption per user and code, and check constraints.
- Action: Opened and followed the .NET backend testing workflow; wrote the failing promo schema tests first, then implemented the EF entities and mapping; also reused the repo's no-cookie WebApplicationFactory client pattern so existing API tests run in this sandbox.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoCodeSchemaTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`.
- Verification evidence: Initial focused promo test failed before implementation because promo entity types were missing; after implementation, focused promo tests passed 4/4. Full `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj` passed 401/401.
- Limitations: NuGet vulnerability metadata lookup warned because `https://api.nuget.org/v3/index.json` was unreachable, but restore/build/test artifacts were available. No secrets were logged.

### 2026-05-25 - system-spec-synthesis - Corrected smoke 10 eval and pipeline split

- Agent: Codex
- Trigger: User provided a two-step implementation contract separating eval harness/report reliability from rewrite pipeline behavior.
- Action: Opened/followed the planning workflow and kept Step A and Step B as separate implementation/commit scopes.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `tests/unit/openai-output.test.ts`; `lib/fact-extraction.ts`; `lib/rewrite-pipeline/checks.ts`; `lib/rewrite-pipeline/model.ts`; `lib/rewrite-pipeline/pipeline.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`; `tests/unit/rewrite-pipeline.test.ts`; `docs/eval-runs/single-input-smoke-10-eval-harness-v2.md`; `docs/eval-runs/single-input-smoke-10-pipeline-fix-v1.md`; `docs/eval-runs/single-input-smoke-10-comparison.md`.
- Verification evidence: Step A committed as `45bb57d`; Step B focused tests passed with `npm test -- tests/unit/rewrite-pipeline-checks.test.ts tests/unit/fact-extraction.test.ts tests/unit/rewrite-pipeline.test.ts`; provider smoke 10 completed in smoke mode only with customer pass 8/10.
- Limitations: Did not expand cases 011-100, did not run focused/full mode, and did not run the old dual-input provider eval. No secrets or provider payloads were logged.

### 2026-05-25 - resilience-test-generation - Semantic judge retry and eval resume

- Agent: Codex
- Trigger: Corrected smoke 10 previously failed on a DeepSeek semantic judge timeout, requiring retry/resume behavior.
- Action: Opened/followed the resilience workflow; added bounded semantic judge retry/backoff and per-case progress checkpoint/resume to the eval runner, with tests using mock providers.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `docs/eval-runs/single-input-smoke-10-eval-harness-v2.md`.
- Verification evidence: Focused Step A tests passed with `npm test -- tests/unit/fact-extraction.test.ts tests/unit/eval-scenarios-corpus.test.ts tests/unit/openai-output.test.ts`; Step A smoke wrote a progress checkpoint and final report without rerunning completed cases.
- Limitations: Retry behavior was verified with mock-provider tests and the smoke run, but no forced live provider outage was induced. No raw provider payloads or credentials were logged.

### 2026-05-25 - state-machine-modeling - Rewrite candidate/fallback gate lifecycle

- Agent: Codex
- Trigger: Step B changed the multi-step rewrite lifecycle: fact extraction, candidate generation, candidate selection, finalization, deterministic gates, policy gates, repair/escalation, fallback, and quality failure.
- Action: Opened/followed the state workflow to keep transitions bounded and explicit; candidate selection now checks reviewer, deterministic, and policy gates before selecting, and fallback must pass fact plus naturalness gates before success.
- Output artifacts: `lib/rewrite-pipeline/pipeline.ts`; `lib/rewrite-pipeline/checks.ts`; `tests/unit/rewrite-pipeline.test.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`.
- Verification evidence: `npm test -- tests/unit/rewrite-pipeline-checks.test.ts tests/unit/fact-extraction.test.ts tests/unit/rewrite-pipeline.test.ts` passed 94/94.
- Limitations: This did not change quota, billing, persistence, or async queue states. No secrets were logged.

### 2026-05-25 - data-module-review - Persistence scope check for rewrite eval work

- Agent: Codex
- Trigger: The task touched rewrite/eval workflows and required checking whether persistence invariants or data modules were involved.
- Action: Opened/reviewed the data-module workflow and ruled out persistence changes for this scoped eval/rewrite pipeline patch.
- Output artifacts: No Prisma, EF Core, migration, transaction, usage-counter, idempotency, or database-access files were changed for this Step A/Step B work.
- Verification evidence: Scoped implementation touched eval scripts, rewrite pipeline modules, tests, and docs only; no data-module test or migration was needed.
- Limitations: This is a negative scope check, not a database review. Existing unrelated data/quota test failures, if present in full `npm test`, were not fixed here. No secrets were logged.

### 2026-05-25 - system-spec-synthesis - Two-field rewrite contract implementation

- Agent: Codex
- Trigger: User approved implementing the two-field frontend/backend contract, 400-word draft cap, new eval parser/grader, and next test strategy.
- Action: Used the previously synthesized contract to implement scoped changes across frontend validation, .NET API validation, eval parsing, and strategy docs.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `lib/rewrite-limits.ts`; `lib/rewrite-word-count.ts`; `lib/validation.ts`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteRequestLimits.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/Program.cs`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Focused frontend/parser/eval unit tests passed, .NET `RewriteApiTests` passed, C# eval tool build passed, and `npm run lint` passed. `npm run typecheck` was attempted but is blocked by a pre-existing missing `stripe` package import in `lib/stripe.ts`.
- Limitations: Did not run live provider eval in this turn. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - dotnet-backend-testing - Two-field rewrite API word limit

- Agent: Codex
- Trigger: Backend behavior changed to accept the frontend two-field request shape and reject drafts over 400 words without creating usage or attempts.
- Action: Added ASP.NET API integration coverage before implementation, then updated shared .NET rewrite request limits and both ASP.NET/Functions validation paths.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteRequestLimits.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter RewriteApiTests --no-restore` passed 11/11; broader `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Rewrite --no-restore` passed 42/42.
- Limitations: NuGet vulnerability metadata checks warned because `https://api.nuget.org/v3/index.json` could not be reached, but restore/build/test artifacts were available. No secrets were logged.

### 2026-05-25 - ui-browser-testing - Rewrite workspace word counter

- Agent: Codex
- Trigger: Frontend rewrite workspace changed from character-count display to 400-word draft limit display and submit gating.
- Action: Updated the existing single-input workspace without adding extra context fields; verified the signed-out browser route loads cleanly and identified that authenticated `/app` workspace visual verification requires a signed-in session.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `lib/rewrite-word-count.ts`; `tests/unit/workspace-copy.test.ts` verification.
- Verification evidence: `npm test -- tests/unit/workspace-copy.test.ts tests/unit/rewrite-email-eval-cases.test.ts tests/unit/validation.test.ts tests/unit/eval-scenarios-corpus.test.ts tests/unit/openai-compatible.test.ts tests/unit/rewrite-pipeline-model.test.ts` passed 61/61; Playwright browser check reached `/sign-in?redirect_to=%2Fapp` with no console errors or failed requests.
- Limitations: Could not visually inspect the authenticated workspace without a valid local signed-in session. No credentials, cookies, or tokens were logged.

### 2026-05-25 - cloud-architecture-cost-review - Staged provider eval strategy implementation

- Agent: Codex
- Trigger: User wants the next provider test window to target sub-30% or sub-20% third-party writing signal scores without unbounded provider spend.
- Action: Updated the DeepSeek adaptive strategy and rewrite memory docs to require staged smoke/focused/full eval, answer-key isolation, provider call reporting, and hard fact/forbidden-claim bars.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: No new cloud resources or fixed-cost infrastructure were introduced. The recommended next command uses smoke mode first and keeps full 100-case runs as a later gate.
- Limitations: Exact provider prices were not quoted because no fixed budget estimate was requested and no official pricing pages were consulted. No secrets were logged.

### 2026-05-25 - system-spec-synthesis - Two-field rewrite contract and eval strategy

- Agent: Codex
- Trigger: User clarified that the product frontend should only send `roughDraftReply` and `tone`, asked what backend/frontend/eval changes are needed, and requested a strategy before implementation.
- Action: Opened and followed the skill to structure the contract, non-goals, rollout order, verification, and open questions.
- Output artifacts: Analysis in the current Codex thread; no frontend, backend, eval, or product code changes.
- Verification evidence: Reviewed the current frontend submit payload, Next proxy, .NET rewrite request contract, .NET validation, model prompt assembly, rewrite engine fact extraction, and eval parser/grader boundaries.
- Limitations: No implementation or live rewrite evaluation was run in this planning pass. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - cloud-architecture-cost-review - Rewrite eval threshold and provider-cost strategy

- Agent: Codex
- Trigger: User wants the new corpus used to drive rewrite quality toward sub-30% or sub-20% third-party writing signal scores, which affects DeepSeek/OpenAI-compatible and Sapling evaluation call volume.
- Action: Opened and followed the skill to keep the proposed strategy staged and bounded rather than repeatedly running full 100-case provider evals.
- Output artifacts: Analysis in the current Codex thread; no cloud resources, deploy commands, or provider configuration changes.
- Verification evidence: Identified that parser/test changes have no fixed infrastructure cost, while semantic grading plus rewrite evaluation adds variable provider calls. Recommended smoke/focused/full progression and call-count reporting before full runs.
- Limitations: Exact provider pricing was not quoted because no fixed budget estimate was requested and no official pricing pages were consulted. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - ui-browser-testing - Two-field rewrite workspace verification plan

- Agent: Codex
- Trigger: User clarified the frontend should keep only the current two-field rewrite payload and asked what needs to change.
- Action: Opened and followed the skill to identify browser-visible checks needed if the word-limit UI or submit behavior changes later.
- Output artifacts: Analysis in the current Codex thread; no UI code changes.
- Verification evidence: Reviewed the rewrite workspace submit payload and current client-side character-limit gating. Planned future verification around word counter, disabled state, submit payload, error state, desktop/mobile layout, console, and network behavior.
- Limitations: No dev server, Playwright, Codex Browser, screenshots, or live browser verification were run because this turn was analysis-only. No secrets or credentials were logged.

### 2026-05-25 - dotnet-backend-testing - Two-field rewrite API verification plan

- Agent: Codex
- Trigger: User asked to adjust the backend around the frontend's two-field rewrite contract and word-count limits.
- Action: Opened and followed the skill to identify required xUnit/API coverage for future .NET backend changes.
- Output artifacts: Analysis in the current Codex thread; no .NET code changes.
- Verification evidence: Reviewed `RewriteRequest`, Azure Functions validation, ASP.NET API validation, `RewriteApiTests`, and existing tests that already use two-field JSON payloads for quota/job flows.
- Limitations: No backend tests were added or run in this planning pass. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - cloud-architecture-cost-review - Semantic judge eval cost gate

- Agent: Codex
- Trigger: The proposed semantic fact judge for the new 100-case rewrite corpus would add LLM provider calls to evaluation runs.
- Action: Opened and followed the skill as a pre-implementation cost gate; reviewed the DeepSeek adaptive rewrite strategy, fact-reconstruct target, and manual setup context.
- Output artifacts: Analysis in the current Codex thread; no cloud resource, deployment, or product code changes.
- Verification evidence: Confirmed no new fixed cloud infrastructure is required for the corpus/parser/grader work. Identified variable usage-cost risk from one additional semantic judge model call per evaluated email case, on top of existing rewrite pipeline and Sapling calls.
- Limitations: Did not quote exact provider prices because no exact budget was requested and no provider pricing page was consulted. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - system-spec-synthesis - New 100-case rewrite eval corpus analysis

- Agent: Codex
- Trigger: User replaced `docs/rewrite-email-eval-cases-100.md` and asked for an implementation-impact analysis before code changes.
- Action: Opened and followed the skill to separate source facts, assumptions, component impact, rollout order, and verification needs.
- Output artifacts: Analysis in the current Codex thread; no product code changes.
- Verification evidence: Validated the new corpus has 100 `### Case` entries, required inline fields and `####` sections, non-empty `must_keep` and `must_not_claim` lists, and no banned positioning terms. Ran `npm run test -- tests/unit/rewrite-email-eval-cases.test.ts`, which failed at the expected old-parser boundary.
- Limitations: Did not implement parser, test, semantic grader, or rewrite-engine changes in this analysis pass. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - replyinmyvoice-rewrite - New eval corpus rewrite impact review

- Agent: Codex
- Trigger: User asked whether the new 100-case corpus requires rewrite agent or engine changes.
- Action: Opened the project rewrite skill and used it as context for evaluating corpus, naturalness, fact-preservation, and rewrite-engine boundaries.
- Output artifacts: Analysis in the current Codex thread; no product code changes.
- Verification evidence: Reviewed the new corpus schema, current parser/test/eval runner usage, and product request path around `factsToPreserve`, `messageToReplyTo`, `whatHappened`, and fact extraction.
- Limitations: Did not call the MCP rewrite tool or run live rewrite quality evaluation. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-24 - dotnet-backend-testing - Azure Functions bare API audience auth

- Agent: Codex
- Trigger: Production Google login still returned to `/sign-in` after callback success, and Azure `/api/me` continued returning 401. The remaining likely token-validation boundary is Entra access-token audience shape.
- Action: Opened and followed the skill; added a focused xUnit regression that resolves both `api://<api-client-id>` and bare `<api-client-id>` from the configured API scope, then updated `FunctionAuthResolver` so either valid Entra audience form is accepted while keeping the required scope/role gate.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new `ResolveAudiences_accepts_api_uri_and_bare_api_client_id_from_scope` regression failed before implementation because the resolver did not expose/derive the bare API client id audience. After the fix, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter FunctionAuthResolverTests --no-restore` passed 9/9 and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 76/76.
- Limitations: Codex still cannot perform the owner's live Google login in this session. No access-token values, cookies, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - ui-browser-testing - Entra login token-shape diagnostics

- Agent: Codex
- Trigger: Production Google login still redirected back to `/sign-in` after the CIAM metadata-issuer fix, and Cloudflare tail continued to show Azure `/api/me` returning 401 after `/auth/callback` succeeded.
- Action: Opened and followed the skill; added safe Worker-side diagnostics that log only access-token shape on Azure auth rejection: summarized audience form, issuer host, scope names, role count, and whether stable identity claims exist. The diagnostic intentionally avoids logging token values, email, name, or raw private claims.
- Output artifacts: `lib/azure-api.ts`; `tests/unit/azure-api.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/azure-api.test.ts` failed before the diagnostic helper existed, then passed after implementation. `npm test -- tests/unit/azure-api.test.ts tests/unit/entra-auth.test.ts tests/unit/middleware.test.ts`, `npm run typecheck`, `npm run lint`, and `npm run cf:build` passed.
- Limitations: This is diagnostic instrumentation, not a final auth fix. It needs one post-deploy owner login retry to capture the safe token profile. No raw access tokens, cookies, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - dotnet-backend-testing - Azure Functions CIAM metadata issuer auth

- Agent: Codex
- Trigger: Production Google login still returned to `/sign-in`; Cloudflare logs showed `/auth/callback` succeeded and `/app` had an access token, but Azure Functions `/api/me` still returned 401.
- Action: Opened and followed the skill; added a focused xUnit regression proving Entra External ID CIAM alias authority must accept the canonical issuer published by the discovery metadata, then updated `FunctionAuthResolver` to include the metadata issuer in JWT `ValidIssuers`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new `ResolveValidIssuers_accepts_ciam_metadata_issuer_for_alias_authority` regression failed before implementation because the resolver lacked metadata issuer support. After the fix, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter FunctionAuthResolverTests --no-restore` passed 8/8 and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 75/75.
- Limitations: Codex cannot perform the owner's live Google login in this session; no access-token values, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - ui-browser-testing - Entra login Azure 401 redirect loop

- Agent: Codex
- Trigger: The production browser-visible login flow still redirected back to `/sign-in` after Google callback.
- Action: Opened and followed the skill; used Cloudflare production tail evidence to trace the browser flow through `/auth/callback`, `/app`, and the Azure account-summary request, then compared Worker Entra config with Azure Functions app settings and the CIAM discovery document.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Production tail showed `/auth/callback` succeeded, `/app` loaded, and `azure_account_summary_auth_rejected { status: 401 }` immediately preceded the `/sign-in` redirect. CIAM discovery returned a canonical metadata issuer host different from the configured tenant-subdomain authority, matching the backend issuer-validation failure fixed in this run.
- Limitations: No authenticated browser screenshot or signed-in `/app` Playwright run was possible without the owner's Google session. No secrets or raw token values were logged.

### 2026-05-24 - dotnet-backend-testing - Azure Functions mapped Entra scope auth

- Agent: Codex
- Trigger: Production Google login reached `/app`, but Cloudflare logs showed Azure `/api/me` returned 401 after access-token retrieval, requiring a .NET Functions auth fix and xUnit regression coverage.
- Action: Opened and followed the skill; added a focused xUnit regression for `JwtSecurityTokenHandler`'s inbound-mapped Entra scope claim and updated `FunctionAuthResolver` to accept the mapped Microsoft scope URI alongside raw `scp`, `scope`, and role claims.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new `HasRequiredScopeOrRole_accepts_inbound_mapped_scope_claim` test failed before the resolver fix, then `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter FunctionAuthResolverTests --no-restore` passed 7/7.
- Limitations: Live signed-in Google login must be retried by the owner after deployment. No token values, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - ui-browser-testing - Entra callback access-cookie redirect loop

- Agent: Codex
- Trigger: Owner reported that production Google sign-in still returned to `/sign-in` after `/auth/callback` redirected to `/app`; this is a browser-visible auth redirect and cookie persistence flow.
- Action: Opened and followed the skill; used production request logs, route/source tracing, and focused Playwright auth-gate verification. Changed callback/password/signup session writes to attach cookies directly to the outgoing `NextResponse.cookies` object, added signed metadata for access-token cookie chunks, reduced callback Set-Cookie churn, and added safe auth-boundary logging for `/app` account-summary failures.
- Output artifacts: `app/auth/callback/route.ts`; `app/api/auth/password/route.ts`; `app/api/auth/signup/start/route.ts`; `app/api/auth/signup/resend/route.ts`; `app/api/auth/signup/verify/route.ts`; `lib/entra-auth.ts`; `lib/azure-api.ts`; `tests/unit/entra-auth.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/entra-auth.test.ts tests/unit/middleware.test.ts` passed 18/18; full `npm test` passed 95/95; `npm run typecheck`, `npm run lint`, standalone `npm run build`, standalone `npm run cf:build`, and `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` passed.
- Limitations: Codex cannot complete the owner's real Google account login inside this session. The deployed fix still needs a production retry from a real browser, and no token values, `.env.local` contents, or provider secrets were logged.

### 2026-05-24 - cloud-architecture-cost-review - C# rewrite real-provider optimization

- Agent: Codex
- Trigger: The task uses real DeepSeek and Sapling provider calls and prepares Azure/Cloudflare production deployment, so it affects provider spend and cloud deployment posture.
- Action: Opened and followed the cost gate; kept the existing Azure Functions Consumption + Azure SQL + Service Bus architecture, avoided new paid infrastructure, and used staged real-provider evals before the final full run.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/`; `docs/rewrite-eval-results/20260524-034340-csharp-rewrite-full.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Final full C# eval completed 100/100 provider success with 133 model calls and 206 Sapling calls; no new Azure resources were created by the implementation.
- Limitations: Real provider usage incurred variable DeepSeek/Sapling API cost. Exact provider billing was not fetched, and no secrets were logged.

### 2026-05-24 - system-spec-synthesis - C# rewrite eval and gate scope

- Agent: Codex
- Trigger: The task changes multi-module C# rewrite behavior, provider contracts, eval reporting, and deployment readiness.
- Action: Opened and followed the planning workflow; scoped the implementation to the C# rewrite provider path, eval runner, gates, prompt strategy, and regression tests without changing the hosting architecture.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/FactReconstructRewriteProvider.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiCompatibleRewriteModelClient.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`.
- Verification evidence: Final eval report `docs/rewrite-eval-results/20260524-034340-csharp-rewrite-full.md` records 100/100 successful measured rewrites and 100/100 below 50% signal.
- Limitations: This run optimizes the C# rewrite engine path; it does not migrate unrelated learning/admin/API-key datastore workstreams.

### 2026-05-24 - resilience-test-generation - rewrite provider retries and gates

- Agent: Codex
- Trigger: The implementation changes Sapling unavailable handling, provider retry behavior, naturalness gate recovery, and deterministic fact/unsupported-judgment gates.
- Action: Opened and followed the resilience workflow; added retry tests for transient writing-signal unavailability and gate regression tests for amount/count preservation and unsupported workplace judgments.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/FactReconstructRewriteProviderTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteEngineCoreTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/FactReconstructRewriteProvider.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 71/71 tests; the final real-provider eval had zero provider failures.
- Limitations: Retry coverage is unit-level plus real-provider eval evidence; it does not simulate every Sapling HTTP status separately.

### 2026-05-24 - dotnet-backend-testing - C# rewrite provider tests

- Agent: Codex
- Trigger: The task adds and changes C#/.NET provider behavior, xUnit tests, and a C# console eval tool.
- Action: Opened and followed the .NET testing workflow; added focused xUnit coverage for Sapling retries, attempt-history retries, exact fact gates, number-word normalization, thousands amounts, membership payment/date preservation, and unsupported judgment labels.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/FactReconstructRewriteProviderTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteEngineCoreTests.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/`.
- Verification evidence: `dotnet build backend-dotnet/ReplyInMyVoice.sln --no-restore -maxcpucount:1` passed; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 71/71 tests.
- Limitations: Browser-visible UI did not change in this run, so no UI/browser skill was used.

### 2026-05-23 - ui-browser-testing - students v2 landing and workspace nudge

- Agent: Codex
- Trigger: The task changes the `/students` landing page, workspace output UI, responsive layout, browser-visible routing, and local verification expectations.
- Action: Opened and followed the skill; selected focused unit/source guards plus attempted local browser verification for `/students` after implementation.
- Output artifacts: `app/students/page.tsx`; `app/globals.css`; `app/sitemap.ts`; `app/app/page.tsx`; `components/app/rewrite-workspace.tsx`; `tests/unit/students-v2-page.test.ts`; `tests/unit/workspace-copy.test.ts`; `tests/unit/pricing-auth-visual-system.test.ts`; `vitest.config.ts`; `.gitignore`; `docs/skill-run-log.md`.
- Verification evidence: `npm run prisma:generate`, `npm run typecheck`, `npm run lint`, and full `npm test` passed. The restricted-term scan over `app`, `components`, `public`, and `lib` returned no matches. Local dev-server startup was attempted for browser verification, but both `0.0.0.0:3000` and `127.0.0.1:3000` failed with sandbox `listen EPERM`.
- Limitations: No browser screenshot or live responsive browser pass was possible because this sandbox cannot bind a local HTTP port. A fallback `npm run build` was attempted and failed before route compilation because DNS to `fonts.googleapis.com` is unavailable for `next/font`; no secrets, deployment, Stripe, schema, middleware, auth, or rewrite-pipeline changes were made.

### 2026-05-23 - data-module-review - M1-007 Entra user id migration

- Agent: Codex
- Trigger: M1-007 changes the Prisma `User` model and adds a database migration for the Entra migration window.
- Action: Opened and followed the skill; reviewed the owned `User` table, existing migrations, auth identifier context, uniqueness invariant, and migration safety before editing.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260523090000_add_user_entra_user_id/migration.sql`; `plans/clerk-to-entra-user-backfill.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` ran and the scoped migration is additive only: nullable `entraUserId` plus a unique index.
- Limitations: Application lookup changes and the actual production backfill are intentionally left to later M1 issues. No secret files, provider dashboards, live money, git, or GitHub actions were touched.

### 2026-05-22 - web-design-engineer - M4-012 landing/header/footer refresh

- Agent: Codex
- Trigger: M4-012 is a browser-visible landing, header, and footer visual refresh.
- Action: Opened and followed the skill; reviewed the aborted implementation, preserved the practical workflow-led direction, fixed mobile overflow, and recorded design decisions plus five-dimension self-critique.
- Output artifacts: `plans/frontend-redesign-design-brief.md`; landing components; `components/site-header.tsx`; `components/site-footer.tsx`; `tailwind.config.ts`; `docs/skill-run-log.md`.
- Verification evidence: Final self-critique average is 7.8/10 for this scoped landing pass; desktop and mobile Playwright checks loaded `/` successfully with no console errors, failed requests, or horizontal overflow.
- Limitations: M4-012 is one scoped pass. M4-013 through M4-015 remain responsible for pricing/auth, `/app`, and final average score >= 8.0 across the full frontend.

### 2026-05-22 - state-machine-modeling - stale git index lock repair lifecycle

- Agent: Codex
- Trigger: The overnight supervisor entered a repair-inbox crash loop because `stash_dirty_worktree` could not write the git index while a stale `.git/index.lock` existed.
- Action: Opened and followed the skill; modeled the supervisor dirty-worktree lifecycle with an additional recovery transition that clears stale index locks before stash operations instead of repeatedly failing the same repair item.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `scripts/analyze_rewrite_quality.py`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh`, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts tests/unit/analyze-rewrite-quality.test.ts`, `npm run typecheck`, `npm run lint`, and full `npm run test` passed.
- Limitations: This repair covers local supervisor/git recovery and local Python compatibility only; it does not change provider dashboard, live money, npm publish, or deployment state.

### 2026-05-22 - ui-browser-testing - M4-012 landing visual refresh

- Agent: Codex
- Trigger: M4-012 changes the landing page, shared header, shared footer, responsive layout, browser-visible navigation, and visual polish.
- Action: Opened and followed the skill; selected desktop and mobile browser verification for `/`, with console/network and layout checks after implementation.
- Output artifacts: `plans/frontend-redesign-design-brief.md`; `components/site-header.tsx`; `components/site-footer.tsx`; `components/landing/hero.tsx`; `components/landing/interactive-demo.tsx`; `components/landing/trust-panel.tsx`; `components/landing/use-cases.tsx`; `components/landing/how-it-works.tsx`; `components/landing/pricing.tsx`; `components/landing/faq.tsx`; `components/landing/closing-cta.tsx`; `tailwind.config.ts`; `plans/task-status.json`.
- Verification evidence: `npm run lint`, `npm run typecheck`, and full `npm run test` passed. Playwright loaded `/` at desktop 1440x1100 and mobile 390x1000 with HTTP 200, no console errors, no failed requests, and no horizontal overflow after fixing the mobile hero/workflow grid.
- Limitations: No auth-gated `/app` workflow, payment flow, live provider call, deployment, or production-domain smoke test is in scope for this landing/header/footer issue.

### 2026-05-22 - state-machine-modeling - supervisor dirty-worktree lifecycle

- Agent: Codex
- Trigger: The overnight supervisor allowed a `needs_human`/repair flow to carry dirty M4-011 files into a later M6-004 repair branch and commit them together.
- Action: Opened and followed the skill; modeled the relevant lifecycle as clean main -> issue branch -> Codex status -> commit/stash/block, with illegal transitions for returning to main or committing when dirty files are undeclared.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh`, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts`, `npm run typecheck`, and `npm run lint` passed. Focused supervisor tests cover branch-before-task writes, no-status preservation, `needs_human`/abort stashing, and the `files_changed` guard that blocks mixed commits.
- Limitations: This covers the shell supervisor lifecycle only; it does not validate GitHub Actions, Cloudflare DNS reachability, or frontend visual quality.

### 2026-05-22 - system-spec-synthesis - supervisor rescue split

- Agent: Codex
- Trigger: The mixed PR #199 needed to be split into implementation-ready scopes without losing M4-011 partial work or M6-004 provider evidence.
- Action: Opened for routing and applied the spec workflow to separate source facts, non-goals, state handling, rollout, and verification in the existing frontend follow-up and custom-domain documents.
- Output artifacts: `plans/frontend-redesign-followups.md`; `plans/custom-domain-attach.md`; `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The rescue split keeps UI source changes out of the supervisor/provider repair branch and records follow-up scopes for the redesign instead of retrying one oversized unattended issue.
- Limitations: The partial UI implementation from the aborted run is preserved separately and is not claimed as complete in this process repair.

### 2026-05-22 - cloud-architecture-cost-review - M6-004 provider-blocker repair

- Agent: Codex
- Trigger: The M6-004 repair item reviews Cloudflare custom-domain verification for `replyinmyvoice.com` after a provider/DNS blocker.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Kept the selected path to read-only Workers domains API verification plus formal-domain smoke from a networked shell, and rejected deploys, DNS changes, dashboard mutation, secret changes, npm publish, and live-money actions.
- Output artifacts: `plans/codex-worker-inbox.md`; `plans/custom-domain-attach.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: Secret-free DNS and curl checks still fail before reaching Cloudflare: `api.cloudflare.com`, `replyinmyvoice.com`, and `example.com` resolve as `ENOTFOUND` from this sandbox.
- Limitations: Current live custom-domain attach state remains unverified until a networked shell runs the documented Cloudflare API and formal-domain smoke checks. No exact pricing lookup was needed because no paid resource or provider-spend action was selected.

### 2026-05-22 - state-machine-modeling - M6-004 repair lifecycle

- Agent: Codex
- Trigger: The repair changes a persisted inbox item lifecycle status for a Cloudflare verification blocker.
- Action: Opened and followed the skill; modeled the repair item transition as `in_progress -> not_actionable` on confirmed sandbox DNS failure, with the allowed next external event being a networked rerun of the documented verification commands.
- Output artifacts: `plans/codex-worker-inbox.md`; `plans/custom-domain-attach.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: The inbox item now records the terminal not-actionable reason, and `plans/custom-domain-attach.md` records states, events, a transition table, invariants, illegal transitions, persistence implications, a test checklist, and exact networked commands.
- Limitations: This lifecycle note covers the repair queue only; it does not verify live Cloudflare state.

### 2026-05-22 - resilience-test-generation - M6-004 provider-blocker routing

- Agent: Codex
- Trigger: The repair item mentions a provider blocker, so the resilience skill was opened to check whether provider-failure testing guidance applied.
- Action: Opened for routing; final scope stayed on read-only Cloudflare DNS/API verification and repair queue classification, without changing retries, timeouts, quota, idempotency, webhook replay, queue redelivery, or recovery behavior.
- Output artifacts: `plans/codex-worker-inbox.md`; `plans/custom-domain-attach.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: Secret-free DNS and curl checks reproduced sandbox reachability failure before any Cloudflare response was returned.
- Limitations: No provider timeout fake, rate-limit test, live Cloudflare API response, or retry behavior test was added because no application resilience behavior changed.

### 2026-05-22 - cloud-architecture-cost-review - M6-004 custom domain check

- Agent: Codex
- Trigger: M6-004 reviews Cloudflare Worker custom-domain attach state for `replyinmyvoice.com` and `replyinmyvoice-app`.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Selected read-only Workers domains API verification and formal-domain smoke checks; rejected deploys, DNS changes, secret changes, and paid-resource creation for this issue.
- Output artifacts: `plans/custom-domain-attach.md`; `docs/preflight-report.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `curl` to the Workers domains API failed before returning Cloudflare metadata because `api.cloudflare.com` could not be resolved from this shell. Formal-domain `curl` checks also failed before reaching the site because `replyinmyvoice.com` could not be resolved.
- Limitations: Current live attach state remains unverified in this sandbox. No secret values were printed or written, no deploy ran, no DNS state changed, and `.env.local` was not modified.

### 2026-05-22 - cloud-architecture-cost-review - M6-001 secret diff retry

- Agent: Codex
- Trigger: Repair item `REPAIR-20260522180011` reviews Cloudflare Worker production secret-name configuration for `replyinmyvoice-app`.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Selected the read-only `wrangler secret list` retry path; rejected secret pushes, deploys, dashboard mutation, and paid-resource changes for this repair.
- Output artifacts: `plans/worker-secret-diff.md`; `plans/codex-worker-inbox.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `XDG_CONFIG_HOME=/private/tmp/replyinmyvoice-wrangler-config npx --no-install wrangler secret list --name replyinmyvoice-app --format json` failed before returning Worker metadata because Cloudflare API DNS resolution is unavailable. A direct DNS lookup returned `ENOTFOUND` for `api.cloudflare.com` and `dash.cloudflare.com`.
- Limitations: The live Worker secret-name list remains unavailable, so `present-in-both`, `missing-in-worker`, and `missing-in-local` are still not authoritative. No secret values were printed or written, no secrets were pushed, no deploy ran, and no provider dashboard state changed.

### 2026-05-19 - system-spec-synthesis - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/spec_outline.py` produced the required implementation-spec headings, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - resilience-test-generation - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/resilience_matrix.py` produced timeout, retry, duplicate, partial-success, concurrency, and malformed-payload failure rows, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - state-machine-modeling - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/state_machine_template.py` produced states, events, transitions, invariants, and illegal-transition sections, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - data-module-review - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/scan_data_risks.py` excluded generated/build/vendor output by default and supported `--limit` / `--include-generated`, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - claude-heavy-planning-handoff - Skill smoke verification

- Agent: Codex with local Claude Code CLI
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/build_handoff_brief.py` produced a sanitized handoff brief, and Codex successfully called local Claude Code non-interactively with `claude -p`, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling and Claude CLI handoff path were smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-21 - skill-run-log - Evidence logging policy installed

- Agent: Codex
- Trigger: User requested a run log so future use of the five tracked skills leaves evidence.
- Action: Added logging policy and append-only run log.
- Output artifacts: `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The `AGENTS.md` routine skill routing rules now require appending to this file whenever a tracked skill is opened, followed, smoke-tested, or used.
- Limitations: This is a logging-policy entry, not a tracked skill run. It does not retroactively prove historical automatic triggers.

### 2026-05-21 - state-machine-modeling - Current code state lifecycle review

- Agent: Codex
- Trigger: User explicitly requested running the `state-machine-modeling` skill to inspect current code state lifecycles.
- Action: Opened and followed the skill; used `agent-skills/state-machine-modeling/scripts/state_machine_template.py` to generate a review skeleton; reviewed subscription, quota/reservation, rewrite attempt, Stripe webhook, outbox, and rewrite job state paths.
- Output artifacts: Review findings delivered in the Codex thread; no production code changed.
- Verification evidence: Ran `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` with 34/34 passing tests; ran `npm test -- tests/unit/quota.test.ts tests/unit/stripe-webhook-events.test.ts` with 7/7 passing tests.
- Limitations: This was a static lifecycle review plus existing test run. It did not implement fixes for the identified state-machine gaps.

### 2026-05-21 - state-machine-modeling - State lifecycle fixes

- Agent: Codex
- Trigger: User requested tests and fixes for the state-machine risks found in the current code review.
- Action: Opened and followed the skill; added transition/invariant tests and updated rewrite attempt, usage reservation, subscription, and quota transitions.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/RewriteAttemptStatus.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/SubscriptionStatus.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; related .NET tests.
- Verification evidence: First ran `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` and observed 5 expected failing tests; after fixes, the same command passed with 38/38 tests.
- Limitations: This fixed the .NET backend state-machine defects. It did not introduce a full Prisma/Next rewrite-attempt reservation table.

### 2026-05-21 - data-module-review - Quota and webhook persistence fixes

- Agent: Codex
- Trigger: The state fixes changed EF Core entities, enum values, quota counters, subscription persistence, and data-access services.
- Action: Opened and followed the skill; ran `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80`; reviewed owned tables/entities and service mutations before changing persistence behavior.
- Output artifacts: Same code and test files listed in the state lifecycle fixes entry; `docs/business-qa-and-deploy-result.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed with 38/38 tests; `npm test -- tests/unit/quota.test.ts tests/unit/stripe-webhook-events.test.ts` passed with 7/7 tests.
- Limitations: No database migration was added because the changed .NET enum values are stored as strings and no schema shape changed.

### 2026-05-21 - resilience-test-generation - Provider, cleanup, quota, and webhook regression tests

- Agent: Codex
- Trigger: The fixes involve provider success after partial failure, expired reservation cleanup, duplicate/terminal events, and paid quota exhaustion.
- Action: Opened and followed the skill; ran `agent-skills/resilience-test-generation/scripts/resilience_matrix.py "rewrite attempt quota reservation lifecycle"`; added deterministic local tests before production fixes.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ExpiredReservationCleanupServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs`.
- Verification evidence: The new tests failed first under the old implementation, then passed after the fixes; final focused verification passed with 38/38 .NET tests and 7/7 Next quota/webhook tests.
- Limitations: Tests use deterministic local SQLite/fakes and do not hit live Stripe, Azure Service Bus, OpenAI, Sapling, Clerk, or production databases.

### 2026-05-21 - dotnet-backend-testing - Skill created and routed

- Agent: Codex
- Trigger: User requested a reusable testing skill that is added to `AGENTS.md` and automatically triggers for future C#/.NET backend testing work.
- Action: Created project skill and added required routing rules.
- Output artifacts: `agent-skills/dotnet-backend-testing/SKILL.md`; `agent-skills/dotnet-backend-testing/agents/openai.yaml`; `agent-skills/dotnet-backend-testing/references/demo-prompts.md`; `AGENTS.md`; `CLAUDE.md`; `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 /Users/qc/.codex/skills/.system/skill-creator/scripts/quick_validate.py agent-skills/dotnet-backend-testing`; output: `Skill is valid!`.
- Limitations: This proves the skill exists and is routed. Future development tasks must append separate entries when the skill is actually used to write or review tests.

### 2026-05-21 - ui-browser-testing - Skill created and routed

- Agent: Codex
- Trigger: User requested a separate UI/browser testing skill for Playwright, browser flows, screenshots, responsive layout, visual review, and `AGENTS.md` auto-trigger rules.
- Action: Created project skill and added required routing rules.
- Output artifacts: `agent-skills/ui-browser-testing/SKILL.md`; `agent-skills/ui-browser-testing/agents/openai.yaml`; `agent-skills/ui-browser-testing/references/demo-prompts.md`; `agent-skills/dotnet-backend-testing/SKILL.md`; `AGENTS.md`; `CLAUDE.md`; `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 /Users/qc/.codex/skills/.system/skill-creator/scripts/quick_validate.py agent-skills/ui-browser-testing`; output: `Skill is valid!`.
- Limitations: This proves the skill exists and is routed. Future UI/browser tasks must append separate entries when the skill is actually used to write tests, run Playwright, inspect screenshots, or verify browser behavior.

### 2026-05-21 - cloud-architecture-cost-review - Skill created and routed

- Agent: Codex
- Trigger: User requested a reusable architecture and cost review skill to prevent overbuilt or expensive cloud choices such as unnecessary always-on Azure App Service for low-usage workloads.
- Action: Created project skill, added a reusable architecture cost review template script, and added required routing rules.
- Output artifacts: `agent-skills/cloud-architecture-cost-review/SKILL.md`; `agent-skills/cloud-architecture-cost-review/agents/openai.yaml`; `agent-skills/cloud-architecture-cost-review/scripts/cost_review_template.py`; `agent-skills/cloud-architecture-cost-review/references/demo-prompts.md`; `AGENTS.md`; `CLAUDE.md`; `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 /Users/qc/.codex/skills/.system/skill-creator/scripts/quick_validate.py agent-skills/cloud-architecture-cost-review`; output: `Skill is valid!`. Ran `python3 agent-skills/cloud-architecture-cost-review/scripts/cost_review_template.py "Azure App Service vs Azure Functions"` and confirmed it prints the architecture cost review headings.
- Limitations: This proves the skill exists and is routed. It does not quote current Azure prices; future exact cost reviews must verify current official provider pricing before using specific numbers.

### 2026-05-21 - cloud-architecture-cost-review - Azure Functions cold start options

- Agent: Codex
- Trigger: User asked whether the project currently uses Azure Functions and how to handle Azure Functions cold start without wasting money.
- Action: Opened and followed the skill; reviewed current project docs and the Azure Functions .NET isolated worker setup; compared low-cost Consumption, async polling, Flex Consumption always-ready, Premium, and App Service tradeoffs.
- Output artifacts: Answer delivered in the Codex thread; no code or infrastructure changed.
- Verification evidence: Confirmed `docs/dotnet-azure-full-run-result.md` records `replyinmyvoice-func-dev` on Linux consumption plan `Y1`; confirmed `backend-dotnet/src/ReplyInMyVoice.Functions/Program.cs` is a minimal .NET 8 isolated worker startup; consulted Microsoft Learn Azure Functions hosting/cold-start documentation.
- Limitations: No live Azure settings were changed and no current Azure pricing numbers were quoted. Exact cost decisions still require checking current Azure pricing for the selected region and plan.

### 2026-05-21 - cloud-architecture-cost-review - Flex Consumption Always Ready cost estimate

- Agent: Codex
- Trigger: User asked for the monthly cost of Azure Functions Flex Consumption with Always Ready for the current project.
- Action: Opened and followed the skill; checked current Azure Retail Prices API for Functions Flex Consumption in `australiaeast`; estimated baseline always-ready monthly cost for 512 MB, 2 GB, and 4 GB instance sizes.
- Output artifacts: Cost estimate delivered in the Codex thread; no code or infrastructure changed.
- Verification evidence: Azure Retail Prices API returned Flex Consumption `Always Ready Baseline`, `Always Ready Execution Time`, and `Always Ready Total Executions` meters for `australiaeast`; monthly estimate used 730 hours/month and the 2 GB default Flex instance size documented by Microsoft Learn.
- Limitations: NZD conversion is approximate; exact billed amount can vary by Azure account currency, taxes, region availability, plan configuration, actual instance memory, execution duration, and whether always-ready is enabled for only HTTP or multiple function groups.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek provider cost discussion

- Agent: Codex
- Trigger: User asked to discuss switching rewrite model roles to DeepSeek, model-tier allocation, and third-party writing-signal provider options before implementation.
- Action: Opened and followed the skill as a cost and provider gate; checked current DeepSeek official model/base URL/pricing docs and compared provider-call cost risk at the architecture level.
- Output artifacts: Discussion delivered in the Codex thread; no business code, environment files, provider configuration, or infrastructure changed.
- Verification evidence: DeepSeek official docs confirmed `https://api.deepseek.com`, `deepseek-v4-flash`, `deepseek-v4-pro`, thinking-mode controls, and the current v4-pro discount window.
- Limitations: This was a pre-implementation discussion only. No live DeepSeek call, key validation, cost benchmark, or production rollout was performed.

### 2026-05-21 - system-spec-synthesis - Rewrite orchestrator context discussion

- Agent: Codex
- Trigger: User asked to discuss a rewrite-orchestrator fix where failed attempts carry original input, previous rewrites, and failure analysis into the next loop.
- Action: Opened and followed the skill; reviewed `docs/fact-reconstruct-rewrite-target.md`, `docs/superpowers/plans/2026-05-20-adaptive-rewrite-agent-orchestrator.md`, and related strategy-memory notes before proposing the design.
- Output artifacts: Discussion delivered in the Codex thread; no implementation spec file or code changed.
- Verification evidence: Project docs confirmed the existing fact-reconstruct pipeline, adaptive orchestrator plan, attempt/failure-kind concepts, Sapling constraints, and no-charge quality-failure rule.
- Limitations: No formal spec artifact was written because the user requested discussion before implementation.

### 2026-05-21 - state-machine-modeling - Rewrite attempt loop discussion

- Agent: Codex
- Trigger: User asked about carrying each failed rewrite attempt and diagnosis into later rewrite/repair loops, which affects the multi-step rewrite-attempt lifecycle.
- Action: Opened and followed the skill; modeled the proposed loop as a bounded attempt ledger with explicit attempt states, transition evidence, retry decisions, success, and quality-failure terminal behavior.
- Output artifacts: Discussion delivered in the Codex thread; no state-machine code or tests changed.
- Verification evidence: Reviewed the adaptive orchestrator plan's required failure kinds, strategy decisions, budget manager, gates, and quality-failure/no-charge behavior.
- Limitations: This was a design discussion only and did not add typed states, transition helpers, or regression tests.

### 2026-05-21 - resilience-test-generation - Provider and writing-signal discussion

- Agent: Codex
- Trigger: User asked about DeepSeek provider routing, per-role thinking configuration, Sapling feedback usage, and alternate writing-signal providers.
- Action: Opened and followed the skill; framed the future implementation around provider timeouts, unavailable writing-signal responses, retry budget limits, no-charge quality failures, and deterministic/local test coverage.
- Output artifacts: Discussion delivered in the Codex thread; no provider adapter, tests, or eval scripts changed.
- Verification evidence: Official Sapling docs confirmed sentence/token-level signal outputs and false-positive/false-negative cautions; project docs confirmed Sapling unavailability is a quality-failure/no-charge condition in the fact-reconstruct route.
- Limitations: No live provider failure test, rate-limit simulation, or alternate signal integration was run.

### 2026-05-21 - cloud-architecture-cost-review - Ten-attempt DeepSeek rewrite budget discussion

- Agent: Codex
- Trigger: User proposed using DeepSeek v4-pro for all rewrite roles initially, allowing up to 10 rewrite/repair attempts, and using DeepSeek credentials already provided outside the thread.
- Action: Opened and followed the skill as a provider-cost and retry-budget gate; treated 10 attempts as a maximum error-budgeted quality path rather than the default path.
- Output artifacts: Discussion delivered in the Codex thread; no provider adapter, environment file, production code, deployment config, or paid infrastructure changed.
- Verification evidence: Prior official DeepSeek documentation check confirmed model names, base URL, thinking controls, and current discounted v4-pro pricing; this turn also generated the synthetic 100-case eval corpus to support staged testing before production rollout.
- Limitations: No live DeepSeek call, key validation, cost benchmark, or production request budget measurement was performed.

### 2026-05-21 - system-spec-synthesis - Attempt history and 100-case eval discussion

- Agent: Codex
- Trigger: User clarified that repair attempts must carry all previous failed originals and asked for a broad 100-case email evaluation corpus before implementation.
- Action: Opened and followed the skill; framed the future implementation as an attempt ledger plus source-of-truth fact ledger, then used Codex 5.5 CLI in batches to generate the requested synthetic evaluation markdown.
- Output artifacts: `docs/rewrite-email-eval-cases-100.md`; discussion delivered in the Codex thread.
- Verification evidence: Verified the markdown contains 100 case headings, 100 `id` fields, and 100 instances of each required field; verified the file is ASCII-only.
- Limitations: This was not a production implementation. The case corpus is synthetic and still needs conversion into runnable eval fixtures before it can drive automated scoring.

### 2026-05-21 - state-machine-modeling - Ten-attempt rewrite attempt ledger discussion

- Agent: Codex
- Trigger: User specified that failed rewrite attempts must carry original input, each failed rewrite, and each failure analysis into the next loop, with a maximum of 10 attempts.
- Action: Opened and followed the skill; modeled the future rewrite request as a bounded lifecycle with attempt records, failure evidence, strategy transitions, budget exhaustion, success, and quality-failure/no-charge terminal states.
- Output artifacts: Discussion delivered in the Codex thread; no state-machine code or tests changed.
- Verification evidence: The proposed ledger design preserves attempt number, strategy, candidate text, reviewer issues, fact-gate result, structure-gate result, Sapling result, diagnosis, and next strategy decision across retries.
- Limitations: No typed transition function, persistence schema, or regression tests were added in this turn.

### 2026-05-21 - resilience-test-generation - Ten-attempt provider and gate resilience discussion

- Agent: Codex
- Trigger: User's max-10 retry design touches provider failures, Sapling signal use, failed candidate carry-forward, quality-gate failures, and no-charge behavior.
- Action: Opened and followed the skill; framed future tests around bounded retries, malformed model output, provider timeout/rate-limit, Sapling unavailable, fact-gate failure, structural-gate failure, and budget exhaustion.
- Output artifacts: Discussion delivered in the Codex thread; no test files changed.
- Verification evidence: The generated 100-case corpus includes high-risk cases for policy/intent gates, quote/list boundaries, support macro voice, sentence-per-paragraph drafts, broken numbered lists, messy forwarded threads, dense facts, low-signal already-natural drafts, and no-change-without-confirmation constraints.
- Limitations: No deterministic fakes, live provider calls, or automated eval run were executed.

### 2026-05-21 - system-spec-synthesis - Gate rule repair discussion

- Agent: Codex
- Trigger: User asked whether failed rewrite reasons should lead to repairing gate rules.
- Action: Opened and followed the skill; framed gate repair as a source-of-truth distinction between real output failures, false-positive gate failures, false-negative gate misses, and fact-ledger defects.
- Output artifacts: Discussion delivered in the Codex thread; no implementation spec or code changed.
- Verification evidence: Reviewed `docs/rewrite-strategy-memory.md` sections on Adaptive Gate Calibration and Reviewed Fact Ledger Before Rewrite.
- Limitations: No gate rule change, tests, or eval run was performed.

### 2026-05-21 - state-machine-modeling - Gate lifecycle discussion

- Agent: Codex
- Trigger: User asked about changing gate behavior based on failure reasons, which affects the rewrite attempt and quality-gate lifecycle.
- Action: Opened and followed the skill; described gate outcomes as hard block, soft diagnostic, repair route, or quality-failure terminal state.
- Output artifacts: Discussion delivered in the Codex thread; no state-machine code changed.
- Verification evidence: Reviewed project state-machine guidance and existing strategy-memory notes distinguishing hard business facts from soft footer/polite-formula issues.
- Limitations: No typed transition helper, enum, or regression test was added.

### 2026-05-21 - resilience-test-generation - Gate regression discussion

- Agent: Codex
- Trigger: User asked whether failure reasons should drive gate fixes, which requires tests for false positives, false negatives, provider failures, and budget exhaustion.
- Action: Opened and followed the skill; stated that any gate repair must ship with targeted regression cases proving it fixes the observed miss without weakening hard fact protections.
- Output artifacts: Discussion delivered in the Codex thread; no tests changed.
- Verification evidence: Reviewed resilience skill guidance and existing strategy-memory required regressions for soft footer misses versus hard money/date/policy failures.
- Limitations: No deterministic fake, focused test command, or eval command was run.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek attempt-ledger strategy doc

- Agent: Codex
- Trigger: User asked to formalize the DeepSeek v4-pro rewrite strategy, including max 10 attempts and provider routing, before opening a new test window.
- Action: Followed the provider cost and budget gate; recorded v4-pro as the current quality-first model for all rewrite roles, deferred v4-flash until quality stabilizes, and documented non-thinking versus thinking/high reasoning budget use.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The strategy doc records `OPENAI_BASE_URL=https://api.deepseek.com`, all current model roles on `deepseek-v4-pro`, a hard 10-attempt cap, and no-secret handling.
- Limitations: No live DeepSeek call, key validation, provider adapter change, cost benchmark, or production rollout was performed.

### 2026-05-21 - system-spec-synthesis - DeepSeek rewrite strategy doc

- Agent: Codex
- Trigger: User asked to write the current rewrite-orchestrator strategy into a new markdown file and update `AGENTS.md` so a new test window can use it.
- Action: Followed the system-spec workflow; converted the discussion into an implementation-ready strategy covering source-of-truth inputs, runtime pipeline, attempt ledger schema, Sapling usage, gate calibration, evaluation modes, and success criteria.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The new strategy document includes the attempt ledger fields, repair prompt inputs, failure tags, staged 10/40/100 evaluation plan, and references the 100-case corpus path.
- Limitations: This produced strategy documentation only. No code, tests, prompts, provider configuration, or eval fixtures were changed.

### 2026-05-21 - state-machine-modeling - Attempt lifecycle strategy doc

- Agent: Codex
- Trigger: The strategy changes the rewrite-attempt lifecycle by requiring all failed candidate texts and failure analyses to flow into later attempts with a hard max of 10 attempts.
- Action: Followed the state-machine workflow; documented attempt records, transition evidence, budget exhaustion, success, and quality-failure/no-charge terminal behavior.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The strategy doc defines the attempt budget table, required attempt ledger fields, failure tags, and quality-failure behavior when no candidate passes within the budget.
- Limitations: No typed state enum, transition helper, persistence schema, or automated lifecycle tests were added.

### 2026-05-21 - resilience-test-generation - Attempt ledger and gate strategy doc

- Agent: Codex
- Trigger: The max-10 DeepSeek loop and gate calibration policy touch provider failures, Sapling availability, quality-failure/no-charge behavior, and regression requirements for gate changes.
- Action: Followed the resilience workflow; documented bounded retry behavior, Sapling unavailable handling, hard versus soft gate failures, structural blockers, and paired regression requirements for future gate repairs.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The strategy doc requires paired gate regressions, no weakened hard fact protections, no successful weak fallback, and staged evaluation using `docs/rewrite-email-eval-cases-100.md`.
- Limitations: No provider timeout fake, Sapling failure test, quality-failure test, or focused eval command was run.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek test window readiness review

- Agent: Codex
- Trigger: User asked whether the new DeepSeek rewrite-quality strategy and 100-case corpus are ready for testing, which affects AI/provider call routing and evaluation cost boundaries.
- Action: Followed the provider cost and budget gate; checked the strategy document, AGENTS routing note, environment examples, model/provider call sites, evaluation scripts, and staged eval assumptions before any live provider calls.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Offline checks confirmed `docs/rewrite-email-eval-cases-100.md` has 100 cases, the strategy document has no non-ASCII characters, common secret patterns were not found in the reviewed docs, targeted rewrite/provider unit tests passed, and `npm run typecheck` passed.
- Limitations: No live DeepSeek, OpenAI, or Sapling call was run. Exact provider pricing was not rechecked because no external paid test was executed. Current code still hard-codes the OpenAI chat completions URL and existing eval scripts do not yet consume the 100-case markdown corpus.

### 2026-05-21 - resilience-test-generation - DeepSeek test window readiness review

- Agent: Codex
- Trigger: User asked whether testing can start for a strategy involving provider routing, max-attempt behavior, Sapling unavailable handling, quality-failure/no-charge semantics, and gate repair regression expectations.
- Action: Followed the resilience review workflow; inspected current provider boundaries, Sapling retry/unavailable behavior, budget-manager limits, strategy-router failure handling, quality-failure tests, and eval reporting before recommending live testing.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Targeted unit tests passed for rewrite budget management, strategy routing, writing-signal behavior, fact-reconstruct model parsing, legacy OpenAI model config, and API quality-failure ordering.
- Limitations: No deterministic DeepSeek fake, OpenAI-compatible base URL test, Sapling timeout eval, max-10 attempt ledger test, or 100-case eval parser was added in this turn.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek test-window implementation gate

- Agent: Codex
- Trigger: User asked to start the next step but stop before live testing, involving DeepSeek/OpenAI-compatible provider routing and AI/provider evaluation budget boundaries.
- Action: Followed the provider cost gate; kept production defaults unchanged, added explicit empty env placeholders, added a test-window-only max-attempt override clamped to 10, and stopped before any paid/live provider call.
- Output artifacts: `lib/openai-compatible.ts`; `.env.example`; `lib/rewrite-pipeline/budget-manager.ts`; `docs/superpowers/plans/2026-05-21-deepseek-test-readiness.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm run lint` passed, `npm run typecheck` passed, and `npm run test` passed with 32 files and 190 tests.
- Limitations: No exact DeepSeek/OpenAI/Sapling pricing was checked because no live paid test was executed. No provider call, deployment, or remote smoke test was run.

### 2026-05-21 - system-spec-synthesis - DeepSeek test-readiness implementation

- Agent: Codex
- Trigger: The task converted the DeepSeek adaptive strategy notes into executable code/test checkpoints across provider routing, eval corpus loading, budget behavior, and attempt-ledger metadata.
- Action: Followed the spec workflow at implementation scope; created a focused implementation plan, mapped the active strategy requirements into code files and tests, and preserved the stop-before-live-testing boundary.
- Output artifacts: `docs/superpowers/plans/2026-05-21-deepseek-test-readiness.md`; `lib/openai-compatible.ts`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; provider/eval/budget/ledger unit tests.
- Verification evidence: Added tests first and observed expected failures, then implemented the minimal code; final verification was `npm run lint`, `npm run typecheck`, and `npm run test` with 190 passing tests.
- Limitations: This prepared local readiness only. It did not run the 100-case live eval, inspect live provider schemas, validate real keys, or deploy.

### 2026-05-21 - state-machine-modeling - Rewrite attempt ledger implementation

- Agent: Codex
- Trigger: The DeepSeek strategy requires a bounded attempt lifecycle with failed candidate evidence, failure analysis, strategy transitions, and a hard maximum of 10 attempts.
- Action: Followed the lifecycle workflow; added a typed attempt-ledger entry, a helper that rejects attempt numbers outside 1-10, metadata on success/failure payloads, and escalation prompts that receive prior failed attempts as negative evidence.
- Output artifacts: `lib/rewrite-pipeline/types.ts`; `lib/rewrite-pipeline/attempt-ledger.ts`; `lib/rewrite-pipeline/pipeline.ts`; `lib/rewrite-pipeline/model.ts`; `tests/unit/rewrite-attempt-ledger.test.ts`; `tests/unit/rewrite-pipeline-model.test.ts`.
- Verification evidence: `tests/unit/rewrite-attempt-ledger.test.ts` verifies ledger shape and hard 10-attempt rejection; `tests/unit/rewrite-pipeline-model.test.ts` verifies escalation prompts include failed candidate text and failure analysis.
- Limitations: The ledger is in response/error metadata and retry prompts, not a persisted database table. No live multi-attempt eval run was executed.

### 2026-05-21 - resilience-test-generation - DeepSeek provider and eval readiness implementation

- Agent: Codex
- Trigger: The implementation touches provider boundary routing, provider-key selection, Sapling/no-charge gate context, staged eval loading, and bounded retry budget behavior.
- Action: Followed the resilience test workflow; added deterministic unit tests with fake fetch for OpenAI-compatible routing and DeepSeek key selection, parser tests for the 100-case corpus, budget override tests, and escalation prompt-history tests.
- Output artifacts: `tests/unit/openai-compatible.test.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; `tests/unit/rewrite-budget-manager.test.ts`; `tests/unit/rewrite-attempt-ledger.test.ts`; `tests/unit/rewrite-pipeline-model.test.ts`; related implementation files.
- Verification evidence: The initial focused test run failed for missing helper/parser/ledger/override behavior; after implementation, `npm run test` passed with 32 files and 190 tests, `npm run typecheck` passed, and `npm run lint` passed.
- Limitations: No live timeout/rate-limit test against DeepSeek, OpenAI, or Sapling was run. Provider failure coverage remains fake-based local unit coverage until the user approves the live test window.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek live smoke test

- Agent: Codex
- Trigger: User explicitly asked to start testing after the DeepSeek test-window readiness work, which creates live DeepSeek and Sapling provider cost exposure.
- Action: Followed the provider cost gate; ran 10-case smoke instead of focused/full, kept all model roles on `deepseek-v4-pro`, used `OPENAI_BASE_URL=https://api.deepseek.com`, disabled thinking for ordinary JSON calls, and kept timeout/case scope bounded.
- Output artifacts: `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; DeepSeek request-option changes in `lib/rewrite-pipeline/model.ts` and tests.
- Verification evidence: Final full-context smoke wrote `docs/scenario-evaluation-results.md` with 10 measured cases, 60-point average signal drop, 10/10 rewrites below 50%, 0/10 worse selected rewrites, 2/10 customer-usable passes, and 8 fact-preservation or unsupported-addition failures.
- Limitations: Exact provider cost was not calculated from billing dashboards. The run was smoke only, not focused or full. No deploy or remote production smoke test was run.

### 2026-05-21 - resilience-test-generation - DeepSeek smoke failure handling

- Agent: Codex
- Trigger: The live smoke hit provider timeout and empty-content failures before completing, then exposed eval harness input-loss defects and no-charge quality failures.
- Action: Followed the resilience workflow; isolated provider health with a minimal DeepSeek request, diagnosed case-level timeout boundaries, added tests for DeepSeek thinking-mode request bodies, fixed the eval harness to preserve context fields, and reran smoke with bounded timeouts.
- Output artifacts: `tests/unit/rewrite-pipeline-model.test.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; `lib/rewrite-pipeline/model.ts`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Minimal DeepSeek health check returned status 200; `extractFacts` diagnostic completed in 14.4 seconds after disabling thinking; final smoke completed 10/10 case boundaries and wrote results. `npm run lint`, `npm run typecheck`, and `npm run test` passed after fixes.
- Limitations: The smoke still fails quality criteria at 2/10 customer-usable. Provider failure tests remain local/fake-based except for this live smoke. No retry/resume support was added to the eval script.

### 2026-05-21 - system-spec-synthesis - Deterministic rewrite gate architecture review

- Agent: Codex
- Trigger: User asked whether the current rewrite architecture has deterministic gates and restrictions similar to an Agent Governance policy gate.
- Action: Opened and followed the skill at discussion scope; reviewed the current rewrite pipeline, quality gates, policy/intent gate, budget manager, validation, quota, observability, and active rewrite strategy docs.
- Output artifacts: Review delivered in the Codex thread; `docs/skill-run-log.md`.
- Verification evidence: Static review confirmed implemented gates in `lib/rewrite-pipeline/pipeline.ts`, `lib/rewrite-pipeline/checks.ts`, `lib/rewrite-pipeline/policy-intent-gate.ts`, `lib/rewrite-pipeline/fact-ledger.ts`, `lib/rewrite-pipeline/budget-manager.ts`, `app/api/rewrite/route.ts`, `lib/quota.ts`, and `lib/observability/rewrite-telemetry.ts`.
- Limitations: No formal spec file, code change, automated test run, live provider call, or deployment was performed in this turn.

### 2026-05-21 - state-machine-modeling - Deterministic rewrite gate lifecycle review

- Agent: Codex
- Trigger: The question maps rewrite attempts, gate failures, retries, budget exhaustion, success, and quality-failure/no-charge behavior to a multi-step lifecycle.
- Action: Opened and followed the skill at review scope; modeled the existing rewrite path as an attempt lifecycle with deterministic preflight, candidate generation, review, fact/structure/policy/naturalness gates, bounded repair/escalation, fallback, success, or terminal quality failure.
- Output artifacts: Review delivered in the Codex thread; `docs/skill-run-log.md`.
- Verification evidence: Static review confirmed an in-memory attempt ledger, hard attempt cap helper, budget approval checks, failure-kind routing, quality-failure terminal path, and no-charge API response.
- Limitations: The attempt ledger is not persisted as a state table, no transition helper was added, and no lifecycle regression tests were run in this turn.

### 2026-05-21 - system-spec-synthesis - Deterministic rewrite gate fixes

- Agent: Codex
- Trigger: User approved starting fixes for the deterministic rewrite gate review findings, including confirmed input limits, context-field contract alignment, and policy gate auditability.
- Action: Opened and followed the skill at implementation scope; converted the review into focused changes across validation, workspace UI, and policy/intent gate metadata.
- Output artifacts: `lib/rewrite-limits.ts`; `lib/validation.ts`; `components/app/rewrite-workspace.tsx`; `lib/rewrite-pipeline/policy-intent-gate.ts`; `lib/rewrite-pipeline/types.ts`; related unit tests.
- Verification evidence: Added failing tests first, then made them pass. Final verification: `npm test` passed with 32 files and 194 tests; `npm run typecheck` passed; `npm run lint` passed.
- Limitations: This did not implement OpenTelemetry/SIEM export or a persisted rewrite-attempt state table.

### 2026-05-21 - ui-browser-testing - Rewrite workspace input contract check

- Agent: Codex
- Trigger: The fix changed the `/app` rewrite workspace form fields, counters, submit payload, and responsive layout.
- Action: Opened and followed the skill; ran focused UI source tests, started the local Next dev server, rendered a temporary local preview route for the authenticated workspace component, checked desktop and mobile screenshots, then deleted the temporary route.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `tests/unit/workspace-copy.test.ts`; temporary preview route was removed before completion.
- Verification evidence: Playwright browser check returned HTTP 200 on desktop and mobile, labels were `Context or message`, `Draft to rewrite`, `Audience`, `Purpose`, `What actually happened`, and `Facts to preserve`; desktop and mobile had no horizontal overflow and no console/page errors. `npm run test:e2e -- tests/e2e/auth-gate.spec.ts` passed with 2/2 tests.
- Limitations: The production `/app` route remains auth-gated, so the visual check used a temporary local preview route with the real `RewriteWorkspace` component and dummy props.

### 2026-05-21 - resilience-test-generation - Rewrite gate fix routing check

- Agent: Codex
- Trigger: The initial repair discussion mentioned quota/no-charge and provider-gate concerns, so the resilience skill was opened to check whether failure-mode test guidance applied.
- Action: Opened for routing; final implementation scope stayed on deterministic input limits, UI contract, and policy metadata, without changing retries, provider failures, quota races, idempotency, webhook replay, or recovery behavior.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Existing quota/no-charge behavior was not changed; full unit suite still passed with 194 tests, including quota and rewrite quality-failure tests.
- Limitations: No new resilience matrix, provider timeout fake, Sapling unavailable test, concurrency test, or quota race test was added because those failure modes were out of scope for this focused fix.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek smoke failure strategy

- Agent: Codex
- Trigger: User asked to think through the current live DeepSeek smoke failures and modification strategy after a provider-backed test window.
- Action: Followed the cost gate at analysis scope; recommended stopping at smoke, avoiding focused/full provider spend, and improving local ledger/gate regressions before more live eval calls.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Reviewed `docs/scenario-evaluation-results.md`, which showed 10 measured smoke cases, 60-point average signal drop, 10/10 below 50% AI-like signal, but only 2/10 customer-usable passes and 8 fact-preservation failures.
- Limitations: No billing dashboard cost calculation, no new provider call, no focused/full eval, and no code change were performed in this strategy-only pass.

### 2026-05-21 - system-spec-synthesis - DeepSeek failure-to-fix strategy

- Agent: Codex
- Trigger: User asked for root-cause thinking and a modification strategy for the current rewrite-quality failures.
- Action: Followed the spec-synthesis workflow at analysis scope; converted smoke evidence into an implementation-ready fix sequence covering fact/constraint ledger, strategy routing, candidate contracts, structural gates, and regression order.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Mapped observed failures from `docs/scenario-evaluation-results.md` to concrete system changes and added a strategy-memory section with root cause, promoted strategy, and required regressions.
- Limitations: No formal implementation plan file or code patch was created in this turn.

### 2026-05-21 - state-machine-modeling - DeepSeek attempt lifecycle failure diagnosis

- Agent: Codex
- Trigger: The failure analysis involved bounded rewrite attempts, failed candidate evidence, repair transitions, terminal `fact_check_failed`, and quality-failure/no-charge behavior.
- Action: Followed the lifecycle workflow at analysis scope; identified that attempts are being spent without a strong enough transition from generic repair to facts-first reconstruct or policy/options rewrite after hard fact misses.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Smoke report showed 9/10 cases using targeted repair, 17 rejected candidate events, and 8 terminal fact failures despite Naturalness Check improvement.
- Limitations: No transition helper, persisted attempt table, or lifecycle test was added in this turn.

### 2026-05-21 - resilience-test-generation - DeepSeek smoke regression strategy

- Agent: Codex
- Trigger: The live smoke failures require regression coverage for hard fact loss, constraint handling, awkward greeting inference, repeated facts, and no-charge terminal failures.
- Action: Followed the resilience test workflow at analysis scope; proposed local regression coverage for failed smoke cases before spending on another provider-backed focused/full run.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Strategy-memory now requires regressions for failed smoke cases, weird greetings, fact dumps, and `Do not...` constraints as forbidden-claim absence checks.
- Limitations: No new test files were created and no test command was run in this turn.

### 2026-05-21 - system-spec-synthesis - DeepSeek local repair pass 1

- Agent: Codex
- Trigger: User approved the next repair pass after the 10-case DeepSeek smoke showed fact/gate failures.
- Action: Followed the spec workflow at implementation scope; converted the failure diagnosis into concrete changes across deterministic fact extraction, eval expectation parsing, structural gates, and fallback routing.
- Output artifacts: `lib/fact-extraction.ts`; `lib/rewrite-eval-cases.ts`; `lib/rewrite-pipeline/checks.ts`; `lib/openai.ts`; `scripts/eval-scenarios.ts`; related unit tests; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Added failing tests first, then made them pass. Final verification: `npm run test` passed with 32 files and 201 tests, `npm run typecheck` passed, and `npm run lint` passed.
- Limitations: This was local-only. No live DeepSeek/OpenAI/Sapling eval, focused run, full run, deployment, or remote smoke test was executed.

### 2026-05-21 - state-machine-modeling - Rewrite gate transition repair pass 1

- Agent: Codex
- Trigger: The repair changed how failed attempts are prevented from passing through structural and fact-gate states, including terminal quality-failure causes from the smoke.
- Action: Followed the lifecycle workflow at implementation scope; tightened transition conditions by making unsupported greetings, repeated fact dumps, and internal context echoes hard deterministic structure failures before a candidate can reach success.
- Output artifacts: `lib/rewrite-pipeline/checks.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Added tests for `Hi Finance`, `Hi Reopening`, repeated facts, and `the user` context echo; focused and full unit suites passed.
- Limitations: The attempt ledger remains in response/error metadata, not persisted as a database table. No new transition function was introduced.

### 2026-05-21 - resilience-test-generation - DeepSeek local regression repair pass 1

- Agent: Codex
- Trigger: The smoke failures involved provider-backed quality failures, no-charge fact-check exits, overbroad fallback routing, and brittle eval checks.
- Action: Followed the resilience test workflow; added deterministic local regressions for hard fact source selection, forbidden-claim separation, damaged-item and sales-onboarding fallback routing, status-noun greetings, repeated fact dumps, and eval expectation normalization.
- Output artifacts: `tests/unit/fact-extraction.test.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`; `tests/unit/openai-output.test.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; related implementation files.
- Verification evidence: Focused test run passed for the four changed test files, then `npm run test`, `npm run typecheck`, and `npm run lint` passed.
- Limitations: Resilience coverage is local and deterministic. No live timeout, rate-limit, provider-unavailable, or Sapling-unavailable test was run in this repair pass.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek smoke after local repair pass 1

- Agent: Codex
- Trigger: User asked to run exactly one 10-case provider-backed smoke after local rewrite-quality repairs, which creates DeepSeek and Sapling variable cost exposure.
- Action: Used the project skill fallback at `agent-skills/cloud-architecture-cost-review/SKILL.md`; kept scope to smoke only, rejected focused/full eval for this turn, kept all model roles on `deepseek-v4-pro`, used `OPENAI_BASE_URL=https://api.deepseek.com`, and stopped after 10 cases.
- Output artifacts: `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Smoke command exited 0 and wrote `docs/scenario-evaluation-results.md` with 10 measured cases, 62-point average signal drop, 10/10 below 50%, 0/10 worse selected rewrites, 4/10 customer-usable passes, and 6 fact-preservation or unsupported-addition failures.
- Limitations: Exact provider billing cost was not checked in dashboards. No focused/full eval, deploy, or remote smoke test was run. The global skill path was unavailable, so the project `agent-skills` copy was used.

### 2026-05-21 - resilience-test-generation - DeepSeek smoke after local repair pass 1

- Agent: Codex
- Trigger: User asked for the 10-case smoke and memory update after local fixes for fact-gate and fallback-routing failures.
- Action: Followed the resilience workflow at execution/review scope; ran the bounded smoke once, inspected pass/failure rows, compared failure kinds, and recorded the next regression targets.
- Output artifacts: `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Smoke improved from 2/10 to 4/10 customer-usable, while failures remained fail-closed/no-charge with `fact_check_failed`. New residual issues were identified: locked `Do not...` constraints still entering runtime facts, and unsafe `Hi Upgrade` / `Hi Original` recipient restoration.
- Limitations: This was a live smoke, not a local fake test or full regression implementation. No new code fix was applied after the smoke, by user instruction to stop after this 10-case window.

### 2026-05-21 - cloud-architecture-cost-review - GitHub push and Cloudflare deployment

- Agent: Codex
- Trigger: User asked to push the current local work to GitHub and deploy the current project online.
- Action: Used the project skill fallback at `agent-skills/cloud-architecture-cost-review/SKILL.md`; reviewed the existing Cloudflare Worker deployment route, GitHub Actions deployment workflows, project deployment notes, and confirmed this release uses current infrastructure without adding paid cloud resources.
- Output artifacts: Current git commit, GitHub push, Cloudflare deployment command, and `docs/skill-run-log.md`.
- Verification evidence: Pre-deploy checks passed: `npm run test` passed with 201 tests, `npm run typecheck` passed, `npm run lint` passed, `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed with 38 tests, and `npm run cf:build` passed.
- Limitations: Exact Cloudflare and GitHub Actions billing dashboards were not checked. Final live deployment and remote smoke results are reported in the Codex thread, not duplicated with any secret-bearing environment details.

## 2026-05-22 - Codex - data-module-review
- Trigger: User requested Prisma schema update and commit/push/PR for ApiKey and ApiKeyUsage migration.
- Action taken: Opened/followed the data-module-review skill as a fast additive-schema checklist while applying the user-provided schema patch.
- Output artifacts: prisma/schema.prisma updated with User.apiKeys, ApiKey, and ApiKeyUsage models; existing migration SQL staged.
- Verification evidence: grep counted ApiKey model declarations before commit.
- Limitations: No full Prisma validation or test suite run due to explicit fast commit/push/PR request.

### 2026-05-22 - system-spec-synthesis - M2.5-003 diagnosis clustering

- Agent: Codex
- Trigger: The task converted a loose milestone brief into an implementation-ready learning-analysis and persistence change.
- Action: Opened and followed the skill at implementation scope; identified source inputs, mapped the clustering contract to `LearningFinding` rows, and kept the change additive.
- Output artifacts: `lib/learningops/cluster.ts`; `lib/learningops.ts`; `scripts/learningops-run.ts`; `tests/unit/learningops-cluster.test.ts`; `tests/unit/learningops.test.ts`; Prisma schema and migration updates.
- Verification evidence: Red test observed first for the missing cluster module, then focused tests passed; final `npm run lint`, `npm run typecheck`, `npm run test`, and Prisma schema validation with dummy local URLs passed.
- Limitations: No provider-backed evaluation, production database migration apply, deployment, or GitHub operation was run.

### 2026-05-22 - data-module-review - M2.5-003 LearningFinding cluster fields

- Agent: Codex
- Trigger: The task changed Prisma schema, migration SQL, and the LearningOps insert path for persisted findings.
- Action: Opened and followed the skill; ran the data-risk scanner, reviewed owned tables and the mutating script, and used nullable additive columns plus indexes for migration safety.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260522123000_add_learning_finding_cluster_fields/migration.sql`; `scripts/learningops-run.ts`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed; Prisma schema validation with dummy local URLs passed; lint, typecheck, and unit tests passed.
- Limitations: The migration was not applied to a live database in this turn, and no raw learning sample text was inspected or logged.

### 2026-05-22 - data-module-review - M2.5-004 StrategyCandidate structured patches

- Agent: Codex
- Trigger: The task changed LearningOps persistence by adding structured prompt/strategy patch metadata to `StrategyCandidate` rows.
- Action: Opened and followed the skill; reviewed the owned tables and `scripts/learningops-run.ts` mutator, ran the data-risk scanner, and used nullable additive columns plus an index for migration safety.
- Output artifacts: `lib/learningops/candidates.ts`; `lib/learningops.ts`; `scripts/learningops-run.ts`; `prisma/schema.prisma`; `prisma/migrations/20260522124500_add_strategy_candidate_structured_patch_fields/migration.sql`; `tests/unit/learningops-candidates.test.ts`; `tests/unit/learningops.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red focused tests failed for the missing candidate module and missing promotion; after implementation, focused tests passed. Final `npm run lint`, `npm run typecheck`, `npm run test`, banned-term scan, and Prisma schema validation with dummy local URLs passed.
- Limitations: The migration was not applied to a live database. No provider-backed rewrite evaluation, deployment, or GitHub operation was run.

### 2026-05-22 - system-spec-synthesis - M2.5-005 promotion handoff

- Agent: Codex
- Trigger: The task converted a roadmap stub for auto-drafting PRs from promotable `StrategyCandidate` rows into an implementation-ready LearningOps handoff contract.
- Action: Opened and followed the skill at implementation scope; mapped source facts from `plans/current-task.md`, the M2.5 roadmap, existing LearningOps modules, and the runbook into a pure promotion-brief builder and daily-run handoff file.
- Output artifacts: `lib/learningops/promotion-brief.ts`; `scripts/learningops-run.ts`; `tests/unit/learningops-promotion-brief.test.ts`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test first failed on the missing promotion-brief module. After implementation, focused LearningOps tests passed, then `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No GitHub, git, Codex MCP, deployment, provider-backed evaluation, or live database run was executed in this turn.

### 2026-05-22 - resilience-test-generation - M2.5-002 incremental eval

- Agent: Codex
- Trigger: The task changed timeout recovery, provider-failure handling, resumable checkpoints, and per-case persistence for the provider-backed eval script.
- Action: Opened and followed the skill; kept provider calls out of unit tests, added parser-only Vitest coverage first, and implemented per-case append plus atomic progress writes so completed work survives later interruption.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `tests/fixtures/learning-corpus-mini.md`; `plans/issues/M2.5-002.md`; `plans/codex-implementation-prompt.md`; `plans/decisions-log.md`.
- Verification evidence: Focused parser test failed before implementation because the parser export was missing and importing the script ran legacy side effects; after refactor, `npm run test -- --run tests/unit/eval-scenarios-corpus.test.ts` passed.
- Limitations: No real 100-case provider evaluation, deployment, or production data access was run.

### 2026-05-22 - cloud-architecture-cost-review - M2.5-007 Cloudflare Cron LearningOps

- Agent: Codex
- Trigger: The task added a scheduled Cloudflare Worker execution path for daily LearningOps.
- Action: Opened and followed the skill; reviewed `docs/manual-setup.md`, `docs/next-development-brief.md`, existing Cloudflare/OpenNext config, and the LearningOps runbook. Selected the existing Worker Cron Trigger instead of a new Worker, Azure timer, queue, or always-on service.
- Output artifacts: `worker.js`; `wrangler.jsonc`; `lib/learningops/scheduled.ts`; `docs/learningops-runbook.md`; `docs/skill-run-log.md`.
- Verification evidence: `npx wrangler deploy --dry-run --outdir /private/tmp/learningops-worker-dry-run` completed with exit 0 and did not deploy. Required validation also passed with `npm run lint`, `npm run typecheck`, and `npm run test`.
- Limitations: Exact Cloudflare billing dashboards were not checked. The dry run emitted a nonfatal local Wrangler log-file permission warning and no production deployment was run.

### 2026-05-22 - system-spec-synthesis - M2.5-007 scheduled LearningOps contract

- Agent: Codex
- Trigger: The task converted the M2.5-007 roadmap stub into an implementation-ready scheduled job contract.
- Action: Opened and followed the skill at implementation scope; mapped source facts from `plans/current-task.md`, the M2.5 roadmap, `docs/next-development-brief.md`, existing LearningOps analyzer/candidate modules, and the promotion handoff into a shared pipeline.
- Output artifacts: `lib/learningops/run.ts`; `lib/learningops/scheduled.ts`; `scripts/learningops-run.ts`; `worker.js`; `tests/unit/learningops-run.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test first failed on the missing `lib/learningops/run` module. After implementation, focused LearningOps tests passed, then `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No live production database run, GitHub operation, Codex MCP PR drafting session, or deployment was executed.

### 2026-05-22 - state-machine-modeling - M2.5-007 LearningRun statuses

- Agent: Codex
- Trigger: The task changed the `LearningRun` lifecycle by requiring terminal statuses `digest_only`, `docs_only`, `promoted`, and `blocked`.
- Action: Opened and followed the skill; modeled the scheduled run as start-blocked-for-safety, analyze recent samples, persist findings/candidates, then transition to the analysis outcome or back to `blocked` on failure.
- Output artifacts: `lib/learningops.ts`; `lib/learningops/run.ts`; `tests/unit/learningops.test.ts`; `tests/unit/learningops-run.test.ts`; `docs/learningops-runbook.md`; `docs/skill-run-log.md`.
- Verification evidence: Unit tests cover `digest_only`, `docs_only`, `promoted`, and failure-to-`blocked`; focused and full Vitest suites passed.
- Limitations: There is no database enum or check constraint for statuses yet; this remains a typed application invariant over the existing text column.

### 2026-05-22 - data-module-review - M2.5-007 LearningOps persistence

- Agent: Codex
- Trigger: The task changed database writes for `LearningRun`, `LearningFinding`, and `StrategyCandidate` from a CLI-only insert path to a shared scheduled pipeline.
- Action: Opened and followed the skill; reviewed `prisma/schema.prisma`, existing LearningOps migrations, and mutating code, then kept the change schema-compatible by reusing existing columns and indexes.
- Output artifacts: `lib/learningops/run.ts`; `scripts/learningops-run.ts`; `tests/unit/learningops-run.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed; focused LearningOps tests and full `npm run test` passed.
- Limitations: The scanner reported many existing quota/idempotency signals outside this change. No live migration or production database write was run.

### 2026-05-22 - resilience-test-generation - M2.5-007 scheduled LearningOps failure handling

- Agent: Codex
- Trigger: The task introduced scheduled background execution and needed deterministic behavior when sample reads or persistence fail.
- Action: Opened and followed the skill; added a local fake SQL test that forces sample-read failure after the run row is created and asserts the run is marked `blocked`.
- Output artifacts: `lib/learningops/run.ts`; `tests/unit/learningops-run.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test failed before the runner existed; after implementation, `tests/unit/learningops-run.test.ts` passed and the full Vitest suite passed.
- Limitations: No live Cloudflare scheduled invocation, production database outage simulation, or external provider failure was run.

### 2026-05-22 - state-machine-modeling - M2.5-008 StrategyCandidate review statuses

- Agent: Codex
- Trigger: The task added admin approval decisions for the persisted `StrategyCandidate.status` lifecycle.
- Action: Opened and followed the skill; modeled admin review as `proposed` to one of `approved`, `needs_revision`, or `rejected`, with later admin correction allowed between review states and invalid statuses rejected before DB writes.
- Output artifacts: `lib/admin/learning.ts`; `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test first failed on the missing admin learning module; after implementation, `tests/unit/admin-learning.test.ts`, `npm run typecheck`, `npm run lint`, and the full `npm run test` suite passed.
- Limitations: The database column remains free text; allowed review states are enforced in the admin mutation helper rather than a DB enum or check constraint.

### 2026-05-22 - data-module-review - M2.5-008 StrategyCandidate admin mutation

- Agent: Codex
- Trigger: The task added an admin UI action that updates `StrategyCandidate.status` in the database.
- Action: Opened and followed the skill; reviewed the LearningOps tables, existing raw SQL patterns, and the new mutation path. Kept the change schema-compatible, scoped the update by candidate id, and added a focused fake-SQL unit test for the update statement.
- Output artifacts: `lib/admin/learning.ts`; `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed and reported existing broad quota/idempotency signals outside this change. Focused and full Vitest suites passed.
- Limitations: No live production database write was run.

### 2026-05-22 - ui-browser-testing - M2.5-008 admin learning page

- Agent: Codex
- Trigger: The task added a frontend admin page and form controls under `/admin/learning`.
- Action: Opened and followed the skill; identified the admin review flow and implemented a dynamic page with recent runs, finding clusters, evidence refs, candidate details, linked work, and approval controls.
- Output artifacts: `app/admin/learning/page.tsx`; `components/admin/admin-shell.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run typecheck`, `npm run lint`, and full `npm run test` passed. Local browser verification was attempted with `npm run dev -- -p 3010` and `npm run dev -- -H 127.0.0.1 -p 3010`, but the sandbox rejected both bind attempts with `EPERM`.
- Limitations: No authenticated browser screenshot was captured because the local dev server could not bind in this sandbox, and no production admin session or live database was used.

### 2026-05-22 - cloud-architecture-cost-review - M2.5-009 canary rollout

- Agent: Codex
- Trigger: The task chose a runtime feature-flag shape for production rewrite strategy canaries and could have introduced Cloudflare KV or another paid runtime dependency.
- Action: Opened and followed the skill; selected environment-gated routing plus existing `RewriteCostLog` telemetry over a new KV namespace or service. The runtime pauses or ramps canary traffic from database metrics without creating paid infrastructure.
- Output artifacts: `lib/rewrite-pipeline/canary.ts`; `app/api/rewrite/route.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused canary tests passed, then `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No exact Cloudflare pricing lookup was needed because no new paid resource was selected, and no production flag was enabled.

### 2026-05-22 - system-spec-synthesis - M2.5-009 canary contract

- Agent: Codex
- Trigger: The task converted a roadmap stub into an implementation-ready rollout contract for promoted rewrite strategies.
- Action: Opened and followed the skill at implementation scope; mapped the source facts into a control/canary strategy-version contract, deterministic assignment, signal-distribution comparison, rollout states, and validation plan.
- Output artifacts: `lib/rewrite-pipeline/canary.ts`; `lib/rewrite-pipeline/config.ts`; `lib/rewrite-pipeline/types.ts`; `app/api/rewrite/route.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test failed first because `lib/rewrite-pipeline/canary.ts` did not exist. After implementation, focused tests, Prisma schema validation, `npm run lint`, `npm run typecheck`, and full `npm run test` passed.
- Limitations: The specific future promoted strategy behavior still has to be implemented in code and labeled with `REWRITE_STRATEGY_CANARY_VERSION` during that release.

### 2026-05-22 - state-machine-modeling - M2.5-009 canary lifecycle

- Agent: Codex
- Trigger: The task introduced a multi-step deployment lifecycle for rewrite strategy canaries.
- Action: Opened and followed the skill; modeled canary state as `off`, `monitoring`, `paused`, `ramping`, and `complete`, with transitions driven by feature flag state, sample maturity, average signal-drop comparison, and ramp thresholds.
- Output artifacts: `lib/rewrite-pipeline/canary.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Unit tests cover default 10% routing, stable assignment, worse-result pause, and better-result ramp. Full lint, typecheck, and Vitest validation passed.
- Limitations: The lifecycle is enforced in typed application code rather than a persisted enum because no new state table was added.

### 2026-05-22 - data-module-review - M2.5-009 canary telemetry query

- Agent: Codex
- Trigger: The task reads production `RewriteCostLog` signal telemetry on the rewrite request path and adds a supporting index.
- Action: Opened and followed the skill; reviewed `RewriteCostLog` schema and raw SQL patterns, kept the rollout on existing telemetry rows, and added an additive `strategyVersion, createdAt` index for the canary comparison query.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260522132500_add_rewrite_cost_log_strategy_version_index/migration.sql`; `lib/rewrite-pipeline/canary.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed with existing broad quota/idempotency findings outside this change. `npx prisma validate` passed with dummy local database URLs, and full lint, typecheck, and Vitest validation passed.
- Limitations: The migration was not applied to a live database in this turn, and no production telemetry query was run.

### 2026-05-22 - system-spec-synthesis - M2.5-005 status verification

- Agent: Codex
- Trigger: The task required converting the sparse M2.5-005 roadmap stub into a concrete implementation decision under the no-git/no-gh Codex protocol.
- Action: Opened and followed the skill at verification scope; checked the existing LearningOps promotion handoff contract, task builder, run pipeline integration, regression tests, and rewrite strategy memory note against `plans/current-task.md`.
- Output artifacts: `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `npm run lint`, `npm run typecheck`, `npm run test`, and the scoped banned-term scan over `app components public lib` passed.
- Limitations: No GitHub, git, Codex MCP, deployment, provider-backed evaluation, or live database run was executed; the implementation intentionally stops at draft promotion-task preparation in this environment.

### 2026-05-22 - state-machine-modeling - M2.5-008 PR link label follow-up

- Agent: Codex
- Trigger: The task still touched the persisted `StrategyCandidate.status` review lifecycle while tightening the admin candidate review display.
- Action: Opened and followed the skill; confirmed the state model remains unchanged. States: `proposed`, `approved`, `needs_revision`, `rejected`, plus existing historical display states. Events: admin review submit for `approved`, `needs_revision`, or `rejected`. Allowed transitions: any current candidate display state can be set to one of the three admin review states by an admin server action. Illegal transitions: blank candidate id or unsupported status is rejected before SQL writes. Invariants: only admin identities can mutate review status, the update is scoped to one candidate id, and display-only linked-work labels do not alter lifecycle state.
- Output artifacts: `lib/admin/learning.ts`; `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The focused admin-learning test was red for missing PR label metadata, then passed after implementation. `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: The database column remains free text; no live admin mutation was run.

### 2026-05-22 - data-module-review - M2.5-008 linked work metadata

- Agent: Codex
- Trigger: The task reads persisted `StrategyCandidate.linkedCommitHash` values into the admin LearningOps review UI.
- Action: Opened and followed the skill; reviewed `prisma/schema.prisma`, `lib/admin/learning.ts`, and the admin unit tests. Findings: no new migration needed; the change derives a display label from the existing nullable linked-work field and does not widen the mutation query. Open questions: none for this scope. Suggested tests: cover GitHub pull-request URL labeling and keep the existing scoped status-update SQL test.
- Output artifacts: `lib/admin/learning.ts`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed and reported existing broad persistence signals outside this change. Focused and full Vitest suites passed.
- Limitations: No production database rows were read or updated.

### 2026-05-22 - ui-browser-testing - M2.5-008 admin linked work label

- Agent: Codex
- Trigger: The task changed visible copy in the `/admin/learning` candidate review panel.
- Action: Opened and followed the skill; chose a focused unit-level UI data contract test because the admin page is auth-gated and data-backed. Attempted local browser verification by starting `npm run dev -- -H 127.0.0.1 -p 3010`.
- Output artifacts: `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The dev server bind attempt failed with `EPERM` before a browser page could load. `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No authenticated browser screenshot was captured in this sandbox; the PR-label behavior is covered by `tests/unit/admin-learning.test.ts`.

### 2026-05-22 - system-spec-synthesis - commercialization north star

- Agent: Codex
- Trigger: The user asked where to store the final commercial goal and how Claude's scheduled monitor and Codex's execution loop should coordinate without losing context.
- Action: Opened and followed the skill; converted the loose product and automation requirements into a durable north-star spec with goals, non-goals, current system, operating architecture, contracts, state handling, security rules, rollout, verification, and open questions.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/overnight-supervisor.sh`; `plans/codex-implementation-prompt.md`; `plans/overnight-directive.md`; `plans/commercialization-roadmap.md`; `docs/skill-run-log.md`.
- Verification evidence: Documentation diff was reviewed and `git diff --check` passed.
- Limitations: This turn did not run product tests because the change is documentation and supervisor guidance only; the active overnight loop was left running in the original worktree.

### 2026-05-22 - cloud-architecture-cost-review - M2.5-010 canary rollback

- Agent: Codex
- Trigger: The task changed a Cloudflare scheduled LearningOps job and could have introduced a new paid alerting or runtime-state service.
- Action: Opened and followed the skill; kept the rollout on the existing Cloudflare scheduled handler and Neon database, added no KV namespace, queue, always-on worker, or new deployed service, and made Resend/GitHub outbound calls optional via environment configuration.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `lib/learningops/scheduled.ts`; `scripts/learningops-run.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: `npx prisma validate`, `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No exact provider pricing lookup was needed because no new paid infrastructure was selected and no live alert or GitHub API call was made.

### 2026-05-22 - system-spec-synthesis - M2.5-010 rollback contract

- Agent: Codex
- Trigger: The task converted a roadmap stub into a concrete job/data contract for post-promotion strategy rollback.
- Action: Opened and followed the skill at implementation scope; mapped the source requirement into a 50-rewrite rolling-window signal monitor, persisted rollback override, request-time traffic-off decision, admin email alert, GitHub follow-up issue hook, and verification plan.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `lib/rewrite-pipeline/canary.ts`; `lib/learningops/scheduled.ts`; `scripts/learningops-run.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused tests first failed for the missing rollback module and missing request-time rollback override. After implementation, focused canary tests, `npx prisma validate`, `npm run lint`, `npm run typecheck`, and full `npm run test` passed.
- Limitations: The detailed issue brief did not exist yet, so the implementation uses the roadmap requirement and existing canary architecture as the contract.

### 2026-05-22 - state-machine-modeling - M2.5-010 rollback lifecycle

- Agent: Codex
- Trigger: The task changed the promoted-strategy canary lifecycle by adding automatic rollback and alert side-effect states.
- Action: Opened and followed the skill; modeled states as env-enabled `monitoring`, persisted `rollback_open`, side-effect statuses `pending/sent/skipped/failed` and `pending/opened/skipped/failed`, plus future manual `resolved`. Events are 50-write window measured, regression threshold crossed, rollback persisted, alert success/failure/skip, issue success/failure/skip, and future manual resolution.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `lib/rewrite-pipeline/canary.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Unit tests cover the 50-rewrite signal query, rollback persistence before outbound alerts, failed outbound alerts keeping rollback active, and request-time assignment forcing canary traffic to 0 for unresolved rollback rows.
- Limitations: Manual rollback resolution is represented by nullable `resolvedAt` but no admin UI for resolving rows was added in this issue.

### 2026-05-22 - data-module-review - M2.5-010 RewriteCanaryRollback persistence

- Agent: Codex
- Trigger: The task added persistent rollback state and raw SQL mutations for canary rollback.
- Action: Opened and followed the skill; reviewed `RewriteCostLog`, LearningOps scheduled SQL patterns, and migration safety. Added an additive `RewriteCanaryRollback` table with indexes and a partial unique index enforcing one unresolved rollback per canary strategy and scenario.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260522143000_add_rewrite_canary_rollback/migration.sql`; `lib/rewrite-pipeline/canary-rollback.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed and reported existing broad quota/idempotency signals outside this change. `npx prisma validate`, focused canary tests, and full Vitest passed.
- Limitations: The migration was not applied to a live database, and no production rollback rows were inserted.

### 2026-05-22 - resilience-test-generation - M2.5-010 rollback alerts

- Agent: Codex
- Trigger: The task added outbound admin email and GitHub issue side effects after rollback persistence.
- Action: Opened and followed the skill; generated a failure matrix for the canary rollback monitor, chose deterministic unit fakes, and tested the partial-success invariant that a persisted rollback remains active even if email or issue creation fails.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 /Users/qc/.codex/skills/resilience-test-generation/scripts/resilience_matrix.py "canary rollback monitor"` produced the timeout, 5xx, 4xx, duplicate, partial-success, concurrency, and malformed-payload rows. Focused canary rollback tests and full Vitest passed.
- Limitations: No live email provider, GitHub API, production database outage, or concurrent scheduled run was executed.

### 2026-05-22 - cloud-architecture-cost-review - Cloudflare Worker size limit

- Agent: Codex
- Trigger: Cloudflare/OpenNext deploy failed validation because the Worker package exceeded the free-plan size limit and Wrangler reported Prisma WASM artifacts in the package.
- Action: Opened and followed the skill as a deployment/cost gate; compared keeping the existing Cloudflare Worker with corrected packaging, upgrading to a paid Worker size limit, and moving runtime architecture. Selected the existing Worker path with scoped bundling and minification because it avoids new paid infrastructure and preserves the scheduled LearningOps wrapper.
- Output artifacts: `wrangler.jsonc`; `package.json`; `scripts/copy-prisma-wasm.mjs` removed; `README.md`; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm run cf:build` completed successfully. `npx opennextjs-cloudflare deploy -- --dry-run --keep-vars --metafile .open-next/wrangler-bundle-meta-fixed-final.json` completed without uploading, without the previous "Attaching additional modules" table, and reported `Total Upload: 3788.79 KiB / gzip: 1061.84 KiB`.
- Limitations: No exact Cloudflare pricing lookup was needed because the selected fix does not upgrade plans or create paid resources. No production deploy was run in this turn.

### 2026-05-22 - data-module-review - Prisma WASM deploy packaging check

- Agent: Codex
- Trigger: The deploy failure named Prisma WASM artifacts and the repo uses Prisma schema/migrations with direct Neon SQL at Worker runtime.
- Action: Opened the skill and reviewed the Prisma/runtime boundary. Confirmed `prisma/schema.prisma`, `docs/manual-setup.md`, `docs/preflight-report.md`, and `lib/db.ts` document and implement Prisma as schema/migration source of truth while Worker runtime DB access uses Neon directly. Removed the stale Prisma WASM copy step from the Cloudflare build path without changing schema, migrations, data access services, counters, indexes, transactions, or persistence invariants.
- Output artifacts: `package.json`; `scripts/copy-prisma-wasm.mjs` removed; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: `rg` found no remaining build-script references to `copy-prisma-wasm` after the script removal, and the Cloudflare build/dry-run passed with no Prisma WASM duplicate-module warnings.
- Limitations: No live database migration or production database query was run because this was a packaging-only change.

### 2026-05-22 - system-spec-synthesis - Cloudflare deploy-scope check

- Agent: Codex
- Trigger: The initial symptom could have required an architecture or deployment-flow spec if fixing the Worker size limit required moving runtime services.
- Action: Opened the skill and checked the scope against project instructions and deployment docs. No full implementation spec was produced because the selected fix stayed within existing Cloudflare/OpenNext deployment configuration and did not change APIs, jobs, data model, state lifecycle, or multi-module runtime behavior.
- Output artifacts: `README.md`; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: The dry-run deploy proved the existing Worker architecture can package under the limit after scoped config changes; no separate architecture handoff was required.
- Limitations: This was a scope check, not a standalone system specification document.

### 2026-05-22 - ui-browser-testing - M4-002 landing demo samples

- Agent: Codex
- Trigger: The task changes the public landing-page interactive demo samples and Naturalness Check values.
- Action: Opened and followed the skill; identified the `/` landing demo flow, added a focused unit regression for documented sample fixtures and salutation-name consistency, and attempted a focused Playwright browser check.
- Output artifacts: `components/landing/interactive-demo.tsx`; `components/landing/sample-cases.ts`; `tests/unit/landing-demo-samples.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused landing demo unit test passed after a red failure for the missing fixture module. `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: Playwright browser verification could not start in this sandbox because Next.js could not bind either `0.0.0.0:3000` or `127.0.0.1:3000` and returned `EPERM`.

### 2026-05-22 - system-spec-synthesis - Rewrite Quality Analysis and Codex worker handoff

- Agent: Codex
- Trigger: User asked to convert owner-facing rewrite-quality analysis requirements and Claude-to-Codex automation notes into durable project goals and implementation-ready docs.
- Action: Opened and followed the skill; used `spec_outline.py` to confirm the required spec headings; mapped the requirement into a first-version offline report, existing `RewriteCostLog`/`RewriteProviderCall` data sources, safety/privacy rules, report artifacts, verification checks, and a sanitized monitor-to-Codex worker queue.
- Output artifacts: `docs/rewrite-quality-analysis-spec.md`; `docs/commercialization-north-star.md`; `plans/commercialization-roadmap.md`; `plans/issue-board.md`; `plans/issue-manifest.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: Documentation and queue contracts reference existing Prisma telemetry fields and keep the first version offline rather than adding new infrastructure or an admin UI.
- Limitations: This turn added the implementation target and handoff contract. The Python/Pandas report script and real production report artifacts are still assigned to M5 follow-up issues.

### 2026-05-22 - data-module-review - M5 telemetry persistence rescue

- Agent: Codex
- Trigger: Rescuing M5-002 changed `RewriteCostLog`/`RewriteProviderCall` persistence and introduced transaction-based provider-call replacement.
- Action: Opened and followed the skill; ran `scan_data_risks.py --limit 80`; reviewed `RewriteCostLog.requestId` uniqueness, `RewriteProviderCall.costLogId` foreign key behavior, transaction order, and retry/idempotency behavior.
- Output artifacts: `lib/observability/rewrite-telemetry.ts`; `tests/unit/rewrite-telemetry.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The review caught a retry edge case where an upserted request log could keep the old primary key while replacement provider calls used the new log id. The fix updates the cost-log id on request-id conflict before deleting and reinserting provider calls; `RewriteProviderCall_costLogId_fkey` uses `ON UPDATE CASCADE`. Focused telemetry tests, full Vitest, lint, typecheck, and `npm run cf:build` passed.
- Limitations: The transaction was verified with deterministic unit mocks and schema inspection, not against a live production database.

### 2026-05-22 - resilience-test-generation - Provider telemetry failure coverage

- Agent: Codex
- Trigger: M5-002 records AI provider and writing-signal provider success/failure telemetry, including malformed output, timeouts, and unavailable signals.
- Action: Opened and followed the skill; generated a failure matrix for rewrite provider telemetry and cost logging; checked deterministic tests for malformed model output, provider-call success/failure recording, Sapling metadata, and transactional persistence.
- Output artifacts: `tests/unit/openai-output.test.ts`; `tests/unit/rewrite-pipeline-model.test.ts`; `tests/unit/rewrite-telemetry.test.ts`; `tests/unit/writing-signal.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `resilience_matrix.py "rewrite provider telemetry and cost logging"` produced timeout, transient 5xx, permanent 4xx, duplicate, partial-success, concurrent, and malformed-payload rows. Focused provider telemetry tests passed with 52/52 tests; full Vitest passed with 253/253 tests.
- Limitations: Tests use local fakes and do not hit live OpenAI/DeepSeek, Sapling, Neon, Stripe, or Cloudflare.

### 2026-05-22 - state-machine-modeling - M5-004 rewrite failure reason telemetry

- Agent: Codex
- Trigger: The task changed request-level rewrite failure lifecycle labels persisted for analysis.
- Action: Opened and followed the skill; modeled `RewriteCostLog.status` states as `success`, `quality_failed`, and `server_failed`, with terminal request-level `errorCode` reasons of `signal_unavailable`, `naturalness_gate_failed`, `fact_check_failed`, `reviewer_threshold_failed`, `server_failed`, or provider-specific codes. Events are successful rewrite, quality gate rejection, provider signal unavailability, reviewer rejection not rescued by fallback, and unexpected server exception.
- Output artifacts: `lib/rewrite-failure-reasons.ts`; `lib/observability/rewrite-telemetry.ts`; `lib/rewrite-learning.ts`; `lib/rewrite-pipeline/pipeline.ts`; `app/api/rewrite/route.ts`; `tests/unit/rewrite-telemetry.test.ts`; `tests/unit/rewrite-learning.test.ts`; `tests/unit/rewrite-pipeline.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red tests first failed for coarse `quality_gate_failed`, hidden `signal_unavailable`, generic `TypeError`, and unreachable `reviewer_threshold_failed`. After implementation, `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No database migration or live telemetry replay was run; existing rows with older coarse labels are not backfilled by this issue.

### 2026-05-22 - data-module-review - M5-004 failure reason persistence

- Agent: Codex
- Trigger: The task changed persisted `errorCode` values in `RewriteCostLog` and `RewriteLearningSample`.
- Action: Opened and followed the skill; reviewed the write paths for request cost logs and learning samples, then added a shared normalizer so quality failures store specific machine-readable reasons, provider-specific call details stay on `RewriteProviderCall.errorCode`, and generic server exceptions persist as `server_failed`.
- Output artifacts: `lib/rewrite-failure-reasons.ts`; `lib/observability/rewrite-telemetry.ts`; `lib/rewrite-learning.ts`; `app/api/rewrite/route.ts`; `tests/unit/rewrite-telemetry.test.ts`; `tests/unit/rewrite-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `scan_data_risks.py --limit 80` completed and reported existing broad quota/idempotency/wall-clock signals outside this scoped change. Focused red/green tests, `npm run lint`, `npm run typecheck`, `npm run test`, and the scoped banned-term scan passed.
- Limitations: No schema or migration change was required, and no production database rows were inspected or updated.

### 2026-05-22 - system-spec-synthesis - Agent monitor and repair queue contract

- Agent: Codex
- Trigger: The owner identified a design flaw where Claude monitor progress output and Codex repair work were not cleanly separated.
- Action: Opened and followed the skill; converted the loose automation workflow into a concrete contract that separates human progress checkpoints from machine repair queue items, assigns ownership across Claude monitor, shell loop, Codex worker, and human owner, and updates the repo source-of-truth docs.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`; Claude scheduled task `replyinmyvoice-loop-monitor` updated through its local hardlink.
- Verification evidence: Local checks confirmed the Claude scheduled task file and accessible hardlink share the updated size, link count, and mtime; `rg` checks in the isolated clone confirmed the new contract keeps `overnight-progress.md` as human reporting and `codex-worker-inbox.md` as the machine repair queue.
- Limitations: The repo docs were updated in an isolated clone/branch to avoid racing the active overnight loop. The live Claude task file path under `Documents/Claude/Scheduled` remains unreadable through shell because of macOS permissions, but its same-inode accessible hardlink was updated and stat confirmed the scheduled path changed.

### 2026-05-22 - state-machine-modeling - Agent handoff lifecycle

- Agent: Codex
- Trigger: The task changed a multi-step automation lifecycle: monitor observation, safe restart, blocker classification, queueing, repair, and owner-only escalation.
- Action: Opened and followed the skill; modeled the lifecycle as `running`, `stalled`, `repair_queued`, `repairing`, `needs_user`, `money_made`, and `stopped`, with explicit invariants that progress reports are not task queues and Codex worker consumes at most one pending inbox item per run.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The updated docs define the separate progress/inbox channels, allowed restart behavior, owner-only blockers, and worker non-racing rules.
- Limitations: No automated transition test was added because this change is an operational-doc and automation-prompt contract, not application runtime code.

### 2026-05-22 - system-spec-synthesis - Main-loop repair inbox orchestration

- Agent: Codex
- Trigger: The owner identified that a 30-minute Claude monitor plus a separate hourly Codex worker was logically mismatched and asked to implement the better design.
- Action: Opened and followed the skill; converted the workflow into a single-executor contract where the shell loop consumes `plans/codex-worker-inbox.md` before issue-board work, while the scheduled Codex automation becomes a dead-man watchdog only.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`; Codex scheduled automation `replyinmyvoice-codex-worker`; Claude scheduled task `replyinmyvoice-loop-monitor` updated through its local hardlink.
- Verification evidence: Added a focused Vitest regression for repair-inbox ordering and non-user failure queueing; `bash -n plans/overnight-supervisor.sh`, focused Vitest, `npm run lint`, `npm run typecheck`, and `git diff --check` passed in the isolated branch.
- Limitations: The change updates the local supervisor and automation contract. It does not change Claude's schedule cadence because Claude already writes progress and repair queue items; the Claude scheduled task text was only aligned to say the shell loop consumes the queue.

### 2026-05-22 - state-machine-modeling - Main-loop repair lifecycle

- Agent: Codex
- Trigger: The orchestration change moved repair handling from a separate hourly worker into the primary shell loop, changing queue ownership and transition timing.
- Action: Opened and followed the skill; modeled repair states as `pending -> in_progress -> done | not_actionable | waiting_user`, with the invariant that owner-only blockers never enter autonomous repair and a healthy shell loop is the only normal repair executor.
- Output artifacts: `plans/overnight-supervisor.sh`; `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The supervisor now checks the repair inbox before `find_next_pending_issue`, queues non-user Codex/GitHub/CI failures into the inbox, and the focused test covers the ordering and queueing contract.
- Limitations: The test validates script structure rather than executing real GitHub PR/CI repair flow, to avoid live PR churn during the orchestration edit.

### 2026-05-22 - cloud-architecture-cost-review - M6-001 Worker secret-name diff

- Agent: Codex
- Trigger: M6-001 reviews Cloudflare Worker production secret configuration for `replyinmyvoice-app`.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Selected a read-only `wrangler secret list` name comparison; rejected secret pushes, deploys, dashboard mutation, and paid-resource changes for this issue.
- Output artifacts: `plans/worker-secret-diff.md`; `docs/skill-run-log.md`.
- Verification evidence: `npx wrangler secret list --name replyinmyvoice-app --format json` was attempted and failed before returning names because the sandbox could not resolve Cloudflare API hostnames. `.env.local` was parsed for names only and was not modified.
- Limitations: The live Worker secret list was unavailable, so `present-in-both`, `missing-in-worker`, and `missing-in-local` remain uncomputed until a networked authenticated shell reruns the command.

### 2026-05-22 - cloud-architecture-cost-review - M6-002 Worker secret push

- Agent: Codex
- Trigger: M6-002 would mutate Cloudflare Worker production secrets for `replyinmyvoice-app`.
- Action: Opened and followed the skill as a cloud/deployment cost and approval gate; compared the requested `wrangler secret put` path with the prerequisite M6-001 diff, read-only Wrangler verification, and dashboard/deploy alternatives. Selected no mutation because the missing-secret list is unavailable and Cloudflare API DNS resolution fails in this sandbox.
- Output artifacts: `plans/worker-secret-diff.md`; `plans/issue-board.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `npx --no-install wrangler secret list --name replyinmyvoice-app --format json` failed before returning Worker metadata because the sandbox could not resolve Cloudflare API hostnames. No `wrangler secret put` command was run.
- Limitations: No production Worker secrets were pushed. A networked, authenticated shell must rerun the M6-001 diff first, then push only names listed under `missing-in-worker` without printing values.

### 2026-05-22 - ui-browser-testing - M4-014 handoff recommendation

- Agent: Codex
- Trigger: The current active issue is M4-014 app workspace visual polish, which changes browser-visible `/app` UI and requires desktop/mobile verification.
- Action: Opened the project `ui-browser-testing` skill and used it to shape the recommendation for the next autonomous run: inspect current dirty state first, complete the app workspace polish, then verify with focused UI/browser checks before PR.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: No product verification was run in this turn; this was a handoff/status recommendation only.
- Limitations: Did not inspect or modify M4-014 implementation files, run Playwright, start the dev server, capture screenshots, or create a PR.

### 2026-05-22 - web-design-engineer - M4-014 handoff recommendation

- Agent: Codex
- Trigger: The current active issue is M4-014 app workspace visual polish, a browser-visible app UI design task.
- Action: Opened the `web-design-engineer` skill and used it to shape the recommendation that the next run should continue from existing design tokens and repo patterns, avoid broad redesign scope, and finish with visual/browser evidence.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: No design implementation or critique scoring was performed in this turn; this was a handoff/status recommendation only.
- Limitations: Did not inspect current UI screenshots, change design code, or run final visual QA.

### 2026-05-22 - web-design-engineer - Frontend redesign issue scoping

- Agent: Codex
- Trigger: The owner installed `web-design-engineer` and asked to add an issue that uses it to redesign the currently weak frontend.
- Action: Opened and followed the skill; read the design-direction and critique references; scoped a high-priority M4 issue that requires current-state critique, design declaration, implementation, final five-dimension scoring, and browser verification.
- Output artifacts: `plans/issues/M4-011.md`; `plans/issue-board.md`; `plans/issue-manifest.md`; GitHub issue `https://github.com/ChuanQiao1128/replyinmyvoice/issues/196`; `docs/skill-run-log.md`.
- Verification evidence: The issue explicitly requires `/Users/qc/.codex/skills/web-design-engineer/SKILL.md`, `ui-browser-testing` routing where applicable, final average design score >= 8.0 with no dimension below 7.0, and desktop/mobile browser checks.
- Limitations: This turn added the queued design work; it did not implement the frontend redesign itself.

### 2026-05-22 - cloud-architecture-cost-review - M6-003 Worker preview smoke

- Agent: Codex
- Trigger: M6-003 reviews the Cloudflare Workers `workers.dev` deployment for launch verification.
- Action: Opened and followed the skill as a read-only cloud/deployment check. Selected a direct route smoke against the existing `replyinmyvoice-app` Worker URL; rejected deploys, DNS changes, secret changes, and paid-resource creation for this issue.
- Output artifacts: `docs/preflight-report.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `curl` and Node `fetch` smoke attempts were blocked before reaching Cloudflare because this sandbox could not resolve DNS for the Worker host, `cloudflare.com`, or `example.com`.
- Limitations: No remote route status was observed. A networked shell must rerun the documented M6-003 `curl` checks.

### 2026-05-22 - ui-browser-testing - M6-003 route smoke workflow

- Agent: Codex
- Trigger: M6-003 is a browser-visible and API route smoke test for `/`, `/pricing`, `/sign-in`, `/app`, `/api/rewrite`, `/api/stripe/webhook`, and `/api/health/db`.
- Action: Opened and followed the skill to identify the user-visible flow and expected route outcomes. Used focused HTTP route checks rather than screenshots because the issue acceptance criteria are status-code based.
- Output artifacts: `docs/preflight-report.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: Local code review confirmed `/app` and `/api/rewrite` are protected by middleware, `GET /api/stripe/webhook` returns a health JSON response, and `GET /api/health/db` performs the DB smoke check. Remote HTTP execution was blocked by DNS failure in this sandbox.
- Limitations: No desktop/mobile screenshot or live browser rendering was captured because the task is a route-status smoke and remote DNS was unavailable.

### 2026-05-22 - system-spec-synthesis - M4-011 no-status repair

- Agent: Codex
- Trigger: The repair inbox item for M4-011 reported that Codex did not write `plans/task-status.json` during a broad frontend redesign run.
- Action: Opened and followed the skill; converted the log evidence and supervisor contract into a scoped repair specification that keeps M4-011 as an umbrella item, splits runnable frontend work into smaller follow-ups, and preserves no-status partial work before cleanup.
- Output artifacts: `plans/frontend-redesign-followups.md`; `plans/codex-implementation-prompt.md`; `plans/overnight-supervisor.sh`; `plans/issues/M4-011.md`; `plans/issue-board.md`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added focused Vitest coverage for M4-011 timebox blocking, early status preflight, and no-status work preservation. The current rescue branch also adds dirty-worktree and declared-file guards before restart.
- Limitations: This repair does not implement the frontend redesign. It records the scoped follow-ups needed before that work can safely run unattended again.

### 2026-05-22 - state-machine-modeling - M4-011 supervisor retry lifecycle

- Agent: Codex
- Trigger: The repair changes issue and supervisor lifecycle behavior after a Codex timeout with no status file.
- Action: Opened and followed the skill; modeled the relevant states as `pending`, `in_progress`, `BLOCKED-AUTONOMY`, `ready_to_commit`, and `needs_human`, with events for timeout, reclassification, and scoped follow-up creation.
- Output artifacts: `plans/frontend-redesign-followups.md`; `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The follow-up document includes state list, event list, transition table, invariants, illegal transitions, persistence implications, and test checklist. Focused Vitest covers M4-011 blocking before task handoff and no-status edit preservation before returning to main.
- Limitations: The state model covers the supervisor retry contract only; it does not alter product UI state or application runtime behavior.

### 2026-05-22 - state-machine-modeling - Supervisor runtime-ledger guard

- Agent: Codex
- Trigger: The M4-013 loop repeatedly stashed ready work because supervisor-maintained ledger files were dirty but not listed in `plans/task-status.json` `files_changed`.
- Action: Opened and followed the skill; modeled the relevant lifecycle as issue branch running -> `ready_to_commit` with declared product files -> runtime ledger dirty -> stage declared files only -> commit/push, with runtime ledgers allowed to remain local supervisor state.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh` and `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` passed. Focused tests cover ignoring supervisor runtime ledgers during `files_changed` validation and staging only declared task files before commit.
- Limitations: This repair covers the supervisor commit boundary; it does not complete or visually verify the preserved M4-013 pricing/auth UI changes.

### 2026-05-22 - web-design-engineer - M4-013 pricing and auth visual alignment

- Agent: Codex
- Trigger: M4-013 changes browser-visible `/pricing`, `/sign-in`, and `/sign-up` UI and explicitly requires `web-design-engineer`.
- Action: Opened and followed the skill; reused the M4-012 ink, paper, clay, sage, mint, sky, `rounded-lg`, and section-band direction; kept auth and billing links unchanged; added focused visual-system regression coverage before implementing.
- Output artifacts: `app/pricing/page.tsx`; `app/sign-in/[[...sign-in]]/page.tsx`; `app/sign-up/[[...sign-up]]/page.tsx`; `components/auth/google-oauth-card.tsx`; `components/ui/button.tsx`; `tests/unit/pricing-auth-visual-system.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `npm test -- tests/unit/pricing-auth-visual-system.test.ts` first failed on the missing M4-012 route/auth/button contracts, then passed after implementation. `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed.
- Limitations: No Stripe, quota, rewrite, provider, telemetry, webhook, infrastructure, or secret behavior was changed.

### 2026-05-22 - ui-browser-testing - M4-013 pricing and auth route checks

- Agent: Codex
- Trigger: M4-013 requires desktop/mobile browser verification for `/pricing`, `/sign-in`, and `/sign-up`, plus signed-out auth redirect behavior.
- Action: Opened and followed the skill; selected route-level browser checks and focused auth redirect verification after the UI implementation; attempted both local Next dev server startup and focused Playwright auth-gate execution.
- Output artifacts: `app/pricing/page.tsx`; `components/auth/google-oauth-card.tsx`; `tests/unit/pricing-auth-visual-system.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed. Signed-out redirect behavior remains covered by `tests/unit/middleware.test.ts`; focused Playwright execution could not start because the local server bind failed before route loading.
- Limitations: Desktop/mobile screenshots were not captured in this sandbox. `npm run dev -- -H 127.0.0.1 -p 3000`, `npm run dev -- -H 0.0.0.0 -p 3001`, and `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` all failed at server startup with `EPERM`; the Codex in-app browser was also unavailable for `iab`.

### 2026-05-22 - state-machine-modeling - Supervisor runtime-only stash guard

- Agent: Codex
- Trigger: After M4-013 merged, the supervisor locally marked it done, then the next iteration stashed supervisor-only runtime ledgers and reverted the local board to `in_progress`.
- Action: Opened and followed the skill; modeled runtime ledger changes as control-plane state that must remain in the worktree across issue starts unless mixed with non-runtime implementation changes.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added a focused test requiring `stash_dirty_worktree` to skip stashing supervisor-only runtime files before any `git stash push`.
- Limitations: This entry covers supervisor lifecycle safety only; it does not change product UI or provider behavior.

### 2026-05-22 - web-design-engineer - M4-014 app workspace visual polish

- Agent: Codex
- Trigger: M4-014 changes the browser-visible `/app` workspace shell and explicitly requires `web-design-engineer`.
- Action: Opened and followed the skill; reused the M4-012 ink, paper, clay, sage, mint, sky, `rounded-lg`, and dense tool-surface direction. Changed only the workspace shell, quota/status strip, paywall presentation, input panel density, tone controls, output rail, and local-history presentation.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `components/app/subscription-status.tsx`; `components/app/paywall-card.tsx`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Added focused visual-system tests first. `npm run test -- tests/unit/workspace-copy.test.ts` failed before implementation on the missing dense workspace shell/status/paywall contracts, then passed after implementation. `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed.
- Limitations: No rewrite, quota, billing, API, telemetry, webhook, provider, infrastructure, pricing, or secret behavior was changed.

### 2026-05-22 - ui-browser-testing - M4-014 app workspace checks

- Agent: Codex
- Trigger: M4-014 requires signed-out `/app` redirect verification, responsive layout checks, console/network review, and any locally available signed-in preview state.
- Action: Opened and followed the skill; selected focused auth-gate/browser checks after implementation and attempted local Next startup, Playwright auth-gate execution, and Codex in-app browser setup.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `components/app/subscription-status.tsx`; `components/app/paywall-card.tsx`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed. `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` could not reach route execution because the configured dev server exited before loading `/app`.
- Limitations: Desktop/mobile screenshots, console/network inspection, and live signed-out redirect observation were blocked in this sandbox. `npm run dev`, `npx next dev -H 127.0.0.1 -p 3000`, Python `http.server`, and Playwright webServer startup all failed with `listen EPERM` / `Operation not permitted`; the Codex in-app browser also reported `iab` unavailable.

### 2026-05-22 - system-spec-synthesis - Supervisor auto-repair enhancement plan

- Agent: Codex
- Trigger: The owner asked how to enhance automatic repair so Cloud/Claude monitor findings do not need to be manually pasted back into Codex.
- Action: Opened and followed the skill; inspected the supervisor script, repair inbox flow, active M4-015 loop behavior, and automation configuration to convert the loose requirement into an implementation-ready supervisor repair architecture.
- Output artifacts: `plans/STOP-OVERNIGHT.txt`; `docs/skill-run-log.md`.
- Verification evidence: Confirmed from `plans/overnight.log` that M4-015 repeatedly returned `needs_human` for sandbox-blocked browser screenshots, then the supervisor stashed branch-local board updates and selected M4-015 again from main. Added a stop signal so the loop exited cleanly before more retries.
- Limitations: This turn produced the repair design and paused the loop; it did not patch `plans/overnight-supervisor.sh` yet.

### 2026-05-22 - state-machine-modeling - Supervisor terminal-state persistence

- Agent: Codex
- Trigger: The supervisor lifecycle is repeatedly selecting a task after terminal `needs_human` outcomes because the blocked board state is not persisted on main.
- Action: Opened and followed the skill; modeled the required lifecycle fix as persisting terminal issue states (`done`, `BLOCKED-*`, `needs_human`, CI/merge failure) to main-side ledger files before returning to selection.
- Output artifacts: `plans/STOP-OVERNIGHT.txt`; `docs/skill-run-log.md`.
- Verification evidence: `plans/overnight.log` shows M4-015 was run four times after `needs_human` outcomes caused by browser/server sandbox limits; `plans/issue-board.md` on main still showed M4-015 as `pending`.
- Limitations: The state-machine fix is identified but not implemented in the supervisor script in this turn.

### 2026-05-23 - state-machine-modeling - Supervisor terminal-state persistence implementation

- Agent: Codex
- Trigger: The owner approved implementing the supervisor auto-repair enhancement after M4-015 repeatedly reselected due to branch-local terminal state.
- Action: Opened and followed the skill; implemented `persist_issue_terminal_state_on_main` and routed no-status, `needs_human`, abort, and remote-merged-after-local-merge-failure outcomes through main-side ledger persistence.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `docs/skill-run-log.md`.
- Verification evidence: Added regression tests for terminal-state persistence, sandbox browser/server blocker classification, and remote merge success after local merge-command failure. `bash -n plans/overnight-supervisor.sh`, focused supervisor Vitest, and full `npm run test` passed.
- Limitations: Did not restart the overnight loop; `plans/STOP-OVERNIGHT.txt` remains a local ignored stop signal until restart is desired.

### 2026-05-23 - resilience-test-generation - Supervisor recovery regression tests

- Agent: Codex
- Trigger: The fix changes supervisor recovery behavior for repeated `needs_human`, no-status, sandbox browser/server failures, and merge-command partial success.
- Action: Opened and followed the skill; added deterministic local regression tests instead of invoking live GitHub or browser infrastructure.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: The first focused test run failed on the missing terminal-state helper and sandbox classifier; the second red test failed on missing remote-merge recovery. After implementation, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` and full `npm run test` passed.
- Limitations: Tests inspect the supervisor shell contract statically; they do not execute a live GitHub merge or browser server startup.

### 2026-05-23 - test-driven-development - Supervisor auto-repair fix

- Agent: Codex
- Trigger: Implementing a bugfix to prevent repeated supervisor retries and false merge-failure repair items.
- Action: Opened and followed the skill; wrote failing tests before changing `plans/overnight-supervisor.sh`, then implemented the minimal shell changes to pass.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: Red run failed with missing `persist_issue_terminal_state_on_main` and sandbox classifier; second red run failed with missing `gh pr view "$PR_URL" --json state,mergedAt` recovery. Green runs passed focused supervisor tests and the full Vitest suite.
- Limitations: No live loop restart or live PR merge simulation was run in this turn.

### 2026-05-23 - ui-browser-testing - M6-005 production smoke repair

- Agent: Codex
- Trigger: M6-005 requires formal-domain route smoke verification for `replyinmyvoice.com`, including public pages, signed-out `/app` redirect behavior, and browser-visible route health.
- Action: Opened and followed the skill; identified the expected route/status checklist, reproduced the sandbox DNS blocker with secret-free DNS and curl checks, and documented the network-capable rerun prerequisite instead of claiming a smoke pass.
- Output artifacts: `docs/preflight-report.md`; `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Node DNS lookup returned `ENOTFOUND` for `replyinmyvoice.com` and `example.com`; curl returned `Could not resolve host` for both hosts before any HTTP status evidence. Required local validations are run separately for this repair.
- Limitations: No desktop/mobile screenshots, console review, or live route status checks were possible from this sandbox because public DNS resolution failed before reaching the site.

### 2026-05-23 - state-machine-modeling - Supervisor runtime ledger preservation

- Agent: Codex
- Trigger: The supervisor could persist a terminal state on a branch, then hide the board/inbox/STOP ledger with a full-worktree stash and reselect the same issue from main.
- Action: Opened and followed the skill; modeled supervisor runtime files as state that must remain visible across no-status, needs-human, abort, and banned-term transitions while implementation diffs are preserved separately.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added regression coverage for runtime-aware stashing, STOP file preservation, and no-status paths using `stash_dirty_worktree`; `bash -n plans/overnight-supervisor.sh` and focused supervisor tests passed.
- Limitations: The live overnight loop was not restarted in this turn; `plans/STOP-OVERNIGHT.txt` remains the local stop signal until restart is intentional.

### 2026-05-23 - resilience-test-generation - Supervisor stash recovery regressions

- Agent: Codex
- Trigger: The fix changes recovery behavior after Codex no-status, banned-term, dirty-worktree, and sandbox/socket-permission blocker outcomes.
- Action: Opened and followed the skill; wrote deterministic source-contract tests before implementation so runtime ledgers are not stashed away when non-runtime work must be preserved.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: The first focused run failed on five missing behaviors, then passed after implementation. Full `npm run test`, `npm run lint`, and `npm run typecheck` passed.
- Limitations: These are local static contract tests for the shell supervisor; no live GitHub merge or Cloudflare deployment was invoked.

### 2026-05-23 - test-driven-development - Supervisor runtime stash fix

- Agent: Codex
- Trigger: Implementing a bugfix to prevent autonomous supervisor reselection caused by stashing terminal ledger state.
- Action: Opened and followed the skill; wrote failing tests first, verified the failures, then implemented the minimal supervisor changes to make them pass.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: Red run failed in `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` on no-status stash routing, socket-permission classification, STOP preservation, path-scoped stash, and raw stash usage. Green runs passed focused tests, `bash -n`, full Vitest, lint, and typecheck.
- Limitations: Did not run the overnight supervisor itself after patching because the local STOP signal is intentionally present.

### 2026-05-23 - dotnet-backend-testing - M6-007 backend suite blocker classification

- Agent: Codex
- Trigger: M6-007 validation status included .NET backend test coverage and needed classification without claiming tests passed when the sandbox blocks test discovery.
- Action: Opened and followed the skill; treated `dotnet test backend-dotnet/ReplyInMyVoice.sln` as the required backend command and classified the observed MSBuild socket/named-pipe permission issue as an autonomous sandbox blocker.
- Output artifacts: `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: The supervisor classifier now recognizes `socket permission` and `named-pipe` as `BLOCKED-AUTONOMY`; TypeScript lint, typecheck, and Vitest passed separately.
- Limitations: No .NET backend test pass is claimed here. The backend suite still needs to be rerun in an environment where MSBuild named-pipe/socket creation is allowed.

### 2026-05-23 - state-machine-modeling - M6-007 repair status narrowing

- Agent: Codex
- Trigger: The M6-007 repair changes persisted lifecycle/status records for the issue board and repair inbox after a non-user `BLOCKED-AUTONOMY` outcome.
- Action: Opened and followed the skill; modeled M6-007 as remaining in `BLOCKED-AUTONOMY` with a narrowed engineering prerequisite, while the repair inbox item moves from `in_progress` to `not_actionable` after documenting the prerequisite.
- Output artifacts: `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/m6-validation-report.md`; `plans/task-status.json`.
- Verification evidence: The issue board now records the loopback-listen Playwright blocker, the inbox item includes worker evidence, and the blockers log no longer frames the item as a user decision. `npm run lint`, `npm run typecheck`, and `npm run test` passed after the ledger edits.
- Limitations: The issue is not marked done because `npm run test:e2e` still needs a non-sandboxed runner that permits local server binding.

### 2026-05-23 - dotnet-backend-testing - M6-007 validation scope repair

- Agent: Codex
- Trigger: M6-007 repair referenced a prior `dotnet test` socket failure and required deciding whether .NET backend tests were part of this issue.
- Action: Opened and followed the skill; reviewed the prior VSTest failure evidence and kept the backend test suite classified separately from the M6-007 Node/Next validation brief.
- Output artifacts: `plans/m6-validation-report.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/task-status.json`.
- Verification evidence: Prior M6-007 evidence showed `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo -m:1 /nr:false` built the test assembly, then VSTest aborted while opening its socket server with sandbox permission denied before tests executed. No backend source was touched in this repair. Current repair validation passed `npm run lint`, `npm run typecheck`, and `npm run test`.
- Limitations: .NET backend tests were not rerun in this repair because the issue does not touch `backend-dotnet/` and the prior failure is an environment permission blocker.

### 2026-05-23 - ui-browser-testing - M6-007 Playwright validation

- Agent: Codex
- Trigger: M6-007 includes `npm run test:e2e`, which runs Playwright browser checks.
- Action: Opened and followed the skill; reviewed the prior Playwright failure and reproduced the underlying loopback bind restriction with a minimal Node HTTP server.
- Output artifacts: `plans/m6-validation-report.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/task-status.json`.
- Verification evidence: `npm run test:e2e` exited before tests because the Playwright web server could not listen on `0.0.0.0:3000` with `EPERM`. The minimal Node HTTP server check also failed to listen on `127.0.0.1` with `EPERM`, matching the Playwright failure before browser route assertions could execute.
- Limitations: No screenshots, console review, or browser route assertions were possible because the server bind failed before browser execution.

### 2026-05-23 - state-machine-modeling - M6-008 live webhook verification

- Agent: Codex
- Trigger: The M6-008 repair changes issue-board and repair-inbox lifecycle status for a live Stripe webhook verification task, and the verification itself has webhook delivery, DB event, and subscription-sync states.
- Action: Opened and followed the skill; used `agent-skills/state-machine-modeling/scripts/state_machine_template.py` and documented the M6-008 states as `pending_operator_event`, `event_delivered`, `event_processed`, `subscription_synced`, and `endpoint_only_verified`.
- Output artifacts: `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/m6-validation-report.md`; `tests/unit/overnight-supervisor-status.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `plans/m6-validation-report.md` now separates endpoint delivery, `StripeEvent` persistence, and `User` subscription sync so synthetic events cannot be overclaimed as entitlement verification. The status taxonomy test was updated after its red run showed M6-008 now belongs with the operator-action blocked issues.
- Limitations: No live Stripe event was sent and no production DB query was run because those require operator-controlled live Stripe and production data access.

### 2026-05-23 - data-module-review - M6-008 webhook DB evidence

- Agent: Codex
- Trigger: M6-008 requires production DB evidence for Stripe webhook processing and subscription state update.
- Action: Opened and followed the skill; reviewed `prisma/schema.prisma`, `app/api/stripe/webhook/route.ts`, `lib/stripe-events.ts`, `lib/stripe.ts`, and `tests/unit/stripe-webhook-events.test.ts`; ran `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 40`.
- Output artifacts: `plans/m6-validation-report.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Existing schema has `StripeEvent.id` as the idempotency key with status, mode, attempts, and timestamps; the webhook handler records `processing`, marks `processed` after handler success, marks `failed` on handler error, and updates `User` only when the event maps to an existing user/customer/subscription. The report now requires both `StripeEvent` and `User` evidence for full M6-008 completion.
- Limitations: This was a read-only review and documentation repair; no migration, webhook handler code, live Stripe call, or production DB query was performed.

### 2026-05-23 - state-machine-modeling - M7-003 Sentry dependency blocker

- Agent: Codex
- Trigger: The M7-003 repair changes persisted lifecycle/status records for an implementation issue after a non-user `BLOCKED-AUTONOMY` outcome.
- Action: Opened and followed the skill; modeled M7-003 as moving from `BLOCKED-AUTONOMY` to `BLOCKED-PROVIDER` when npm registry DNS is unavailable, with a return to `in_progress` only after a networked npm runner can generate the `@sentry/nextjs` lockfile.
- Output artifacts: `plans/m7-003-sentry-prerequisite.md`; `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/task-status.json`.
- Verification evidence: `npm view @sentry/nextjs version --json` failed with `ENOTFOUND registry.npmjs.org`; local `node_modules/@sentry` and npm cache did not contain `@sentry/nextjs`. Full validation is run separately for this repair.
- Limitations: This repair does not implement Sentry source wiring because committing `package.json` without a generated `package-lock.json` would leave the dependency state inconsistent.

### 2026-05-23 - state-machine-modeling - Overnight supervisor hardening

- Agent: Codex
- Trigger: The supervisor lifecycle had a livelock where `preserving_worktree` failure looped back into repair processing forever, and CI `waiting` states could reach merge logic after timeout.
- Action: Opened and followed the skill; modeled the supervisor transitions so repeated stash failures move toward a stop signal, CI pending/unknown states cannot transition to merge, and only one supervisor instance can mutate repo state.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added static contract tests for NUL status parsing, stash failure stop behavior, CI merge gating, single-instance locking, and token isolation. Focused test initially failed on five missing behaviors, then passed after implementation.
- Limitations: This does not restart the overnight loop; the STOP signal remains the deliberate guard until the hardened supervisor branch is reviewed/merged.

### 2026-05-23 - resilience-test-generation - Overnight supervisor failure-mode coverage

- Agent: Codex
- Trigger: The change touches retries, partial stash creation, CI timeout behavior, duplicate supervisor runs, and credential exposure across process boundaries.
- Action: Opened and followed the skill; built focused local tests for partial success after persistence (`git stash` creates a stash then exits nonzero), repeated failure livelock prevention, CI timeout/no-check handling, concurrent runner prevention, and environment isolation for Codex child workers.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh`, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts`, full `npm run test`, `npm run lint`, and `npm run typecheck` passed after the fix.
- Limitations: These are local source-contract and unit checks; no live GitHub CI failure was induced and no autonomous loop was restarted.

### 2026-05-23 - test-driven-development - Overnight supervisor hardening

- Agent: Codex
- Trigger: Implementing bugfixes for the overnight supervisor after a live stash livelock.
- Action: Opened and followed the skill; wrote failing regression tests first for the missing hardening behaviors, verified the focused suite failed on the intended five cases, then implemented the minimal script changes.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red run: `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` failed on five missing hardening checks. Green runs: focused test passed 22/22, full Vitest passed 47 files / 286 tests, lint passed, and typecheck passed.
- Limitations: The test suite verifies the supervisor contract statically and through local commands; it does not run a live overnight automation cycle.

### 2026-05-23 - system-spec-synthesis - Model-aware dispatcher design

- Agent: Codex
- Trigger: The user asked whether Claude can assign whole coherent tasks to a strong model while using parallel Codex workers only for independent frontend/backend/docs/test work.
- Action: Opened and followed the skill; synthesized the dispatcher requirements into an implementation-ready specification with context, goals, non-goals, architecture, data model, job contracts, security, rollout, verification, and open questions.
- Output artifacts: `docs/superpowers/specs/2026-05-23-model-aware-dispatcher-design.md`; `docs/skill-run-log.md`.
- Verification evidence: The spec separates strong-model single-owner, domain-parallel, mechanical-parallel, and blocked/manual assignment modes, and includes concrete dispatcher/coordinator/worker contracts.
- Limitations: This turn produced a design/spec only. No dispatcher script, worker execution, PR merge automation, or loop restart was implemented.

### 2026-05-23 - state-machine-modeling - Model-aware dispatcher lifecycle

- Agent: Codex
- Trigger: The dispatcher design introduces lifecycle states for issue assignment, worker execution, PR/CI handling, and coordinator merge decisions.
- Action: Opened and followed the skill; modeled assignment states from `queued` through `merged`, including blocked/stopped states, events, transition table, invariants, illegal transitions, and test checklist.
- Output artifacts: `docs/superpowers/specs/2026-05-23-model-aware-dispatcher-design.md`; `docs/skill-run-log.md`.
- Verification evidence: The spec now forbids `ci_waiting -> merged` without terminal success, forbids worker direct merge, requires one active assignment group per issue, and requires non-overlapping worker-owned paths for parallel groups.
- Limitations: These are design constraints and planned tests, not an implemented state transition helper yet.

### 2026-05-22 - state-machine-modeling - Agent permission and work allocation policy

- Agent: Codex
- Trigger: The owner clarified that unattended execution must consider permissions first and should not split coherent work across agents when a strong model can handle the whole task with less token overhead.
- Action: Opened and followed the skill; modeled the assignment lifecycle as permission gate -> work-allocation gate -> single-owner coherent task or independent parallel split -> execution/monitoring.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-implementation-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: Documentation now defines autonomous, provider/sandbox, user-only, paid-resource/secret/dashboard, and workspace-race categories; it also defines single-owner default, parallelization criteria, and token-discipline rules including prompt/context-cache efficiency.
- Limitations: This is an operating-contract update only. It does not change the currently running shell loop process until the docs are merged and the loop next reads them.

### 2026-05-23 - state-machine-modeling - Claude monitor watchdog circuit breaker

- Agent: Codex
- Trigger: The owner provided a Claude monitor watchdog/circuit-breaker proposal for detecting stale or stuck shell-supervisor runs and escalating after one failed safe restart.
- Action: Opened and followed the skill; modeled monitor states, events, transition table, invariants, illegal transitions, persistence implications, and a dry-run checklist for the scheduled Sonnet monitor prompt.
- Output artifacts: `plans/monitor-watchdog-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The prompt now separates `stopped`, `healthy`, `suspect`, `stale`, `stuck`, `restarted_once`, and `escalated`, forbids dispatch/source/board mutations, and requires a single safe `screen` restart before escalation.
- Limitations: This creates the prompt document only. It does not install or update the remote Claude scheduled task and does not restart the shell loop.

### 2026-05-23 - resilience-test-generation - Claude monitor watchdog recovery rules

- Agent: Codex
- Trigger: The watchdog proposal changes recovery behavior for stale logs, residual `codex exec` processes, stash growth, repeated error signatures, and duplicate stale triggers.
- Action: Opened and followed the skill; converted the recovery cases into a failure matrix and dry-run checklist covering timeout, duplicate trigger, partial-success, concurrent runner, stash accumulation, and malformed board/status failures.
- Output artifacts: `plans/monitor-watchdog-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The prompt limits recovery to one safe restart per failure signature, records state in `codex-supervisor/monitor-watchdog-state.json`, and escalates by touching `plans/STOP-OVERNIGHT.txt` plus writing `codex-supervisor/inbox/emergency-YYYYMMDD-HHMM.md`.
- Limitations: No live monitor trigger was run and no Claude schedule was modified in this turn.

### 2026-05-23 - state-machine-modeling - Overnight supervisor stash success cleanup

- Agent: Codex
- Trigger: The supervisor repair-inbox flow had generated repeated dirty-worktree-stash-failed blocker records and needed a precise recovery state for successful stash preservation versus genuine stash failure.
- Action: Opened and followed the skill; modeled the stash preservation lifecycle as clean/runtime-only, successful preservation, genuine failure, repeated-failure stop, and post-fix cleanup of generated blocker records.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/blockers-log.md`; `plans/codex-worker-inbox.md`; `plans/decisions-log.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused supervisor regression test first failed because the successful stash path lacked an explicit success return, then passed after adding the return. Duplicate generated blocker entries were counted at 1206 and collapsed to one forensic summary.
- Limitations: This does not restart the loop; `plans/STOP-OVERNIGHT.txt` remains user-controlled.

### 2026-05-23 - resilience-test-generation - Overnight supervisor stash recovery regression

- Agent: Codex
- Trigger: The change covers timeout recovery, repeated stash failure handling, partial stash behavior, generated blocker cleanup, and safe restart prerequisites for the long-running supervisor loop.
- Action: Opened and followed the skill; added focused regression coverage for the successful stash path and preserved the existing failure-path coverage for partial stash drop and repeated-failure stop signaling.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/blockers-log.md`; `plans/codex-worker-inbox.md`; `plans/decisions-log.md`; `docs/skill-run-log.md`.
- Verification evidence: Red run: `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` failed on the missing explicit return. Green run: the same focused suite passed 23/23 after the script change.
- Limitations: No live shell-loop restart, provider call, deploy, dashboard change, live-money action, or secret mutation was performed.

### 2026-05-23 - state-machine-modeling - M9-003 repair unblock

- Agent: Codex
- Trigger: The M9-003 repair required changing persisted issue-board lifecycle states so the supervisor test suite and prior scoped-release decision agreed.
- Action: Opened and followed the skill; modeled issue rows with `pending`, `in_progress`, blocked categories, and `done` states, then applied the allowed `BLOCKED-AUTONOMY` to `pending` transition to M1-007, M1-009, M3-001, M3-002, and M3-005, plus the M9-003 repair transition to `done` after implementation and validation.
- Output artifacts: `plans/issue-board.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Focused supervisor test passed after the board repair, then `npm run lint`, `npm run typecheck`, `npm run test`, and `npm run build --prefix packages/mcp-server` passed.
- Limitations: No git, GitHub, live money, provider dashboard, npm publish, secret, `.env.local`, or `.dev.vars` action was performed.

### 2026-05-23 - state-machine-modeling - Main loop pending-row restart unblock

- Agent: Codex
- Trigger: The overnight loop exited immediately because scoped M1/M3 rows were still persisted as `BLOCKED-AUTONOMY` on `main`, leaving no pending work for the supervisor.
- Action: Opened and followed the skill; modeled the issue-board rows with `pending`, `in_progress`, and blocked states, then applied the allowed `BLOCKED-AUTONOMY` to `pending` transition for M1-007, M1-009, M3-001, M3-002, and M3-005 after confirming each has a scoped issue brief.
- Output artifacts: `plans/issue-board.md`; `plans/overnight-progress.md`; `docs/skill-run-log.md`.
- Verification evidence: Issue-board row checks confirmed all five target rows are `pending`; focused supervisor Vitest passed before restart.
- Limitations: This does not change provider settings, secrets, `.env.local`, `.dev.vars`, live money, or deployment configuration.

### 2026-05-23 - state-machine-modeling - State-agnostic supervisor board test

- Agent: Codex
- Trigger: The overnight loop repeatedly misclassified otherwise completed issue work because a supervisor unit test required scoped board rows to remain `pending` even after those rows advanced to `in_progress`, `done`, or blocked states.
- Action: Opened and followed the skill; modeled issue-board status as a lifecycle field that may move through `pending`, `in_progress`, blocked states, and `done`, then changed the test to validate row presence and table structure instead of one transient status value.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red run reproduced the failure by advancing M1-007, M1-009, and M3-001 in a temporary board state; green runs passed the focused supervisor Vitest against both the advanced temporary state and the restored main board state.
- Limitations: This changes test coverage only. It does not recover the M1-009 or M3-001 stashes, restart the loop, change provider settings, touch secrets, or deploy.

### 2026-05-24 - ui-browser-testing - Students page polish commit

- Agent: Codex
- Trigger: The task changed browser-visible `/students` page spacing and requested desktop/mobile layout review before a local commit.
- Action: Opened and followed the skill; inspected the students-page CSS, fixed the hero preview before/after columns to use content-sized height, attempted Codex Browser and live localhost verification, then used a no-network static Chromium render of the students markup with `app/globals.css` for desktop/mobile layout checks.
- Output artifacts: `app/globals.css`; `docs/skill-run-log.md`.
- Verification evidence: Static render at 1440px and 390px reported no horizontal overflow, no console errors, and `.student-preview .compare-col` computed `min-height: 0px` with content-sized heights. `npm run typecheck`, `npm run lint`, and `npm test` passed under Node 22.13.1; the banned-term scan returned no matches.
- Limitations: The Codex Browser plugin reported no available `iab` backend, and live `localhost:3021` browser/curl access from the sandbox was blocked even though `lsof` showed a listener on port 3021, so the browser layout check used a static CSS render rather than the running dev server.

### 2026-05-24 - ui-browser-testing - Pivot Phase 0 reply-decision copy

- Agent: Codex
- Trigger: The task changed browser-visible landing, pricing, terms, workspace label, and `/students` copy for the reply-decision repositioning.
- Action: Opened and followed the skill; updated user-visible copy only, verified the dead pricing component was unreferenced before deletion, and checked `/` plus `/students` at desktop and mobile sizes with Playwright.
- Output artifacts: `components/landing/*`; `app/students/page.tsx`; `app/pricing/page.tsx`; `app/terms/page.tsx`; `components/app/rewrite-workspace.tsx`; `tests/unit/*copy*.test.ts`; `docs/skill-run-log.md`; `plans/decisions-log.md`.
- Verification evidence: With Node 22.13.1 first on PATH, staged-state `npm run lint`, `npm run typecheck`, `npm run test`, and `npm run build` passed. The banned-term grep returned no matches. Playwright screenshots for `/` and `/students` at 1440px and 390px showed no horizontal overflow, no console errors, and required hero/boundary copy present.
- Limitations: Validation used `git stash --keep-index` to exclude pre-existing unstaged tracked changes in `components/site-header.tsx`, `components/site-footer.tsx`, `docs/skill-run-log.md`, `lib/admin-visible.ts`, and `lib/rewrite-completeness.ts`; those unrelated changes were restored afterward and were not part of this task. No deploy, main merge, Stripe, schema, secret, or provider setting changes were made.

### 2026-05-24 - cloud-architecture-cost-review - C# rewrite backend migration

- Agent: Codex
- Trigger: The owner set the goal to keep Azure Functions Consumption + Azure SQL + Service Bus, move the backend runtime to C#, avoid App Service, then merge and deploy.
- Action: Opened and followed the skill; kept the existing scale-to-zero Azure Functions architecture, explicitly rejected App Service reintroduction, checked Azure app-setting names without printing values, and used GitHub Actions as the Cloudflare deploy path after local wrangler lacked `CLOUDFLARE_API_TOKEN`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; C# provider adapters; Azure proxy routes; `README.md`; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 60/60; `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`, and `npm run cf:build` passed; Azure Functions deploy passed; Azure `/api/health`, `/api/health/db`, and unsigned `/api/rewrite` auth-boundary smokes passed.
- Limitations: No new paid resources were created. Local Cloudflare deploy could not complete without `CLOUDFLARE_API_TOKEN`; production Cloudflare deploy is expected through GitHub Actions secrets after merge.

### 2026-05-24 - system-spec-synthesis - Public backend C# runtime target

- Agent: Codex
- Trigger: The task converted a loose goal, "backend all C# and services all Azure," into implementation boundaries spanning rewrite, billing/webhook proxies, account/quota reads, deployment, and verification.
- Action: Opened and followed the skill; scoped the implementation to the public runtime path first: C# rewrite provider and adapters, Cloudflare BFF proxies to Azure Functions, Azure account/quota reads, and documented remaining legacy TS cleanup.
- Output artifacts: `app/api/rewrite/route.ts`; `app/api/stripe/checkout/route.ts`; `app/api/stripe/webhook/route.ts`; `app/app/page.tsx`; C# rewrite engine/provider files; tests; `README.md`; `docs/business-qa-and-deploy-result.md`.
- Verification evidence: Frontend and backend tests/builds passed as recorded in `docs/business-qa-and-deploy-result.md`.
- Limitations: Legacy TS rewrite/learningops/observability files remain for historical tests/admin cleanup and were not deleted in this slice.

### 2026-05-24 - state-machine-modeling - Rewrite quality failure and quota lifecycle

- Agent: Codex
- Trigger: Moving rewrite execution to C# affects rewrite attempt states, queued worker processing, quality failures, and quota reservation/finalization behavior.
- Action: Opened and followed the skill; preserved the existing `Pending -> Processing -> Succeeded/Failed/Expired` attempt lifecycle and implemented provider quality failures as failed provider results so `RewriteJobProcessor` releases reservations instead of finalizing quota.
- Output artifacts: `FactReconstructRewriteProvider.cs`; C# rewrite tests; existing `RewriteJobProcessor` integration path.
- Verification evidence: `FactReconstructRewriteProviderTests` cover Sapling unavailable, structure failure, naturalness failure, and successful output; full .NET suite passed 60/60.
- Limitations: No database schema transition was required in this slice.

### 2026-05-24 - data-module-review - Public runtime Prisma dependency removal

- Agent: Codex
- Trigger: The public `/app`, rewrite, Stripe checkout, and webhook paths moved away from TS/Prisma/Neon helpers toward Azure Functions and Azure SQL.
- Action: Opened and followed the skill; removed public route imports of `lib/users`, `lib/quota`, and TS Stripe handlers; changed `/app` to use Azure account summary; removed generated Prisma client type imports from remaining legacy helpers so typecheck no longer depends on generated Prisma client.
- Output artifacts: `app/app/page.tsx`; `app/api/stripe/checkout/route.ts`; `app/api/stripe/webhook/route.ts`; `lib/users.ts`; `lib/quota.ts`; `lib/subscription.ts`; related tests.
- Verification evidence: `npm run typecheck`, `npm test`, and `npm run build` passed; Azure `/api/health/db` returned Azure SQL.
- Limitations: Legacy Prisma/Neon helper files still exist for historical tests and admin/learning cleanup; no data migration or schema deletion was attempted.

### 2026-05-24 - resilience-test-generation - C# provider failure gates

- Agent: Codex
- Trigger: The C# rewrite provider touches external model and Sapling boundaries where timeouts, unavailable signals, malformed output, structure failures, and naturalness failures must not charge quota.
- Action: Opened and followed the skill; added deterministic fakes and tests for Sapling unavailable before model call, structure-gate failure, naturalness-gate failure, and successful result JSON; added HTTP adapter tests for OpenAI-compatible/DeepSeek and Sapling request/response behavior.
- Output artifacts: `FactReconstructRewriteProviderTests.cs`; `RewriteProviderAdapterTests.cs`; provider implementation files.
- Verification evidence: Focused provider tests passed, then full .NET suite passed 60/60.
- Limitations: Live signed-in rewrite was not executed because this Codex session has no authenticated Entra user token.

### 2026-05-24 - dotnet-backend-testing - C# rewrite engine/provider implementation

- Agent: Codex
- Trigger: The task added C#/.NET rewrite-engine logic, provider adapters, DI registration, and worker-facing failure behavior.
- Action: Opened and followed the skill; used xUnit and deterministic fakes, verified RED failures before implementation, then added domain, provider, adapter, and DI tests.
- Output artifacts: `RewriteEngineCoreTests.cs`; `FactReconstructRewriteProviderTests.cs`; `RewriteProviderAdapterTests.cs`; `InfrastructureServiceCollectionTests.cs`; C# domain/infrastructure files.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 60/60 and `dotnet build backend-dotnet/ReplyInMyVoice.sln --no-restore` passed.
- Limitations: Tests use fake model/Sapling clients for deterministic coverage; live provider behavior was limited to app-setting-name checks and unauthenticated Azure smokes.

### 2026-05-24 - claude-heavy-planning-handoff - C# backend migration routing

- Agent: Codex
- Trigger: The requested goal spans multiple services and could qualify for Claude Code heavy planning.
- Action: Opened and followed the skill's routing guidance, but did not call Claude CLI because the owner explicitly asked Codex to proceed with implementation and the first safe public-runtime slice was locally executable.
- Output artifacts: None beyond this log entry.
- Verification evidence: Codex implemented and verified the migration slice directly with the checks listed above.
- Limitations: No Claude handoff document or Claude CLI result was produced, so Claude Code did not participate in this run.

### 2026-05-24 - dotnet-backend-testing - Post-deploy backend verification

- Agent: Codex
- Trigger: The owner asked Codex to keep testing after production deployment, fix issues autonomously, and ensure the C# Azure backend remained deployed successfully.
- Action: Opened and followed the skill; reran the full .NET backend suite after the production merge and rechecked the remote Azure Functions health boundary.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 60/60; `https://replyinmyvoice-func-dev.azurewebsites.net/api/health` returned `{"ok":true,"service":"replyinmyvoice-functions"}` with HTTP 200.
- Limitations: No signed-in live rewrite was executed because this Codex session does not have an authenticated user access token.

### 2026-05-24 - ui-browser-testing - Post-deploy frontend and E2E verification

- Agent: Codex
- Trigger: The owner asked Codex to keep testing after production deployment; the browser-visible Cloudflare Worker, landing page, auth gate, and API proxy behavior needed verification.
- Action: Opened and followed the skill; ran Playwright E2E, investigated the landing-page assertion failure, and updated the test to match the current footer positioning copy.
- Output artifacts: `tests/e2e/commercial-site.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Production `https://replyinmyvoice.com/` returned HTTP 200; `https://replyinmyvoice.com/api/health/db` returned `{"ok":true,"database":"azure-sql"}` with HTTP 200; unsigned production `POST /api/rewrite` returned HTTP 401 with `{"error":"unauthorized"}`. The focused Playwright failure was traced to stale expected copy: the page now says "Built for practical replies..." instead of the previous "Built for real communication workflows".
- Limitations: Browser E2E covers signed-out gates and static commercial pages only; authenticated rewrite UX remains untested without a real user session token.

### 2026-05-24 - ui-browser-testing - Entra Google callback returns to sign-in

- Agent: Codex
- Trigger: Owner reported that Google login successfully reached `/auth/callback` and redirected to `/app`, but `/app` immediately bounced back to `/sign-in`.
- Action: Opened and followed the skill; traced the auth cookie write/read path through `lib/entra-auth.ts`, `middleware.ts`, `/auth/callback`, and existing auth tests. Added a regression test proving the signed `rimv_session` cookie exceeded the browser 4KB limit when it embedded Entra access/refresh tokens, then changed `rimv_session` to persist only the browser gate identity fields while storing the Azure access token in separate signed chunked cookies for server-side Azure proxy calls.
- Output artifacts: `lib/entra-auth.ts`, `tests/unit/entra-auth.test.ts`, `docs/skill-run-log.md`.
- Verification evidence: Watched the new test fail at 8378-byte cookie size before the fix. After the fix, `npm test -- tests/unit/entra-auth.test.ts`, `npm test -- tests/unit/middleware.test.ts`, full `npm test` (94 tests on the current `origin/main` line), `npm run typecheck`, `npm run lint`, `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium`, and a clean standalone `npm run cf:build` all passed. Pushed commit `5b20341` to `main`; GitHub Actions `cloudflare-worker` run `26358606532` and `dotnet-azure` run `26358606522` passed. Production smoke returned `https://replyinmyvoice.com/` 200, signed-out `/app` 307 to `/sign-in`, `/api/health/db` `{"ok":true,"database":"azure-sql"}`, and unsigned `/api/rewrite` 401.
- Limitations: No live production Google login was performed from the owner's browser in this turn. Local direct `npm run cf:deploy` could not deploy without `CLOUDFLARE_API_TOKEN`, so production deploy used the configured GitHub Actions secrets. No secrets, token values, `.env.local` contents, or provider credentials were logged.

### 2026-05-24 - ui-browser-testing - Rewrite workspace pending result crash

- Agent: Codex
- Trigger: Owner showed a production `/app?checkout=cancelled` client-side exception after login and testing the workspace: `Cannot read properties of undefined (reading 'length')`.
- Action: Opened and followed the skill; traced the browser-visible crash to `/app` treating non-success rewrite payloads as successful results. Added response normalization so missing optional UI fields cannot crash the workspace, changed `/api/rewrite` to normalize direct/resultJson success payloads, kept pending attempts distinct with their `attemptId`, added a signed-in attempt polling proxy, and changed the client to keep polling the same attempt instead of creating a new request.
- Output artifacts: `lib/rewrite-response.ts`; `app/api/rewrite/route.ts`; `app/api/rewrite-attempts/[attemptId]/route.ts`; `components/app/rewrite-workspace.tsx`; `tests/unit/rewrite-response.test.ts`; `tests/unit/rewrite-api-quality.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Watched the new focused tests fail before implementation, then pass. `npm test` passed 99/99; `npm run typecheck` passed; `npm run lint` passed; `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` passed 3/3; `npm run cf:build` completed and included `/api/rewrite-attempts/[attemptId]`.
- Limitations: Local Playwright verification covered signed-out browser gates and API rejection only; this Codex session still does not have the owner's authenticated production browser session, so the final live signed-in rewrite flow needs owner-side retest after deployment.

### 2026-05-24 - cloud-architecture-cost-review - Rewrite packs pricing analysis

- Agent: Codex
- Trigger: Owner asked for analysis of the latest rewrite-pack pricing scheme, including Stripe fees, GST exposure, AI/provider cost per rewrite, and launch guardrails.
- Action: Opened and followed the skill; reviewed `docs/rewrite-packs-pricing-spec.md`, compared the proposed Free Trial / Quick Pack / Value Pack / Pro/API / Focus Pack economics, and verified current public Stripe NZ card fees plus IRD GST rate/registration-threshold guidance from official sources.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Official Stripe NZ pricing confirmed domestic cards at 2.65% + NZ$0.30, international cards at 3.5% + NZ$0.30, and +2% if currency conversion is required. Official IRD pages confirmed GST registration at NZ$60,000 taxable turnover expectation/actual threshold and GST at 15% for registered businesses.
- Limitations: This was a pricing/cost review only; no code, Stripe products, Stripe Prices, database migrations, or paid resources were created. Real provider cost per rewrite still needs production measurement from rewrite cost telemetry before increasing included rewrite counts.

### 2026-05-25 - dotnet-backend-testing - Rewrite certainty-preservation gate

- Agent: Codex
- Trigger: Owner asked to modify the rewrite engine after a test case showed good AI-signal reduction but a fact risk from changing `seems` into a definite `is due to` claim.
- Action: Opened and followed the skill; wrote a failing xUnit regression test before production changes, then added deterministic uncertainty-preservation handling in the C# rewrite fact gate and prompt guidance.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteEngineCoreTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/FactReconstructRewriteProvider.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiCompatibleRewriteModelClient.cs`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The focused test `FactGate_blocks_uncertainty_strengthening_from_seems_to_is_due_to` first failed because the current gate passed the candidate; after implementation it passed. `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore --filter Rewrite` passed 38/38.
- Limitations: This verifies deterministic gate behavior and rewrite prompt guidance locally; it does not prove a live Sapling/DeepSeek production rewrite score for the exact Emma sample until remote authenticated rewrite smoke runs after deployment.

### 2026-05-25 - resilience-test-generation - Applicability check for certainty-preservation change

- Agent: Codex
- Trigger: The change touches provider-backed rewrite quality gates, so Codex checked whether resilience-test-generation was needed for retries, provider failures, idempotency, or recovery behavior.
- Action: Opened the skill and determined this was a deterministic fact-preservation bugfix, not a retry/timeout/provider-failure behavior change. No resilience matrix was generated.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Existing rewrite provider retry tests remained within the `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore --filter Rewrite` run, which passed 38/38.
- Limitations: No new provider failure scenario was added because the requested behavior is certainty preservation, not dependency failure handling.

### 2026-05-25 - ui-browser-testing - Rewrite-packs pricing cutover (homepage, /pricing, paywall)

- Agent: Claude Code
- Trigger: UI/copy cutover replacing the old subscription pricing (Starter NZ$9.90/55, Pro 110, Exam Week Pass, Top-up, Students framing) with the rewrite-packs model across the homepage, /pricing, and the /app paywall; required local webpage verification.
- Action: Skill is not indexed in this Claude Code session, so followed the project source checklist (`agent-skills/ui-browser-testing`) via the harness preview tools. Started the Next.js dev server and verified /pricing (desktop + mobile), homepage hero/stats/pricing block, footer + nav, and the /students -> / redirect; checked console for errors; captured desktop and mobile screenshots.
- Output artifacts: `app/pricing/page.tsx`; `components/landing/{pricing-v2,hero,trust-panel,closing-cta,faq}.tsx`; `components/site-header.tsx`; `components/site-footer.tsx`; `app/sitemap.ts`; `components/app/paywall-card.tsx`; `app/app/page.tsx`; `components/app/rewrite-workspace.tsx`; `components/auth/google-oauth-card.tsx`; `app/terms/page.tsx`; `next.config.ts` (redirects); `tests/unit/{pricing-auth-visual-system,workspace-copy}.test.ts`; removed `app/students/page.tsx`, `app/launch/page.tsx`, `tests/unit/students-v2-page.test.ts`.
- Verification evidence: `npm run typecheck`, `npm run test` (95 passed / 18 files), `npm run build`, and `npm run cf:build` all passed; banned-term grep clean; dev-server preview showed /pricing rendering Quick/Value(Most popular)/Pro·API with graceful "Available soon" gating, homepage pricing block + hero updated, footer/nav free of old model and "Students", and /students + /launch returning to / with no console errors (desktop + mobile screenshots captured).
- Limitations: Local dev-server verification only; the OpenNext/Cloudflare Worker runtime can differ from local (prerendered pages have 500'd in prod before despite local gates passing), so /pricing, the /students redirect, and the /app paywall must be re-verified on the deployed Worker. The /app paywall and workspace nudge were verified via source/contract tests, not an authenticated exhausted-quota browser session. Stripe pack Prices are not yet created, so all paid CTAs are intentionally "Available soon".

### 2026-05-25 - system-spec-synthesis - Single-input draft rewrite eval contract

- Agent: Codex
- Trigger: Owner corrected the rewrite-quality eval target from a dual-input reply eval to the current product-shaped single-input draft rewrite eval.
- Action: Opened and followed the skill, then wrote an implementation-ready spec covering context, goals, non-goals, current system, proposed architecture, data model, runner contract, state/error handling, privacy, rollout, and verification.
- Output artifacts: `docs/single-input-draft-rewrite-eval-spec.md`; `docs/rewrite-email-eval-cases-100.md`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `docs/rewrite-strategy-memory.md`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`.
- Verification evidence: `npm test -- tests/unit/rewrite-email-eval-cases.test.ts tests/unit/eval-scenarios-corpus.test.ts` passed 30/30; `EVAL_CORPUS=email-100 npx tsx scripts/eval-scenarios.ts --mode=smoke --limit=0 --output=/tmp/replyinmyvoice-single-input-eval-dry-run.md` loaded 10 smoke cases and exited without provider calls.
- Limitations: Only cases 001-010 are materialized. Provider smoke was intentionally not run after the contract pivot. `npm run typecheck` remains blocked by the existing missing `stripe` package/type declaration in `lib/stripe.ts`.

### 2026-05-25 - cloud-architecture-cost-review - Eval provider-cost gate

- Agent: Codex
- Trigger: The requested rewrite-quality eval path can spend DeepSeek and Sapling calls; the old provider smoke had already run against the wrong dual-input corpus.
- Action: Opened and followed the skill's cost-control posture. Stopped the old eval process, kept this turn to local parser/runner validation, and documented staged provider usage: local validation first, 10-case smoke second, focused/full only after the corpus and judge shape are stable.
- Output artifacts: `docs/single-input-draft-rewrite-eval-spec.md`; `docs/rewrite-email-eval-cases-100.md`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/skill-run-log.md`.
- Verification evidence: Local dry-run used `--limit=0`, loaded 10 smoke cases, wrote `/tmp/replyinmyvoice-single-input-eval-dry-run.md`, and did not require DeepSeek or Sapling calls.
- Limitations: No new cost telemetry was generated because no provider calls were made under the corrected corpus contract.

### 2026-05-25 - cloud-architecture-cost-review - Corrected single-input smoke calibration

- Agent: Codex
- Trigger: Owner asked to run the corrected single-input warm-tone smoke 10 against the real provider path as a calibration run, explicitly limited to the 10 materialized cases.
- Action: Opened and followed the skill's provider-cost gate. Confirmed no eval process was running, reran local tests and lint, performed a `--limit=0` dry-run, then ran only `--mode=smoke --limit=10` against the existing provider environment. Did not run focused/full mode, materialize more cases, or change prompts/scoring/parser/corpus.
- Output artifacts: `docs/eval-runs/single-input-smoke-10-corrected.md`; `docs/eval-runs/single-input-smoke-10-corrected-triage.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/rewrite-email-eval-cases.test.ts tests/unit/eval-scenarios-corpus.test.ts` passed 30/30; `npm run lint` passed; dry-run loaded exactly 10 smoke cases; completed smoke report recorded 10/10 evaluated, 4/10 customer-usable pass, 4/10 strict signal pass.
- Limitations: The first provider attempt aborted on a DeepSeek connect timeout during semantic judging; after a no-credential endpoint probe returned HTTP 401, one identical rerun completed. Raw provider payloads are not logged, so judge-only separation is verified by mapping tests and report behavior rather than request capture. No optimization fixes were made in this run.

### 2026-05-25 - cloud-architecture-cost-review - Admin Mongo/ELK necessity review

- Agent: Codex
- Trigger: Owner asked whether the Reply In My Voice admin console needs MongoDB or ELK.
- Action: Opened and followed the skill; reviewed current manual setup/run-result docs, admin pages, cost/learning metric helpers, EF Core models, and Application Insights wiring. Selected the existing SQL + Application Insights architecture for the production admin console and rejected MongoDB/ELK as duplicate paid/operational infrastructure for the current workload.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Confirmed `/admin` uses request-level rewrite quality/cost/learning telemetry; confirmed `RewriteCostLog`, `RewriteProviderCall`, `LearningRun`, `LearningFinding`, `StrategyCandidate`, and `RewriteLearningSample` are already modeled in SQL/EF or Prisma history; confirmed Application Insights is already configured for API/Functions/Worker observability.
- Limitations: Exact current MongoDB/Cosmos/Elastic pricing was not checked because no numeric monthly run-rate was quoted; this was an architecture necessity review, not an implementation or deployment change.

### 2026-05-25 - data-module-review - Admin Mongo/ELK persistence fit review

- Agent: Codex
- Trigger: The MongoDB question affects persistence boundaries for admin telemetry, learning samples, quota, billing, and rewrite-attempt records.
- Action: Opened and followed the skill; inspected SQL schema ownership and invariants around `RewriteAttempt`, `UsageReservation`, `StripeEvent`, `OutboxMessage`, admin cost logs, provider calls, and learning tables. Determined MongoDB should not become an authoritative store for admin, billing, quota, or rewrite job state.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: SQL schema already has unique/idempotency constraints, row-version concurrency tokens, foreign keys, and indexed admin lookup paths for current admin screens. A helper `scan_data_risks.py --limit 80` run was started but stopped after it did not return promptly in the dirty workspace; manual targeted reads supplied the review evidence.
- Limitations: No code, migrations, tests, or database resources were changed. This does not rule out a future isolated, non-authoritative local Mongo/ELK portfolio demo fed from exported events.

### 2026-05-25 - system-spec-synthesis - Admin console capability and phase review

- Agent: Codex
- Trigger: Owner asked what the management/admin console should include and what order to build it in.
- Action: Opened and followed the skill; converted the loose admin-console question into staged requirements covering actors, current system, data ownership, read/write boundaries, operational modules, state lifecycles, security, rollout, and verification.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Reviewed current `/admin` overview, metric queries, admin auth helper, manual setup notes, Azure cutover docs, and EF Core table mappings for users, quota, rewrite attempts, Stripe events, outbox, cost logs, learning, API keys, and referrals.
- Limitations: This was an analysis/specification response only; no implementation spec file, admin API, UI, migration, or cloud resource was created.

### 2026-05-25 - state-machine-modeling - Admin lifecycle coverage review

- Agent: Codex
- Trigger: The admin console requirements review covers lifecycle-heavy areas: rewrite attempts, usage reservations, Stripe webhook processing, outbox dispatch, subscriptions, credits, canary rollback, and API keys.
- Action: Opened and followed the skill; identified which admin phases must expose state lists, transition history, illegal/stuck states, retryable failures, and recovery actions without sidestepping C#/.NET transition logic.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Confirmed persisted state-bearing tables and indexes in EF Core: `RewriteAttempts`, `UsageReservations`, `StripeEvents`, `OutboxMessages`, `RewriteCredits`, `LearningRuns`, `StrategyCandidates`, `RewriteCanaryRollbacks`, `ApiKeys`, and `ApiKeyUsages`.
- Limitations: No formal state-machine markdown was generated and no transition helper/test changes were made; this was used to order admin-console capabilities.

### 2026-05-25 - cloud-architecture-cost-review - Admin console phased architecture review

- Agent: Codex
- Trigger: Owner asked about admin-console phases, including whether Python should participate and how to avoid unnecessary infrastructure.
- Action: Opened and followed the skill; recommended keeping the production admin on existing Next.js/.NET/Azure SQL/Application Insights foundations, using Python only as an optional read-only analytics/reporting layer, and rejecting always-on duplicate admin stacks until a clear cost-approved need exists.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Current docs show Cloudflare frontend, Azure Functions/.NET API, Azure SQL, Service Bus, and Application Insights as the production target; prior App Service fixed run-rate was removed in favor of Azure Functions consumption.
- Limitations: Exact current cloud prices were not checked because no specific monthly run-rate was quoted and no paid resource change was proposed.

### 2026-05-25 - data-module-review - Admin console data ownership review

- Agent: Codex
- Trigger: Admin-console phase planning touches user, quota, billing, webhook, queue, cost, learning, and API-key persistence invariants.
- Action: Opened and followed the skill; reviewed the SQL/EF table set and current admin query layer to classify which modules should be read-only, which future operator actions require C# API endpoints, and which actions need audit logging.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Current admin metrics query `RewriteCostLog`/`RewriteProviderCall`; EF Core has unique/idempotency indexes and concurrency tokens for the lifecycle-critical tables; manual setup notes say runtime account/quota/rewrite data is now served from Azure SQL through Azure Functions.
- Limitations: No new admin audit-log schema was added; direct SQL write prevention remains a design recommendation until implemented.

### 2026-05-25 - resilience-test-generation - Semantic judge malformed JSON retry

- Agent: Codex
- Trigger: The single-input dev-20 provider validation hit a semantic judge malformed JSON failure, which changes provider-failure retry and fail-closed behavior.
- Action: Opened and followed the skill; generated a resilience matrix for malformed semantic judge JSON, added a unit regression for retrying malformed judge responses, and updated the eval runner to retry semantic judge parsing before failing a case.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/eval-scenarios-corpus.test.ts` passed after the retry regression was added; later focused unit verification passed with `npm test -- tests/unit/rewrite-pipeline.test.ts tests/unit/eval-scenarios-corpus.test.ts`.
- Limitations: The later v6 provider partial was intentionally stopped after 6/8 cases per the corrected rerun strategy; no final dev-20 pass, merge, or deploy is claimed in this entry.

### 2026-05-25 - cloud-architecture-cost-review - Cloudflare rewrite-quality validation deploy

- Agent: Codex
- Trigger: User requested merge and deploy after the rewrite-quality validation task.
- Action: Opened and followed the skill; reviewed `docs/manual-setup.md`, `docs/business-qa-and-deploy-result.md`, `README.md`, and `docs/next-development-brief.md`; confirmed this deploy should keep the existing Cloudflare Worker/OpenNext frontend plus Azure Functions/Azure SQL backend path.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 agent-skills/cloud-architecture-cost-review/scripts/cost_review_template.py "Cloudflare rewrite-quality validation deploy"` and reviewed the current deployment docs. Selected option: deploy the existing Cloudflare Worker through the existing `npm run cf:deploy` path with `--keep-vars`. Rejected options: no new Cloudflare service, Azure App Service, queue, database, or always-on worker is needed for this code-only validation deploy.
- Limitations: No exact provider pricing was quoted or checked because this task does not create, resize, or switch paid infrastructure; production smoke still needs to run after deploy.

### 2026-05-27 - system-spec-synthesis - Voice + Fidelity quality-track pivot spec

- Agent: Claude Code
- Trigger: Owner decided (2026-05-27) to stop the AI-detection track and formally pivot to a Voice + Fidelity quality track, and asked to turn the module list into next steps (converting loose product notes into an executable engineering plan).
- Action: Opened and followed the skill (workflow + Output Contract headings); re-read AGENTS.md and the C# rewrite engine; produced an implementation-ready spec converting the owner's 6-module notes (ProtectedTermLedger, BoundaryGate, VoiceProfile, MinimalHumanEdit, SendabilityGate, Quality A/B) into contracts, data model, gate chain, rollout, and verification, reusing the eval-only byproducts from the 10-round investigation and demoting Pangram to offline observation only.
- Output artifacts: `plans/voice-fidelity-quality-track-spec.md` (+ this log entry). Detection-track findings recorded in `plans/translation-roundtrip-pilot.md`.
- Verification evidence: Spec includes a Verification Plan (xUnit + Quality A/B + banned-term grep) and Phase-1 acceptance criteria — notably the hardened FidelityJudge must FAIL the three known object/term-drift misses (seat credit→letter of credit, planter→flowerpot, saucer→tea tray). No code shipped; eval-only until reviewed.
- Limitations: Spec leaves explicit Open Questions (voice-sample intake UI/consent/retention, mode exposure, "user preference" metric, Manus prod cost/latency, VoiceProfile EF schema + migration) as product/architecture decisions, not silent assumptions. `data-module-review` and `cloud-architecture-cost-review` flagged before Phase 3 / any paid prod dependency.

### 2026-05-28 - dotnet-backend-testing - Stage 1 EN->ZH ClaimLedger pilot

- Agent: Codex
- Trigger: Owner asked Codex to start Sub-Phase 1.1 from `plans/phase1-claim-ledger-handoff.md`, adding C# eval-tool behavior and tests for Youdao EN->ZH post-checking and drift reporting.
- Action: Opened and followed the skill; added xUnit coverage for deterministic ZH fact survival checks, batched ClaimLedger verdict parsing, empty ClaimLedger warning behavior, model selection, and markdown drift report rendering before implementing the eval-only pilot.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-101348-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (6 tests). Full suite later passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` after adding the new tests. Real provider verification ran the locked 10-case set with `STAGE1_EN_TO_ZH_PILOT=1` and wrote the report above: Youdao calls 10, DeepSeek calls 20, structured claims 89/93 preserved.
- Limitations: No production rewrite path, Stripe, Azure, DNS, or deployment code was changed. Literal fact-anchor survival remains intentionally naive in 1.1 and does not yet count Chinese equivalents for names, weekdays, or sentence-level constraints; those rows are input for the 1.2 repair step.

### 2026-05-28 - system-spec-synthesis - Stage 1 v1 simplification next-step ordering

- Agent: Codex
- Trigger: Owner clarified the Phase 1 v1 shape: no placeholders and no Chinese GPTZero; focus on FactLedger, ClaimLedger, Youdao EN->ZH, ZH fact coverage, minimal Chinese repair, re-check, and safe Chinese intermediate output.
- Action: Opened and followed the skill to translate the clarified requirements into a concrete next-step implementation order for Sub-Phase 1.2 without reopening the architecture.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Reviewed current Sub-Phase 1.1 implementation and report artifact `docs/rewrite-eval-results/20260528-101348-stage1-en-zh-pilot.md`; no code or tests were changed in this planning response.
- Limitations: This entry records planning only. Phase 1.2 minimal Chinese repair implementation, tests, and provider run remain to be done.

### 2026-05-28 - dotnet-backend-testing - Stage 1.2 Chinese minimal repair loop

- Agent: Codex
- Trigger: Owner approved the v1 simplification and asked what to do next; Codex proceeded with Sub-Phase 1.2 implementation in C# eval-tool code.
- Action: Opened and followed the skill; added xUnit coverage for the repair loop, no-op pass path, safe-intermediate report rendering, and claim-regression rejection. Implemented bounded Chinese minimal repair, second post-check, raw/final ZH report output, and a guard that rejects repairs that reduce ClaimLedger survival.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-103956-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused tests passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (10 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (281 tests). Real provider 10-case Stage 1.2 run used Youdao calls 10 and DeepSeek calls 40; final structured claim preservation was 92/92 after one bounded repair pass.
- Limitations: All 10 cases still fail the literal fact-anchor criterion because the current deterministic FactLedger check is intentionally naive and still requires English verbatim anchors for translated names, weekdays, and sentence fragments. Next work should add preserve-mode-aware hard fact extraction/checking before treating fact failures as final Phase 1 failure.

### 2026-05-28 - system-spec-synthesis - Stage 1.3 preserve-mode-aware hard facts

- Agent: Codex
- Trigger: Owner specified Sub-Phase 1.3 requirements: do not strengthen repair, do not add placeholders/GPTZero/back-translation, and instead make hard fact extraction/checking preserve-mode-aware.
- Action: Opened and followed the skill to scope 1.3 as eval-only hard-fact narrowing plus deterministic exact/normalized/alias checking, leaving production rewrite behavior unchanged.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted owner requirements into implementation checkpoints: normalized date/money/percent/count/duration matching, exact acronym matching, alias matching, structured fact check rows, repair-row filtering, and rerun of the same 10 cases.
- Limitations: This log entry records planning for 1.3; implementation evidence is in the following dotnet-backend-testing entry.

### 2026-05-28 - dotnet-backend-testing - Stage 1.3 preserve-mode-aware hard fact checker

- Agent: Codex
- Trigger: Sub-Phase 1.3 implementation adds C# eval-tool behavior and tests for normalized hard-fact matching and repair-row filtering.
- Action: Opened and followed the skill; added xUnit tests for Chinese date equivalence, dropped-day failure, money equivalence/wrong amount, percent and duration equivalence, acronym exact preservation, alias matching, generic product-name failure, normalized-pass repair filtering, and Stage 1 hard-fact ledger narrowing. Implemented `StageOneHardFactLedgerBuilder`, `StageOneHardFactChecker`, structured `ZhFactCheckItem`, match-kind summaries, and additive Domain fields `ExactOrTranslatedAlias` / `AllowedAliases`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-105841-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused tests passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (18 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (289 tests). Real provider 10-case Stage 1.3 run used Youdao calls 10 and DeepSeek calls 40; hard facts narrowed to 111, final hard-fact preservation was 80/111, and final structured claim preservation stayed 92/92.
- Limitations: Alias generation/classification for person, organization, product, and system names is not implemented. Remaining failures are more explainable but not all resolved; the next likely target is approved alias handling rather than stronger repair prompts.

### 2026-05-28 - system-spec-synthesis - Stage 1.4 approved alias and failure classification

- Agent: Codex
- Trigger: Owner scoped Sub-Phase 1.4 as failure attribution plus approved alias boundaries, with explicit non-goals: no placeholders, GPTZero, English back-translation, production rewrite-path edits, or stronger Chinese repair prompt.
- Action: Opened and followed the skill to turn the requirements into implementation checkpoints: `FactFailureKind`, approved/proposed alias separation, failure breakdown reporting, repair-row routing, hard-fact demotion, and rerun of the locked 10-case set.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted the owner requirements into a bounded eval-only plan before implementation; implementation and provider evidence are recorded in the following dotnet-backend-testing entry.
- Limitations: This entry records planning only. It did not create a production alias catalog or real TermLedger.

### 2026-05-28 - dotnet-backend-testing - Stage 1.4 approved alias and failure classification

- Agent: Codex
- Trigger: Sub-Phase 1.4 implementation adds C# eval-tool behavior and xUnit coverage for failure classification, approved alias boundaries, and repair filtering.
- Action: Opened and followed the skill; added focused tests for approved alias pass, proposed/unapproved alias review, generic entity failure, acronym exact translation failure, person-name alias review, Chinese numeral duration normalizer gaps, weekday equivalents, over-extracted phrase handling, capitalized non-name demotion, failure breakdown rendering, and non-actionable repair filtering. Implemented `ZhFactFailureKind`, `RecommendedNextAction`, `ProposedAliases`, failure breakdown report output, repair report filtering, weekday normalization, and Stage 1 hard-fact demotion for obvious capitalized non-name noise.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-114558-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (27 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (298 tests). Real provider 10-case Stage 1.4 run used Youdao calls 10 and DeepSeek calls 30; hard facts narrowed to 88, final hard-fact preservation was 76/88, final structured claim preservation was 94/94, and all remaining fact failures were `alias_not_approved`.
- Limitations: The approved alias catalog itself is not implemented; `ProposedAliases` are review-only and do not auto-pass. Next work should build a controlled alias catalog / glossary rather than strengthening Chinese repair.

### 2026-05-29 - system-spec-synthesis - Stage 1.5 approved alias catalog effect check

- Agent: Codex
- Trigger: Owner asked to continue to the next step and see the effect after Sub-Phase 1.4 showed all remaining hard-fact failures were `alias_not_approved`.
- Action: Opened and followed the skill to scope 1.5 as an eval-only approved alias catalog, with explicit non-goals: no LLM auto-approval, no placeholders, no GPTZero, no English back-translation, no production rewrite-path changes, and no stronger Chinese repair prompt.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted the next step into implementation checkpoints: approved aliases may pass, proposed/unapproved aliases remain review-only, and the same locked 10-case set must be rerun to compare failure breakdown against 1.4.
- Limitations: This entry records planning only. It does not introduce a persistent production glossary or user-managed alias approval UI.

### 2026-05-29 - dotnet-backend-testing - Stage 1.5 approved alias catalog effect check

- Agent: Codex
- Trigger: Sub-Phase 1.5 implementation adds C# eval-tool behavior and tests for an approved alias catalog.
- Action: Opened and followed the skill; added xUnit tests proving a known approved alias (`Jamie` -> `杰米`) becomes an allowed alias and that an unapproved proposed alias still does not auto-pass. Implemented `StageOneApprovedAliasCatalog` and wired it after Stage 1 hard-fact ledger building.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-123932-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (29 tests). Real provider 10-case Stage 1.5 run used Youdao calls 10 and DeepSeek calls 28; final hard-fact preservation improved to 87/88, final structured claim preservation was 93/93, and pass count improved to 9/10.
- Limitations: The catalog is eval-only and seeded only with aliases observed in the locked 10-case run. The remaining failure is a standalone count `30` extracted from `5:30 p.m.` and translated as `下午五点半`; that should be handled by time normalizer / count extractor tightening, not by alias approval or stronger Chinese repair.

### 2026-05-29 - system-spec-synthesis - Stage 1.6 time normalizer and count extractor tightening

- Agent: Codex
- Trigger: Owner asked to do the next step: time normalizer / count extractor tightening after Sub-Phase 1.5 left only `30` from `5:30 p.m.` as a hard-fact failure.
- Action: Opened and followed the skill to scope 1.6 narrowly: fix the eval-only Stage 1 hard-fact builder so clock-minute fragments are not treated as standalone Count facts, without changing repair prompts, placeholders, GPTZero, English back-translation, or the production rewrite path.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted the next step into implementation checkpoints: add a RED test for filtering clock-minute count fragments while preserving real quantities, implement Stage 1-only filtering, rerun focused tests, and rerun the locked 10-case provider set.
- Limitations: This entry records planning only. It does not redesign the production `FactLedgerExtractor` or add a full time entity model.

### 2026-05-29 - dotnet-backend-testing - Stage 1.6 time normalizer and count extractor tightening

- Agent: Codex
- Trigger: Sub-Phase 1.6 implementation changes C# eval-tool hard-fact filtering and tests for clock-minute count artifacts.
- Action: Opened and followed the skill; added a focused xUnit test proving `30` in `5:30 p.m.` is filtered while ordinary quantities such as `18 seats` are preserved. Implemented Stage 1-only clock-minute detection in `StageOneHardFactLedgerBuilder`.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-124707-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD test failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (30 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (301 tests). Real provider 10-case Stage 1.6 run used Youdao calls 10 and DeepSeek calls 26; final hard-fact preservation was 86/86, final failure breakdown was `none`, and all 10 cases passed.
- Limitations: The tightening is eval-only. It filters count facts whose only source occurrence is the minute component of a clock time; it does not add a full `Time` fact type or a production glossary.

### 2026-05-29 - dotnet-backend-testing - Stage 1.7 30-case generalization check

- Agent: Codex
- Trigger: Owner asked to expand from the fixed 10-case set to about 30 wider samples to validate whether the approved alias catalog and count/time tightening generalize.
- Action: Opened and followed the skill; ran a 30-case Stage 1 provider baseline, added focused xUnit coverage for filtering clock-hour count fragments and wider capitalized non-entity words, implemented eval-only hard-fact builder tightening, and updated report rendering so unrepaired failing cases show final drift details.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-130115-stage1-en-zh-pilot.md`; `docs/rewrite-eval-results/20260528-131237-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (33 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (304 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case rerun used Youdao calls 30 and DeepSeek calls 80; final hard-fact failures dropped from 53 to 35, final `value_changed` failures dropped from 2 to 0, and final ClaimLedger preservation was 100% for all 30 cases.
- Limitations: The approved alias catalog remains eval-only and seeded from the locked 10-case set. The 30-case remainder is all `alias_not_approved`, split between real entity aliases and over-extracted single-token entity fragments; no production glossary, placeholders, GPTZero, English back-translation, or stronger Chinese repair prompt was added.

### 2026-05-29 - dotnet-backend-testing - Stage 1.8 hard-fact placement and entity merge

- Agent: Codex
- Trigger: Owner approved the next step after the 30-case expansion showed remaining failures were alias/placement problems rather than Chinese repair problems.
- Action: Opened and followed the skill; added focused xUnit coverage for merging title-case entity fragments, preserving salutation person names, demoting non-entity capitalized words, routing unmatched exact-or-alias entities to alias review, and keeping merged person names as person facts. Implemented eval-only title-case fragment merge and refined exact-or-alias failure classification.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-133028-stage1-en-zh-pilot.md`; `docs/rewrite-eval-results/20260528-133915-stage1-en-zh-pilot.md`; `docs/rewrite-eval-results/20260528-135002-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (37 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (308 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case final rerun used Youdao calls 30 and DeepSeek calls 82; hard facts narrowed to 243, final hard-fact failures dropped to 26, and every remaining final failure was `alias_not_approved` routed to `approve_alias`.
- Limitations: This remains eval-only. It does not create a production glossary, does not auto-approve aliases, and does not introduce placeholders, GPTZero, English back-translation, or stronger Chinese repair prompts. Role-like phrases such as `Senior Support Lead` still need a later TermLedger/translated-equivalent decision.

### 2026-05-29 - dotnet-backend-testing - Stage 1.9 controlled alias catalog

- Agent: Codex
- Trigger: Owner said to continue after Stage 1.8 left only approved-alias review failures on the 30-case set.
- Action: Opened and followed the skill; added focused xUnit coverage proving observed wide-sample person/place aliases pass only after explicit catalog approval, and role/title fragments are demoted from hard FactLedger. Expanded the eval-only alias catalog with reviewed 30-case aliases and kept role-like phrases out of hard facts.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-141031-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (39 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (310 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case run used Youdao calls 30 and DeepSeek calls 84; all 30 cases passed, final hard-fact failures were zero, and final ClaimLedger preservation was 100% for all cases.
- Limitations: Alias approval is still eval-only and case-derived. It does not create a production glossary, alias provenance model, domain scoping, user review UI, placeholders, GPTZero, English back-translation, or stronger Chinese repair prompt.

### 2026-05-29 - dotnet-backend-testing - Stage 1.10 structured alias glossary

- Agent: Codex
- Trigger: Owner asked to continue after Stage 1.9 proved the controlled alias catalog can make the 30-case set pass.
- Action: Opened and followed the skill; added focused xUnit coverage for structured alias entries, unapproved/proposed alias routing, and domain-scope isolation. Replaced the eval-only flat alias dictionary with `StageOneAliasEntry` records carrying alias language, provenance source, approval state, and domain scope while preserving the existing 30-case aliases.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-142745-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (41 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (312 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case run used Youdao calls 30 and DeepSeek calls 82; all 30 cases passed, final fact summary remained 241/241 preserved, and final failure breakdown was none.
- Limitations: The structured glossary remains eval-only and in-memory. It does not add a persistent production glossary store, tenant/domain management UI, migration, placeholders, GPTZero, English back-translation, or stronger Chinese repair prompt.

### 2026-05-31 - cloud-architecture-cost-review - Entra External ID tenant clarification

- Agent: Codex
- Trigger: Owner asked why the local Azure CLI tenant `53d7668e-c994-4634-8c4d-ff116a03c0b9` could not be used instead of the configured CIAM tenant, and whether a new tenant was required.
- Action: Opened and followed the skill for an identity architecture/cost gate only; read `docs/manual-setup.md` and `docs/next-development-brief.md`, checked the Azure CLI default tenant and tenant list, then used a read-only Microsoft Graph token for the configured CIAM tenant to inspect the frontend app registration.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: `az account show` reported default tenant `53d7668e-c994-4634-8c4d-ff116a03c0b9`; `az account tenant list` also showed the configured CIAM tenant `614ea821-6ef3-43e2-8613-d4b13fae115d`; Microsoft Graph read of app id `02ffae8e-3d30-42d0-86cd-9b858ab33252` returned display name `Reply In My Voice Frontend`, `isFallbackPublicClient: true`, production/local callback URLs, and beta `nativeAuthenticationApisEnabled: all`.
- Limitations: User-flow and identity-provider reads failed without delegated Graph permissions such as `IdentityUserFlow.Read.All` and `IdentityProvider.Read.All`, so email/password, email OTP, reset, and Google provider status still require portal confirmation or additional Graph consent. No Azure or Entra resources were created or modified.

### 2026-05-31 - cloud-architecture-cost-review - CIAM tenant readiness for email auth E2E

- Agent: Codex
- Trigger: Owner asked whether the existing `info@timeawake.co.nz` accessible tenant can complete the remaining Entra External ID setup and when email-code/logout/reset end-to-end testing can run.
- Action: Opened and followed the skill for an identity architecture/cost gate only; used read-only Graph and native-auth probes against the configured CIAM tenant without creating or modifying resources.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Microsoft Graph `/me` in tenant `614ea821-6ef3-43e2-8613-d4b13fae115d` returned `info_timeawake.co.nz#EXT#@replyinmyvoicecustomers.onmicrosoft.com` as `Member`; `/me/memberOf` returned `Global Administrator`; `/organization` returned display name `Reply In My Voice Customers` and tenant type `CIAM`. Direct native-auth sign-in/reset probes for nonexistent addresses returned `user_not_found` instead of client/tenant/native-auth configuration errors. The production logout endpoint returned 307 to `/` and cleared `rimv_session`, `rimv_oauth`, and access-token cookies.
- Limitations: Registration start was intentionally not triggered because it sends a real verification email. User-flow and identity-provider portal screens were not changed.

### 2026-05-31 - ui-browser-testing - Production auth pages readiness check

- Agent: Codex
- Trigger: Owner asked when email verification, logout, and related auth flows can be tested end to end.
- Action: Opened and followed the skill; inspected the production sign-up, sign-in, and forgot-password pages with Playwright and used non-email-sending API probes for signed-out/native-auth behavior.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Production `/sign-up`, `/sign-in`, and `/forgot-password` each loaded with HTTP 200 and visible email fields; Playwright confirmed page titles and form visibility. Production `/api/auth/signin` with a unique nonexistent address returned `404 {"error":"user_not_found","ok":false}`. Production `/api/auth/reset/start` with a unique nonexistent address returned `404 {"error":"No account for this email. Please create one.","ok":false}`. Browser request failures observed were expected Cloudflare RUM/navigation aborts, not app route failures.
- Limitations: The real sign-up verification-code flow, reset-code flow, authenticated `/app`, and sign-out button flow still require a live mailbox code from the owner and were not completed in this check.

### 2026-05-31 - ui-browser-testing - Dynamic auth E2E workflow completion

- Agent: Codex
- Trigger: Owner asked to run Entra External ID email/password auth E2E through a dynamic GitHub issue workflow with live code handoff, issue closure, and a sentinel monitor.
- Action: Opened and followed the skill; verified production sign-up, `/app` session creation, logout signed-out gate, email/password re-login, forgot-password reset-code completion, and new-password sign-in. Used GitHub issues #356-#362 as the workflow ledger, fixed the discovered CIAM issuer session-minting bug through PR #363, and stopped the heartbeat sentinel after all workflow issues closed.
- Output artifacts: GitHub issues #356-#362; PR #363; `docs/skill-run-log.md`.
- Verification evidence: #357, #358, #359, #360, #361, #356, and #362 were closed with evidence comments. PR #363 merged to main as `a84023a43ede6f04bb891ce2927f4eac6c9e65e0`; main Cloudflare workflow `26707889589` completed build-test and deploy successfully. Runtime verification showed sign-in HTTP 302 followed by `/app` HTTP 200 with a workspace marker, logout HTTP 307 followed by `/app` HTTP 307 to `/sign-in?redirectTo=%2Fapp`, re-login HTTP 302 followed by `/app` HTTP 200, reset verify HTTP 200 with `ok: true`, and new-password sign-in HTTP 302 followed by `/app` HTTP 200.
- Limitations: The run used one owner-accessible test email alias and owner-provided verification codes. Codes, temporary passwords, cookies, and tokens were intentionally not written to GitHub or docs.

### 2026-05-31 - ui-browser-testing - Auth module completion status audit

- Agent: Codex
- Trigger: Owner asked whether the registration/login module was complete and what auth coverage remained untested.
- Action: Opened and followed the skill; rechecked GitHub issue closure, the merged CIAM issuer fix PR, the successful main Cloudflare deployment, and the existing auth unit/E2E test coverage.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: GitHub issues #356-#362 were all `CLOSED` with `COMPLETED` state reason and `verified` label. PR #363 was merged to `main` as `a84023a43ede6f04bb891ce2927f4eac6c9e65e0`. Main Cloudflare workflow `26707889589` completed successfully. Auth coverage was present in `tests/e2e/auth-gate.spec.ts`, `tests/e2e/auth-forgot.spec.ts`, `tests/unit/auth-signup-routes.test.ts`, `tests/unit/auth-signin-route.test.ts`, `tests/unit/auth-reset-routes.test.ts`, `tests/unit/entra-native-auth.test.ts`, `tests/unit/entra-auth.test.ts`, and `tests/unit/auth-rate-limit.test.ts`.
- Limitations: This was a status audit, not a new browser run. Remaining optional coverage includes Google OAuth live E2E, live resend-code paths, live negative-code/password-policy cases, production rate-limit behavior, mobile visual screenshots, session-expiry behavior, cross-browser coverage, and account deletion if that is considered part of auth acceptance.

### 2026-05-31 - ui-browser-testing - Remaining auth coverage issue planning

- Agent: Codex
- Trigger: Owner asked to discuss, before starting, converting the remaining auth coverage gaps into GitHub issues supervised by subagents and a sentinel, with real frontend testing through Playwright MCP.
- Action: Opened and followed the skill for planning only; no GitHub issues, browser runs, account deletions, or sentinels were started.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Proposed a production-focused issue breakdown for Google OAuth, account cleanup, resend paths, negative auth cases, mobile screenshots, session behavior, rate-limit smoke, and cleanup/ledger closure. The plan requires Playwright MCP/browser evidence, console/network review, issue evidence comments, and no secrets or one-time codes in GitHub.
- Limitations: Planning only. Execution awaits owner approval and clarification on whether the `chuanqiao1128@gmail.com` Google test user should remain registered after the flow or be cleaned up.

### 2026-05-31 - cloud-architecture-cost-review - Temporary SQL access for auth cleanup

- Agent: Codex
- Trigger: Owner approved starting the remaining auth E2E workflow, which required deleting a pre-existing Google test account and checking Azure SQL-backed app state.
- Action: Opened and followed the skill; compared the zero-cost temporary firewall/read-only verification path against unsafe direct production mutations. Added a single temporary Azure SQL firewall rule for the current IP, used it to verify and erase only the old test account through project `AccountService`, then removed the rule.
- Output artifacts: GitHub issue #365; `docs/skill-run-log.md`.
- Verification evidence: Temporary rule `codex-auth-e2e-20260531` was created for the current IP and later deleted. Backend lookup found one matching app user with no Stripe subscription; project `AccountService.DeleteAccountAsync` erased that state with postcheck active old external-auth id count 0. CIAM user delete returned 204, permanent deleted-item delete returned 204, and post-delete lookup returned 404.
- Limitations: The temporary SQL access was used only for this cleanup check. No Azure resources were created or resized, no fixed monthly cost was added, and no connection strings, passwords, tokens, or one-time codes were logged.

### 2026-05-31 - dotnet-backend-testing - Account deletion retry-strategy fix

- Agent: Codex
- Trigger: The auth cleanup exposed a production-path `.NET` bug: `AccountService.DeleteAccountAsync` failed when SQL Server retrying execution strategy was enabled around a user-initiated transaction.
- Action: Opened and followed the skill; added a failing xUnit regression test using a retrying EF execution strategy, then wrapped the account-erasure transaction in `Database.CreateExecutionStrategy().ExecuteAsync`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: New focused test failed before the fix with `The configured execution strategy 'TestExecutionStrategy' does not support user-initiated transactions`; after the fix, the focused test passed. `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter "FullyQualifiedName~AccountServiceTests"` passed 9 tests. `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo` passed 397 tests.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-05-31 - ui-browser-testing - Remaining auth coverage dynamic workflow execution

- Agent: Codex
- Trigger: Owner approved starting the remaining auth coverage workflow with GitHub issues, subagent-style/sentinel oversight, and real frontend testing through Playwright MCP/browser automation.
- Action: Opened and followed the skill; executed GitHub issue workflow #364-#375 with production browser checks for Google OAuth self-service sign-up, Google logout/re-login, email sign-up and reset resend paths, negative auth cases, desktop/mobile screenshots, session persistence, protected-route redirects, and bounded rate-limit smoke.
- Output artifacts: GitHub issues #364-#375; PR #376; local screenshot artifacts under `/tmp/rimv-auth-visual-20260531`; `docs/skill-run-log.md`.
- Verification evidence: #365-#373 were closed with evidence comments. Google auth used the correct Entra branch `Use another account -> Sign in with Google -> attribute collection` and reached `/app`. Email sign-up/reset resend flows reached cooldown and resend states, completed with owner-supplied codes, and post-reset email login reached `/app`. Negative tests showed friendly errors for wrong password, weak sign-up value, wrong verification code, and stale flow-cookie calls. Desktop and mobile auth screenshots had no horizontal overflow, clipped controls, unexpected console errors, or failed requests. Session checks preserved `/app` across refresh/navigation and redirected signed-out `/app` to `/sign-in?redirectTo=%2Fapp`. Rate-limit smoke kept normal auth pages at HTTP 200 and returned bounded invalid sign-in attempts as HTTP 401, not 500.
- Limitations: Owner-provided one-time codes and temporary sign-in values were intentionally not recorded in GitHub, docs, or logs. Cross-browser coverage beyond Chromium/Chrome was not part of this issue set.

### 2026-05-31 - cloud-architecture-cost-review - Final auth E2E cleanup verification

- Agent: Codex
- Trigger: Final cleanup for the remaining auth coverage workflow required verifying Azure SQL-backed app erasure without leaving paid or long-lived infrastructure changes.
- Action: Reused the cost-gated temporary-access pattern: deleted test account state through the app UI/account API, hard-deleted matching CIAM users through Microsoft Graph, created a single temporary Azure SQL firewall rule only for cleanup verification, queried for target app-user remnants, and removed the firewall rule.
- Output artifacts: GitHub issue #374; `docs/skill-run-log.md`.
- Verification evidence: App account deletion UI returned to the public home page without delete errors for the verified email alias and the Google test account. Microsoft Graph active deletes and deleted-item hard deletes returned 204 for the matching CIAM users; the post-delete target-user query returned an empty list. Azure SQL verification returned `target_original_rows 0` for the test emails/original Entra object ids and `recent_erased_rows 3` for recent anonymized app-user rows.
- Limitations: The temporary SQL access was only used for cleanup verification and no secrets or connection strings were logged. An older pre-existing `info+rimv-e2e-20260531@timeawake.co.nz` CIAM test user was observed but not touched because it was outside this owner-approved Gmail cleanup scope.

### 2026-06-02 - system-spec-synthesis - Promo-code trial design (replace auto free-3)

- Agent: Claude Code (supervisor, interactive).
- Trigger: Owner asked to replace the automatic 3-free-rewrites model with a redeemable universal promo code (alphanumeric, expires end of Aug 2026), and to first capture the design in an MD doc on a new branch without writing code. This converts loose product notes into an implementation-ready engineering plan (system-spec-synthesis), and also applied the state-machine-modeling and data-module-review lenses for the redemption lifecycle and the new persistence tables.
- Action: Opened and followed the skill's output contract. Grounded the design by reading the live C#/Azure SQL backend (no code changed): `AccountService.GetUsagePlan`/`GetOrCreateAccountSummaryAsync`, `AdminService.GrantCreditsAsync`, `QuotaService` credit-overflow consumption, `RewriteCredit` entity, `AppDbContext` conventions, Functions HTTP route pattern, and the frontend copy + contract tests. Confirmed no existing promo/coupon code and that the C# `Referral` entity is vestigial. Authored a full spec (Context/Goals/Non-Goals/Current System/Architecture/Data Model/API & Job Contracts/State & Error Handling/Security & Privacy/Rollout/Verification/Open Questions + copy & interaction design + Codex implementation checkpoints). Created branch `feat/promo-code-trial`.
- Output artifacts: `plans/promo-code-trial-spec.md`; branch `feat/promo-code-trial`; `docs/skill-run-log.md`.
- Verification evidence: Design only — no code, tests, migrations, or deploys executed. Key architecture claims verified against live source (free "3" is a `UsagePeriod.QuotaLimit`, not a credit; `/api/me` already sums credits into `remaining` with per-source `ExpiresInDays`; redemption can reuse the `RewriteCredit{Source="PROMO"}` grant shape so the consumption/paywall/quota-race path is unchanged). Confirmed no driver/codex process was running and the payment wave was `WAVE_DONE` before branching, to avoid a shared-checkout collision.
- Limitations: Planning only; implementation of source changes is delegated to Codex and awaits owner confirmation of the open inputs (actual code value, exact expiry instant/timezone, granted-credit TTL, global cap, distribution channel). Honest constraint documented in the spec: a single universal code cannot be fully abuse-proofed; per-user-unique codes are the real upgrade path the schema already supports.

### 2026-06-02 - state-machine-modeling - PROMO-02 redemption lifecycle

- Agent: Codex
- Trigger: GitHub issue #428 changes the promo-code and per-user redemption lifecycle with terminal success, duplicate, expired, exhausted, and IP-velocity outcomes.
- Action: Opened and followed the skill; modeled `PromoCode` as redeemable only when active, within the valid window, and below the global cap, and modeled `PromoCodeRedemption` as none -> applied with duplicate apply rejected by the unique index.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoServiceTests --no-restore` passed 11 tests; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 412 tests.
- Limitations: PROMO-02 implements service-level redeem/status only; HTTP endpoint mapping and `/api/me` promo block are left to PROMO-03.

### 2026-06-02 - data-module-review - PROMO-02 promo persistence invariants

- Agent: Codex
- Trigger: GitHub issue #428 changes EF-backed promo redemption persistence, usage-credit grants, idempotency, and global cap accounting.
- Action: Opened and followed the skill; reviewed the existing `PromoCode`, `PromoCodeRedemption`, `RewriteCredit`, `AppDbContext`, `AccountService`, `AdminService.GrantCreditsAsync`, `QuotaService`, and `StripeEventService` transaction patterns before implementing the service.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: New tests assert one `RewriteCredit{Source="PROMO"}` per successful redemption, one applied redemption per user/code, exact `RedemptionCount` under parallel cap pressure, salted IP hash storage, and no second grant on duplicate redeem. Full backend suite passed with 412 tests.
- Limitations: No schema or migration changes were made in PROMO-02 because PROMO-01 already supplied the entities, indexes, and checks.

### 2026-06-02 - resilience-test-generation - PROMO-02 duplicate and race coverage

- Agent: Codex
- Trigger: GitHub issue #428 requires idempotent redemption, concurrent same-user replay protection, exact global-cap behavior under parallel load, and IP velocity handling.
- Action: Opened and followed the skill; wrote SQLite integration tests that assert final persisted state across duplicate calls, same-user parallel calls, N+ parallel cap attempts, IP velocity block/flag behavior, and production missing trusted IP fail-closed behavior.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-02 test class passed 11 tests; full backend suite passed 412 tests. `git diff --check` passed. Changed backend files had no forbidden-substring matches.
- Limitations: SQLite file-backed WAL fixtures were used for local race tests; production SQL Server still relies on its retrying execution strategy and the same atomic conditional update.

### 2026-06-02 - dotnet-backend-testing - PROMO-02 xUnit/SQLite coverage

- Agent: Codex
- Trigger: GitHub issue #428 adds C#/.NET backend service behavior and requires xUnit + SQLite acceptance coverage.
- Action: Opened and followed the skill; added `PromoServiceTests` using existing xUnit, FluentAssertions, EF Core SQLite, deterministic clocks, and local logger capture.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused run failed before implementation because `PromoService`/result types were missing; after implementation, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoServiceTests --no-restore` passed 11 tests and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 412 tests.
- Limitations: A local git commit was attempted but blocked by sandbox write access to the parent repository's worktree metadata; no push or PR was attempted.

### 2026-06-02 - system-spec-synthesis - PROMO-03 endpoint and account summary contract

- Agent: Codex
- Trigger: GitHub issue #429 and `plans/promo-issues/PROMO-03-user-endpoints.md` require API contract implementation for `POST /api/promo/redeem`, optional status, and `/api/me` promo summary data.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, `plans/promo-issues/PROMO-03-user-endpoints.md`, and `plans/promo-code-trial-spec.md` sections 4, 12, and 13. Converted the requirements into implementation checkpoints: thin HTTP mapper over `PromoService`, proxy-secret guarded IP extraction only, stable promo error codes, unchanged credit consumption math, `/api/me` promo block, and WebApplicationFactory acceptance coverage.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-03 acceptance tests passed 13 tests; full `dotnet test backend-dotnet/ReplyInMyVoice.sln --logger "console;verbosity=normal"` passed 425 tests.
- Limitations: The ASP.NET Core API route mirror was added so existing WebApplicationFactory tests can assert the same contract; the deployed Azure Functions route is also implemented in `PromoHttpFunctions`.

### 2026-06-02 - state-machine-modeling - PROMO-03 redemption HTTP outcomes

- Agent: Codex
- Trigger: PROMO-03 exposes the redemption lifecycle over HTTP and must preserve duplicate, invalid, expired, exhausted, velocity, config-error, and success transitions from `PromoService`.
- Action: Opened and followed the skill; modeled `PromoCode` availability as active-window-cap derived state and `PromoCodeRedemption` as none -> applied, with duplicate apply rejected by the unique index and mapped to `already_redeemed`. Locked illegal/terminal outcomes in endpoint tests instead of adding new state fields.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `PromoApiTests` asserts 401, 400, 403, 422 invalid, 422 expired, 409 already, 409 exhausted, 429, 500 server_config, and 200 success mappings; the full backend suite passed 425 tests.
- Limitations: No new persisted states were added; PROMO-03 only exposes and summarizes the existing PROMO-02 lifecycle.

### 2026-06-02 - data-module-review - PROMO-03 promo summary and credit-label invariants

- Agent: Codex
- Trigger: PROMO-03 changes EF-backed account summary reads, active promo credit accounting, and per-source labels.
- Action: Opened and followed the skill; reviewed `AccountService`, `PromoService`, promo entities, `RewriteCredit`, and `AppDbContext` invariants. Ran `scan_data_risks.py --limit 40 backend-dotnet/src/ReplyInMyVoice.Infrastructure`; implemented read-only promo summary derivation from `PromoCodeRedemptions` plus active `RewriteCredit{Source="PROMO"}` rows and mapped the PROMO source label to `Trial rewrites`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Account service/API tests assert `promo.hasRedeemed`, `eligible`, `trialRemaining`, `trialExpiresAt`, and `Trial rewrites`; full `dotnet test` passed 425 tests.
- Limitations: The scan reported broad pre-existing quota/idempotency signals in infrastructure and migrations; no schema changes were made for PROMO-03.

### 2026-06-02 - resilience-test-generation - PROMO-03 HTTP failure matrix

- Agent: Codex
- Trigger: PROMO-03 tests authentication failures, malformed payloads, proxy/Turnstile gate behavior, duplicate redemption, global-cap exhaustion, IP velocity, and server config failure paths.
- Action: Opened and followed the skill; generated a `promo-redeem` resilience matrix and implemented deterministic WebApplicationFactory tests for malformed JSON, missing verified Turnstile token, repeated requests, exhausted cap, five-redemption IP velocity block, missing IP hash salt, and trusted-IP/XFF handling.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-03 test command passed 13 tests; full backend suite passed 425 tests. Changed backend files had no forbidden-substring matches.
- Limitations: The 403 test represents the backend/API mirror rejecting a request without a verified Turnstile token; the future Next.js proxy issue still owns live Cloudflare Turnstile verification and forwarding.

### 2026-06-02 - dotnet-backend-testing - PROMO-03 WebApplicationFactory endpoint coverage

- Agent: Codex
- Trigger: PROMO-03 requires C#/.NET WebApplicationFactory integration tests for every redeem status code path and `/api/me` promo block serialization.
- Action: Opened and followed the skill; wrote failing WebApplicationFactory and service tests first, then implemented the Functions endpoint, ASP.NET Core API mirror, and account summary changes. Reused xUnit, FluentAssertions, EF Core SQLite, and project header-auth test conventions. Added host-builder reload guards to touched API test classes to avoid local host-factory hangs.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused tests failed before implementation because `AccountSummary.Promo` and `/api/promo/redeem` were missing. After implementation, `timeout 180 dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~PromoApiTests|FullyQualifiedName~AccountSummaryIncludesPromoBlockAndTrialCreditLabel|FullyQualifiedName~Me_includes_promo_block_and_trial_credit_label" --logger "console;verbosity=normal"` passed 13 tests, and full `dotnet test backend-dotnet/ReplyInMyVoice.sln --logger "console;verbosity=normal"` passed 425 tests.
- Limitations: A local git commit was attempted but blocked by sandbox write access to `/Users/qc/Desktop/CloudFlare/.git/worktrees/issue-429/index.lock`; no push or PR was attempted.

### 2026-06-02 - system-spec-synthesis - PROMO-04 admin promo API contract

- Agent: Codex
- Trigger: GitHub issue #430 and `plans/promo-issues/PROMO-04-admin-endpoints.md` require implementation-ready admin API contracts for promo create/list/detail/update/disable/enable.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, the PROMO-04 brief, and `plans/promo-code-trial-spec.md` section 7. Converted the requirements into implementation checkpoints: reuse `AdminAccess.RequireAdminAsync`, keep backend-only scope, normalize/store promo codes through the existing EF entities, write `AdminAuditLog` for every promo mutation, and expose aggregate stats with IP hash clusters only.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-04 tests passed 5 tests with `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter AdminPromoTests --no-restore`; full `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 430 tests.
- Limitations: This issue implemented backend endpoints only; the admin UI remains owned by PROMO-10.

### 2026-06-02 - state-machine-modeling - PROMO-04 admin active-state transitions

- Agent: Codex
- Trigger: PROMO-04 changes admin-controlled promo-code lifecycle transitions for disable and enable, while list/detail status is derived from active flag, validity window, and global cap.
- Action: Opened and followed the skill; modeled `PromoCode` status as pending/active/expired/exhausted/disabled derived state and implemented disable/enable as explicit `IsActive` transitions with audit rows. Kept redemptions and credits unchanged by admin list/detail/stat reads.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `AdminPromoTests` asserts non-admin rejection, create, duplicate rejection, disable, and stats payload behavior; full backend suite passed 430 tests.
- Limitations: No new persisted enum state was added because the promo spec defines admin status as derived from existing columns.

### 2026-06-02 - data-module-review - PROMO-04 promo admin persistence and audit

- Agent: Codex
- Trigger: PROMO-04 changes EF-backed promo-code administration, validation, audit persistence, and redemption-stat queries.
- Action: Opened and followed the skill; reviewed `PromoCode`, `PromoCodeRedemption`, `RewriteCredit`, `AdminAuditLog`, `AppDbContext`, `PromoService`, and existing admin audit patterns. Implemented service-layer validation mirroring promo CHECK constraints, duplicate-code rejection before save plus unique-index fallback, audit JSON containing `promoCodeId` and changed field names, and stats derived from applied redemptions plus linked credit consumption.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `scan_data_risks.py --limit 40` ran and reported broad pre-existing quota/idempotency signals, with no PROMO-04-specific blocker found. Focused tests and full backend suite passed.
- Limitations: `AdminAuditLog` has no dedicated promo-code foreign-key column, so PROMO-04 records `promoCodeId` inside `DetailsJson` to reuse the existing audit table without a schema change.

### 2026-06-02 - dotnet-backend-testing - PROMO-04 admin endpoint integration tests

- Agent: Codex
- Trigger: PROMO-04 requires C#/.NET integration tests for admin auth, create/audit, duplicate handling, disable persistence, and privacy-safe stats.
- Action: Opened and followed the skill; wrote failing xUnit tests first against `AdminHttpFunctions`, then implemented `PromoAdminService` and the six admin promo routes. Reused existing `DbFixture`, xUnit, FluentAssertions, EF Core SQLite, and direct Functions invocation patterns.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused test run failed before implementation because `CreatePromoCode`, `DisablePromoCode`, `GetPromoCodeDetail`, and response types were missing. After implementation, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter AdminPromoTests --no-restore` passed 5 tests and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 430 tests.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because the remote package vulnerability feed was unavailable, but restore/build/test completed with cached packages.

### 2026-06-02 - resilience-test-generation - PROMO-05 parallel redeem and failure matrix

- Agent: Codex
- Trigger: GitHub issue #431 and `plans/promo-issues/PROMO-05-concurrency-security-tests.md` require adversarial promo redemption tests for duplicate requests, parallel global-cap pressure, invalid code states, proxy trust handling, IP velocity, and validity boundaries.
- Action: Opened and followed the skill; identified `PromoService.RedeemAsync` as the critical operation and `PromoCode.RedemptionCount == COUNT(Applied redemptions)` plus one credit per applied redemption as the invariant. Covered duplicate same-user requests, cap=1 parallel users through `Task.WhenAll`, expired/disabled code states, untrusted proxy headers, production fail-closed behavior, same-IP velocity flag/block thresholds, and inclusive `ValidUntil`.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoConcurrencyTests --no-restore` passed 8 tests; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 438 tests.
- Limitations: This issue adds coverage only. No production code, schema, deploy, payment, or frontend files were changed.

### 2026-06-02 - state-machine-modeling - PROMO-05 promo redeem lifecycle checks

- Agent: Codex
- Trigger: PROMO-05 tests promo-code and promo-redemption lifecycle transitions under duplicate, invalid, expired, disabled, capped, and velocity-limited inputs.
- Action: Opened and followed the skill; modeled states as `PromoCode` pending/active/expired/exhausted/disabled derived from `IsActive`, validity window, and cap, and `PromoCodeRedemption` none -> applied with duplicate apply rejected. Events covered redeem request, duplicate redeem request, code disabled, code expired, cap reached, proxy trust missing, IP threshold reached, and boundary-time redeem. Invariants: an applied redemption creates exactly one PROMO credit, duplicate requests do not create a second credit, cap=1 cannot exceed one applied redemption, expired/disabled codes create no credit, and equality with `ValidUntil` remains redeemable.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new tests assert illegal transitions return `AlreadyRedeemed`, `InvalidCode`, `Expired`, `CapReached`, `IpVelocityBlocked`, or `ServerConfig` without extra credits; focused and full backend tests passed.
- Limitations: No new persisted state field or transition helper was added because PROMO-05 is a test-lock issue only.

### 2026-06-02 - data-module-review - PROMO-05 promo persistence invariants

- Agent: Codex
- Trigger: PROMO-05 reviews and test-locks EF-backed promo counters, redemptions, unique user/code behavior, IP hashes, and credit grants under concurrency.
- Action: Opened and followed the skill; reviewed `PromoCode`, `PromoCodeRedemption`, `RewriteCredit`, `AppUser`, `AppDbContext`, `PromoService`, `PromoApiTests`, and existing SQLite fixture patterns. Added file-backed SQLite tests for real parallel transactions and asserted persisted final state after each adversarial path.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Assertions cover `RewriteCredits`, applied `PromoCodeRedemptions`, `PromoCodes.RedemptionCount`, nullable `RedeemIpHash` for untrusted proxy input, and no user/credit/redemption rows on production config failure. Focused and full backend suites passed.
- Limitations: No schema or migration change was made; broad app/lib prohibited-term grep still reports pre-existing matches in `lib/rewrite-eval-cases.ts`, which this issue did not touch.

### 2026-06-02 - dotnet-backend-testing - PROMO-05 concurrency and security xUnit coverage

- Agent: Codex
- Trigger: PROMO-05 requires C#/.NET xUnit + SQLite/retrying execution-strategy coverage for backend concurrency and security cases.
- Action: Opened and followed the project skill; added `PromoConcurrencyTests` with xUnit, FluentAssertions, EF Core SQLite, file-backed SQLite for parallel service tests, and WebApplicationFactory for API proxy trust handling. Kept test helpers local to the new class and used generated per-test config values instead of fixed secret-like placeholders.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoConcurrencyTests --no-restore` passed 8 tests; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 438 tests; `git diff --check` passed; the new backend test file had no prohibited-term matches.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because the remote package vulnerability feed was unavailable, but restore/build/test completed with cached packages.

### 2026-06-02 - system-spec-synthesis - PROMO-06 free baseline consistency checkpoint

- Agent: Codex
- Trigger: GitHub issue #432 and `plans/promo-issues/PROMO-06-free-baseline-zero.md` require an implementation-ready free-baseline cutover plan across `/api/me`, `ReserveAsync`, EF migration, and account erasure.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-06 issue body, the PROMO-06 brief, and `plans/promo-code-trial-spec.md` sections 4 and 16.1. Converted the requirements into implementation checkpoints: free baseline default zero with `FREE_BASELINE_REWRITES` runtime override, preserve paid quota, document that `ReserveAsync` receives and applies the caller quota instead of trusting stale persisted free-period limits, add a forward-only data migration for `free:lifetime`, and extend account erasure to clear promo redemption IP hashes while retaining rows.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602091811_FreeBaselineZero.cs`; backend acceptance tests; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-06 test command passed 46 tests; full `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 445 tests after the generated migration pair was finalized.
- Limitations: PR description authoring is handled by the supervisor; the required finding is recorded in this log and final report for the supervisor to carry into the PR.

### 2026-06-02 - state-machine-modeling - PROMO-06 quota and promo-credit states

- Agent: Codex
- Trigger: PROMO-06 changes free quota, promo-credit availability, exhausted/paywall behavior, and account erasure privacy state.
- Action: Opened and followed the skill; modeled account quota states as free-baseline zero, promo credit available, promo credit exhausted, and paid unchanged. Events covered account summary read, rewrite reserve request, promo-credit consumption, old free-period row after migration, and account erase. Invariants: new inactive users have zero remaining without credit, stale free-period rows do not add remaining quota, reserve without credit creates no attempt/reservation/outbox, active PROMO credits are the only trial remaining source, exhausted PROMO credits set `Usage.Exhausted`, and erasure retains redemption records while nulling IP hash data.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Tests assert the allowed and rejected state transitions above; focused tests passed 46/46 and full backend tests passed 445/445.
- Limitations: No new persisted state enum was added; the state remains derived from existing usage-period counters, active credits, and promo redemption rows.

### 2026-06-02 - data-module-review - PROMO-06 UsagePeriods migration and promo erasure

- Agent: Codex
- Trigger: PROMO-06 changes EF migration data, usage counter invariants, and `PromoCodeRedemption` privacy handling during account deletion.
- Action: Opened and followed the skill; reviewed `UsagePeriod`, `RewriteCredit`, `UsageReservation`, `PromoCodeRedemption`, `AppDbContext`, `AccountService`, and `QuotaService` together. Added a generated EF migration pair with SQL-only `Up` that updates `UsagePeriods` rows where `PeriodKey='free:lifetime'` to `QuotaLimit=0`, refreshes `UpdatedAt`, and changes `RowVersion`; `Down` is empty for forward-only behavior. Ran `scan_data_risks.py --limit 40 backend-dotnet`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602091811_FreeBaselineZero.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602091811_FreeBaselineZero.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FreeBaselineMigrationTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `FreeBaselineMigrationTests` inspects migration operations for the free-row update and empty `Down`; full backend tests passed 445/445. Touched-file prohibited-term scan returned no matches.
- Limitations: The first scan invocation used multiple roots and returned a usage error; it was rerun successfully with the single `backend-dotnet` root. The scan reports broad pre-existing quota/idempotency signal rows by design.

### 2026-06-02 - resilience-test-generation - PROMO-06 no-credit and stale-row failure matrix

- Agent: Codex
- Trigger: PROMO-06 tests quota reservation behavior around stale free-period rows, no-credit requests, promo-credit availability, and exhausted promo credits.
- Action: Opened and followed the skill; treated `QuotaService.ReserveAsync` plus `/api/rewrite` as the critical operation and `no successful rewrite attempt without period quota or active credit` as the invariant. Added deterministic tests for no-credit rewrite rejection, old free period with two used rewrites rejecting the next request, promo credit preserving success paths, and exhausted promo credit setting account exhaustion.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-06 tests passed 46/46; full backend tests passed 445/445.
- Limitations: No live provider, payment, queue, or cloud dependency was called; tests use EF SQLite and local WebApplicationFactory paths.

### 2026-06-02 - dotnet-backend-testing - PROMO-06 backend acceptance coverage

- Agent: Codex
- Trigger: PROMO-06 requires C#/.NET tests for `/api/me`, `ReserveAsync`, EF migration behavior, promo-credit account summary, promo exhaustion, and account erasure.
- Action: Opened and followed the skill; wrote failing xUnit/WebApplicationFactory/EF SQLite tests before production changes, then implemented the account service, rewrite caller wiring, EF migration, and adjusted legacy credit-total expectations. Reused xUnit, FluentAssertions, EF Core SQLite, WebApplicationFactory, and existing local fixture patterns.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FreeBaselineMigrationTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminCreditAdjustTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused test run failed before implementation because `GetUsagePlan` lacked a configuration overload; after implementation, focused tests passed 46/46 and full `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 445/445.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because the remote package vulnerability feed was unavailable, but restore/build/test completed with cached packages.

### 2026-06-02 - system-spec-synthesis - PROMO-07 redeem proxy contract

- Agent: Codex
- Trigger: GitHub issue #433 and `plans/promo-issues/PROMO-07-redeem-proxy.md` define a new Next.js BFF API route contract for promo redemption, Turnstile verification, trusted Cloudflare IP forwarding, and runtime secret validation.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-07 issue body, the PROMO-07 brief, and `plans/promo-code-trial-spec.md` sections 8.1, 8.3, and 13. Converted the requirements into checkpoints: same-origin POST, `{code, turnstileToken}` parsing, Turnstile `siteverify` with `cf-connecting-ip`, runtime `TURNSTILE_SECRET_KEY` and `PROMO_PROXY_SHARED_SECRET` checks, Azure bearer forwarding, and response pass-through.
- Output artifacts: `app/api/promo/redeem/route.ts`; `lib/turnstile.ts`; `lib/auth-rate-limit.ts`; `tests/unit/promo-redeem-route.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red test first failed because `app/api/promo/redeem/route.ts` did not exist; after implementation, `npm run test -- tests/unit/promo-redeem-route.test.ts` passed 7/7, `npm run typecheck` passed, and full `npm run test` passed 302/302.
- Limitations: The optional promo status proxy was not implemented because PROMO-07 marks it optional and acceptance is fully covered by the redeem route. No UI, backend, payment, deployment, or secret file was changed.

### 2026-06-02 - resilience-test-generation - PROMO-07 Turnstile and fail-closed proxy tests

- Agent: Codex
- Trigger: PROMO-07 changes a provider-dependent route and requires tests for blocked/missing Turnstile tokens, production missing secrets, and trusted IP/secret forwarding.
- Action: Opened and followed the skill; treated `/api/promo/redeem` as the critical operation and `no Azure redeem call unless same-origin, configured, captcha-verified, and authenticated` as the invariant. Added deterministic local fetch fakes for Turnstile success and rejection, asserted missing token rejection, asserted production missing secret errors happen before provider calls, and asserted forwarded `X-Client-IP` comes from `cf-connecting-ip` even when `x-forwarded-for` is present.
- Output artifacts: `tests/unit/promo-redeem-route.test.ts`; `app/api/promo/redeem/route.ts`; `lib/turnstile.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused route tests passed 7/7 and full Vitest passed 302/302 with no live Turnstile, Azure, database, or payment dependency calls.
- Limitations: No retry policy was added; provider failure is fail-closed as `invalid_captcha`, matching the issue's proxy gate behavior.

### 2026-06-02 - state-machine-modeling - PROMO-08 redeem UI quota states

- Agent: Codex
- Trigger: PROMO-08 changes `/app` branching for no-redemption, active trial-credit, exhausted trial, and paid quota states.
- Action: Opened and followed the skill; modeled the UI state entity as the `/api/me` account summary. States: new signed-in account with zero remaining and no promo redemption; active promo trial credit; redeemed and exhausted promo trial; paid account with current quota; paid account with exhausted monthly quota. Events: account summary read, successful redeem, redeem error, trial credit consumption, paid quota exhaustion. Allowed transitions: new zero-quota account -> redeem card; redeem success -> workspace; trial credit remaining -> workspace; redeemed zero remaining -> buy paywall; paid exhausted -> billing-management paywall. Illegal transitions: new zero-quota account -> buy paywall; paid exhausted account -> forced redeem card; error response -> workspace. Invariants: trial display uses 3 as the grant size, PROMO source is labeled `Trial rewrites`, and the universal code value is never rendered.
- Output artifacts: `lib/promo-app-state.ts`; `tests/unit/promo-app-state.test.ts`; `app/app/page.tsx`; `docs/skill-run-log.md`.
- Verification evidence: `npm run test -- tests/unit/promo-app-state.test.ts tests/unit/workspace-copy.test.ts` passed 13/13; `npm run typecheck` passed; `npm run test` passed 309/309; `npm run build` passed and listed `/app`.
- Limitations: No backend persistence state was changed; the state remains derived from the existing account summary and promo block.

### 2026-06-02 - ui-browser-testing - PROMO-08 redeem card and responsive flow

- Agent: Codex
- Trigger: PROMO-08 adds a browser-visible redeem form, Turnstile widget, inline error states, success refresh, `/app` empty-state branching, and mobile overflow acceptance.
- Action: Opened and followed the skill; identified the user-visible flow as signed-in `/app` -> redeem card -> inline error or redeem success -> `/api/me` refetch -> workspace trial quota line. Added a focused Playwright spec with a local Azure account-summary mock, signed test session cookies, browser-level redeem response stubs, and a Turnstile stub covering new user, success, inline errors, exhausted trial, and mobile overflow.
- Output artifacts: `components/app/redeem-code-card.tsx`; `app/app/page.tsx`; `tests/e2e/promo-redeem-ui.spec.ts`; `playwright.config.ts`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run typecheck` passed; `npm run test` passed 309/309; `npm run lint` passed; `npm run build` passed. Focused Playwright attempts reached the browser launch phase after production build/start, but local Chromium launch failed before any page loaded because this macOS sandbox denies the browser process Mach-port registration.
- Limitations: Browser assertions are committed and ready for the supervisor/CI environment, but local Playwright execution could not complete in this sandbox. A full guard scan still reports pre-existing copy-guard strings in `lib/rewrite-eval-cases.ts`; the diff-only scan for this issue returned no matches.

### 2026-06-02 - system-spec-synthesis - PROMO-09 signup verification contract

- Agent: Codex
- Trigger: PROMO-09 changes the Entra-native signup API contract and browser-visible signup form by requiring Turnstile verification and listed email-domain rejection before account creation.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-09 issue body, `plans/promo-issues/PROMO-09-signup-hardening.md`, and `plans/promo-code-trial-spec.md` sections 7.3 and 8.3. Converted the requirements into checkpoints: server-side signup-start gate, runtime Turnstile verification, bundled listed-domain checker, signup widget using `NEXT_PUBLIC_TURNSTILE_SITE_KEY` with dev test fallback, and scoped unit/E2E coverage without changing Google sign-in.
- Output artifacts: `app/api/auth/signup/start/route.ts`; `components/auth/google-oauth-card.tsx`; `components/auth/auth-panels.module.css`; `lib/disposable-email-domains.ts`; `lib/disposable-email-domains.json`; `tests/unit/auth-signup-routes.test.ts`; `tests/e2e/auth-gate.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused signup route test failed before implementation for the new gates, then `npm run test -- tests/unit/auth-signup-routes.test.ts` passed 10/10. `npm run typecheck`, `npm run test`, and `npm run build` passed after implementation.
- Limitations: Local commit creation failed because Git worktree metadata is outside the writable sandbox. No push, PR, deploy, payment, or secret-file changes were attempted.

### 2026-06-02 - resilience-test-generation - PROMO-09 Turnstile failure gates

- Agent: Codex
- Trigger: PROMO-09 adds provider-dependent signup protection and fail-closed behavior for missing, rejected, or unavailable Turnstile verification.
- Action: Opened and followed the skill; treated `/api/auth/signup/start` as the critical operation and `no Entra signup call unless email/password are valid, domain is allowed, and Turnstile verifies` as the invariant. Added deterministic local mocks for Turnstile success, failed token verification, and missing runtime config.
- Output artifacts: `tests/unit/auth-signup-routes.test.ts`; `app/api/auth/signup/start/route.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run test -- tests/unit/auth-signup-routes.test.ts` passed 10/10 and asserts that Entra `signupStart` and `signupChallenge` are not called for listed domains, missing tokens, failed verification, or missing verification config.
- Limitations: No live Cloudflare, Entra, database, or payment dependency was called; provider behavior is covered with local fakes. No secrets or token values were logged.

### 2026-06-02 - ui-browser-testing - PROMO-09 signup form verification

- Agent: Codex
- Trigger: PROMO-09 adds a Turnstile widget and browser-visible signup errors for blank verification and listed email domains.
- Action: Opened and followed the skill; added focused Playwright coverage to `tests/e2e/auth-gate.spec.ts` with an in-page Turnstile stub, blank-token disabled-submit check, listed-domain guidance check, and normal signup-start success flow to the email-code step.
- Output artifacts: `components/auth/google-oauth-card.tsx`; `components/auth/auth-panels.module.css`; `tests/e2e/auth-gate.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run build` passed. `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/rimv-promo-09-ms-playwright npx playwright test tests/e2e/auth-gate.spec.ts` and `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/rimv-promo-09-ms-playwright npm run test:e2e` reached the browser launch phase but could not run page assertions because this macOS sandbox denies Chromium Mach-port registration.
- Limitations: The Playwright assertions are present for CI/supervisor verification, but local browser execution is environment-blocked. Changed-file guard scan returned no matches; a full repo scan still reports pre-existing copy-guard strings in an unrelated rewrite eval fixture.

### 2026-06-02 - system-spec-synthesis - PROMO-10 admin promo-code console contract

- Agent: Codex
- Trigger: PROMO-10 adds the first `/admin` page plus a thin Next.js admin proxy for the existing C# promo-code admin endpoints.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-10 issue body, `plans/promo-issues/PROMO-10-admin-ui.md`, and `plans/promo-code-trial-spec.md` section 7. Converted the requirements into checkpoints: server-side auth/admin gating, same-origin admin mutations, bearer forwarding, create/list/detail/disable/enable proxy routes, duplicate-code field errors, derived list status, and stats sanitization to hash clusters only.
- Output artifacts: `app/admin/promo-codes/page.tsx`; `app/admin/promo-codes/loading.tsx`; `app/api/admin/promo-codes/**/route.ts`; `components/admin/promo-codes-admin.tsx`; `lib/admin-promo-codes.ts`; `lib/admin-promo-proxy.ts`; `tests/unit/admin-promo-codes.test.ts`; `tests/unit/admin-promo-proxy-route.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused tests first failed because the admin helper and proxy routes did not exist; after implementation, `npm test -- tests/unit/admin-promo-codes.test.ts tests/unit/admin-promo-proxy-route.test.ts` passed 12/12. `npm run test`, `npm run typecheck`, `npm run lint`, and `npm run build` passed.
- Limitations: No backend persistence, payment, secrets, deployment, or production branch wiring was changed.

### 2026-06-02 - ui-browser-testing - PROMO-10 admin page browser coverage

- Agent: Codex
- Trigger: PROMO-10 requires browser-visible admin states, desktop/mobile screenshots, duplicate field errors, immediate disable state, and no browser-visible raw IP values.
- Action: Opened and followed the skill; added a focused Playwright spec with a local Azure admin mock, signed test-session cookies, signed-out redirect coverage, non-admin 403 view, admin create/duplicate/stats/disable coverage, mobile and desktop screenshot capture calls, and a direct mock redeem check after disable. Also adjusted Playwright to use one worker so the fixed mock backend port is not raced across files.
- Output artifacts: `tests/e2e/admin-promo-codes.spec.ts`; `playwright.config.ts`; `components/admin/promo-codes-admin.tsx`; `docs/skill-run-log.md`.
- Verification evidence: `npm run test` passed 325/325; `npm run typecheck` passed; `npm run lint` passed; `npm run build` passed. Focused Playwright attempts with an installed temporary Chromium cache reached browser launch after production build/start, but local Chromium failed before page load because this macOS sandbox denies Chromium Mach-port registration.
- Limitations: Playwright assertions and screenshot capture are committed for the supervisor/CI environment, but local browser execution could not complete in this sandbox. Changed-file guard scan returned no matches; a full repo scan still reports pre-existing copy-guard strings in `lib/rewrite-eval-cases.ts`.

### 2026-06-02 - ui-browser-testing - PROMO-11 trial-code copy verification

- Agent: Codex
- Trigger: PROMO-11 changes browser-visible landing, pricing, developers, auth, footer, and workspace quota copy from baseline access language to trial-code redemption language.
- Action: Opened and followed the skill; identified the user-visible flows as `/`, `/pricing`, `/developers`, `/sign-up`, and `/app` quota/nudge copy. Updated source-string contract tests first, observed the focused tests fail on the old copy, then updated the copy surfaces and reran the focused tests green. Started the local Next dev server and checked rendered HTTP responses for the updated copy on the public routes.
- Output artifacts: `components/landing/hero.tsx`; `components/landing/closing-cta.tsx`; `components/landing/pricing-v2.tsx`; `app/pricing/page.tsx`; `components/site-footer.tsx`; `app/developers/page.tsx`; `components/auth/google-oauth-card.tsx`; `app/app/page.tsx`; `components/app/rewrite-workspace.tsx`; `lib/rewrite-eval-cases.ts`; `tests/unit/pricing-auth-visual-system.test.ts`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused copy tests failed before implementation and then passed 11/11. Final `npm run test` passed 326/326; `npm run typecheck` passed; required restricted-term grep returned no matches; stale old-copy phrase scan returned no matches. Dev server returned HTTP 200 for `/`, `/pricing`, `/developers`, and `/sign-up`, and the rendered responses contained the expected trial-code copy.
- Limitations: Local Chromium launch through Playwright failed before page load because this macOS sandbox denied Chromium Mach-port registration, so screenshot-level browser verification could not be completed here. The dev server also reported watcher `EMFILE` warnings in the sandbox, but the checked routes compiled and returned 200.

### 2026-06-01 - dotnet-backend-testing - PAY-03 forged Stripe webhook signature tests

- Agent: Codex
- Trigger: GitHub issue #380/PAY-03 required C# xUnit coverage for production Stripe webhook signature rejection behavior.
- Action: Opened and followed the skill; added focused API-level tests around `StripeWebhookFunction` with SQLite persisted-state assertions.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `cd backend-dotnet && dotnet test ReplyInMyVoice.sln --filter StripeWebhookApiTests` passed 9/9; `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: Test-only change; no production code, deploy, push, or PR creation. NuGet vulnerability metadata warnings appeared because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed.

### 2026-06-01 - resilience-test-generation - PAY-03 webhook signature failure matrix

- Agent: Codex
- Trigger: PAY-03 tests stale/tampered Stripe webhook delivery and requires proving rejected webhooks create no event or credit side effects.
- Action: Opened and followed the resilience test workflow; covered wrong-secret, mutated-body-after-signing, and stale-timestamp failure cases using deterministic local HMAC signatures.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `cd backend-dotnet && dotnet test ReplyInMyVoice.sln --filter StripeWebhookApiTests` passed 9/9; `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: No live Stripe calls were made; tests exercise local signature verification and database side effects only. No secrets or credential values were logged.

### 2026-06-01 - state-machine-modeling - PAY-23 payment dunning grace state

- Agent: Codex
- Trigger: PAY-23 changes the subscription lifecycle for failed renewal payments, grace expiry, terminal Stripe subscription states, and recovery.
- Action: Opened/followed the state-machine workflow; modeled paid states (`Active`, `Trialing`, `Testing`, `PastDue`), terminal/non-paying states (`Inactive`, `Canceled`), events (`invoice.payment_failed`, `invoice.payment_succeeded`, local grace expiry, `subscription.updated` terminal statuses), illegal stale recovery from terminal states, and persisted grace timestamps.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/SubscriptionStatus.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/AppUser.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `docs/support-runbook.md`.
- Verification evidence: `cd backend-dotnet && dotnet test` passed 411/411; support runbook §3.1 documents the transition timeline and invariants.
- Limitations: No live Stripe webhook or real email was sent; behavior is verified with local SQLite and fakes.

### 2026-06-01 - resilience-test-generation - PAY-23 webhook replay and notification fakes

- Agent: Codex
- Trigger: PAY-23 requires idempotent webhook replay behavior and no duplicate notifications for failed-payment, paused, and recovered states.
- Action: Opened/followed the resilience workflow; used deterministic notification and billing-portal fakes, replayed the same Stripe event IDs in tests, and kept notification send actions post-commit so billing state is not rolled back by provider calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`.
- Verification evidence: Focused `StripeEventServiceTests` passed 24/24; full `cd backend-dotnet && dotnet test` passed 411/411.
- Limitations: Provider outages were not induced against live Stripe or Resend; tests prove local fake boundaries only. NuGet vulnerability metadata warnings appeared because `https://api.nuget.org/v3/index.json` was unavailable.

### 2026-06-01 - data-module-review - PAY-23 AppUser grace persistence

- Agent: Codex
- Trigger: PAY-23 adds persisted payment failure/grace state to the EF Core `AppUser` model and migration history.
- Action: Opened/followed the data-module workflow; added nullable `PaymentFailedAt` and `PaymentGraceEndsAt`, updated EF model configuration and snapshot, and generated `AddPaymentGraceState` migration.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/AppUser.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260531214813_AddPaymentGraceState.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260531214813_AddPaymentGraceState.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.
- Verification evidence: `cd backend-dotnet && dotnet test` passed 411/411.
- Limitations: Migration application was not run against a live database; no secrets or connection strings were logged.

### 2026-06-01 - dotnet-backend-testing - PAY-23 dunning tests

- Agent: Codex
- Trigger: PAY-23 requires .NET unit/integration coverage for `invoice.payment_failed`, local grace expiry, recovery, and idempotent replay.
- Action: Opened/followed the .NET backend testing workflow; added xUnit coverage in the existing SQLite-backed `StripeEventServiceTests` with hand-written fakes for notifications and billing portal sessions.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`.
- Verification evidence: `dotnet test ReplyInMyVoice.sln --filter StripeEventServiceTests --no-restore` passed 24/24; `cd backend-dotnet && dotnet test` passed 411/411.
- Limitations: No real email, Stripe charge, deployment, push, or PR was performed.

### 2026-06-01 - state-machine-modeling - PAY-26 dispute chargeback runbook

- Agent: Codex
- Trigger: GitHub issue #394 / PAY-26 explicitly required `state-machine-modeling` for the dispute and chargeback operations lifecycle.
- Action: Opened and followed the state-machine workflow to document dispute states, events, transitions, invariants, illegal transitions, persistence implications, and a future test checklist.
- Output artifacts: `docs/dispute-chargeback-runbook.md`; `docs/skill-run-log.md`.
- Verification evidence: Runbook includes the required deadline guidance, evidence checklist, repeat-disputer policy, and dispute lifecycle model.
- Limitations: No optional direct `payment_intent` admin evidence endpoint was added; PAY-26 was completed as a docs-only operational runbook. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-06-01 - resilience-test-generation - PAY-32 webhook delivery monitoring

- Agent: Codex
- Trigger: GitHub issue #400 / PAY-32 required webhook failure monitoring and replay-safe operations coverage.
- Action: Opened and followed the skill; identified Stripe webhook processing as the critical operation, with database-backed idempotency and replay as the resilience invariant.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/HealthFunction.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/webhook-ops-runbook.md`.
- Verification evidence: Added readiness coverage that seeds a failed Stripe event and asserts it is surfaced, plus coverage for the configured no-processed-event window. The regression failed before implementation, then `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter "Ready_health" --no-restore` passed 2/2 and `cd backend-dotnet && dotnet test` passed 408/408.
- Limitations: PAY-02 dashboard alert creation remains an owner/operator action. NuGet vulnerability metadata lookup warned because `https://api.nuget.org/v3/index.json` was unavailable; restore/build/test still completed. No secrets or raw webhook payloads were logged.

### 2026-06-01 - data-module-review - PAY-32 StripeEvent readiness metrics

- Agent: Codex
- Trigger: The task reads `StripeEvent` and `RewriteCredit.StripeEventId` persistence state to expose webhook health and document idempotent replay.
- Action: Opened and followed the skill; reviewed `StripeEvent` status fields, `ProcessedAt`, EF configuration, and the unique `RewriteCredit.StripeEventId` invariant. Ran the bundled data-risk scan with `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 40 backend-dotnet`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/HealthFunction.cs`; `docs/webhook-ops-runbook.md`.
- Verification evidence: No schema or migration change was needed. The readiness query now counts all unresolved `StripeEvent.Status = Failed` rows and reads the latest processed Stripe event timestamp without mutating persistence.
- Limitations: The data-risk scan reports broad repo signals and was used only as a scope/risk check. No migration was generated. No secrets or private payloads were logged.

### 2026-06-01 - dotnet-backend-testing - PAY-32 readiness health coverage

- Agent: Codex
- Trigger: The issue required a backend test for seeded failed Stripe events and the change touched .NET Azure Functions readiness behavior.
- Action: Opened and followed the skill; used xUnit/FluentAssertions with the existing SQLite-backed test fixture, wrote the readiness regression before production changes, and ran focused then full backend tests.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/HealthFunction.cs`.
- Verification evidence: Initial focused test failed because `lastProcessedStripeEvent` was absent and older failed events were not counted. After implementation, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter "Ready_health" --no-restore` passed 2/2 and `cd backend-dotnet && dotnet test` passed 408/408.
- Limitations: This did not add browser/UI coverage because PAY-32 is backend/docs-only. NuGet vulnerability metadata lookup warned due external index access, but tests passed. No secrets were logged.

### 2026-06-01 - state-machine-modeling - PAY-01 subscription renewal failure lifecycle

- Agent: Codex
- Trigger: GitHub issue #378/PAY-01 changes subscription/quota lifecycle and explicitly requires state-machine-modeling.
- Action: Opened and followed the skill; modeled the paid states (`Active`, `Trialing`, `Testing`), free states (`Inactive`, `Canceled`), subscription update/delete events, `invoice.payment_failed`, duplicate webhook delivery, and the invariant that non-paying Stripe states must not receive the paid usage plan.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `docs/rewrite-packs-pricing-spec.md`; `docs/stripe-live-mode-cutover.md`.
- Verification evidence: Added xUnit coverage for `past_due`, `unpaid`, `incomplete`, `incomplete_expired`, `paused`, and `canceled` subscription updates mapping to a non-paid status and `AccountService.GetUsagePlan` returning `free/free:lifetime/3`. Focused `StripeEventServiceTests` passed 20/20; full `cd backend-dotnet && dotnet test` passed 404/404.
- Limitations: No new enum states or migrations were added. `invoice.payment_failed` logs the renewal failure hook; quota downgrade remains driven by Stripe subscription status events.

### 2026-06-01 - resilience-test-generation - PAY-01 webhook replay and renewal failure handling

- Agent: Codex
- Trigger: PAY-01 adds handling for Stripe webhook replay/idempotency and renewal failure behavior.
- Action: Opened and followed the skill; targeted duplicate event delivery as the critical failure mode, kept tests local with EF SQLite, and avoided live Stripe calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`.
- Verification evidence: The new `invoice.payment_failed` test failed first because no invoice-specific structured log was emitted; after implementation it passed and asserted first delivery processed, second delivery skipped, `StripeEvent` row count stayed 1, no rewrite credits were granted, and the renewal-failure log was emitted once with correlation/customer/subscription/user fields. Full `cd backend-dotnet && dotnet test` passed 404/404.
- Limitations: Email/SMS/customer notification behavior remains out of scope for PAY-09.

### 2026-06-01 - data-module-review - PAY-01 StripeEvent idempotency persistence

- Agent: Codex
- Trigger: PAY-01 reuses `StripeEvent` deduplication and touches subscription/quota persistence invariants.
- Action: Opened and followed the skill; reviewed `AppDbContext` indexes/keys, `StripeEventService` transaction boundaries, `StripeEvents.EventId` primary-key dedupe, and `RewriteCredits.StripeEventId` uniqueness.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`.
- Verification evidence: No schema or migration change was needed. The invoice failure handler uses the existing transactional webhook path and persists one `StripeEvent` row per Stripe event id. Focused `StripeEventServiceTests` passed 20/20; full `cd backend-dotnet && dotnet test` passed 404/404.
- Limitations: There is no separate invoice-failure table; PAY-02 alerting is expected to consume the structured log hook.

### 2026-06-01 - dotnet-backend-testing - PAY-01 xUnit payment lifecycle coverage

- Agent: Codex
- Trigger: PAY-01 requires new .NET/xUnit tests for subscription status mapping and `invoice.payment_failed` idempotency.
- Action: Opened and followed the skill; added service-level EF SQLite tests for subscription mapping and invoice replay, then stabilized existing API test clients with `HandleCookies = false` to avoid the macOS sandbox `CookieContainer` domain-name failure while preserving assertions.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`.
- Verification evidence: Red run: `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter "FullyQualifiedName~StripeEventServiceTests"` failed on the missing invoice failure log. Green runs: the same focused command passed 20/20, and `cd backend-dotnet && dotnet test` passed 404/404. `git diff --check` passed.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-06-01 - cloud-architecture-cost-review - PAY-19 notification provider choice

- Agent: Codex
- Trigger: PAY-19 explicitly requires cloud-architecture-cost-review before adding transactional notification infrastructure.
- Action: Opened and followed the skill; compared Azure Communication Services Email, SendGrid, and Resend for a low-traffic Azure Functions backend. Selected Resend because it can be configured behind existing runtime env names without creating a new Azure resource or adding an uncached NuGet dependency in this worktree.
- Output artifacts: `plans/decisions-log.md`; `.env.example`; `backend-dotnet/src/ReplyInMyVoice.Functions/local.settings.example.json`; `docs/manual-setup.md`.
- Verification evidence: Provider decision recorded in `plans/decisions-log.md`; runtime config is opt-in via `NOTIFICATIONS_PROVIDER=resend`; missing/disabled config logs a no-op and does not throw. No paid resource, deployment, dashboard action, or real email send was performed.
- Limitations: Exact provider pricing was not quoted or checked because no paid-resource action was taken. Resend requires a valid runtime key and verified sender/domain outside source control before real email can be sent.

### 2026-06-01 - dotnet-backend-testing - PAY-19 notification tests

- Agent: Codex
- Trigger: PAY-19 adds C#/.NET notification infrastructure and requires xUnit coverage for fake provider invocation and missing-config no-op behavior.
- Action: Opened and followed the skill; wrote failing notification tests first, then added `INotificationService`, typed templates, a fake-provider unit test path, no-op fallback, Resend provider wiring, and DI registration.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/NotificationServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Notifications/*`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`.
- Verification evidence: Red run failed on missing notification namespace/contracts. Focused green run `cd backend-dotnet && dotnet test ReplyInMyVoice.sln --filter NotificationServiceTests` passed 3/3. Full `cd backend-dotnet && dotnet test` passed 407/407.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages. Tests do not send real email; the provider-enabled test only resolves the DI type.

### 2026-06-01 - ui-browser-testing - PAY-06 payment E2E

- Agent: Codex
- Trigger: GitHub issue #383/PAY-06 requires Playwright E2E coverage for pricing checkout, signed payment webhooks, `/app` balance, refund clawback, and anonymous checkout redirect behavior.
- Action: Opened and followed the skill; added a focused Playwright spec with signed session cookies, a local payment test API, mocked Stripe Checkout URL, signed webhook delivery through the Next.js `/api/stripe/webhook` route, `/app` balance assertions, and skip behavior when the payment webhook signing env name is absent.
- Output artifacts: `tests/e2e/payment-flow.spec.ts`; `tests/e2e/payment-flow-mock-api.ts`; `playwright.config.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run typecheck` passed. `npm run test` passed 298/298. `npm run lint` passed. `git diff --check` passed. Banned-term grep over `app components public lib` returned no matches. `npx playwright test tests/e2e/payment-flow.spec.ts --project=chromium` passed in absent-config mode with 2 skipped. A standalone HTTP smoke against `tests/e2e/payment-flow-mock-api.ts` verified signed checkout webhook grant from 3 to 13 and signed full refund clawback from 13 to 3.
- Limitations: Browser-executed Playwright remains blocked in this macOS sandbox by Chromium `MachPortRendezvousServer` permission failure before page assertions execute. `PAYMENT_E2E_STRIPE_WEBHOOK_SECRET=... npx playwright test tests/e2e/payment-flow.spec.ts --project=chromium` and `npm run test:e2e` both failed for that launch reason, not from route assertions. The E2E uses a local payment test API and does not call Stripe or create a live payment.

### 2026-06-01 - state-machine-modeling - PAY-06 checkout grant and refund clawback

- Agent: Codex
- Trigger: PAY-06 tests payment and quota lifecycle transitions: signed-out checkout redirect, signed checkout completion grant, and refund clawback.
- Action: Opened and followed the skill; modeled the tested lifecycle as signed-out pricing click, signed-in free balance, checkout URL emitted without grant, signed `checkout.session.completed` grant, signed `charge.refunded` clawback, and duplicate-event safe handling in the local payment test API.
- Output artifacts: `tests/e2e/payment-flow.spec.ts`; `tests/e2e/payment-flow-mock-api.ts`.
- Verification evidence: The Playwright spec asserts no balance change before webhook, grant to 13 after Quick Pack completion, and clawback to 3 after full refund. The standalone signed-webhook HTTP smoke passed those same state transitions without browser execution.
- Limitations: No production state machine, EF model, migration, or backend service code was changed.

### 2026-06-01 - resilience-test-generation - PAY-06 signed webhook E2E resilience

- Agent: Codex
- Trigger: PAY-06 adds tests around signed payment webhook delivery and refund accounting, which overlaps webhook replay and payment-provider safety rules.
- Action: Opened and followed the skill; kept the payment flow hermetic, avoided live Stripe calls, required a signing env name for the payment E2E, verified webhook signatures in the local test API, and included duplicate-event dedupe in the test API state.
- Output artifacts: `tests/e2e/payment-flow.spec.ts`; `tests/e2e/payment-flow-mock-api.ts`; `playwright.config.ts`.
- Verification evidence: Absent-config Playwright run skipped the spec cleanly. The signed-webhook HTTP smoke passed checkout grant and refund clawback. Full unit tests, typecheck, lint, and diff checks passed.
- Limitations: Browser-backed Playwright assertions could not execute in this sandbox because Chromium launch is blocked. The new E2E does not depend on Stripe CLI, Stripe network calls, or live payment keys.

### 2026-06-01 - dotnet-backend-testing - PAY-10 payment resilience test gaps

- Agent: Codex
- Trigger: GitHub issue #385/PAY-10 explicitly requires .NET backend tests for Stripe refund replay, Stripe API-version pinning, quota reservation races, and rewrite worker timeout release.
- Action: Opened and followed the skill; added xUnit/FluentAssertions tests using existing EF SQLite fixtures and deterministic fake providers, plus an internal visibility hook for the Stripe billing pin guard test.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobProcessorTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Properties/AssemblyInfo.cs`.
- Verification evidence: Focused backend run `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter "FullyQualifiedName~StripeBillingServiceTests|FullyQualifiedName~StripeEventServiceTests|FullyQualifiedName~QuotaServiceTests|FullyQualifiedName~RewriteJobProcessorTests" --no-restore` passed 45/45. Full `cd backend-dotnet && dotnet test` passed 412/412.
- Limitations: The Stripe SDK exposes `StripeConfiguration.ApiVersion` as read-only, so the mismatch test drives the same internal pin check through an overload while also asserting the live SDK value equals the pinned version. NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-06-01 - resilience-test-generation - PAY-10 replay, timeout, and quota-race coverage

- Agent: Codex
- Trigger: PAY-10 changes/tests retryable Stripe refund ordering, true concurrent quota reservation, and provider timeout/cancellation recovery.
- Action: Opened and followed the skill; framed tests around timeout, duplicate/replay, partial ordering, and concurrent request failure modes. Kept all provider and Stripe behavior local with fakes and JSON payloads; no live Stripe, OpenAI, Sapling, Azure, or production database calls were made.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobProcessorTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`.
- Verification evidence: Added coverage for refund-before-grant failing retryably then replaying after credit arrives, `Task.WhenAll` quota reservation on one remaining slot producing exactly one `Created` and one `QuotaExceeded`, and provider `OperationCanceledException`/`TaskCanceledException` releasing reservations with `provider_timeout` and refunding quota/credit. Focused backend run passed 45/45; full `cd backend-dotnet && dotnet test` passed 412/412.
- Limitations: Retry handling is bounded to reservation-race/concurrency exceptions; no new external provider retry policy, live network timeout test, or deployment behavior was added.

### 2026-06-01 - ui-browser-testing - PAY-10 buy-button checkout branch coverage

- Agent: Codex
- Trigger: PAY-10 requires frontend checkout flow coverage for `components/landing/buy-button.tsx` 401, success, and API error branches.
- Action: Opened and followed the skill; added a Vitest unit flow test under `tests/unit/` that exercises the button handler with mocked fetch, window navigation, and React state updates.
- Output artifacts: `tests/unit/buy-button-checkout-flow.test.ts`.
- Verification evidence: Focused `npm run test -- tests/unit/buy-button-checkout-flow.test.ts` passed 3/3. Full `npm run typecheck` passed. Full `npm run test` passed 301/301. Banned-term scan over `app components public lib` returned no matches.
- Limitations: The issue allowed Vitest or Playwright; this implementation uses Vitest and does not add screenshot or browser-run artifacts because no visual layout changed.

### 2026-06-01 - state-machine-modeling - PAY-10 payment and rewrite recovery transitions

- Agent: Codex
- Trigger: PAY-10 touches webhook lifecycle, usage reservation lifecycle, rewrite attempt lifecycle, and quota/credit transitions.
- Action: Opened and followed the skill; modeled states as `StripeEvent` Processing/Failed/Processed, `UsageReservation` Pending/Released/Finalized/Expired, and `RewriteAttempt` Pending/Processing/Failed/Succeeded/Expired. Events modeled: out-of-order `charge.refunded`, refund replay after credit grant, duplicate processed refund, concurrent `ReserveAsync`, and provider timeout/cancellation. Allowed transitions added/tested: no-credit refund -> Failed/retryable with no credit mutation; replayed refund -> Processed with clamped grant; concurrent reservation -> one Pending reservation and one `QuotaExceeded`; provider timeout from Processing -> Failed attempt plus Released reservation. Illegal transitions tested include duplicate processed refund not reapplying and reservation race not over-reserving.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`; related xUnit tests.
- Verification evidence: Focused backend run passed 45/45. Full `cd backend-dotnet && dotnet test` passed 412/412.
- Limitations: No enum values or database migrations were added; the work only clarified existing transition behavior and added recovery tests.

### 2026-06-01 - data-module-review - PAY-10 EF quota and payment persistence invariants

- Agent: Codex
- Trigger: PAY-10 changes EF-backed usage counters, idempotency/replay rows, credit balances, and transaction behavior under concurrency.
- Action: Opened and followed the skill; reviewed `AppDbContext` uniqueness/concurrency configuration, `StripeEventService` transaction behavior, `QuotaService` read-then-write reservation path, `RewriteJobProcessor` timeout release path, and the new tests against the relevant invariants.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/*`.
- Verification evidence: Findings: no migration needed; existing unique keys on `StripeEvents.EventId`, `UsagePeriods(userId, periodKey)`, `RewriteAttempts(userId, idempotencyKey)`, and `UsageReservations.RewriteAttemptId` support the new tests. The quota race test uses file-backed SQLite with WAL and asserts one attempt/reservation/outbox row. `git diff --check`, banned-term scan over `app components public lib`, full `cd backend-dotnet && dotnet test`, `npm run typecheck`, and `npm run test` all passed.
- Limitations: The review did not run a live SQL Server concurrency test; coverage is local EF SQLite integration coverage matching the issue brief.

### 2026-06-01 - ui-browser-testing - PAY-07 admin UI

- Agent: Codex
- Trigger: PAY-07 adds frontend admin pages, admin proxy routes, forms, auth gating, and Playwright coverage.
- Action: Opened and followed the skill; added `/admin` and `/admin/users/[userId]` pages, browser-facing admin components, same-origin bearer proxy routes under `/api/admin/*`, unit proxy tests, and `tests/e2e/admin.spec.ts` for admin and non-admin flows.
- Output artifacts: `app/admin/*`; `app/api/admin/*`; `components/admin/*`; `lib/admin-api-proxy.ts`; `lib/admin-auth.ts`; `lib/admin-types.ts`; `tests/unit/admin-api-routes.test.ts`; `tests/e2e/admin.spec.ts`; `playwright.config.ts`.
- Verification evidence: `npm run typecheck`, `npm run test`, `npm run build`, `npm run lint`, `git diff --check`, and banned-term grep over `app components public lib` passed. The admin route unit test passed 3/3. Dev-server HTTP checks returned 200 for an admin session and rendered the expected denied view for a non-admin session.
- Limitations: Local Playwright Chromium could not launch in this macOS sandbox (`MachPortRendezvousServer` permission denied), so `npx playwright test tests/e2e/admin.spec.ts --project=chromium` failed before executing page assertions. The Browser plugin was attempted but `iab` was unavailable in this session.

### 2026-06-01 - cloud-architecture-cost-review - PAY-02 payment observability wiring

- Agent: Codex
- Trigger: GitHub issue #379/PAY-02 explicitly requires cloud-architecture-cost-review before adding Sentry/PostHog payment observability across Cloudflare Worker and Azure Functions.
- Action: Opened and followed the skill; kept the existing Cloudflare Worker + Azure Functions consumption + Application Insights architecture, added runtime-keyed Worker/PostHog/Sentry-compatible instrumentation, and rejected adding a second backend APM package because Application Insights is already registered in `ReplyInMyVoice.Functions`.
- Output artifacts: `lib/payment-observability.ts`; `lib/payment-observability-client.ts`; `app/api/observability/payment/route.ts`; `app/api/stripe/*`; `app/api/me/route.ts`; `docs/observability.md`; `docs/manual-setup.md`; `docs/support-runbook.md`; `.env.example`; `backend-dotnet/src/ReplyInMyVoice.Functions/local.settings.example.json`.
- Verification evidence: `npm run typecheck`, `npm run test`, `npm run build`, `npm run lint`, `cd backend-dotnet && dotnet build ReplyInMyVoice.sln`, and `cd backend-dotnet && dotnet test ReplyInMyVoice.sln` passed. Banned-term scan over `app components public lib` returned no matches.
- Limitations: No paid resources, dashboard monitors, Worker secrets, Azure app settings, GitHub secrets, deploy commands, or source-map upload automation were created in this turn. Owner still needs to create PostHog, Azure Monitor, Sentry, and UptimeRobot dashboard configuration from `docs/observability.md`.

### 2026-06-01 - dotnet-backend-testing - PAY-02 Functions payment failure telemetry

- Agent: Codex
- Trigger: PAY-02 changes C#/.NET payment and webhook failure paths and required backend test coverage for the explicit `webhook_failed` event hook.
- Action: Opened and followed the skill; wrote a failing xUnit service test for a Stripe checkout webhook sync failure, then added structured Application Insights-compatible logs with `PaymentObservabilityEvent` and `CorrelationId` in `StripeEventService`, `StripeWebhookFunction`, and `BillingHttpFunctions`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeWebhookFunction.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/BillingHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs`.
- Verification evidence: Red run: focused `StripeEventServiceTests.ProcessWebhookEventAsync_LogsWebhookFailedWhenCheckoutSyncFails` failed because no `webhook_failed` error log existed. Green run passed after implementation. Full `dotnet test ReplyInMyVoice.sln` passed 408/408; `dotnet build ReplyInMyVoice.sln` passed.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages. Account/Billing API tests needed the existing polling/no-reload WebApplicationFactory guard to avoid macOS sandbox `PhysicalFilesWatcher` stack overflow during full-suite runs.

### 2026-06-01 - resilience-test-generation - PAY-04 Stripe API failure tests

- Agent: Codex
- Trigger: PAY-04 explicitly requires Stripe provider-failure tests for checkout-create and admin refund paths.
- Action: Opened and followed the skill; modeled Stripe session creation timeout and refund timeout as deterministic fake-provider failures, asserted final persisted state, and avoided live Stripe calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRefundTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/BillingHttpFunctions.cs`.
- Verification evidence: Red run `cd backend-dotnet && dotnet test ReplyInMyVoice.sln --filter "StripeBillingServiceTests|StripeBillingApiTests|AdminRefundTests"` failed on missing Stripe billing fake seam. Focused green run with the same filter passed 10/10. Full `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-06-01 - dotnet-backend-testing - PAY-04 xUnit payment failure coverage

- Agent: Codex
- Trigger: PAY-04 adds C#/.NET backend tests for checkout-create and refund Stripe API failures.
- Action: Opened and followed the skill; added WebApplicationFactory API coverage for checkout 5xx behavior, EF SQLite service coverage for checkout persistence state, and AdminService coverage for refund failure audit/credit invariants.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRefundTests.cs`.
- Verification evidence: Focused `cd backend-dotnet && dotnet test ReplyInMyVoice.sln --filter "StripeBillingServiceTests|StripeBillingApiTests|AdminRefundTests"` passed 10/10 after implementation. Full `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: The initial focused API run without the test-host reload guard hit the known macOS WebApplicationFactory file-watcher timeout pattern; the Stripe billing API test class now sets the same reload-disable environment variables used by existing API tests.

### 2026-06-01 - data-module-review - PAY-04 checkout/refund persistence invariants

- Agent: Codex
- Trigger: PAY-04 requires proving no partial DB state for failed checkout-create and refund operations.
- Action: Opened and followed the skill; reviewed `AppUsers`, `RewriteCredits`, and `AdminAuditLogs` mutations, moved checkout user/customer persistence until after successful Stripe session creation, and kept admin refund success audit persistence after successful refund only.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRefundTests.cs`.
- Verification evidence: Checkout failure test asserts existing user email, Stripe customer id, row version, and credits remain unchanged. Refund failure test asserts zero admin audit rows, zero refund-success audit rows, unchanged credit grant/consumption, and deterministic refund idempotency key. Full `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: No schema or migration changes were needed.

### 2026-06-01 - data-module-review - PAY-05 credit receipt URL persistence

- Agent: Codex
- Trigger: PAY-05 changes `RewriteCredit` persistence and adds an EF Core migration for Stripe receipt reconciliation.
- Action: Opened and followed the skill; reviewed `RewriteCredit`, `AppDbContext`, `StripeEventService.SyncCheckoutSessionAsync`, purchase-history projections, admin payment projections, and the generated migration for add-only/nullability safety.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260531201712_AddRewriteCreditReceiptUrl.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260531201712_AddRewriteCreditReceiptUrl.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`; payment service/projection updates.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 80 backend-dotnet` was run for persistence context. The new migration `Up` contains only `migrationBuilder.AddColumn<string>(name: "StripeReceiptUrl", table: "RewriteCredits", type: "nvarchar(2048)", maxLength: 2048, nullable: true)`, and `rg` over the new migration found no `DropColumn`, `DropTable`, `AlterColumn`, `Rename`, raw SQL, data delete, or drop-index operation. Full `cd backend-dotnet && dotnet test` passed 408/408.
- Limitations: The webhook stores a receipt URL only when Stripe supplies an expanded `payment_intent.latest_charge.receipt_url`; non-expanded or missing charge data remains nullable as required.

### 2026-06-01 - dotnet-backend-testing - PAY-05 receipt URL xUnit coverage

- Agent: Codex
- Trigger: PAY-05 requires xUnit coverage for receipt URL persistence and `GET /api/me/payments` response shape.
- Action: Opened and followed the skill; wrote failing receipt URL assertions first, then added service-level EF SQLite coverage for expanded Stripe checkout receipt URLs, missing receipt URLs, account purchase-history serialization, and admin payment detail projection.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminServiceTests.cs`.
- Verification evidence: Red run `cd backend-dotnet && dotnet test ReplyInMyVoice.sln --filter "FullyQualifiedName~StripeEventServiceTests|FullyQualifiedName~AccountApiTests"` failed on missing `RewriteCredit.StripeReceiptUrl`, proving the new assertions were active. Focused green receipt tests passed. Full `cd backend-dotnet && dotnet test` passed 408/408.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-06-01 - cloud-architecture-cost-review - PAY-20 GST tax readiness

- Agent: Codex
- Trigger: PAY-20 explicitly requires cloud-architecture-cost-review for Stripe Tax readiness and GST threshold monitoring.
- Action: Opened and followed the skill; selected the no-new-infrastructure option: keep Stripe Tax dashboard/legal setup owner-only, add a default-off checkout flag, compute turnover from the existing `RewriteCredit` ledger, and reuse PAY-19 notification infrastructure when configured.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeCheckoutSessionOptionsFactory.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/TaxTurnoverService.cs`; `docs/gst-tax-playbook.md`.
- Verification evidence: Official IRD GST registration guidance confirmed the NZ$60,000 actual/expected 12-month threshold; official Stripe Checkout Tax docs confirmed `automatic_tax`, required billing address collection, `customer_update[address]=auto`, and tax ID collection for Checkout. Focused backend tests passed 7/7, and full `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: No Stripe Dashboard setting, IRD registration, deploy command, paid cloud resource, or real charge was performed. Exact tax/accounting advice remains owner/accountant responsibility.

### 2026-06-01 - data-module-review - PAY-20 turnover tracker

- Agent: Codex
- Trigger: PAY-20 computes rolling gross revenue from `RewriteCredit` payment fields and surfaces it in admin stats.
- Action: Opened and followed the skill; reviewed `RewriteCredit` fields, `AppDbContext` mapping, `StripeEventService` purchase grant writes, and `AdminService.GetStatsAsync`. Kept the change read-side only with no migration because `Source`, `GrantedAt`, `StripeAmountTotal`, and `StripeCurrency` already support the report.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/TaxTurnoverService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/TaxTurnoverServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminServiceTests.cs`.
- Verification evidence: The new xUnit test seeds in-window/out-of-window/admin/non-NZD credits and verifies only gross in-window NZD purchases count toward the configured warning threshold. Focused backend tests passed 7/7, and full `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: The report follows PAY-20's specified `RewriteCredit` purchase source and does not convert non-NZD amounts or subtract refunds.

### 2026-06-01 - dotnet-backend-testing - PAY-20 checkout tax and turnover tests

- Agent: Codex
- Trigger: PAY-20 adds C# backend checkout option wiring, turnover computation, notification template wiring, and admin stats response coverage.
- Action: Opened and followed the skill; wrote failing tests first for the missing checkout options factory, turnover service, and admin stats field, then implemented the minimal backend code to pass.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeCheckoutSessionOptionsFactoryTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/TaxTurnoverServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminServiceTests.cs`; related production service files.
- Verification evidence: Red run failed on missing `StripeCheckoutSessionOptionsFactory`, missing `TaxTurnoverService`, and missing `AdminStatsResponse.GstTurnover`. Focused green run `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter "FullyQualifiedName~StripeCheckoutSessionOptionsFactoryTests|FullyQualifiedName~TaxTurnoverServiceTests|FullyQualifiedName~AdminServiceTests"` passed 7/7. Full `cd backend-dotnet && dotnet test` passed 410/410.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-06-01 - ui-browser-testing - PAY-20 admin stats routing check

- Agent: Codex
- Trigger: PAY-20 says to surface the GST turnover tracker in admin stats, which may affect browser-visible admin UI depending on implementation.
- Action: Opened the skill as a routing check; kept this issue backend/API-scoped by adding `gstTurnover` to `AdminStatsResponse` without changing `app/`, `components/`, `lib/`, `public/`, or Playwright files.
- Output artifacts: None in frontend/browser paths.
- Verification evidence: The restricted vocabulary scan over `app components public lib` returned no matches. No browser-visible files were changed, so no Playwright/browser run was applicable for PAY-20.
- Limitations: The admin frontend does not render a dedicated GST turnover tile in this issue; the machine-checkable surface is the backend admin stats response.

### 2026-06-01 - data-module-review - PAY-21 receipt URL payment history contract

- Agent: Codex
- Trigger: PAY-21 changes the `RewriteCredit` payment ledger and `AccountService.GetPurchaseHistoryAsync` contract to expose Stripe-hosted receipt links.
- Action: Opened and followed the skill; reviewed the owned `RewriteCredit` table/entity, `AppDbContext` mapping, migration safety, caller-scoped purchase-history query, and account payment projection. Added a nullable `StripeReceiptUrl` column with a 2048-character limit and no tax calculation logic.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260601090000_AddRewriteCreditReceiptUrl.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `docs/payment-receipts-tax-invoices.md`.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed and surfaced broad existing quota/idempotency signals, with no new blocker for this nullable receipt-link projection. Focused account tests passed 13/13; full backend `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test` passed 408/408.
- Limitations: PAY-21 relies on Stripe receipt capture populating `StripeReceiptUrl`; this change surfaces and stores the value but does not hand-roll tax math or generate invoices.

### 2026-06-01 - dotnet-backend-testing - PAY-21 payment history API coverage

- Agent: Codex
- Trigger: PAY-21 adds C#/.NET tests for purchase-history receipt links and the ASP.NET Core `/api/me/payments` route.
- Action: Opened and followed the skill; used an EF Core SQLite service test for the persisted receipt URL invariant and a `WebApplicationFactory` API test for route/auth/JSON contract behavior.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`.
- Verification evidence: Red run failed on missing `StripeReceiptUrl`, `ReceiptUrl`, and missing frontend proxy route. Green runs: `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --no-restore --filter "FullyQualifiedName~AccountServiceTests|FullyQualifiedName~AccountApiTests"` passed 13/13; full backend `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test` passed 408/408.
- Limitations: Local .NET test runs emitted `NU1900` warnings because the NuGet vulnerability feed was unavailable. Without `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`, this macOS sandbox recursed through config change tokens and stack-overflowed in WebApplicationFactory before PAY-21 assertions could complete.

### 2026-06-01 - ui-browser-testing - PAY-21 account receipts UI

- Agent: Codex
- Trigger: PAY-21 adds a browser-visible account-area receipts / purchase-history view and a Playwright acceptance test.
- Action: Opened and followed the skill; added the `/api/me/payments` thin proxy, `AzureAccountPayment` client type, account purchase-history table, Stripe receipt link rendering, a React component acceptance test, and `tests/e2e/account-receipts.spec.ts` with seeded account/payment responses.
- Output artifacts: `app/api/me/payments/route.ts`; `components/account/account-panel.tsx`; `lib/azure-api.ts`; `tests/unit/account-api.test.ts`; `tests/unit/account-receipts-component.test.ts`; `tests/e2e/account-receipts.spec.ts`; `docs/payment-receipts-tax-invoices.md`.
- Verification evidence: `npm run typecheck`, `npm run test` (301 tests), `npm run build`, `npm run test -- tests/unit/account-api.test.ts`, `npm run test -- tests/unit/account-receipts-component.test.ts`, `git diff --check`, and banned-term grep over `app components public lib` passed. `npx playwright test tests/e2e/account-receipts.spec.ts --project=chromium` reached browser startup and failed before page execution with Chromium `MachPortRendezvousServer` permission denied in the local macOS sandbox.
- Limitations: No desktop/mobile screenshots were captured because local Chromium could not launch. Component coverage verifies the seeded purchase list and receipt link; the Playwright spec is present for the supervisor or a browser-capable environment to execute.

### 2026-06-01 - resilience-test-generation - PAY-22 Stripe reconciliation failure coverage

- Agent: Codex
- Trigger: GitHub issue #390/PAY-22 requires detecting missed webhooks and payment/ledger drift without Stripe writes.
- Action: Opened and followed the skill; modeled the critical payment-provider boundary with deterministic fake Stripe payment-intent data, a local EF SQLite ledger, and a fake read-only Stripe client that tracks write attempts.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeReconciliationServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeReconciliationService.cs`.
- Verification evidence: Red run failed on missing reconciliation contracts. Green focused run `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter StripeReconciliationServiceTests` passed 4/4, including paid-but-no-grant, amount-mismatch, grant-but-no-payment, clean dataset, and read-only client assertions. Full `cd backend-dotnet && dotnet test` passed 411/411.
- Limitations: Tests use fake Stripe payment intents only; no live Stripe, refund, grant, or charge creation call was made.

### 2026-06-01 - data-module-review - PAY-22 reconciliation run persistence

- Agent: Codex
- Trigger: PAY-22 adds a persisted reconciliation summary and reads the `RewriteCredit` payment ledger by `StripePaymentIntentId`.
- Action: Opened and followed the skill; reviewed `RewriteCredit` payment fields and indexes, added a `StripeReconciliationRun` table with count fields and structured report JSON, and kept reconciliation write behavior limited to recording the run summary.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/StripeReconciliationRun.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260531213301_AddStripeReconciliationRuns.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`.
- Verification evidence: EF SQLite tests verified persisted run counts/report JSON and admin stats summary. `git diff --check` passed. Full `cd backend-dotnet && dotnet test` passed 411/411.
- Limitations: The reconciler does not auto-create grants, refunds, or webhook replay records. Manual event reprocessing remains optional/out of scope for this issue.

### 2026-06-01 - dotnet-backend-testing - PAY-22 reconciliation xUnit coverage

- Agent: Codex
- Trigger: PAY-22 requires backend unit tests and `cd backend-dotnet && dotnet test`.
- Action: Opened and followed the skill; wrote failing service-level xUnit tests first, then implemented the reconciliation service, Stripe read client adapter, notification alert hook, scheduled Functions timer, migration, and admin stats summary.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeReconciliationServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeReconciliationService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeReconciliationTimerFunction.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Notifications/NotificationTemplates.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`.
- Verification evidence: Focused red run failed on missing reconciliation types. Focused green run `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter StripeReconciliationServiceTests` passed 4/4. Full backend gate `cd backend-dotnet && dotnet test` passed 411/411. Banned-term grep over `app components public lib` returned no matches.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages. `dotnet ef migrations add` generated the migration after the application host timed out and fell back to the design-time context factory.

### 2026-06-01 - data-module-review - PAY-25 price versioning credit invariants

- Agent: Codex
- Trigger: GitHub issue #393/PAY-25 changes `RewriteCredit` persistence and explicitly requires `data-module-review`.
- Action: Opened and followed the skill; reviewed `RewriteCredit`, `StripeBillingService.SkuDefinitions`, checkout metadata grant creation, account summary balance reads, and partial refund clawback math. Added nullable `OriginalAmountGranted` with migration backfill so historical purchase size is persisted independently of the current SKU map.
- Output artifacts: `docs/price-change-playbook.md`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260531224044_AddRewriteCreditOriginalAmountGranted.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`.
- Verification evidence: Data risk scan ran over `backend-dotnet/src`; focused Stripe event tests passed 21/21; full `cd backend-dotnet && dotnet test` passed 408/408.
- Limitations: EF migration generation emitted a design-time host timeout warning after producing the migration, so the migration was reviewed and the backfill SQL was added manually.

### 2026-06-01 - dotnet-backend-testing - PAY-25 historical grant regression

- Agent: Codex
- Trigger: PAY-25 requires a .NET regression test proving later SKU size changes do not alter the balance of an old granted credit.
- Action: Opened and followed the skill; added a failing xUnit regression in `StripeEventServiceTests` for an old 7-rewrite `quick_pack` grant while the current SKU map grants 10, then fixed the service to use the persisted original grant count for cumulative partial refunds.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs`.
- Verification evidence: Red run failed with `Expected credit.AmountGranted to be 3, but found 5`; green focused run for the regression passed 1/1; focused `StripeEventServiceTests` passed 21/21; full `cd backend-dotnet && dotnet test` passed 408/408.

### 2026-06-01 - data-module-review - PAY-29 accounting revenue export

- Agent: Codex
- Trigger: PAY-29 reads payment/accounting data from `RewriteCredit` and existing usage/cost persistence without changing schema.
- Action: Opened and followed the skill; reviewed `RewriteCredit`, `UsagePeriod`, `UsageReservation`, `RewriteCostLog`, `AppDbContext`, existing admin endpoints, and ran the data risk scanner over `backend-dotnet`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminServiceTests.cs`.
- Verification evidence: No migration or new persisted field was added. The export reads admin payment rows from the existing credit ledger, emits only payment/accounting fields, and uses page-sized reads plus response-body CSV writes. Focused `dotnet test ReplyInMyVoice.sln --filter AdminAccountingRevenueCsv --no-restore` passed 3/3; full `cd backend-dotnet && dotnet test` passed 410/410; `git diff --check` passed.
- Limitations: `receiptUrl` is included as a CSV column but remains empty when the ledger has no stored receipt URL; receipt capture is outside PAY-29.

### 2026-06-01 - dotnet-backend-testing - PAY-29 admin CSV export coverage

- Agent: Codex
- Trigger: PAY-29 requires .NET backend coverage for admin CSV export, non-admin 403, CSV escaping, date range filtering, and paged export behavior.
- Action: Opened and followed the skill; wrote failing xUnit tests first for the missing function/service methods, then implemented the admin function and service writer until the focused suite passed.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/TestHostEnvironment.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`.
- Verification evidence: Red run `dotnet test ReplyInMyVoice.sln --filter AdminAccountingRevenueCsv` failed on missing `ExportAccountingRevenueCsv` and `WriteAccountingRevenueCsvAsync`. Green runs: `dotnet test ReplyInMyVoice.sln --filter AdminAccountingRevenueCsv --no-restore` passed 3/3, isolated API host timeout repros passed after moving test-host settings to a module initializer, full `cd backend-dotnet && dotnet test` passed 410/410, and `git diff --check` passed.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-06-01 - cloud-architecture-cost-review - PAY-30 multi-currency design

- Agent: Codex
- Trigger: GitHub issue #398/PAY-30 explicitly requires cloud-architecture-cost-review for multi-currency payment coverage.
- Action: Opened and followed the skill; reviewed the payment audit, manual setup notes, Azure/Cloudflare backend posture, current Stripe SKU mapping, and PAY-22/PAY-29 reporting briefs. Recommended separate Stripe Prices per SKU per currency, explicit user currency selection with optional geo preselection, and NZD fallback without adding cloud resources or application-side exchange-rate logic.
- Output artifacts: `docs/multi-currency-plan.md`; `docs/skill-run-log.md`.
- Verification evidence: `docs/multi-currency-plan.md` records the architecture cost review, approval gates, rejected options, and reporting implications. `cd backend-dotnet && dotnet test` passed 407/407. `git diff --check` passed.
- Limitations: No exact Stripe fee comparison was performed because PAY-30 does not select a concrete additional currency, quote prices, create Stripe Prices, or provision paid resources. No optional multi-currency implementation was added because the issue did not include owner opt-in.

### 2026-06-01 - system-spec-synthesis - PAY-30 multi-currency implementation-ready plan

- Agent: Codex
- Trigger: PAY-30 asks for a design document covering future SKU/currency price mapping, currency selection, data persistence, and reconciliation/export behavior.
- Action: Opened and followed the skill; converted the issue, PAY brief, payment audit, current backend/frontend code, and reporting briefs into an implementation-ready specification with goals, non-goals, current system, proposed architecture, data model, API contracts, rollout, verification, and open questions.
- Output artifacts: `docs/multi-currency-plan.md`; `docs/skill-run-log.md`.
- Verification evidence: `docs/multi-currency-plan.md` is non-empty and documents per-currency Stripe price IDs, SKU-to-currency price mapping, user/geo currency selection, existing `RewriteCredit.StripeCurrency` persistence, and PAY-22/PAY-29 grouping by currency. `cd backend-dotnet && dotnet test` passed 407/407. `git diff --check` passed.
- Limitations: The output is a design/spec artifact only. Backend SKU resolution, frontend currency selection, and two-currency unit coverage remain deferred until owner supplies a concrete currency decision and Stripe price IDs through runtime configuration.

### 2026-06-01 - resilience-test-generation - PAY-31 checkout velocity and refund review signals

- Agent: Codex
- Trigger: GitHub issue #399/PAY-31 requires purchase-side abuse controls and explicitly names resilience-test-generation for checkout velocity limiting and refund-abuse monitoring tests.
- Action: Opened and followed the skill; identified checkout-session creation as the critical operation, kept Stripe calls behind a fake billing service, and wrote the failing 429/under-limit API test before adding the limiter.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/CheckoutVelocityLimiter.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/BillingHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`; `docs/fraud-controls.md`.
- Verification evidence: Red run failed on missing `CheckoutVelocityLimiter`. Green focused run `cd backend-dotnet && dotnet test ReplyInMyVoice.sln --filter "FullyQualifiedName~StripeBillingApiTests|FullyQualifiedName~AdminServiceTests"` passed 11/11. Full `cd backend-dotnet && dotnet test` passed 409/409.
- Limitations: The limiter is process-local and intended as a purchase-side throttle before creating a Stripe Checkout session. Stripe Radar dashboard rules remain owner-configured.

### 2026-06-01 - data-module-review - PAY-31 AdminAuditLog refund review aggregate

- Agent: Codex
- Trigger: PAY-31 reads refund entries from `AdminAuditLog` and adds admin stats derived from persisted audit data.
- Action: Opened and followed the skill; reviewed the existing `AdminAuditLog` action/details shape and reused `AdminRefundAuditDetails` parsing without adding tables, migrations, or auto-actions.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminServiceTests.cs`; `docs/fraud-controls.md`.
- Verification evidence: Added xUnit coverage for count-threshold and amount-threshold refund review flags computed from persisted audit rows. Focused admin/billing tests passed 11/11; full `cd backend-dotnet && dotnet test` passed 409/409.
- Limitations: Malformed refund audit details are ignored for the aggregate. The stats are informational and do not suspend, refund, or block a user.

### 2026-06-01 - dotnet-backend-testing - PAY-31 backend tests

- Agent: Codex
- Trigger: PAY-31 adds C#/.NET backend behavior and requires tests for checkout velocity and refund-review stats.
- Action: Opened and followed the skill; added WebApplicationFactory coverage for checkout 429 behavior, EF SQLite-backed function coverage for admin stats, and a test-process polling watcher bootstrap to prevent the macOS sandbox FileSystemWatcher host-startup crash.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/TestEnvironmentBootstrap.cs`.
- Verification evidence: Red run failed on missing checkout limiter/admin stats properties. Green runs: the new checkout test passed 1/1, the new admin stats test passed 1/1, focused admin/billing tests passed 11/11, and full `cd backend-dotnet && dotnet test` passed 409/409.
- Limitations: Dotnet restore emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but cached restore/build/test succeeded.

### 2026-06-01 - ui-browser-testing - PAY-31 admin refund-review stat surface

- Agent: Codex
- Trigger: PAY-31 adds a browser-visible admin stats tile for the refund-review aggregate.
- Action: Opened and followed the skill; updated the admin stats TypeScript contract, dashboard stat tile, and admin Playwright fixture/assertion for the new refund-review count.
- Output artifacts: `components/admin/admin-dashboard.tsx`; `lib/admin-types.ts`; `tests/e2e/admin.spec.ts`.
- Verification evidence: `npm run typecheck` passed; `npm run test` passed 298/298; banned-term grep over `app components public lib` returned no matches.
- Limitations: `npx playwright test tests/e2e/admin.spec.ts --project=chromium` failed before page assertions because Chromium launch is blocked in this sandbox by `MachPortRendezvousServer` permission denied. A Next dev server started during the attempt, but the sandbox refused permission to terminate its child process afterward.

### 2026-06-03 - ui-browser-testing - PROMO-FIX-01 admin header entry

- Agent: Codex
- Trigger: PROMO-FIX-01 changes the browser-visible site header navigation for signed-in admins.
- Action: Opened and followed the skill; added focused server-rendered header coverage for admin, non-admin, and signed-out navigation states, then reused the existing admin session helper in the header.
- Output artifacts: `components/site-header.tsx`; `tests/unit/site-header.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run failed on missing `href="/admin"` for the admin session, focused green run passed 3/3, `npm run typecheck` passed, `npm run test` passed 348/348, and the banned-term grep over `app components public lib` returned no matches.
- Limitations: No screenshot run was added because the change is a conditional server-rendered nav link with existing styling and no layout/CSS changes.

### 2026-06-03 - ui-browser-testing - PROMO-FIX-02 workspace-first redeem modal

- Agent: Codex
- Trigger: PROMO-FIX-02 changes the browser-visible `/app` workspace, quota status bar, redeem-code modal, and out-of-credit nudges.
- Action: Opened and followed the skill; updated focused unit and Playwright acceptance coverage for workspace-first rendering, redeem modal behavior, and inline buy/manage nudges before implementing the frontend-only change.
- Output artifacts: `app/app/page.tsx`; `components/app/rewrite-workspace.tsx`; `components/app/subscription-status.tsx`; `components/app/redeem-code-card.tsx`; `lib/promo-app-state.ts`; `tests/unit/promo-app-state.test.ts`; `tests/unit/workspace-copy.test.ts`; `tests/e2e/promo-redeem-ui.spec.ts`; `tests/e2e/promo-full-loop.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red unit run failed on the old full-page state/card contracts. Focused green unit run passed 14/14. `npm run typecheck` passed. `npm run test` passed 349/349. Banned-term grep over `app components public lib` returned no matches.
- Limitations: Focused Playwright run `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/pw-browsers-issue-452 npx playwright test tests/e2e/promo-redeem-ui.spec.ts --project=promo-chromium` could not execute browser assertions because Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied. Local git commit was also blocked because the shared worktree git index lives outside the writable root.

### 2026-06-03 - dotnet-backend-testing - FIX-03 admin Functions route metadata

- Agent: Codex
- Trigger: FIX-03 changes C# Azure Functions admin HTTP trigger routes and backend route-test coverage.
- Action: Opened and followed the skill; added a failing reflection test proving every `AdminHttpFunctions` `HttpTrigger` route uses the non-reserved `console/` prefix, then renamed the Functions route attributes and updated frontend proxy target unit expectations.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRouteMetadataTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `app/api/admin/**/route.ts`; `app/admin/promo-codes/page.tsx`; `tests/unit/admin-api-routes.test.ts`; `tests/unit/admin-promo-proxy-route.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red run `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter AdminRouteMetadataTests` failed because all admin routes still used `admin/`. Green focused run passed 1/1. Full `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 491/491. `npm run typecheck` passed. `npm run test` passed 349/349. `grep -rE 'Route = "admin/' backend-dotnet/src` returned no matches. Frontend `/api/admin` forward-target grep over `app lib` returned no matches. Banned-term grep over `app components public lib` returned no matches.
- Limitations: Initial `npm ci` failed because npm attempted to create a cache outside the writable sandbox; rerunning with `--cache /private/tmp/npm-cache-issue455` succeeded. Local git commit was blocked because the shared worktree git index lives outside the writable root. The current shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE` warnings, but typecheck and tests passed.

### 2026-06-03 - ui-browser-testing - FIX-04 workspace action placement

- Agent: Codex
- Trigger: FIX-04 changes the browser-visible `/app` subscription status bar and out-of-credits output-column nudge.
- Action: Opened and followed the skill; wrote failing source-level workspace copy coverage for the old `Upgrade` label and output-column account actions, updated promo Playwright assertions for the top-bar `Buy rewrites` button and absence of output-column action links, then moved the account actions into the status bar by copy/prop cleanup only.
- Output artifacts: `components/app/subscription-status.tsx`; `components/app/rewrite-workspace.tsx`; `tests/unit/workspace-copy.test.ts`; `tests/e2e/promo-redeem-ui.spec.ts`; `tests/e2e/promo-full-loop.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm run test -- tests/unit/workspace-copy.test.ts` failed on the old `Upgrade` label and output nudge controls. Focused green run passed 8/8. `npm run typecheck` passed. `npm run test` passed 349/349. Banned-term grep over `app components public lib` returned no matches.
- Limitations: Focused Playwright run `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/promo-ux2-issue-456-ms-playwright npx playwright test tests/e2e/promo-redeem-ui.spec.ts --project=promo-chromium` built the app but could not execute browser assertions because Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied. Initial Playwright attempt also required installing Chromium into a temporary browser cache.

### 2026-06-03 - ui-browser-testing - ADM-01 promo create defaults

- Agent: Codex
- Trigger: ADM-01 changes the browser-visible admin promo-code create form defaults, promo status guidance, and admin navigation.
- Action: Opened and followed the skill; added a failing component render test for immediate-active defaults, 90-day expiry, status legend copy, back-to-admin navigation, and `derivePromoCodeStatus` behavior, then added matching admin Playwright acceptance coverage.
- Output artifacts: `components/admin/promo-codes-admin.tsx`; `tests/unit/admin-promo-codes-component.test.ts`; `tests/e2e/admin-promo-codes.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm run test -- tests/unit/admin-promo-codes-component.test.ts` failed on missing `href="/admin"`. Focused green run passed 1/1. `grep -n "inFiveMinutes" components/admin/promo-codes-admin.tsx` returned no matches. `npm run typecheck` passed. `npm run test` passed 350/350. Banned-term grep over `app components public lib` returned no matches. Authenticated local page smoke against `http://127.0.0.1:3001/admin/promo-codes` passed with `validFrom=2026-06-03T19:42`, `validUntil=2026-09-01T19:42`, and `days=90.00`.
- Limitations: `npx playwright test tests/e2e/admin-promo-codes.spec.ts -g "admin create defaults" --project=promo-chromium` could not run before dependency install because npm attempted to write its cache outside the writable sandbox. After installing dependencies and Chromium into writable temp paths, the focused Playwright run still could not execute browser assertions because Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied. The in-app browser backend was also unavailable for this session. Local verification servers on ports 3001 and 45934 were started for the authenticated HTTP smoke, but the sandbox denied signal-based cleanup afterward.

### 2026-06-03 - ui-browser-testing - ADM-02 dashboard nav and user management

- Agent: Codex
- Trigger: ADM-02 changes the browser-visible `/admin` dashboard navigation, Users table filtering, status guidance, and row actions.
- Action: Opened and followed the skill; wrote route and admin Playwright coverage for the promo-code navigation link, erased-account hiding note, status legend, and confirmed user erase action before implementing the Next DELETE proxy and dashboard UI.
- Output artifacts: `components/admin/admin-dashboard.tsx`; `lib/admin-api-proxy.ts`; `app/api/admin/users/[userId]/route.ts`; `tests/unit/admin-api-routes.test.ts`; `tests/e2e/admin.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red route run `npm run test -- tests/unit/admin-api-routes.test.ts` failed on missing `DELETE`; focused green run passed 5/5. `npm run typecheck` passed. `npm run test` passed 351/351. `npm run lint` exited 0 with one existing `components/account/account-panel.tsx` warning. Banned-term grep over `app components public lib` returned no matches. `git diff --check` passed.
- Limitations: Focused Playwright run `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/ms-playwright-issue-460 npx playwright test tests/e2e/admin.spec.ts --project=chromium` could not execute browser assertions because Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied. Installing dependencies initially hit a root-owned npm cache, so dependencies were installed using a writable temp npm cache. The in-app browser backend was unavailable for this worker session.

### 2026-06-03 - system-spec-synthesis - ADM-03 admin delete-user endpoint

- Agent: Codex
- Trigger: ADM-03 adds a new C# Functions API contract, service method, guard behavior, audit entry, and backend acceptance checks.
- Action: Opened and followed the skill; treated GitHub issue #461 and `plans/admin-polish-issues/ADM-03-backend-delete-user-endpoint.md` as the authoritative implementation-ready spec, then mapped it to the existing admin route/service/test structure without creating a separate design artifact.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminDeleteUserTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRouteMetadataTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Issue acceptance greps found both GET and DELETE `console/users/{userId}` triggers and `AdminDeleteUser`; full `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 496/496.
- Limitations: No new standalone spec was written because the issue brief was already the approved spec for this unattended worker run.

### 2026-06-03 - state-machine-modeling - ADM-03 account erase guard states

- Agent: Codex
- Trigger: ADM-03 changes account erase lifecycle behavior from the admin console, including active target, self-target, missing target, and already-erased target states.
- Action: Opened and followed the skill; modeled the allowed event as admin DELETE on an active non-self account, with terminal erased accounts and self-delete attempts rejected before mutation or audit.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminDeleteUserTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Added xUnit coverage for successful active-account erase plus audit, missing-user not found, self-delete forbidden, and already-erased forbidden. Focused `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter AdminDeleteUser` passed 5/5; full solution tests passed 496/496.
- Limitations: The state model is enforced in service code and tests; no new persisted status field or migration was added.

### 2026-06-03 - data-module-review - ADM-03 account erase persistence

- Agent: Codex
- Trigger: ADM-03 mutates persisted `AppUser` account data through the existing account erase service and writes `AdminAuditLog`.
- Action: Opened and followed the skill; reviewed `AccountService.DeleteAccountAsync`, `AdminService` audit patterns, `AdminAuditLog`, `AppUser`, and SQLite-backed tests. Reused the existing erase path instead of duplicating child-row scrubbing or adding cascade deletes.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminDeleteUserTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 20 backend-dotnet/src` completed and returned existing broad persistence signals. New tests assert user anonymization, usage-period reset, credit reset, audit creation, and no audit for rejected guards. No migration files are modified.
- Limitations: The account erase and audit insert are separate database saves because `AccountService.DeleteAccountAsync` owns its existing transaction boundary; no schema changes were made.

### 2026-06-03 - dotnet-backend-testing - ADM-03 backend delete-user coverage

- Agent: Codex
- Trigger: ADM-03 adds C# Azure Functions and `AdminService` behavior requiring xUnit/backend coverage.
- Action: Opened and followed the skill; wrote failing tests first for missing `DeleteUserAsync`, result types, public erased-account helper, function route, and route metadata, then implemented the endpoint and service until focused and full backend suites passed.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminDeleteUserTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminRouteMetadataTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `docs/skill-run-log.md`.
- Verification evidence: Red run `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter AdminDeleteUser` failed on missing delete-user API symbols. Green focused run passed 5/5. Full `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 496/496. Acceptance greps passed; banned-term scans over the diff, backend source/tests, and `app components public lib` returned no matches.
- Limitations: `dotnet test` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore/build/test completed. Local git commit was blocked because the shared worktree git index lives outside the writable sandbox.

### 2026-06-04 - ui-browser-testing - PROMO-ADMIN-B list-first promo-code admin IA

- Agent: Codex
- Trigger: PROMO-ADMIN-B changes browser-visible `/admin/promo-codes` layout, create/edit forms, row actions, responsive table behavior, and Playwright admin promo flows.
- Action: Opened and followed the skill; wrote a failing component test for exported create defaults and table-first markup, then moved the create/edit forms into modal dialogs, moved stats into a right-side drawer, added client-side search/status/sort controls, and updated the focused admin promo Playwright spec for the new modal/drawer flow.
- Output artifacts: `components/admin/promo-codes-admin.tsx`; `tests/unit/admin-promo-codes-component.test.ts`; `tests/e2e/admin-promo-codes.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm test -- tests/unit/admin-promo-codes-component.test.ts` failed on missing `initialFormValues` export and missing `role="table"`. Focused green run passed 2/2. `npm run typecheck` passed. `npm run test` passed 354/354. `npm run build` passed. Banned-term grep over `app components public lib` returned no matches.
- Limitations: Focused Playwright run `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/claude-501/ms-playwright npx playwright test tests/e2e/admin-promo-codes.spec.ts --project=promo-chromium` could not execute browser assertions because Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied. `npm run build` emitted an existing `components/account/account-panel.tsx` hook-dependency warning outside this issue scope.

### 2026-06-04 - dotnet-backend-testing - AUTH-EMAIL-01 federated email claim capture

- Agent: Codex
- Trigger: AUTH-EMAIL-01 changes C# API email-claim resolution and requires xUnit backend coverage.
- Action: Opened and followed the skill; wrote failing xUnit coverage for the missing API claims helper, then extracted claim-to-email resolution into `AuthEmailResolver` and kept the existing dev header path in `Program.cs`. Also added frontend unit coverage for the matching TypeScript claim priority and safe callback diagnostics.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/AuthEmailResolver.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiAuthEmailResolverTests.cs`; `lib/entra-auth.ts`; `tests/unit/entra-auth.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `dotnet test --filter ApiAuthEmailResolverTests` failed because `ReplyInMyVoice.Api.AuthEmailResolver` did not exist. Focused red run `npm run test -- tests/unit/entra-auth.test.ts` failed 7/25 on missing `emailClaim` export. Focused green runs passed 6/6 backend resolver tests and 26/26 frontend auth tests. Full `npm run typecheck` passed. Full `npm run test` passed 362/362. Full `cd backend-dotnet && dotnet test` passed 505/505. Source substring scan over changed source/test areas returned no matches.
- Limitations: `npm ci` initially failed because npm attempted to write cache outside the writable worktree; rerunning with an in-worktree cache succeeded. Local `git add`/commit was blocked because this worktree's Git metadata is outside the writable sandbox. The shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE` warnings, but typecheck and tests passed. `dotnet test` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore/build/test completed.

### 2026-06-04 - ui-browser-testing - WS-01 dynamic trial quota display

- Agent: Codex
- Trigger: WS-01 changes browser-visible `/app` trial quota copy and promo Playwright assertions.
- Action: Opened and followed the skill; wrote failing unit coverage for exact trial expiry copy and configured promo grant totals, then updated `/app` to derive trial remaining/granted values from existing quota sources and updated promo e2e assertions for the exact expiry suffix.
- Output artifacts: `app/app/page.tsx`; `lib/promo-app-state.ts`; `lib/azure-api.ts`; `tests/unit/promo-app-state.test.ts`; `tests/unit/workspace-copy.test.ts`; `tests/e2e/promo-redeem-ui.spec.ts`; `tests/e2e/promo-full-loop.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm run test -- tests/unit/promo-app-state.test.ts` failed on the old expiry copy and missing trial credit helper; a later focused red run caught partial missing-grant undercounting. Focused green run `npm run test -- tests/unit/promo-app-state.test.ts` passed 9/9. `npm run typecheck` passed. `npm run test` passed 364/364. `npm run build` passed. Source substring scan over `app components public lib` returned no matches. `git diff --check` passed.
- Limitations: Focused Playwright run with `PLAYWRIGHT_BROWSERS_PATH=./.playwright-browsers` for the two promo specs could not execute browser assertions because Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied. The first Playwright attempt also needed a local Chromium install. Local `git add`/commit was blocked because this worktree's Git metadata is outside the writable sandbox. The shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE` during install, but typecheck, unit tests, and build passed.

### 2026-06-04 - ui-browser-testing - WS-02 workspace buy rewrites checkout flow

- Agent: Codex
- Trigger: WS-02 changes the browser-visible `/app` workspace billing button, auth redirect behavior, and checkout error state.
- Action: Opened and followed the skill; wrote failing unit coverage for the workspace `Buy rewrites` button before replacing the sku-less direct Functions checkout call with the same-origin checkout proxy, explicit `value_pack` sku, 401 sign-in redirect, and inline error state.
- Output artifacts: `components/app/subscription-status.tsx`; `tests/unit/workspace-buy-button-checkout-flow.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm test -- tests/unit/workspace-buy-button-checkout-flow.test.ts` failed 3/4 on the direct Functions call, missing proxy request, and missing error state. Focused green run passed 4/4. Existing `npm test -- tests/unit/buy-button-checkout-flow.test.ts` passed 3/3. `npm run typecheck` passed. `npm run test` passed 368/368. `npm run lint` exited 0 with one existing `components/account/account-panel.tsx` hook-dependency warning. `npm run build` passed with the same existing warning. Source substring scan over `app components public lib` returned no matches. `git diff --check` passed.
- Limitations: Focused Playwright run `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/claude-501/ms-playwright npx playwright test tests/e2e/promo-redeem-ui.spec.ts -g "top-bar buy rewrites button starts checkout" --project=promo-chromium` could not execute browser assertions because Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied. `npm ci` initially hit a root-owned shared npm cache; rerunning with `npm_config_cache=/private/tmp/npm-cache-issue481` succeeded. Local `git add`/commit was blocked because this worktree's Git metadata is outside the writable sandbox. The shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE` during install, but typecheck, unit tests, lint, and build passed.

### 2026-06-04 - ui-browser-testing - WS-03 per-user rewrite history scoping

- Agent: Codex
- Trigger: WS-03 changes browser-visible `/app` local rewrite history and sign-out behavior.
- Action: Opened and followed the skill; wrote a failing unit test for local history key scoping before adding a shared localStorage helper, passing the Azure account user key into `RewriteWorkspace`, clearing the legacy key on workspace mount, and clearing local rewrite history before logout from the header and account panel.
- Output artifacts: `lib/rewrite-history.ts`; `components/app/rewrite-workspace.tsx`; `app/app/page.tsx`; `components/sign-out-link.tsx`; `components/site-header.tsx`; `components/account/account-panel.tsx`; `tests/unit/rewrite-history.test.ts`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm run test -- tests/unit/rewrite-history.test.ts` failed because `lib/rewrite-history` did not exist. Focused green run passed 3/3. Focused copy/header run passed 14/14. `npm run typecheck` passed. `npm run test` passed 371/371. `npm run build` passed. Source substring scan over `app components public lib` returned no matches.
- Limitations: `npm ci` initially hit a root-owned shared npm cache; rerunning with `npm_config_cache=/private/tmp/rimv-ws-03-npm-cache` succeeded. The shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE` during install, but typecheck, unit tests, and build passed. No Playwright browser run was added because the issue acceptance required unit coverage for the localStorage helper and no layout or e2e flow change.

### 2026-06-04 - ui-browser-testing - WS-04 bold workspace redesign

- Agent: Codex
- Trigger: WS-04 changes browser-visible `/app` layout, shared button/textarea primitives, quota actions, AI Signal presentation, recent rewrite history, responsive behavior, and Playwright-pinned workspace affordances.
- Action: Opened and followed the project skill; wrote failing unit assertions for the new layout contracts before replacing the mixed Tailwind surface with a single workspace shell, shared UI buttons, shared draft textarea, promoted recent rewrites section, slim AI Signal empty state, and updated checkout-flow test traversal for shared Button components.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `components/app/subscription-status.tsx`; `components/ui/button.tsx`; `components/ui/textarea.tsx`; `tests/unit/workspace-copy.test.ts`; `tests/unit/workspace-buy-button-checkout-flow.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red `npm run test -- tests/unit/workspace-copy.test.ts` failed on the old AI Signal empty card, text-link quota actions, and old `max-w-5xl` workspace body. Focused green runs passed for workspace copy, checkout-flow, and visual-system unit coverage. Full `npm run typecheck`, `npm run test`, `npm run build`, and `npm run cf:build` passed. Restricted-copy scan over `app components public lib` returned no matches. `git diff --check` passed.
- Limitations: Focused promo Playwright specs could not execute browser assertions because Chromium launch is blocked in this macOS sandbox before test code runs. `npm ci` required a writable cache under `/private/tmp`. The shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE` during install, but typecheck, unit tests, and builds passed. Local `git add`/commit was blocked because this worktree's Git metadata is outside the writable sandbox.

### 2026-06-04 - ui-browser-testing - PRICE-01 pricing page redesign foundation

- Agent: Codex
- Trigger: PRICE-01 changes browser-visible `/pricing` hero hierarchy, rewrite pack cards, downstream pricing sections, responsive CSS, and page rendering.
- Action: Opened and followed the project skill; wrote a failing unit test for the pricing foundation slots, unit-price helper lines, group labels, component marker comments, and CSS region before implementing the redesigned pricing page and three downstream component stubs.
- Output artifacts: `app/pricing/page.tsx`; `app/globals.css`; `components/landing/pricing-comparison.tsx`; `components/landing/pricing-trust.tsx`; `components/landing/pricing-faq.tsx`; `tests/unit/pricing-redesign-page.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm run test -- tests/unit/pricing-redesign-page.test.ts` failed on missing component imports, helper lines, component files, and CSS markers. Focused green run passed 4/4. Final `npm run typecheck` passed. Final `npm run test` passed 378/378. Required restricted-copy scan over `app components public lib` returned no matches. `npm run lint` exited 0 with one unrelated existing warning in `components/account/account-panel.tsx`. `git diff --check` passed. Local dev `curl -fsS http://127.0.0.1:3101/pricing` returned 200 HTML containing the new group labels, unit-price helper lines, and the three downstream sections.
- Limitations: Chromium Playwright screenshots could not execute because browser launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied; Firefox/WebKit were not installed. The Next dev server emitted repeated `EMFILE` watcher warnings while still serving `/pricing` with HTTP 200. `npm ci` required a writable cache under `/private/tmp`. The shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE`, but typecheck, tests, and lint completed.

### 2026-06-04 - ui-browser-testing - NAV-01 shared mobile navigation

- Agent: Codex
- Trigger: NAV-01 changes the browser-visible shared header navigation, narrow-screen link visibility, and mobile menu behavior.
- Action: Opened and followed the project skill; wrote a failing source/render test for the missing menu affordance and broad mobile link hide before adding a CSS-only `<details>` disclosure, preserving the server-resolved auth branches, and limiting CSS changes to the nav region.
- Output artifacts: `components/site-header.tsx`; `app/globals.css`; `tests/unit/site-header-mobile-nav.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run `npm run test -- tests/unit/site-header-mobile-nav.test.ts` failed on missing `mobile-nav-menu` header/CSS tokens. Focused green run passed 3/3. Existing header focused run passed 6/6 across `tests/unit/site-header.test.ts` and `tests/unit/site-header-mobile-nav.test.ts`. Final `npm run typecheck` passed. Final `npm run test` passed 381/381. Required restricted substring scan over `app components public lib` returned no matches.
- Limitations: Local browser assertions could not execute because Playwright Chromium launch is blocked in this macOS sandbox by `MachPortRendezvousServer` permission denied; full Chromium also crashed before page load, and a fresh Firefox install under `/private/tmp` exited before launch. The Next dev server emitted repeated `EMFILE` watcher warnings while reaching ready state. `npm ci` initially hit a root-owned shared npm cache; rerunning with `--cache /private/tmp/npm-cache-issue-494` succeeded. Local `git add`/commit was blocked because this worktree's Git metadata is outside the writable sandbox. The shell uses Node v24.9.0 while `package.json` declares `>=22 <23`; npm emitted `EBADENGINE`, but typecheck and unit tests completed.

### 2026-06-04 - system-spec-synthesis - API-01 API key service contract

- Agent: Codex
- Trigger: API-01 changes an API-key data model and service contract from issue #503 plus `plans/rewrite-api-v1/SPEC.md`.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, the API-01 brief, and the rewrite API spec data/key-format sections. Converted the requirements into scoped checkpoints: add nullable `ApiKey.Last4`, keep plaintext reveal-once, hash with runtime `API_KEY_PEPPER`, list only masked summaries, owner-only revoke, register the infrastructure service, and avoid auth resolver/routes.
- Output artifacts: Implementation checkpoints reflected in `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs`, EF model/migration files, and `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`.
- Verification evidence: Focused red `dotnet test --filter ApiKeyServiceTests` failed on missing `ApiKeyService`; focused green passed 4/4; final `dotnet build` passed; final `dotnet test` passed 509/509.
- Limitations: No separate spec file was added because the issue and brief were already implementation-ready and the requested scope was a narrow prerequisite service.

### 2026-06-04 - state-machine-modeling - API-01 API key revoke lifecycle

- Agent: Codex
- Trigger: API-01 adds revoke behavior for persisted API keys with active/revoked lifecycle semantics.
- Action: Opened and followed the skill; modeled API-key state as active when `RevokedAt` is null and revoked when `RevokedAt` is set. Events covered create key, list keys, owner revoke, non-owner revoke attempt, and duplicate owner revoke. Invariants: plaintext is never stored; `KeyHash` remains unique; `Last4` is nullable display metadata; non-owner revoke does not mutate; revoked timestamp is not rewritten by duplicate revoke.
- Output artifacts: `ApiKeyService.GenerateAsync`, `ListAsync`, and `RevokeAsync`; `ApiKeyServiceTests.RevokeAsync_sets_revoked_at_for_owner_and_returns_false_for_non_owner`.
- Verification evidence: Focused service tests passed 4/4 after implementation; final `dotnet test` passed 509/509.
- Limitations: API-key auth resolution, expiry handling, and rate-limit state are intentionally deferred to later API wave issues.

### 2026-06-04 - data-module-review - API-01 ApiKey Last4 persistence

- Agent: Codex
- Trigger: API-01 changes EF Core entity mapping, adds a nullable `ApiKeys.Last4` column, and introduces a data access service that mutates `ApiKeys`.
- Action: Opened and followed the skill; reviewed `ApiKey`, `AppDbContext`, existing unique `KeyHash` index, service factory patterns, and generated migration output. Confirmed the migration is additive/nullable only, preserves existing rows, keeps the existing `KeyHash` uniqueness invariant, and scopes list/revoke queries by `UserId`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKey.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260604111210_AddApiKeyLast4.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260604111210_AddApiKeyLast4.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs`.
- Verification evidence: `dotnet ef migrations add AddApiKeyLast4` completed; migration body adds nullable `nvarchar(4)` `Last4` and drops it on rollback; final `dotnet build` passed; final `dotnet test` passed 509/509.
- Limitations: The service does not add collision retry logic around the unique hash index because the issue did not request it and the generated key has high entropy.

### 2026-06-04 - dotnet-backend-testing - API-01 ApiKeyService coverage

- Agent: Codex
- Trigger: API-01 requires new xUnit tests for API key generation, hashing, masked listing, and revoke ownership.
- Action: Opened and followed the project skill; wrote failing service tests first using the existing EF SQLite `DbFixture`, then implemented `ApiKeyService`, the nullable display column, DI registration, and the EF migration. Used the existing lowest test level that proves persisted state and service behavior.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs`; API key EF model and migration files; `docs/skill-run-log.md`.
- Verification evidence: Initial red run `dotnet test --filter ApiKeyServiceTests` failed on missing `ApiKeyService`. A focused run exposed SQLite `DateTimeOffset` ordering, fixed by materializing owner rows before in-memory ordering. Final focused run passed 4/4. Final `dotnet build` passed. Final `dotnet test` passed 509/509. Restricted substring scan over `app components public lib` returned no matches.
- Limitations: `dotnet` commands emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore/build/test completed. `dotnet ef` reported local tool version 8.0.8 versus runtime 8.0.19 and still generated the migration. Local `git add`/commit was blocked because this worktree's Git metadata is outside the writable sandbox.

### 2026-06-04 - data-module-review - API-02 ApiKeyAuthResolver lookup

- Agent: Codex worker
- Trigger: API-02 adds a Functions auth helper that reads `ApiKeys` by the existing unique `KeyHash` and updates successful key usage metadata.
- Action: Opened and followed the project skill; reviewed `ApiKey`, `AppDbContext` key indexes, `ApiKeyService.ComputeHash`, and the resolver lookup/update path. Confirmed the change adds no schema or migration, keeps plaintext out of storage, rejects revoked/expired rows before mutation, and updates only `LastUsedAt` after a valid lookup.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyAuthResolverTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80 backend-dotnet` completed; output was broad/noisy and did not identify a new API-key resolver data issue. Focused resolver tests passed 6/6 with persisted `LastUsedAt` verification.
- Limitations: The scan reports many existing quota/idempotency signals across the backend and is not specific to this two-file change.

### 2026-06-04 - dotnet-backend-testing - API-02 ApiKeyAuthResolver coverage

- Agent: Codex worker
- Trigger: API-02 requires new xUnit coverage for valid, unknown, revoked, expired, missing-header, and non-live-prefix API key auth outcomes.
- Action: Opened and followed the project skill; wrote focused xUnit tests first using the existing EF SQLite `DbFixture` and `DefaultHttpContext`, then implemented the static resolver helper in the Functions auth namespace.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyAuthResolverTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused run with `--no-restore` stopped on missing fresh-worktree assets; focused red run `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter ApiKeyAuthResolverTests` then failed on missing `ApiKeyAuthResolver`. Focused green run passed 6/6.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore and focused tests completed.

### 2026-06-04 - system-spec-synthesis - API-04 v1 rewrite result contract

- Agent: Codex worker
- Trigger: API-04 implements the key-authenticated `GET /api/v1/rewrite/{id}` result contract from issue #506, the API-04 brief, and `plans/rewrite-api-v1/SPEC.md`.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, the API-04 brief, and the v1 API spec sections for result polling and error handling. Converted them into scoped checkpoints: resolve API key to user, query `RewriteAttempt` with `Id` plus `UserId`, map pending/processing/succeeded/failed states into the v1 result body, preserve owner isolation, and leave submit plus worker behavior unchanged.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `plans/decisions-log.md`.
- Verification evidence: Focused red `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter V1_rewrite_result` failed on missing v1 result behavior. Focused green passed 3/3. `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter RewriteApiTests` passed 19/19. `cd backend-dotnet && dotnet test` passed 522/522.
- Limitations: No separate spec file was added because the issue and brief were already implementation-ready and the change is a narrow endpoint addition.

### 2026-06-04 - state-machine-modeling - API-04 rewrite attempt result projection

- Agent: Codex worker
- Trigger: API-04 exposes persisted `RewriteAttemptStatus` values through a public polling endpoint.
- Action: Opened and followed the skill; modeled the read-only projection as `Pending`/`Processing` -> `processing`, `Succeeded` -> `succeeded` with parsed result data, and `Failed`/`Expired` -> `failed` with a stable error body. The endpoint performs no state transitions and does not mutate worker or quota state.
- Output artifacts: `V1RewriteHttpFunctions.GetRewriteResult`; ASP.NET v1 mirror route in `Program.cs`; `RewriteApiTests.V1_rewrite_result_maps_pending_succeeded_and_failed_attempts`.
- Verification evidence: Focused state mapping test failed before implementation with HTTP 404, then passed after the route and mapping helpers were added. Full backend `dotnet test` passed 522/522.
- Limitations: Illegal transition tests were not added because this endpoint is read-only and delegates lifecycle mutation to the existing worker and quota services.

### 2026-06-04 - data-module-review - API-04 owner-only attempt lookup

- Agent: Codex worker
- Trigger: API-04 reads `RewriteAttempts` by API-key owner and must not expose another user's attempt.
- Action: Opened and followed the skill; reviewed `RewriteAttempt`, `ApiKey`, the `ApiKeys.KeyHash` index, `RewriteAttempts` user/idempotency indexes, `ApiKeyAuthResolver`, and existing attempt lookup patterns. Kept the data change to an `AsNoTracking` read filtered by both `Id` and `UserId`; no schema or migration change was needed.
- Output artifacts: owner-filtered queries in `V1RewriteHttpFunctions.GetRewriteResult` and the ASP.NET v1 mirror route; ownership test in `RewriteApiTests`.
- Verification evidence: `V1_rewrite_result_returns_not_found_for_attempt_owned_by_another_user` verifies non-owner access returns `404` with the v1 error shape. `cd backend-dotnet && dotnet test` passed 522/522.
- Limitations: The endpoint does not add retention, deletion, or usage-meter changes; those are outside API-04.

### 2026-06-04 - dotnet-backend-testing - API-04 result polling coverage

- Agent: Codex worker
- Trigger: API-04 requires xUnit coverage for processing, succeeded, failed, owner-only, and key-auth result polling behavior.
- Action: Opened and followed the project skill plus the test-driven-development skill; wrote failing tests first in the existing `RewriteApiTests` WebApplicationFactory suite, then implemented the Functions endpoint and the ASP.NET mirror route used by the test host.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`.
- Verification evidence: Focused red `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter V1_rewrite_result` failed on missing route behavior. Focused green passed 3/3. Rewrite API focused run passed 19/19. Full `cd backend-dotnet && dotnet test` passed 522/522.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore and all test runs completed.

### 2026-06-04 - state-machine-modeling - API-06 API key revoke lifecycle

- Agent: Codex worker
- Trigger: API-06 exposes owner-scoped API key creation, listing, and revoke behavior through Entra-authenticated account endpoints.
- Action: Opened and followed the skill; modeled persisted API key states as active when `RevokedAt` is null, revoked when `RevokedAt` is set, and expired when `ExpiresAt` is in the past. Events covered create, list, owner revoke, other-user revoke, and duplicate owner revoke. The endpoint performs only active-to-revoked mutation through `ApiKeyService.RevokeAsync`; other-user or missing keys return `404` without mutation.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyHttpFunctionsTests.cs`.
- Verification evidence: Focused red `dotnet test --filter ApiKeyHttpFunctionsTests` failed on the missing Functions class. Focused green passed 2/2. Full `cd backend-dotnet && dotnet test` passed 524/524.
- Limitations: The key-authenticated v1 rewrite endpoints and usage endpoints were intentionally left unchanged for other wave issues.

### 2026-06-04 - data-module-review - API-06 API key endpoint data invariants

- Agent: Codex worker
- Trigger: API-06 reads and mutates persisted `ApiKeys` through an account-management Functions endpoint.
- Action: Opened and followed the skill; reviewed `ApiKey`, `AppDbContext`, `ApiKeyService`, and existing account auth patterns together. Confirmed no schema change was needed, list/revoke operations stay scoped by canonical `AppUser.Id`, list responses expose only masked display keys, and create responses return plaintext only from the service result without storing it.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyHttpFunctionsTests.cs`.
- Verification evidence: `ApiKeyHttpFunctionsTests` verifies create plaintext response, stored hash and last four characters, masked list response without plaintext or hash, owner revoke setting `RevokedAt`, and other-user revoke returning `404`. Full `cd backend-dotnet && dotnet test` passed 524/524.
- Limitations: No new migration, transaction shape, or collision retry behavior was added because API-06 only adds HTTP access to the existing API key service.

### 2026-06-04 - dotnet-backend-testing - API-06 key CRUD endpoint coverage

- Agent: Codex worker
- Trigger: API-06 requires xUnit coverage for Entra-authenticated API key create/list/revoke behavior.
- Action: Opened and followed the project skill; wrote focused xUnit tests first using existing EF SQLite fixtures and header-auth test requests, then implemented `ApiKeyHttpFunctions`. Added a small service tuple extension so the created endpoint can return the persisted `createdAt` value.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyHttpFunctionsTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs`.
- Verification evidence: Initial focused run failed on missing `ApiKeyHttpFunctions`. Focused backend run passed 2/2 after implementation. Full `cd backend-dotnet && dotnet test` passed 524/524.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore and all tests completed.

### 2026-06-05 - ui-browser-testing - API-07 developer key manager UI

- Agent: Codex worker
- Trigger: API-07 adds browser-visible signed-in portal UI, navigation, form submission, copy action, loading/error states, and revoke confirmation for API key management.
- Action: Opened and followed the project skill; identified the visible flow, added source-contract coverage first, implemented the signed-in `/developers/keys` page and client key panel, and verified the local route with signed and signed-out HTTP requests.
- Output artifacts: `app/developers/keys/page.tsx`; `components/developers/api-keys-panel.tsx`; `components/site-header.tsx`; `tests/unit/developer-keys-ui.test.ts`; `tests/unit/site-header.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red `npm run test -- tests/unit/developer-keys-ui.test.ts tests/unit/site-header.test.ts` failed on missing page/link, then passed after implementation. `npm run typecheck` passed. `npm run test` passed 400/400. Banned-term grep over `app components public lib` returned no matches. Local dev server route check returned `307` to `/sign-in` when signed out and signed-in HTML contained the key manager, create form, one-time notice, and signed-in nav link.
- Limitations: Playwright screenshot/interaction verification could not run because both cached Chromium and system Chrome were blocked by the macOS sandbox before page load. The first dev-server attempt also hit file-watch `EMFILE` warnings due a generated npm cache; removing that cache and restarting with `WATCHPACK_POLLING=true` restored route serving.

### 2026-06-05 - system-spec-synthesis - API-08 v1 per-key RPM contract

- Agent: Codex worker
- Trigger: API-08 implements the v1 API rate-limit and usage-log contract from issue #510, the API-08 brief, and `plans/rewrite-api-v1/SPEC.md`.
- Action: Opened and followed the skill; converted the issue and spec into scoped checkpoints: resolve the concrete API key row, check the key's recent `ApiKeyUsage` rows before reservation, return the v1 `rate_limited` error shape on `429`, log one usage row for valid-key v1 calls, and leave the Entra website path unchanged.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Focused red run for `V1_rewrite_submit_enforces_per_key_rate_limit_without_reservation_for_rejected_call` failed with the fourth request returning `202` instead of `429`; focused green run later passed 2/2 new API-08 tests. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: No standalone spec file was added because the issue body and brief were already implementation-ready.

### 2026-06-05 - resilience-test-generation - API-08 rate-limit rejection behavior

- Agent: Codex worker
- Trigger: API-08 changes and tests rate-limit behavior and must preserve quota/reservation invariants under repeated calls.
- Action: Opened and followed the skill; defined the critical invariant as `429` being uncharged and reservation-free while still writing one usage row. Chose the existing WebApplicationFactory plus EF SQLite integration level because it proves HTTP status, persisted usage rows, reservations, outbox messages, and quota counters together.
- Output artifacts: `RewriteApiTests.V1_rewrite_submit_enforces_per_key_rate_limit_without_reservation_for_rejected_call`; rate-window checks in `Program.cs` and `V1RewriteHttpFunctions.cs`.
- Verification evidence: The regression failed before implementation with `202` on the over-limit call, then passed after the pre-reservation window check. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: The test covers sequential requests inside one process; no concurrent RPM race test was added because the spec explicitly excludes an in-flight cap and the issue acceptance is sequential.

### 2026-06-05 - state-machine-modeling - API-08 submit and usage lifecycle

- Agent: Codex worker
- Trigger: API-08 changes the submit lifecycle around rate-limit rejection, usage rows, quota reservation, and result polling.
- Action: Opened and followed the skill; modeled the relevant lifecycle as valid API key request -> recent-call check -> validation or quota/idempotency outcome -> optional reservation -> exactly one usage row for valid-key v1 calls. The illegal transition covered is over-limit submit creating a `RewriteAttempt`, `UsageReservation`, or outbox message.
- Output artifacts: `V1_rewrite_submit_enforces_per_key_rate_limit_without_reservation_for_rejected_call`; `V1_rewrite_result_writes_api_key_usage_row`; pre-reservation rate checks in the v1 submit paths.
- Verification evidence: The new submit lifecycle test asserts the over-limit call returns `429`, `RewriteAttempts`, `UsageReservations`, and `OutboxMessages` stay at the allowed-call count, `UsedCount` remains `0`, and the `429` call is logged. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: Existing worker finalization states were not changed or retested beyond the full suite because API-08 only gates before reservation.

### 2026-06-05 - data-module-review - API-08 ApiKeyUsage RPM window

- Agent: Codex worker
- Trigger: API-08 reads and writes `ApiKeyUsage` rows and relies on persisted `ApiKey.RateLimitPerMinute`.
- Action: Opened and followed the skill; reviewed `ApiKey`, `ApiKeyUsage`, `AppDbContext` indexes, the v1 auth resolver, and usage logging paths. Kept the change schema-free, used the existing `{ ApiKeyId, CreatedAt }` index for non-SQLite providers, and added a SQLite-only timestamp comparison branch because EF SQLite cannot translate `DateTimeOffset` window predicates.
- Output artifacts: `ApiKeyAuthResolver.ResolveAsync`; `IsV1RateLimitedAsync`; `IsRateLimitedAsync`; API-08 integration tests.
- Verification evidence: Focused tests first exposed the missing rate check, then exposed the SQLite `DateTimeOffset` query translation issue. After the provider-aware branch, focused tests passed 2/2 and full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: No migration was added. The SQLite branch loads the current key's timestamps client-side only in tests/local SQLite; production-style providers keep database-side filtering.

### 2026-06-05 - dotnet-backend-testing - API-08 xUnit integration coverage

- Agent: Codex worker
- Trigger: API-08 requires xUnit/integration coverage for `RateLimitPerMinute + 1` submit calls and per-call usage rows.
- Action: Opened and followed the project skill plus test-driven-development; added failing tests first in the existing `RewriteApiTests` WebApplicationFactory suite, then implemented the route/function changes and reran focused plus full backend checks.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/ApiKeyAuthResolver.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`.
- Verification evidence: Focused red submit test failed with `202` instead of `429`; focused red result usage test failed with no `ApiKeyUsage` row. Focused green run passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore and all tests completed.

### 2026-06-05 - system-spec-synthesis - API-09 v1 usage contract check

- Agent: Codex worker
- Trigger: API-09 adds the key-authenticated `GET /api/v1/usage` contract from issue #511, the API-09 brief, and `plans/rewrite-api-v1/SPEC.md`.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, the API-09 brief, and the v1 API spec. Checked the implementation checkpoints: key auth, response shape `{ scope, periodKey, quota, used, remaining, periodEnd }`, quota values sourced from `AccountService.GetOrCreateAccountSummaryAsync`, missing or invalid key returning `401`, and a Next proxy route that forwards caller authorization without site-session auth.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `app/api/v1/usage/route.ts`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `tests/unit/v1-usage-route.test.ts`.
- Verification evidence: Focused backend red run `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter V1_usage` failed with missing-route `404`s, then focused green passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526; `npm run typecheck` passed; `npm run test` passed 398/398; restricted-term scan over `app components public lib` returned no matches.
- Limitations: No separate spec file was added because the existing wave spec and issue brief already define the narrow API contract.

### 2026-06-05 - state-machine-modeling - API-09 usage read projection

- Agent: Codex worker
- Trigger: API-09 reads quota state for a key owner and must not alter usage, reservation, or rewrite-attempt lifecycles.
- Action: Opened and followed the skill. State list: API key active, revoked, expired; usage period current or absent. Event list: usage read with valid key, missing key, invalid key, revoked/expired key. Transition table: valid read keeps API key and usage period states unchanged while updating only best-effort key last-used metadata through the existing resolver; auth failures return `401` with no quota or reservation mutation. Invariants: endpoint reports the same account summary quota, used, and remaining numbers as the website path; rejected reads create no `UsagePeriod` or `UsageReservation`; no rewrite job or reservation is created. Illegal transitions: a read must not reserve quota, consume quota, create attempts, or expose another user's counters.
- Output artifacts: `V1_usage_returns_account_summary_usage_for_seeded_key_user`; `V1_usage_rejects_missing_or_invalid_key`; v1 usage routes in the ASP.NET and Functions hosts.
- Verification evidence: Focused state test failed before implementation with `404`, then passed. Full backend tests passed 526/526.
- Limitations: No lifecycle transition helper was added because API-09 is a read-only projection over existing account and key state.

### 2026-06-05 - data-module-review - API-09 account usage data invariants

- Agent: Codex worker
- Trigger: API-09 reads `ApiKeys`, `AppUsers`, and account usage counters for a public v1 endpoint.
- Action: Opened and followed the skill; reviewed the existing API key lookup, account summary service, `UsagePeriod` counters, and rewrite credit inclusion path. Kept the change read-only for usage tables, filtered the key owner through the existing auth resolver in Functions, and reused `AccountService.GetOrCreateAccountSummaryAsync` so quota, used, and remaining values are not recomputed separately.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: New integration test seeds a paid usage period with used and reserved counts, then asserts the v1 response equals `AccountService` summary values. Missing and invalid key test asserts `401` and no usage-period or reservation rows. Full backend tests passed 526/526.
- Limitations: No schema, migration, transaction, or index changes were needed.

### 2026-06-05 - dotnet-backend-testing - API-09 usage endpoint coverage

- Agent: Codex worker
- Trigger: API-09 requires xUnit/integration coverage that the v1 usage endpoint returns the backend usage math and rejects missing or invalid keys.
- Action: Opened and followed the project skill plus the test-first workflow; added failing tests in the existing `RewriteApiTests` WebApplicationFactory suite, verified the missing route failure, then implemented the ASP.NET mirror route and the Azure Functions route.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`.
- Verification evidence: Focused red `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter V1_usage` failed on `404`; focused green passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore and all test runs completed.

### 2026-06-05 - resilience-test-generation - API-10 duplicate submit idempotency

- Agent: Codex worker
- Trigger: API-10 tests repeated public v1 submit requests and idempotency conflict handling.
- Action: Opened and followed the skill; identified the critical invariant as one persisted rewrite attempt, one usage reservation, and one outbox job for repeated same-key/same-body submit requests, with same-key/different-body returning `409` and no extra reservation.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused `dotnet test ReplyInMyVoice.sln --filter "FullyQualifiedName~V1_rewrite_submit_same_idempotency"` passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: The scope was duplicate request handling only; no retry, queue, worker, or external service behavior was changed.

### 2026-06-05 - state-machine-modeling - API-10 submit reservation lifecycle

- Agent: Codex worker
- Trigger: API-10 verifies the submit path around `RewriteAttempt` and `UsageReservation` lifecycle states.
- Action: Opened and followed the skill; modeled the tested path as new submit -> `Pending` attempt plus `Pending` reservation, duplicate same-key/same-body submit -> existing `Pending` attempt projection, and same-key/different-body submit -> conflict with no state mutation. Invariants: accepted duplicate returns the original id, reserved count remains one, used count remains zero until worker success, and no second outbox job is created.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: New v1 tests assert HTTP status, same id, `RewriteAttempts.Count == 1`, `UsageReservations.Count == 1`, `OutboxMessages.Count == 1`, `UsedCount == 0`, and `ReservedCount == 1`. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: No new transition function or enum was added because the existing quota service already owns lifecycle mutation and API-10 only required endpoint-level verification.

### 2026-06-05 - data-module-review - API-10 reservation persistence invariant

- Agent: Codex worker
- Trigger: API-10 verifies persisted idempotency behavior across `RewriteAttempts`, `UsageReservations`, `UsagePeriods`, and outbox records.
- Action: Opened and followed the skill; reviewed `RewriteRequestService.CreateAttemptAsync`, `QuotaService.ReserveAsync`, the v1 submit route, and existing API tests together. Confirmed no schema or `QuotaService` semantic change was needed. Added tests that assert final persisted counts and counters, not only response codes.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused API-10 tests passed 2/2 after fixture stabilization. Mixed `dotnet test ReplyInMyVoice.sln --filter "FullyQualifiedName~V1_rewrite_submit_same_idempotency|FullyQualifiedName~ApiKey"` passed 14/14. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: The v1 test fixture now seeds equivalent hashes for the known test API-key hash settings because related API-key test classes mutate a process-wide setting during parallel runs.

### 2026-06-05 - dotnet-backend-testing - API-10 v1 submit integration coverage

- Agent: Codex worker
- Trigger: API-10 requires xUnit/integration coverage for public v1 submit idempotency behavior.
- Action: Opened and followed the project skill; added two WebApplicationFactory integration tests in the existing `RewriteApiTests` suite. The first repeats the same `Idempotency-Key` and draft and asserts the same response id plus one persisted attempt/reservation. The second reuses the same key with a different draft and asserts `409 idempotency_conflict` plus no duplicate reservation.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused API-10 run passed 2/2. A mixed API-key run initially reproduced a parallel test fixture issue, then passed 14/14 after seeding stable test hashes. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched, but restore/build/test completed. Production endpoint code was left unchanged because the new integration tests passed against the existing idempotency wiring.

### 2026-06-05 - state-machine-modeling - API-11 terminal rewrite attempt states

- Agent: Codex worker
- Trigger: API-11 verifies that API-created rewrite attempts do not remain in a nonterminal polling state after worker failure or reservation expiry.
- Action: Opened and followed the skill; modeled `RewriteAttempt` transitions as `Pending` to `Processing` to `Succeeded`, `Failed`, or `Expired`, with `Failed` and `Expired` projected to the v1 `failed` result body. Confirmed `UsageReservation` transitions release or expire quota without incrementing used quota.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter "FullyQualifiedName~RewriteApiTests.V1_rewrite_result_maps_expired_api_attempt_to_failed_without_charging_usage|FullyQualifiedName~RewriteApiTests.V1_rewrite_provider_failure_sets_api_attempt_failed_without_charging_usage"` passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: No production transition helper was added because the existing quota and worker paths already implement the needed transitions.

### 2026-06-05 - data-module-review - API-11 reservation accounting invariants

- Agent: Codex worker
- Trigger: API-11 verifies persisted quota reservation and attempt state for API-originated expiry and provider failure paths.
- Action: Opened and followed the skill; reviewed `RewriteAttempt`, `UsageReservation`, `UsagePeriod`, `QuotaService.ReleaseExpiredReservationsAsync`, `QuotaService.ReleaseAsync`, `RewriteRequestService.CreateAttemptAsync`, and `RewriteJobProcessor.ProcessAsync`. Added assertions for `UsedCount`, `ReservedCount`, reservation status, attempt status, and error code.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused API terminal-state tests passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: No schema or migration change was needed.

### 2026-06-05 - resilience-test-generation - API-11 expiry and provider failure coverage

- Agent: Codex worker
- Trigger: API-11 tests recovery from a worker that never runs and a provider failure, preserving the no-charge invariant.
- Action: Opened and followed the skill; chose WebApplicationFactory integration tests with local SQLite persistence and deterministic provider fake. Covered API submit followed by TTL sweep, and API submit followed by worker processing with a failing provider result.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused terminal-state test command passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: Live cloud queues and external providers were not contacted; tests use in-process services and local SQLite.

### 2026-06-05 - dotnet-backend-testing - API-11 v1 terminal-state tests

- Agent: Codex worker
- Trigger: API-11 adds xUnit coverage for terminal state and no-charge behavior in the .NET rewrite API.
- Action: Opened and followed the project skill; added two tests to the existing `RewriteApiTests` WebApplicationFactory suite so the assertions exercise the v1 submit and poll surface plus persisted quota state.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused test command passed 2/2. Full `cd backend-dotnet && dotnet test` passed 526/526.
- Limitations: Focused tests passed on the existing implementation, so no production code was changed.

### 2026-06-05 - data-module-review - P2-01 Stripe invoice persistence

- Agent: Codex worker
- Trigger: GitHub issue #527 adds an EF Core entity, migration, webhook upsert path, foreign key, and invoice id idempotency invariant.
- Action: Opened and followed the skill; reviewed `AppDbContext`, `StripeEventService`, existing `StripeEvent` and `RewriteCredit` patterns, migration output, and final persisted-state assertions. Ran `scan_data_risks.py --limit 40 .`; the bounded scan produced existing broad persistence signals and no new blocking finding for this scoped table.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/StripeInvoice.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260605092351_AddStripeInvoice.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260605092351_AddStripeInvoice.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`.
- Verification evidence: New focused test passed 1/1 after the implementation. Full `cd backend-dotnet && dotnet test` passed 533/533. `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched. No production data migration was applied locally.

### 2026-06-05 - state-machine-modeling - P2-01 invoice webhook row lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #527 changes webhook lifecycle persistence for Stripe invoice status updates.
- Action: Opened and followed the skill. State list: `draft`, `open`, `paid`, `void`, `uncollectible` as mirrored from Stripe invoice status. Event list: `invoice.finalized`, `invoice.paid`, `invoice.payment_succeeded`, `invoice.payment_failed`, and duplicate delivery of an already processed event id. Transition table: finalized creates or updates the row to the payload status or `open`; paid/payment_succeeded creates or updates to payload status or `paid`; payment_failed updates the same row to the payload status while existing dunning behavior sets the user to `PastDue`; duplicate processed event id makes no row change. Invariants: one row per invoice id, row owner resolved from the local Stripe customer/subscription mapping, same event id is skipped by `StripeEvents`, later distinct invoice events may update the same invoice row, and checkout credit/quota behavior remains separate. Illegal transitions: missing invoice id or missing local user does not create an orphan invoice row; processed duplicate event ids cannot overwrite the existing row. Persistence implications: `StripeInvoices.Id` is the primary key, `UserId` is a cascade FK, and `(UserId, CreatedAt)` supports user-scoped history reads. Test checklist: insert from `invoice.paid`, duplicate event replay leaves one row, later `invoice.payment_failed` updates status and attempt count on that row.
- Output artifacts: same P2-01 code and test files listed in the data-module-review entry.
- Verification evidence: The new xUnit test asserts the transition checklist and persisted fields. Full `cd backend-dotnet && dotnet test` passed 533/533.
- Limitations: This issue does not add the future billing-history read endpoint or UI.

### 2026-06-05 - resilience-test-generation - P2-01 invoice webhook replay coverage

- Agent: Codex worker
- Trigger: GitHub issue #527 acceptance requires duplicate webhook replay safety and update behavior for later invoice events.
- Action: Opened and followed the skill; identified the critical operation as webhook invoice persistence inside the existing serializable event transaction. Failure matrix focus: duplicate event id, partial persistence guarded by the transaction, unmatched local user, malformed missing invoice id, and later distinct invoice event update. Implemented the lowest-level test as an xUnit service persistence test with local SQLite and no live Stripe calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`.
- Verification evidence: Focused red run failed on missing `StripeInvoices` surface; focused green run passed 1/1. Full `cd backend-dotnet && dotnet test` passed 533/533.
- Limitations: Concurrent duplicate invoice delivery was not separately simulated because existing `StripeEvents` processing already owns event-id dedupe and this issue only adds invoice-row upsert behavior.

### 2026-06-05 - dotnet-backend-testing - P2-01 invoice webhook xUnit coverage

- Agent: Codex worker
- Trigger: GitHub issue #527 requires xUnit coverage in `backend-dotnet/tests/ReplyInMyVoice.Tests/` for invoice upsert, replay, and update behavior.
- Action: Opened and followed the project skill plus test-first workflow; added `ProcessWebhookEventAsync_InvoicePaidUpsertsInvoiceAndPaymentFailedUpdatesSameRow` to the existing `StripeEventServiceTests` SQLite service suite, verified the test failed before the entity/DbSet existed, then implemented the minimal model and service code to pass.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/StripeInvoice.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`.
- Verification evidence: Focused command `dotnet test tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter FullyQualifiedName~ProcessWebhookEventAsync_InvoicePaidUpsertsInvoiceAndPaymentFailedUpdatesSameRow` passed 1/1 after initially failing as expected. Full `cd backend-dotnet && dotnet test` passed 533/533. `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore/build/test still completed.

### 2026-06-05 - state-machine-modeling - P2-04 payment grace expiry lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #528 changes subscription, payment grace, and quota lifecycle transitions.
- Action: Opened and followed the skill. State list: `Active`, `Trialing`, and `Testing` project paid quota; `PastDue` keeps paid quota during grace; `Inactive` projects the free baseline; `Canceled` remains the explicit subscription-deleted/account-delete state. Event list: `invoice.payment_failed`, `invoice.payment_succeeded`, `customer.subscription.updated` with terminal dunning statuses, `customer.subscription.deleted`, and the scheduled payment-grace expiry timer. Transition table: `PastDue` + expired grace -> `Inactive` + clear grace + cancel Stripe subscription; `PastDue` + still in grace -> no change; paid states + timer -> no change; updated `unpaid`/`canceled` -> `Inactive` + clear grace; deleted -> `Canceled`. Invariants: expired grace cannot keep paid quota, terminal updated statuses clear grace, duplicate timer runs do not downgrade or cancel again, and subscription deletion behavior remains separate.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run failed on missing cancel and updated `canceled` terminalization, then focused green run passed 30/30. Full `cd backend-dotnet && dotnet test` passed 537/537. `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: No new transition helper or enum value was added because the existing service owns these transitions.

### 2026-06-05 - data-module-review - P2-04 AppUser billing state persistence

- Agent: Codex worker
- Trigger: GitHub issue #528 changes persisted `AppUser` subscription status, payment grace fields, and quota projection.
- Action: Opened and followed the skill; reviewed `AppUser`, `AppDbContext`, `AccountService.GetUsagePlan`, `StripeEventService`, and related tests. Confirmed no schema or migration change was needed. Ran `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 40 .`; the bounded scan returned existing broad quota/idempotency signals and no scoped blocking finding for this change.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/IStripeBillingService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The updated xUnit service test asserts downgraded status, cleared grace fields, free quota projection, unchanged active/in-grace users, and exactly one fake cancel request. Full `cd backend-dotnet && dotnet test` passed 537/537; `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: No production migration was applied locally because the persistence shape did not change.

### 2026-06-05 - resilience-test-generation - P2-04 retry-safe grace expiry and terminal dunning

- Agent: Codex worker
- Trigger: GitHub issue #528 touches payment-provider retry cleanup, webhook replay behavior, and recovery from an expired payment grace state.
- Action: Opened and followed the skill; identified the critical operation as scheduled `PastDue` expiry plus post-commit Stripe subscription cancel. Dependency boundaries: EF Core database, Azure timer runtime, Stripe billing abstraction, notification post-commit actions, and webhook event replay. Failure matrix focus: duplicate timer run, active user not eligible, still-in-grace user not eligible, terminal subscription update replay, and no live Stripe network in tests. Implemented deterministic fake billing assertions.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PaymentGraceExpiryFunction.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused red run failed for the intended missing behaviors; focused green run passed 30/30. Full `cd backend-dotnet && dotnet test` passed 537/537; `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: A Stripe cancel provider failure is handled by the existing post-commit action runner, but this issue's acceptance did not require a new explicit cancel-failure test.

### 2026-06-05 - dotnet-backend-testing - P2-04 grace expiry xUnit coverage

- Agent: Codex worker
- Trigger: GitHub issue #528 requires xUnit coverage for expired grace downgrade, free-baseline quota projection, unchanged non-expired users, and fake Stripe cancel invocation.
- Action: Opened and followed the project skill plus test-first workflow; updated the existing `StripeEventServiceTests` SQLite service suite and fake billing service. The red run failed because the cancel request was missing and updated `canceled` still persisted as `Canceled`; after implementation, the focused service suite passed.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeEventServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/IStripeBillingService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PaymentGraceExpiryFunction.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused `dotnet test ReplyInMyVoice.sln --filter FullyQualifiedName~StripeEventServiceTests` failed red with 2 expected failures, then passed 30/30 after implementation. Full `cd backend-dotnet && dotnet test` passed 537/537. `cd backend-dotnet && dotnet build` succeeded with 0 errors.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore/build/test still completed.

### 2026-06-05 - cloud-architecture-cost-review - P2-04 Azure Functions timer cost check

- Agent: Codex worker
- Trigger: GitHub issue #528 adds a scheduled Azure Functions timer.
- Action: Opened and followed the skill; reviewed `docs/manual-setup.md`, `docs/next-development-brief.md`, and `docs/dotnet-azure-full-run-result.md`. Selected option: reuse the existing Azure Functions consumption runtime with one daily timer trigger. Rejected options: new App Service, new always-on worker, new queue, or new Azure resource. No exact pricing lookup was needed because no exact price was quoted and no new resource was created.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PaymentGraceExpiryFunction.cs`; `docs/skill-run-log.md`.
- Verification evidence: `cd backend-dotnet && dotnet test` passed 537/537; `cd backend-dotnet && dotnet build` succeeded with 0 errors. No `az`, deploy, provision, or live payment command was run.
- Limitations: This was a local architecture/cost check only; no live Azure timer smoke was run because the supervisor owns deployment.
### 2026-06-05 - resilience-test-generation - P2-10 v1 rate-limit headers

- Agent: Codex worker
- Trigger: P2-10 changes and tests per-key rate-limit behavior and `429` response metadata for `/api/v1/*`.
- Action: Opened and followed the skill; identified the invariant as one existing `ApiKeyUsages` minute-window source used for both the limit decision and response headers. Added failing assertions for successful v1 submit headers and the rate-limited submit response before implementation.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused red `dotnet test --filter "FullyQualifiedName~V1_rewrite_submit_with_valid_key_reserves_usage_and_returns_processing_id|FullyQualifiedName~V1_rewrite_submit_enforces_per_key_rate_limit_without_reservation_for_rejected_call"` failed on missing headers, then passed 2/2 after implementation. Full `cd backend-dotnet && dotnet test` passed 532/532.
- Limitations: No live cloud runtime or external provider was contacted; the test uses the existing in-process API host and SQLite fixture.

### 2026-06-05 - data-module-review - P2-10 rate-limit window source

- Agent: Codex worker
- Trigger: P2-10 requires response headers computed from the same stored window data the limiter already uses, without adding another store or changing quota math.
- Action: Opened and followed the skill; reviewed `ApiKeyUsage`, the v1 limiter helpers, and the v1 completion paths together. Replaced the boolean limiter query with a window metadata query over `ApiKeyUsages` and reused that result for headers and `429` decisions.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `docs/skill-run-log.md`.
- Verification evidence: New integration assertions check persisted API-key usage rows still exist for accepted and rejected v1 submit calls. Full `cd backend-dotnet && dotnet test` passed 532/532.
- Limitations: No schema, migration, quota, billing, or price behavior changed. The `/api/v1/usage` route now participates in the same per-key window and usage log so its headers come from the same source.

### 2026-06-05 - dotnet-backend-testing - P2-10 v1 header integration tests

- Agent: Codex worker
- Trigger: P2-10 acceptance requires xUnit/integration coverage for `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, and `Retry-After` on v1 success and rate-limit responses.
- Action: Opened and followed the project skill; added header assertions to the existing `RewriteApiTests` WebApplicationFactory suite and kept the assertions tied to response status plus persisted usage state.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused backend header tests passed 2/2. Full `cd backend-dotnet && dotnet test` passed 532/532.
- Limitations: Existing integration coverage exercises the ASP.NET API host; the Azure Functions host was compiled by the full backend test run and updated with matching header logic.

### 2026-06-05 - ui-browser-testing - P2-09 developers page redesign

- Agent: Codex worker
- Trigger: GitHub issue #530 changes the browser-visible `/developers` page, API docs layout, CTA path, and header discovery expectations.
- Action: Opened and followed the skill; identified `/developers` as the user-visible flow. Attempted in-app Browser verification, then Playwright desktop/mobile verification after the in-app Browser was unavailable. Replaced the stale developer page with async v1 API docs and added scoped responsive styles. Added source-level Vitest coverage for the documented async endpoints, key CTA, errors, rate limits, idempotency, paid quota, 30-day retention, and stale-string removal.
- Output artifacts: `app/developers/page.tsx`; `app/globals.css`; `tests/unit/developers-page.test.ts`; `tests/unit/pricing-auth-visual-system.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused `npm run test -- tests/unit/developers-page.test.ts tests/unit/pricing-auth-visual-system.test.ts` passed 5/5 after failing red on the stale page. `npm run typecheck` passed. Full `npm run test` passed 417/417. The issue greps for banned copy and stale `/developers` strings returned no matches. `npm run build` passed and listed `/developers` as a static route. `next start --port 3001` served `/developers` with HTTP 200, and rendered HTML checks found all required API docs with no stale strings.
- Limitations: The in-app Browser was unavailable in this session. Headless Chromium launch failed under the macOS sandbox, and WebKit/Firefox were not installed in the shared Playwright cache, so no desktop/mobile screenshot inspection was completed. `next dev --port 3000` hit local `EMFILE` watch warnings and returned 404 for all routes in this symlinked dependency setup; production `next start` verified the built route instead. The sandbox denied signals to the lingering port-3000 dev process.

### 2026-06-05 - system-spec-synthesis - P2-03 billing history endpoint

- Agent: Codex worker
- Trigger: GitHub issue #531 and `plans/rewrite-api-v1/phase2-issues/P2-03-billing-history-endpoint.md` require a new user-scoped API contract that merges pack purchases, subscription invoices, and refunds.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the P2-03 brief, `plans/rewrite-api-v1/PHASE-2-SPEC.md` BILLH-02 lines, existing account routes, `AccountService`, `StripeInvoice`, `RewriteCredit`, and proxy patterns. Implementation spec summary: context is the P2-01 `StripeInvoice` table plus existing purchase history; goal is a unified newest-first read model; non-goals are webhook, schema, payment, or UI changes; API contract is `GET /api/me/billing/history` returning `type`, `date`, `description`, `amount`, `currency`, `status`, `receiptUrl`, and `hostedInvoiceUrl`; security requires Entra auth and user-id filtering; verification requires xUnit ownership/order/amount assertions plus Next proxy tests and typecheck.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AccountHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `app/api/me/billing/history/route.ts`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `tests/unit/account-api.test.ts`.
- Verification evidence: Focused red backend run failed on missing `GetBillingHistoryAsync` and `GetBillingHistory`; focused green `dotnet test tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter BillingHistory` passed 3/3; full `cd backend-dotnet && dotnet test` passed 540/540; `npm run typecheck` passed; `npm run test` passed 419/419.
- Limitations: Refund dates are projected from the adjusted purchase credit row because the current persisted refund credit state does not store a separate refund timestamp.

### 2026-06-05 - data-module-review - P2-03 billing history read model

- Agent: Codex worker
- Trigger: GitHub issue #531 changes account-service data access over `RewriteCredit` and `StripeInvoice` for billing history.
- Action: Opened and followed the skill; reviewed owned tables/entities (`RewriteCredits`, `StripeInvoices`, `AppUsers`) and service read paths. Findings: no schema/migration change required; history reads filter by canonical `UserId`; pack rows reuse `GetPurchaseHistoryAsync`; subscription rows expose only invoice URLs and normalized invoice fields; refund rows are inferred only from purchase credits where `OriginalAmountGranted` exceeds `AmountGranted`; no card data is selected.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`.
- Verification evidence: The new service test seeds user A and user B with packs, invoices, and a refund-adjusted purchase credit, then asserts newest-first dates, ownership isolation, amounts, currency, statuses, receipt URL, and hosted invoice URL. Full `cd backend-dotnet && dotnet test` passed 540/540.
- Limitations: No data-risk scan script was run for this read-only endpoint because no persistence model or migration changed.

### 2026-06-05 - state-machine-modeling - P2-03 invoice and refund status projection

- Agent: Codex worker
- Trigger: GitHub issue #531 reviews subscription invoice statuses and refund-adjusted purchase credits in a user-facing history endpoint.
- Action: Opened and followed the skill. State list: subscription invoice status is the persisted Stripe invoice `Status`; pack rows are projected as `paid`; inferred refund rows are projected as terminal `refunded`. Event list: no new events; existing webhook processing creates/updates `StripeInvoice` and adjusts purchase credits on `charge.refunded`. Transition table: none added by this issue; the endpoint maps current persisted state to a read model. Invariants: the endpoint must not mutate status, must not create billing rows, must not expose another user's rows, and must keep payment-provider lifecycle behavior separate from history rendering. Illegal transitions: unauthenticated requests return 401, and user B rows cannot appear in user A history.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AccountHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`.
- Verification evidence: Focused `BillingHistory` xUnit tests passed 3/3; full `cd backend-dotnet && dotnet test` passed 540/540.
- Limitations: This issue did not add a transition helper because it only projects existing states.

### 2026-06-05 - dotnet-backend-testing - P2-03 billing history xUnit coverage

- Agent: Codex worker
- Trigger: GitHub issue #531 requires xUnit coverage for seeded pack purchases, `StripeInvoice` rows, a refund, sorting, ownership isolation, and amount/currency correctness.
- Action: Opened and followed the project skill plus test-first workflow. Added service-level SQLite coverage in `AccountServiceTests` and endpoint/auth coverage in `AccountApiTests`. The initial focused run failed before implementation because `AccountService.GetBillingHistoryAsync` and `AccountHttpFunctions.GetBillingHistory` did not exist; after implementation the focused suite passed.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AccountHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`.
- Verification evidence: `dotnet test tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter BillingHistory` passed 3/3; full `cd backend-dotnet && dotnet test` passed 540/540.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore and tests still completed successfully.

### 2026-06-06 - ui-browser-testing - GA-03 API legal and data pages

- Agent: Codex worker
- Trigger: GitHub issue #549 changes browser-visible `/developers` navigation and adds three developer legal/data pages.
- Action: Opened and followed the project skill; identified `/developers`, `/developers/terms`, `/developers/acceptable-use`, and `/developers/data` as the user-visible routes. Added source-level Vitest coverage for links, default-exported route files, draft status, operator/domain copy, quota/metering wording, acceptable-use obligations, and data-retention wording. Added the three static pages using the existing developer page classes.
- Output artifacts: `app/developers/page.tsx`; `app/developers/terms/page.tsx`; `app/developers/acceptable-use/page.tsx`; `app/developers/data/page.tsx`; `tests/unit/developers-page.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red `npm test -- tests/unit/developers-page.test.ts` failed on missing legal links and missing route files, then passed 2/2 after implementation. `npm run typecheck` passed. Full `npm run test` passed 429/429. `npm run lint` completed with 0 errors and 1 unrelated existing warning. The restricted vocabulary scan over `app components public lib` returned no matches. `next dev` served all four local routes with HTTP 200 and expected rendered strings.
- Limitations: The in-app Browser target was unavailable in this session. Local Chromium launch was blocked by macOS sandbox process permissions, and alternate Playwright engines were not installed, so screenshot inspection was not completed. `next dev` used port 3001 because port 3000 was already occupied and emitted local `EMFILE` file-watch warnings while still compiling and serving the checked routes.

### 2026-06-06 - system-spec-synthesis - SBX-01 sandbox API test keys

- Agent: Codex worker
- Trigger: GitHub issue #556 and `plans/rewrite-api-v1/adv-issues/SBX-01-sandbox-test-keys.md` change the v1 API contract, key data model, and developer key creation flow.
- Action: Opened and followed the skill; produced an implementation spec in-session. Context: live API keys use `rmv_live_` with peppered SHA-256 hashes, v1 submit reserves quota and writes outbox, and key UI proxies to Azure Functions. Goals: add `rmv_test_` keys, keep auth hashing unchanged, return deterministic sandbox submit/poll/usage responses, keep rate limits, and avoid quota/outbox/engine side effects. Non-goals: payment, deployment, live-key weakening, provider calls, and broad API refactors. Verification plan: xUnit sandbox/live regressions, EF migration, Next source tests, typecheck, full unit tests, and restricted-term scan.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKey.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260606040825_AddApiKeyIsTest.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `components/developers/api-keys-panel.tsx`; related tests.
- Verification evidence: Focused backend red run failed on missing `IsTest` and sandbox contracts, focused green passed 48/48. Full `cd backend-dotnet && dotnet test` passed 552/552. `npm run typecheck` passed. Full `npm run test` passed 448/448. Restricted-term scans returned no matches.
- Limitations: No separate spec document was created because the issue brief was already implementation-ready and the supervisor asked for issue-only delivery.

### 2026-06-06 - state-machine-modeling - SBX-01 v1 sandbox attempt lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #556 changes the v1 rewrite submit-to-poll lifecycle for a new key type.
- Action: Opened and followed the skill. State list: live submit remains `Pending` via quota reservation and outbox; sandbox submit persists an immediately `Succeeded` attempt with a namespaced test idempotency key; poll returns `processing`, `succeeded`, `failed`, or `not_found` according to attempt state and key type. Events: authenticated submit, duplicate submit, conflicting duplicate submit, owner-scoped poll, usage query, revoked/expired auth. Transition table: test submit -> persisted succeeded sandbox attempt with no reservation/outbox; duplicate same draft -> same attempt id; duplicate different draft -> conflict; test poll of non-test attempt -> not found; live poll of test attempt -> not found; revoked/expired -> 401.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: `RewriteApiTests` asserts sandbox submit->poll success, sandbox usage scope, unchanged `UsagePeriod` and credits, no `UsageReservation`, no outbox, no provider-call rows, live success charging exactly one use, and revoked/expired live keys returning 401. Full backend tests passed 552/552.
- Limitations: A dedicated attempt type column was not added; sandbox attempts are identified by the internal namespaced idempotency key to keep scope limited to SBX-01.

### 2026-06-06 - data-module-review - SBX-01 ApiKey IsTest persistence

- Agent: Codex worker
- Trigger: GitHub issue #556 adds a persisted `ApiKey.IsTest` boolean and changes API-key creation/listing data contracts.
- Action: Opened and followed the skill; reviewed `ApiKey`, `AppDbContext`, EF migrations, `ApiKeyService`, auth lookup, `UsagePeriod`, `UsageReservation`, outbox, API usage logs, and tests. Ran the bundled data-risk scan over `backend-dotnet`; it returned broad existing quota/idempotency signals and no scoped blocker. Added a non-null `IsTest` column with default `false`; existing rows remain live keys. Confirmed the hash lookup remains unique on `KeyHash`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKey.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260606040825_AddApiKeyIsTest.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs`.
- Verification evidence: xUnit coverage asserts generated live keys store `IsTest=false`, generated test keys store `IsTest=true`, summaries expose the flag, test v1 flow leaves `UsagePeriod` and credits unchanged, and live success still finalizes exactly one reservation. Full backend tests passed 552/552.
- Limitations: The initial EF CLI command timed out through the design-time host but eventually produced the migration pair; a manual fallback migration was deleted before completion.

### 2026-06-06 - resilience-test-generation - SBX-01 quota/outbox/provider side-effect guard

- Agent: Codex worker
- Trigger: GitHub issue #556 requires the sandbox key path to avoid quota consumption, outbox enqueue, engine calls, and charge-like side effects while rate limiting remains active.
- Action: Opened and followed the skill; failure matrix focused on duplicate idempotency key, conflicting duplicate draft, missing/revoked/expired auth, quota-exhausted live users, rate-limit source reuse, and provider side effects. Implemented deterministic local xUnit assertions rather than external provider calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`.
- Verification evidence: The sandbox test asserts no `UsageReservation`, no outbox row, no `RewriteProviderCall`, unchanged `UsagePeriod`, unchanged credit consumption, and logged API usage rows for submit/poll/usage. Existing and new live-path tests assert reservation and finalization still work. Full backend tests passed 552/552.
- Limitations: No live queue, worker, provider, payment, or cloud smoke was run; the local database side-effect checks prove the sandbox path never creates the job source that would invoke the worker.

### 2026-06-06 - dotnet-backend-testing - SBX-01 xUnit coverage

- Agent: Codex worker
- Trigger: GitHub issue #556 requires xUnit coverage for test-key submit->poll, zero quota effect, no outbox/engine call, live path unchanged, and revoked/expired auth.
- Action: Opened and followed the skill plus test-first workflow. Added failing tests in `ApiKeyServiceTests`, `ApiKeyAuthResolverTests`, `ApiKeyHttpFunctionsTests`, and `RewriteApiTests`; the red run failed on missing `IsTest` and sandbox contracts. Implemented the minimal production changes, then reran focused and full backend tests.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyAuthResolverTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyHttpFunctionsTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Focused `dotnet test ReplyInMyVoice.sln --filter "ApiKeyServiceTests|ApiKeyAuthResolverTests|ApiKeyHttpFunctionsTests|RewriteApiTests"` passed 48/48 after implementation. Full `cd backend-dotnet && dotnet test` passed 552/552.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore and tests still completed successfully.

### 2026-06-06 - ui-browser-testing - SBX-01 developer key UI

- Agent: Codex worker
- Trigger: GitHub issue #556 changes browser-visible developer API key creation and listing UI.
- Action: Opened and followed the skill; identified `/developers/keys` key management as the affected flow. Added source-level Vitest coverage for forwarding `{ test: true }`, `isTest` response/list handling, a `Create test key` control, and accessible `Test key` badges. Updated the React component to show test keys with `rmv_test_` masked fallback and a visible `Test` badge.
- Output artifacts: `components/developers/api-keys-panel.tsx`; `tests/unit/api-keys-route.test.ts`; `tests/unit/developer-keys-ui.test.ts`.
- Verification evidence: Focused `npm run test -- tests/unit/api-keys-route.test.ts tests/unit/developer-keys-ui.test.ts` passed 14/14 after implementation. `npm run typecheck` passed. Full `npm run test` passed 448/448. Restricted-term scans over `app components public lib` and changed backend source/tests returned no matches.
- Limitations: No browser screenshot run was completed; the key manager is an authenticated dashboard surface and this issue's machine acceptance required typecheck and unit tests.

### 2026-06-06 - verification-before-completion - SBX-01 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #556 after implementation and test runs.
- Action: Opened and followed the skill; checked the working tree file list, confirmed generated migration files are included, reviewed the recorded command evidence, and avoided completion claims until the verification outputs were available.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: `git status --short` showed only the SBX-01 implementation/test/log files and the two new EF migration files. Previously run acceptance gates passed: focused backend xUnit 48/48, `RewriteApiTests` 30/30, full backend xUnit 552/552, focused key UI Vitest 14/14, `npm run typecheck`, full Vitest 448/448, `git diff --check`, and restricted vocabulary scans.
- Limitations: No local commit was created because writing the worktree git index lock was blocked by filesystem permissions outside the writable worktree.
### 2026-06-06 - dynamic-delivery-workflow - WH-01 API result webhooks

- Agent: Codex worker
- Trigger: Supervised unattended delivery wave for GitHub issue #557 / WH-01, with strict no-push/no-PR/no-deploy constraints.
- Action: Opened and followed the project skill. Confirmed scope is only API result webhooks, read the issue body, `plans/rewrite-api-v1/adv-issues/WH-01-webhooks.md`, `AGENTS.md`, and `CLAUDE.md`, and kept work on branch `delivery/api-adv/WH-01-557`.
- Output artifacts: Local code, tests, migration, docs, and this run log only.
- Verification evidence: No `git push`, PR, deploy, Azure provision, or live payment command was run.
- Limitations: The supervisor owns push and PR creation after re-verification.

### 2026-06-06 - system-spec-synthesis - WH-01 API result webhooks

- Agent: Codex worker
- Trigger: GitHub issue #557 plus the WH-01 brief require API/data/job contracts for per-key webhook configuration, terminal rewrite delivery records, signed delivery, and docs.
- Action: Opened and followed the skill; converted the issue and brief into an implementation checklist covering context, goals, non-goals, contracts, data model, state transitions, auth boundaries, failure handling, and verification.
- Output artifacts: `plans/rewrite-api-v1/webhooks.md`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/WebhookDelivery.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDeliveryService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDispatcherService.cs`; `app/api/keys/[id]/webhook/route.ts`; `components/developers/api-keys-panel.tsx`; related tests.
- Verification evidence: Focused backend webhook tests passed 28/28. Full `cd backend-dotnet && dotnet test` passed 554/554. `npm run typecheck` passed. Full `npm run test` passed 450/450.
- Limitations: No separate system-spec document was created because the issue and WH-01 brief were already implementation-scoped.

### 2026-06-06 - cloud-architecture-cost-review - WH-01 webhook dispatcher timer

- Agent: Codex worker
- Trigger: WH-01 adds scheduled webhook delivery work in the Azure Functions backend.
- Action: Opened and followed the skill; selected the existing Azure Functions timer pattern already used by `OutboxDispatcherTimerFunction`, with no new always-on worker, App Service, queue, database, or paid cloud resource. Added only an in-process dispatcher service plus timer trigger.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/WebhookDispatcherTimerFunction.cs`; `plans/rewrite-api-v1/scheduled-jobs.md`.
- Verification evidence: Full `cd backend-dotnet && dotnet test` passed 554/554. No deploy, provision, or live cloud command was run.
- Limitations: This was a local architecture/cost check only; the supervisor owns deployment/runtime smoke.

### 2026-06-06 - state-machine-modeling - WH-01 webhook delivery lifecycle

- Agent: Codex worker
- Trigger: WH-01 adds a multi-step webhook delivery lifecycle with pending, delivered, failed, retries, locks, and terminal rewrite events.
- Action: Opened and followed the skill. State list: `Pending`, `Delivered`, `Failed`. Events: terminal API rewrite finalized, terminal API rewrite released/expired, dispatcher 2xx response, dispatcher non-2xx/exception, retry limit reached. Invariants: no delivery for website attempts; core rewrite finalization/release remains authoritative if enqueue fails; only pending due deliveries are claimed; delivered and failed states are terminal.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/WebhookDeliveryStatus.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/WebhookDelivery.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDispatcherService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`.
- Verification evidence: xUnit coverage asserts API success enqueue, website no-op, failed release enqueue, delivered on 200, and failed after retry limit. Full `cd backend-dotnet && dotnet test` passed 554/554.
- Limitations: Webhook configuration rotation keeps the existing key-rotation behavior by carrying configuration to the replacement key.

### 2026-06-06 - data-module-review - WH-01 webhook persistence

- Agent: Codex worker
- Trigger: WH-01 changes EF entities, migrations, indexes, relationships, and persistence invariants for API keys and webhook deliveries.
- Action: Opened and followed the skill; reviewed `ApiKey`, `ApiKeyUsage`, `RewriteAttempt`, `AppDbContext`, and migration output. Added nullable per-key `WebhookUrl` and `WebhookSecret`, a new `WebhookDelivery` table, unique delivery guard on `(ApiKeyId, RewriteAttemptId)`, due-delivery indexes, and concurrency token mapping.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKey.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/WebhookDelivery.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260606043949_AddApiResultWebhooks.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260606043949_AddApiResultWebhooks.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.
- Verification evidence: EF migration was generated by `dotnet ef migrations add AddApiResultWebhooks`; focused SQLite tests found and then verified the DateTimeOffset query path fix. Full `cd backend-dotnet && dotnet test` passed 554/554.
- Limitations: No production migration was applied locally.

### 2026-06-06 - resilience-test-generation - WH-01 webhook failure handling

- Agent: Codex worker
- Trigger: WH-01 changes retries, backoff, provider/network delivery failures, duplicate enqueue risk, and isolation from the core rewrite finalize/release path.
- Action: Opened and followed the skill; built the failure matrix around enqueue exception, non-API terminal attempt, duplicate delivery, non-2xx receiver response, retry exhaustion, and cancellation/exception handling during dispatch. Implemented deterministic fake sender tests and enqueue-failure isolation.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDeliveryService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDispatcherService.cs`.
- Verification evidence: Focused backend webhook suite passed 28/28. Full `cd backend-dotnet && dotnet test` passed 554/554.
- Limitations: No external HTTP receiver was contacted; delivery tests use an in-memory fake sender.

### 2026-06-06 - dotnet-backend-testing - WH-01 xUnit coverage

- Agent: Codex worker
- Trigger: WH-01 acceptance requires xUnit coverage for API enqueue, website no-op, HMAC signing, delivered/failed dispatcher outcomes, and finalize-path isolation.
- Action: Opened and followed the project skill plus test-first workflow. Added backend tests to existing API key and quota suites plus a new dispatcher service suite. The initial focused red run failed on missing webhook contracts/services; after implementation, the focused suite passed.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyHttpFunctionsTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`.
- Verification evidence: Focused backend webhook test filter passed 28/28. Full `cd backend-dotnet && dotnet test` passed 554/554.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore and tests still completed successfully.

### 2026-06-06 - ui-browser-testing - WH-01 API key webhook UI

- Agent: Codex worker
- Trigger: WH-01 changes browser-visible API key management UI and Next proxy routes for setting/clearing key webhook URLs.
- Action: Opened and followed the project skill; identified the developer API keys panel and `/api/keys/[id]/webhook` route as the browser-visible surfaces. Added source-level unit tests for proxy forwarding and pinned UI copy/strings for webhook URL management and one-time signing value reveal.
- Output artifacts: `components/developers/api-keys-panel.tsx`; `app/api/keys/[id]/webhook/route.ts`; `tests/unit/api-keys-route.test.ts`; `tests/unit/developer-keys-ui.test.ts`.
- Verification evidence: `npm run typecheck` passed. Full `npm run test` passed 450/450.
- Limitations: No Playwright/browser screenshot pass was run because the issue acceptance required unit/typecheck gates and this worker run did not start a local web server.

### 2026-06-06 - system-spec-synthesis - RFX-01 public API paid gate

- Agent: Codex worker
- Trigger: GitHub issue #562 / RFX-01 changes the public API v1 submit contract from quota-only rejection to a paid-entitlement gate before reservation.
- Action: Opened and followed the skill; translated the issue and brief into an implementation checklist. Context: live API keys currently loaded the account plan and reserved quota without checking paid entitlement. Goals: reject live free-baseline API calls with `402 api_requires_paid_plan`, keep sandbox keys exempt, allow active/trialing/testing subscriptions and usable purchased credits, and preserve website rewrite free-quota behavior. Non-goals: no frontend changes, no payment provider changes, no schema migration, no deployment.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Focused backend filter for `AccountServiceTests|RewriteApiTests` passed 50/50 after the red compile failure on missing `HasPaidApiEntitlementAsync`. Full `cd backend-dotnet && dotnet test` passed 566/566.
- Limitations: No separate spec document was created because the issue and RFX-01 brief were already implementation-scoped.

### 2026-06-06 - state-machine-modeling - RFX-01 API entitlement and reservation boundary

- Agent: Codex worker
- Trigger: RFX-01 changes the quota/reservation lifecycle by adding an entitlement decision before a public API live request can enter pending reservation state.
- Action: Opened and followed the skill. State list: live key authenticated; sandbox key authenticated; live account has API paid entitlement; live account lacks API paid entitlement; quota reservation pending; quota rejected. Events: submit v1 rewrite, sandbox key detected, active/trialing/testing subscription found, usable purchased credit found, no entitlement found, reservation created, quota exhausted. Transition table: sandbox submit -> sandbox attempt; live entitled submit -> quota reservation path; live not entitled submit -> `402 api_requires_paid_plan`; entitled but quota exhausted -> existing `402 quota_exhausted`. Invariants: no reservation, rewrite attempt, outbox message, or credit consumption on entitlement rejection; sandbox remains exempt; website rewrite path remains unchanged.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: xUnit asserts free-baseline live key rejection with no reservation, active subscription acceptance through existing v1 submit test, usable purchased credit acceptance, and sandbox key acceptance through existing sandbox test. Full `cd backend-dotnet && dotnet test` passed 566/566.
- Limitations: Concurrent depletion after the entitlement check is still handled by the existing quota reservation path, which can return quota exhaustion.

### 2026-06-06 - data-module-review - RFX-01 account and credit entitlement lookup

- Agent: Codex worker
- Trigger: RFX-01 reviews quota/account persistence invariants for `AppUser.SubscriptionStatus`, `RewriteCredit`, `UsagePeriod`, `UsageReservation`, and pre-reservation API rejection.
- Action: Opened and followed the skill; reviewed AccountService, QuotaService, AppDbContext mappings, and tests together. Findings: no new schema/migration required; entitlement lookup is read-only and uses tracked reservation code only after the gate passes; SQLite DateTimeOffset limitations are avoided by materializing candidate purchased credits before expiry filtering; rejection creates no usage period, reservation, attempt, outbox message, or credit consumption. Ran the data-risk scan against the infrastructure project root.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80 backend-dotnet/src/ReplyInMyVoice.Infrastructure` completed and reported existing quota/idempotency hotspots for review. Focused backend tests passed 50/50. Full `cd backend-dotnet && dotnet test` passed 566/566.
- Limitations: The scan is signal-based and intentionally broad; it is not a proof of all persistence invariants.

### 2026-06-06 - dotnet-backend-testing - RFX-01 xUnit paid gate coverage

- Agent: Codex worker
- Trigger: RFX-01 acceptance requires xUnit coverage for free-baseline live key rejection, active subscription acceptance, purchased-credit acceptance, sandbox exemption, and no reservation/charge on rejection.
- Action: Opened and followed the project skill plus test-first workflow. Added AccountService helper tests and extended RewriteApiTests before production code. The first focused run failed because `HasPaidApiEntitlementAsync` did not exist, then passed after implementation.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Focused `dotnet test ReplyInMyVoice.sln --filter "FullyQualifiedName~AccountServiceTests|FullyQualifiedName~RewriteApiTests" --no-restore` passed 50/50. Full `cd backend-dotnet && dotnet test` passed 566/566.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore and tests still completed successfully.

### 2026-06-06 - verification-before-completion - RFX-01 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #562 after implementation and test runs.
- Action: Opened and followed the skill; reran the proof commands after implementation and before making completion claims. Checked full backend tests, diff whitespace, changed-file blocked-copy scan, and worktree status.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: Fresh `cd backend-dotnet && dotnet test` passed 566/566. `git diff --check` passed. Changed-file blocked-copy scan returned no matches. `git status --short` showed only the six RFX-01 modified files.
- Limitations: Local commit creation failed because the git worktree index lock is outside the writable sandbox at `/Users/qc/Desktop/CloudFlare/.git/worktrees/issue-562/index.lock`.

### 2026-06-06 - state-machine-modeling - RFX-02 usage accounting and v1 result environment boundary

- Agent: Codex worker
- Trigger: GitHub issue #563 / RFX-02 changes quota accounting, usage windows, and v1 attempt result visibility across live and test API key environments.
- Action: Opened and followed the skill. State list: free period quota, paid period quota, active credit balance, pending period reservation, credit consumed balance, live API key result read, test API key result read. Events: account summary requested, period reservation created/finalized, credit-backed reservation created/released, usage series requested, recent usage requested, v1 result polled. Transition table: free/paid period usage contributes to period used/reserved; active credit consumption contributes to aggregate used and credit remaining; series/recent queries enter a bounded `1..90` day window; live result reads can see only live attempts; test result reads can see only test attempts. Invariants: `quota - used - reserved == remaining`; remaining never negative; credit remaining is preserved when old period usage exists; cross-environment result reads return not found.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyUsageQueryService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; related xUnit tests.
- Verification evidence: Focused backend filter for `AccountServiceTests|ApiKeyUsageQueryServiceTests|ApiUsageHttpFunctionsTests|RewriteApiTests` passed 58/58. Full `cd backend-dotnet && dotnet test` passed 571/571. `npm run typecheck` passed.
- Limitations: Credit reservations are still represented by the existing `RewriteCredit.AmountConsumed` model; no schema change was made.

### 2026-06-06 - data-module-review - RFX-02 usage counters and API usage query bounds

- Agent: Codex worker
- Trigger: RFX-02 changes usage counters, credit accounting, EF query shape, and API key usage persistence reads.
- Action: Opened and followed the skill; reviewed `AccountService`, `QuotaService`, `ApiKeyUsageQueryService`, `ApiKeyUsage`, `ApiKey`, and existing SQLite test fixtures together. Findings: no migration required; aggregate account usage could be made coherent without changing the response contract; SQLite cannot translate `DateTimeOffset` lower-bound LINQ filters, so the bounded usage read uses parameterized SQL for the user/key/date join and composes projections afterward; recent rows are limited to the same 90-day safety window.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyUsageQueryService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminCreditAdjustTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyUsageQueryServiceTests.cs`.
- Verification evidence: New command-capture xUnit coverage asserts the API usage query includes a `CreatedAt >=` lower bound. Full `cd backend-dotnet && dotnet test` passed 571/571. `git diff --check` passed.
- Limitations: The query-bound proof is local SQLite/EF coverage plus SQL text capture; no production database query plan was captured.

### 2026-06-06 - dotnet-backend-testing - RFX-02 xUnit coverage

- Agent: Codex worker
- Trigger: RFX-02 acceptance requires xUnit coverage for usage invariant across free/paid/credit, bounded API usage windows, and live/test result isolation.
- Action: Opened and followed the project skill plus test-first workflow. Added failing tests first, confirmed red failures for credit aggregate math, unbounded `days`, recent rows outside the window, missing query lower bound, and live-key reads of test attempts. Implemented minimal backend fixes, then updated existing account/admin credit tests to the new coherent aggregate definition.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminCreditAdjustTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyUsageQueryServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiUsageHttpFunctionsTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Red run failed for the intended RFX-02 behaviors before production edits. Focused backend filter passed 58/58 after implementation. Full `cd backend-dotnet && dotnet test` passed 571/571.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore and tests still completed successfully.

### 2026-06-06 - verification-before-completion - RFX-02 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #563 after implementation and gate runs.
- Action: Opened and followed the skill; reran proof commands after implementation before completion claims. Checked full backend tests, frontend typecheck, source-only restricted-copy scan, diff whitespace, and worktree status.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: Fresh `cd backend-dotnet && dotnet test` passed 571/571. `npm run typecheck` passed after restoring `node_modules` with `npm ci --cache /private/tmp/rfx-02-563-npm-cache`. Source-only restricted-copy scan over `app components public lib backend-dotnet/src backend-dotnet/tests` returned no matches when excluding generated output. `git diff --check` passed.
- Limitations: Initial `npm run typecheck` failed because `node_modules` was absent; initial `npm ci` failed on an unwritable supervisor npm cache, so the install was rerun with a writable temp cache. npm also warned this shell uses Node 24 while the project declares Node 22.

### 2026-06-06 - data-module-review - RFX-03 API rate-limit counters and RequestId index

- Agent: Codex worker
- Trigger: GitHub issue #564 / RFX-03 changes EF Core persistence for API key usage lookup indexing and introduces a persisted per-key fixed-window rate-limit counter.
- Action: Opened and followed the skill; reviewed `ApiKeyUsage`, `ApiKey`, `RewriteAttempt.IdempotencyKey`, AppDbContext mappings, generated migration output, and V1 submit/result/usage mutation paths together. Invariants checked: rate-limit decisions no longer depend on best-effort `ApiKeyUsage` analytics rows; limiter write failure fails closed; over-length idempotency keys cannot reach `RewriteAttempt` persistence; `ApiKeyUsage.RequestId` gets a plain lookup index; the migration adds only one direct FK from `ApiKeyRateLimitWindows` to `ApiKeys`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKeyRateLimitWindow.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyRateLimiter.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260606080828_AddApiRateLimitWindowsAndUsageRequestIdIndex.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.
- Verification evidence: Generated migration contains `IX_ApiKeyUsages_RequestId`, unique `IX_ApiKeyRateLimitWindows_ApiKeyId_WindowStart`, and no second cascade path to `RewriteAttempts`. Focused `V1RewriteRateLimitTests` passed 4/4. Full `cd backend-dotnet && dotnet test` passed 575/575. `cd backend-dotnet && dotnet build` passed.
- Limitations: `ApiKeyUsage.RequestId` was intentionally indexed but not made unique because usage rows are analytics/audit records and duplicate logical IDs can occur across retries or endpoint classes; no production migration was applied.

### 2026-06-06 - resilience-test-generation - RFX-03 concurrent rate-limit and fail-closed coverage

- Agent: Codex worker
- Trigger: RFX-03 changes rate-limit enforcement under concurrent public API submits and failure handling when limiter/usage persistence fails.
- Action: Opened and followed the skill; built a failure matrix covering concurrent submits, duplicate/racing counter creation, usage analytics write failure, limiter unavailable, malformed/over-length idempotency input, and quota side effects. Implemented deterministic SQLite xUnit coverage with a trigger that rejects every `ApiKeyUsages` insert to prove rate limiting does not depend on those rows.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/V1RewriteRateLimitTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyRateLimiter.cs`; V1 submit/result/usage handler updates in `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` and `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`.
- Verification evidence: Focused test `V1_rewrite_submit_enforces_per_key_rate_limit_under_concurrent_usage_write_failures` accepted exactly 2 of 10 concurrent requests at a limit of 2, returned `429 rate_limited` for the rest, created exactly 2 attempts/reservations/outbox messages, and left `ApiKeyUsages` empty due the trigger. `V1_rewrite_submit_fails_closed_when_rate_limiter_is_unavailable` returned `503 rate_limit_unavailable` with no attempt/reservation/outbox.
- Limitations: The concurrency proof uses file-backed SQLite WAL in tests; SQL Server behavior is backed by serializable transactions, a unique counter-window index, concurrency-token retries, and build-verified EF SQL Server mappings.

### 2026-06-06 - dotnet-backend-testing - RFX-03 xUnit coverage

- Agent: Codex worker
- Trigger: RFX-03 acceptance requires xUnit coverage for concurrent rate limits, fail-closed limiter behavior, over-length `Idempotency-Key`, and the migration index.
- Action: Opened and followed the project skill plus test-first workflow. Added `V1RewriteRateLimitTests` before production code. The first focused run failed at compile time because `IApiKeyRateLimiter` / `ApiKeyRateLimitResult` did not exist; after implementation, the focused class failed only on the missing migration index; after EF migration generation, the focused class passed.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/V1RewriteRateLimitTests.cs`.
- Verification evidence: Focused `dotnet test tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter V1RewriteRateLimitTests --no-restore` passed 4/4. Existing `RewriteApiTests` passed 32/32. Full `cd backend-dotnet && dotnet test` passed 575/575.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore/build/test still completed successfully.

### 2026-06-06 - verification-before-completion - RFX-03 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #564 after implementation and gate runs.
- Action: Opened and followed the skill; reran proof commands before completion claims. Checked focused RFX-03 tests, existing V1 rewrite API tests, full backend build, full backend test suite, and banned-term scans.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: Focused `V1RewriteRateLimitTests` passed 4/4. Existing `RewriteApiTests` passed 32/32. `cd backend-dotnet && dotnet build` passed. `cd backend-dotnet && dotnet test` passed 575/575. Targeted `npm run test -- tests/unit/openapi-spec.test.ts` passed 3/3 after `npm ci --cache /private/tmp/rfx-03-564-npm-cache`. Banned-term scans over `app components public lib` and changed files returned no matches.
- Limitations: The first EF migration command took the default host-factory timeout before returning; it still generated the migration successfully. `npm ci` reported existing audit findings and a Node 24 runtime warning against the repo's Node 22 engine. No push, PR, deploy, or production database migration was run.

### 2026-06-06 - data-module-review - RFX-04 webhook URL persistence boundary

- Agent: Codex worker
- Trigger: GitHub issue #565 / RFX-04 reviews persisted API key webhook URLs and webhook delivery rows that can outlive the save-time validator.
- Action: Opened and followed the skill; reviewed `ApiKey.WebhookUrl`, `WebhookDelivery.Url`, API key rotation, webhook setup, delivery claim/send/failure marking, and SQLite test fixtures together. Findings: no migration required; old rows need send-time validation before dispatch; rejection should follow the existing failed-attempt path and must not expose the signing value.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookEndpointSafety.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDispatcherService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`.
- Verification evidence: Focused webhook/API key/infrastructure xUnit filter passed 34/34 after implementation. Full `cd backend-dotnet && dotnet test` passed 583/583.
- Limitations: No schema migration or backfill was added; existing unsafe rows are handled by send-time rejection.

### 2026-06-06 - resilience-test-generation - RFX-04 webhook network hardening

- Agent: Codex worker
- Trigger: RFX-04 changes outbound webhook network failure handling, redirect behavior, connect-time host resolution, and per-request timeouts.
- Action: Opened and followed the skill; built focused resilience coverage for non-HTTPS input, local/link-local/private saved destinations, old-row send-time refusal, and registered handler redirect prevention. Used deterministic unit tests and handler inspection instead of live remote calls.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookHttpClientFactory.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`.
- Verification evidence: Initial focused test run failed for the intended behaviors. Focused rerun passed 34/34. Full `cd backend-dotnet && dotnet test` passed 583/583.
- Limitations: Redirect behavior is verified through handler configuration rather than a live redirect server, because the webhook sender now rejects non-HTTPS/local test targets before the request leaves the process.

### 2026-06-06 - dotnet-backend-testing - RFX-04 xUnit webhook hardening coverage

- Agent: Codex worker
- Trigger: RFX-04 acceptance requires xUnit coverage for webhook URL rejection/acceptance, old-row send-time validation, and no-redirect HTTP client configuration.
- Action: Opened and followed the project skill plus test-first workflow. Added failing tests in `ApiKeyServiceTests`, `WebhookDispatcherServiceTests`, and `InfrastructureServiceCollectionTests`, confirmed red failures, then implemented the shared validator, send-time guards, and typed HTTP client handler configuration.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyHttpFunctionsTests.cs`.
- Verification evidence: Red focused run failed with 7 expected failures. Focused rerun passed 34/34. Full `cd backend-dotnet && dotnet test` passed 583/583.
- Limitations: Public URL acceptance uses a deterministic test resolver for `example.com`; function and dispatcher fixtures use a public literal endpoint to avoid external DNS in tests.

### 2026-06-06 - verification-before-completion - RFX-04 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #565 after implementation and gate runs.
- Action: Opened and followed the skill; reran proof commands before completion claims. Checked focused xUnit coverage, full backend tests, restricted-copy scans, and project UI copy scan.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: Focused webhook/API key/infrastructure filter passed 34/34. Full `cd backend-dotnet && dotnet test` passed 583/583. Restricted-copy scans over touched files, backend source/tests, and `app components public lib` returned no matches.
- Limitations: Local commit creation failed because the git worktree index lock is outside the writable sandbox at `/Users/qc/Desktop/CloudFlare/.git/worktrees/issue-565/index.lock`. No push, PR, deploy, production database migration, or live webhook call was run.

### 2026-06-06 - system-spec-synthesis - RFX-05 webhook reliability scope

- Agent: Codex worker
- Trigger: GitHub issue #566 / RFX-05 changes webhook job lifecycle, event contract headers, retry behavior, and EF Core origin persistence.
- Action: Opened and followed the skill; converted the issue body, RFX-05 brief, AGENTS.md constraints, and cross-review notes into a scoped implementation spec covering context, goals, non-goals, current system, data model, job contract, state handling, security/privacy, rollout, verification, and open questions.
- Output artifacts: Concise working spec in the session update; implementation checkpoints executed in `WebhookDispatcherService`, `WebhookDeliveryService`, `RewriteAttempt`, `QuotaService`, v1 submit handlers, EF migration, and xUnit coverage.
- Verification evidence: Focused RFX-05 filter passed 59/59. Full `cd backend-dotnet && dotnet build` passed. Full `cd backend-dotnet && dotnet test` passed 586/586.
- Limitations: No deployment, push, PR, production database migration, or live webhook call was run.

### 2026-06-06 - state-machine-modeling - RFX-05 webhook delivery lifecycle

- Agent: Codex worker
- Trigger: RFX-05 adds an in-flight webhook delivery state and terminal poison-row behavior.
- Action: Opened and followed the skill; modeled `WebhookDelivery` states as `Pending`, `InProgress`, `Delivered`, and `Failed`; transitions are due claim, send success, retryable send failure, max-attempt failure, and stale in-progress recovery after lease expiry. Invariants checked: terminal states are not due-claimed, in-flight rows are not reclaimed during the timer edge, retries clear locks, and poison rows become terminal at the retry limit.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/WebhookDeliveryStatus.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDispatcherService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`.
- Verification evidence: `DispatchDueAsync_does_not_claim_delivery_again_on_next_timer_tick_while_send_is_running` passed; `DispatchDueAsync_terminalizes_delivery_when_attempt_data_is_missing` passed; focused RFX-05 filter passed 59/59.
- Limitations: Lease recovery is covered by service-level tests with deterministic fakes; no live receiver was called.

### 2026-06-06 - data-module-review - RFX-05 API key origin persistence

- Agent: Codex worker
- Trigger: RFX-05 changes EF Core persistence by adding the source API key to `RewriteAttempt` and changing webhook enqueue lookup invariants.
- Action: Opened and followed the skill; reviewed `RewriteAttempt`, `ApiKey`, `ApiKeyUsage`, `WebhookDelivery`, `UsageReservation`, AppDbContext mappings, generated migration, v1 submit paths, and enqueue service together. Findings addressed: source-of-truth origin must live on the attempt, website attempts keep null origin, webhook delivery FK remains non-cascading, and usage analytics rows are no longer required to enqueue terminal API attempts.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteAttempt.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260606084307_AddRewriteAttemptApiKeyId.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/WebhookDeliveryService.cs`.
- Verification evidence: Generated migration adds nullable `RewriteAttempts.ApiKeyId`, `IX_RewriteAttempts_ApiKeyId`, and `FK_RewriteAttempts_ApiKeys_ApiKeyId` with `NoAction`. `FinalizeSuccessAsync_enqueues_webhook_delivery_from_persisted_api_key_id_without_usage_row` passed. Full backend build and tests passed.
- Limitations: Existing rows are left with null origin and therefore do not enqueue new webhook deliveries retroactively.

### 2026-06-06 - resilience-test-generation - RFX-05 webhook delivery failure matrix

- Agent: Codex worker
- Trigger: RFX-05 changes webhook timeouts, leases, retry races, poison delivery handling, and signed request replay-bounding.
- Action: Opened and followed the skill; built deterministic tests for slow receiver in-flight protection, missing required data at max attempts, persisted-origin enqueue without usage analytics, non-API no-enqueue behavior, and timestamp-covered signatures. Implemented serialization/race retry around claim transactions using the same bounded retry style as other persistence race paths.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; dispatcher retry code in `WebhookDispatcherService`.
- Verification evidence: Initial focused run failed at the missing `InProgress` state. After implementation, focused RFX-05 filter passed 59/59 and full `cd backend-dotnet && dotnet test` passed 586/586.
- Limitations: Serialization retry is covered by code path and full test suite, not by a SQL Server deadlock harness.

### 2026-06-06 - dotnet-backend-testing - RFX-05 xUnit coverage

- Agent: Codex worker
- Trigger: RFX-05 acceptance requires xUnit coverage for slow delivery claim behavior, poison terminal state, persisted API key origin, API-only webhook enqueue, and timestamp-covered signing.
- Action: Opened and followed the skill plus test-first workflow. Added focused xUnit tests before implementation in existing backend test classes, watched the focused run fail on the missing lifecycle state, then implemented the minimum backend changes and reran focused and full backend suites.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/WebhookDispatcherServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`.
- Verification evidence: Focused `dotnet test ReplyInMyVoice.sln --filter "WebhookDispatcherServiceTests|QuotaServiceTests|RewriteApiTests"` passed 59/59. Full `cd backend-dotnet && dotnet test` passed 586/586. Full `cd backend-dotnet && dotnet build` passed with 0 warnings and 0 errors.
- Limitations: The EF tooling `migrations list` command timed out while checking database status because no local SQL Server is reachable; the generated migration still compiles and was verified by build/test.

### 2026-06-06 - verification-before-completion - RFX-05 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #566 after implementation and gate runs.
- Action: Opened and followed the skill; reran proof commands before completion claims. Checked focused RFX-05 xUnit coverage, full backend build, full backend tests, project UI copy scan, and changed-line restricted-copy scan.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: Focused RFX-05 filter passed 59/59. `cd backend-dotnet && dotnet build` passed. `cd backend-dotnet && dotnet test` passed 586/586. Restricted-copy scans over `app components public lib` and changed lines returned no matches.
- Limitations: No push, PR, deploy, production database migration, or live webhook call was run.

### 2026-06-06 - resilience-test-generation - RFX-06 provider exception release

- Agent: Codex worker
- Trigger: GitHub issue #567 / RFX-06 changes provider failure recovery and quota release behavior for rewrite jobs.
- Action: Opened and followed the skill; identified provider exception, timeout, explicit failed result, malformed result, expired reservation, and queue redelivery boundaries. Added deterministic xUnit coverage for the generic provider exception path so failed provider work releases quota immediately and records a terminal attempt.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobProcessorTests.cs`.
- Verification evidence: Focused `RewriteJobProcessorTests` first failed on `ProcessAsync_releases_reservation_when_provider_throws` because the exception rethrew. After implementation, focused `RewriteJobProcessorTests` passed 8/8. Full `cd backend-dotnet && dotnet test -m:1` passed 592/592.
- Limitations: No bounded provider retry was added; the existing local pattern is prompt terminal release for non-timeout provider exceptions.

### 2026-06-06 - state-machine-modeling - RFX-06 rewrite attempt terminal release

- Agent: Codex worker
- Trigger: RFX-06 changes `RewriteAttempt` / `UsageReservation` lifecycle transitions for provider exception handling.
- Action: Opened and followed the skill; modeled states as `Pending`, `Processing`, `Succeeded`, `Failed`, and `Expired`. Events reviewed: claim for processing, provider success, provider explicit failure, provider exception, timeout, malformed result, expiry, and redelivery. Invariants checked: terminal attempts are no-ops on redelivery, failed/expired attempts do not increment `UsedCount`, and released reservations leave `ReservedCount` at zero.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobProcessorTests.cs`.
- Verification evidence: New provider-exception test asserts `UsageReservationStatus.Released`, `RewriteAttemptStatus.Failed`, `UsedCount == 0`, and `ReservedCount == 0`. Focused processor tests passed 8/8; full backend tests passed 592/592.
- Limitations: No new enum state or migration was needed; the existing `Failed` terminal state remains the release target.

### 2026-06-06 - data-module-review - RFX-06 ApiKey RowVersion and quota invariants

- Agent: Codex worker
- Trigger: RFX-06 reviews EF Core persistence invariants for `ApiKey.RowVersion`, `UsageReservation`, `UsagePeriod`, and provider failure accounting.
- Action: Opened and followed the skill; reviewed `ApiKey` entity, `AppDbContext` concurrency mapping, API key service write paths, quota release behavior, and worker tests together. Chose the low-risk client-managed Guid option from the brief, documented the contract on `ApiKey.RowVersion`, and added focused service assertions for create, revoke, rotate, set webhook, and clear webhook paths.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/ApiKey.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`.
- Verification evidence: `ApiKey_write_paths_bump_client_managed_row_version` passed inside the focused API key service filter. Full backend build passed. Full backend tests passed 592/592.
- Limitations: No DB-generated rowversion migration was added, avoiding churn in existing optimistic-concurrency mappings and tests.

### 2026-06-06 - dotnet-backend-testing - RFX-06 xUnit backend coverage

- Agent: Codex worker
- Trigger: RFX-06 acceptance requires xUnit coverage for missing production API key pepper behavior, test-environment tolerance, provider exception release, and client-managed key concurrency token bumps.
- Action: Opened and followed the project skill plus test-first workflow. Added focused tests in `ApiKeyServiceTests` and `RewriteJobProcessorTests`, confirmed red failures for production missing-pepper hashing and provider exception release, then implemented minimal backend changes and reran focused and full backend gates.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteJobProcessorTests.cs`.
- Verification evidence: Red API key focused run failed on missing production pepper throw; red processor focused run failed because the provider exception rethrew. Green focused runs passed `ApiKeyServiceTests` 18/18 and `RewriteJobProcessorTests` 8/8. Exact `cd backend-dotnet && dotnet build` passed. Exact `cd backend-dotnet && dotnet test` passed 592/592.
- Limitations: `dotnet` emitted `NU1900` warnings because NuGet vulnerability metadata could not be fetched; restore/build/test still completed successfully.

### 2026-06-06 - verification-before-completion - RFX-06 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #567 after implementation and gate runs.
- Action: Opened and followed the skill; reran proof commands before completion claims. Checked focused xUnit coverage, full backend build, full backend tests, and restricted substring scan over app/UI and backend source/test directories.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: `ApiKeyServiceTests` passed 18/18. `RewriteJobProcessorTests` passed 8/8. Exact `cd backend-dotnet && dotnet build` passed. Exact `cd backend-dotnet && dotnet test` passed 592/592. Restricted substring scan over `app components public lib backend-dotnet/src backend-dotnet/tests` returned no matches.
- Limitations: An initial parallel focused test run hit Functions worker build artifact races; sequential focused tests and exact full backend commands later passed. No push, PR, deploy, production database migration, or live provider call was run.
### 2026-06-06 - system-spec-synthesis - RFX-08 SDK and OpenAPI contract accuracy

- Agent: Codex worker
- Trigger: GitHub issue #561 / RFX-08 changes the documented v1 API contract and the TypeScript SDK contract.
- Action: Opened and followed the skill; converted the issue and `plans/rewrite-api-v1/fix-issues/RFX-08-sdk-openapi.md` into an implementation checklist covering SDK idempotency, submit response validation, polling timing, UUID rewrite ids, nullable usage periods, unauthenticated response headers, request body schema limits, and verification gates.
- Output artifacts: `packages/sdk/src/index.ts`; `packages/sdk/package.json`; `packages/sdk/LICENSE`; `packages/sdk/README.md`; `public/openapi.json`; `tests/unit/sdk.test.ts`; `tests/unit/openapi-spec.test.ts`.
- Verification evidence: Focused SDK/OpenAPI red run failed on the missing contract behavior, then passed 15/15 after implementation. `npm run typecheck`, `npm run test`, `cd packages/sdk && npm run build`, `git diff --check`, package metadata check, and scoped restricted-term scan all passed.
- Limitations: No live API or deployment smoke was run; this issue is limited to local SDK/spec/test artifacts.

### 2026-06-06 - resilience-test-generation - RFX-08 SDK idempotency and polling

- Agent: Codex worker
- Trigger: RFX-08 changes retry-safe SDK submit behavior and the polling loop timing.
- Action: Opened and followed the skill; identified the critical invariant that repeated logical submits can carry a stable idempotency key and that polling should not hammer the result endpoint immediately. Added deterministic unit tests using mocked fetch and fake timers for idempotency header forwarding, invalid submit response handling, initial poll delay, timeout behavior, and failed job propagation.
- Output artifacts: `tests/unit/sdk.test.ts`; `packages/sdk/src/index.ts`.
- Verification evidence: Focused SDK/OpenAPI tests passed 15/15. Full `npm run test` passed 457/457. `npm run typecheck` and `cd packages/sdk && npm run build` passed.
- Limitations: Tests use local mocked fetch only; no external API endpoint was contacted.

### 2026-06-06 - ui-browser-testing - RFX-07 developer export controls

- Agent: Codex worker
- Trigger: GitHub issue #568 / RFX-07 changes browser-visible developer Usage and Billing CSV export controls, loading/error states, and same-origin API reads.
- Action: Opened and followed the skill; identified the user-visible export flows, added a client CSV download helper test, updated component source assertions for fetch/blob export wiring, converted export links to buttons with loading/error states, and updated the developer billing Playwright expectation to the new button/download behavior.
- Output artifacts: `components/developers/usage-panel.tsx`; `components/developers/billing-panel.tsx`; `lib/client-csv-download.ts`; `tests/unit/client-csv-download.test.ts`; `tests/unit/developer-keys-ui.test.ts`; `tests/unit/developer-billing-panel.test.ts`; `tests/e2e/developer-billing.spec.ts`.
- Verification evidence: Focused unit run for export/client/component tests passed 61/61. Full `npm run test` passed 462/462 before the Playwright expectation update. Focused Playwright command `npx playwright test tests/e2e/developer-billing.spec.ts --project=chromium` was attempted for real-browser coverage.
- Limitations: The focused Playwright run hung during local server startup with no reporter output and the sandbox blocked process inspection/termination. No deploy, push, PR, or live payment action was run.

### 2026-06-06 - resilience-test-generation - RFX-07 v1 proxy failure handling

- Agent: Codex worker
- Trigger: GitHub issue #568 / RFX-07 changes tests for v1 proxy backend fetch failures and telemetry delivery scheduling.
- Action: Opened and followed the skill; identified the Azure proxy boundary and failure invariant: failed upstream fetches must not surface framework 500s and telemetry must be scheduled without blocking the response. Added deterministic route tests for submit, result, and usage proxy failures returning the documented error shape with HTTP 502.
- Output artifacts: `app/api/v1/rewrite/route.ts`; `app/api/v1/rewrite/[id]/route.ts`; `app/api/v1/usage/route.ts`; `lib/api-observability.ts`; `tests/unit/public-rewrite-api-route.test.ts`; `tests/unit/v1-usage-route.test.ts`; `tests/unit/api-observability.test.ts`.
- Verification evidence: Red focused run failed on thrown upstream errors and missing scheduler. After implementation, focused v1 proxy and observability tests passed. Full `npm run test` passed 462/462 before the Playwright expectation update.
- Limitations: No retry behavior was added; RFX-07 only required deterministic 502 wrapping and non-blocking telemetry scheduling. No live Azure, PostHog, or Sentry call was run.

### 2026-06-06 - cloud-architecture-cost-review - RFX-09 SQL Server CI migration gate

- Agent: Codex worker
- Trigger: RFX-09 changes the `dotnet-azure` CI/CD deploy gate for EF Core migrations and SQL Server validation.
- Action: Opened and followed the project skill; compared an ephemeral SQL Server service container with the fallback idempotent-script/static-check option and rejected production Azure SQL for CI validation.
- Output artifacts: `.github/workflows/dotnet-azure.yml`; `docs/ci-sqlserver-migration-gate.md`.
- Verification evidence: Selected `mcr.microsoft.com/mssql/server:2022-latest` service container with no new paid resource and no production SQL connection. `deploy.needs` now requires both `build-test` and `sqlserver-migration`.
- Limitations: Exact provider pricing was not checked because no paid cloud resource or exact cost estimate was introduced; the cost impact is GitHub Actions runner time and container pull time only.

### 2026-06-06 - system-spec-synthesis - RFX-09 implementation contract

- Agent: Codex worker
- Trigger: RFX-09 converts the cross-review finding and GitHub issue into an implementation-ready CI gate and documentation contract.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue brief, `CROSS-REVIEW.md`, `dotnet-azure.yml`, Azure readiness docs, and the EF design-time context path, then wrote the gate specification.
- Output artifacts: `docs/ci-sqlserver-migration-gate.md`; `.github/workflows/dotnet-azure.yml`.
- Verification evidence: The spec records context, goals, non-goals, architecture, data model impact, job contract, error handling, security/privacy, rollout, verification, and open questions.
- Limitations: No separate broad architecture handoff was needed because the scope is a single workflow gate and docs note.

### 2026-06-06 - data-module-review - RFX-09 EF migration process coverage

- Agent: Codex worker
- Trigger: RFX-09 reviews EF Core migration safety and the gap caused by SQLite `EnsureCreated` test fixtures not exercising SQL Server migrations.
- Action: Opened and followed the skill; reviewed `AppDbContextDesignTimeFactory`, `AppDbContext`, EF package setup, migrations location, and the current workflow migration command. No entity, migration, or runtime data-access changes were made.
- Output artifacts: `docs/ci-sqlserver-migration-gate.md`; `.github/workflows/dotnet-azure.yml`.
- Verification evidence: The workflow gate applies the current migration chain to an empty SQL Server database through `ConnectionStrings__DefaultConnection`; the docs note records findings and suggested tests.
- Limitations: Local Docker daemon was unavailable, so the actual SQL Server container run is delegated to GitHub Actions; local EF script generation was used as a compile-time migration check.

### 2026-06-06 - verification-before-completion - RFX-09 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #569 after CI gate implementation.
- Action: Opened and followed the skill; reran proof commands before completion claims. Checked backend tests, frontend typecheck, EF migration script generation, workflow YAML syntax, diff whitespace, banned-term scans, and local Docker availability.
- Output artifacts: Final report for the supervisor; `docs/skill-run-log.md`.
- Verification evidence: `cd backend-dotnet && dotnet test` passed 592/592. `npm run typecheck` passed after `npm ci --cache ./.npm-cache`. `dotnet ef migrations script --idempotent --project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj --startup-project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj --context AppDbContext --output /tmp/rfx09-migrations-infra-final.sql` exited 0 and wrote a 2,142-line script. Workflow YAML parsed successfully with Ruby Psych. `git diff --check` passed. Project and changed-file banned-term scans returned no matches.
- Limitations: Docker client is installed, but `docker info` cannot connect to the local daemon; no local SQL Server container migration update was run. The GitHub Actions job is the first full container-backed execution of the new gate.

### 2026-06-08 - system-spec-synthesis - VER-02 build metadata implementation contract

- Agent: Codex worker
- Trigger: GitHub issue #582 / VER-02 converts the issue body and brief into a scoped Functions package metadata implementation.
- Action: Opened and followed the skill at checklist level; read `AGENTS.md`, `CLAUDE.md`, the issue brief, workflow, Functions startup, project file, version function tests, and readiness docs. Mapped the contract to generated JSON, startup configuration loading, two CI publish sites, and publish/test verification.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj`; `backend-dotnet/src/ReplyInMyVoice.Functions/Program.cs`; `.github/workflows/dotnet-azure.yml`; `backend-dotnet/tests/ReplyInMyVoice.Tests/VersionFunctionTests.cs`; `plans/decisions-log.md`.
- Verification evidence: Publish override wrote `/tmp/ver-pub/version.generated.json` with the expected commit and timestamp. Focused `VersionFunctionTests` passed 3/3, full Release solution tests passed 595/595, and issue grep gates passed.
- Limitations: No separate long-form spec was written because the issue body and repo brief already contain the implementation-ready contract.

### 2026-06-08 - dotnet-backend-testing - VER-02 runtime version metadata proof

- Agent: Codex worker
- Trigger: GitHub issue #582 / VER-02 appends C# xUnit coverage proving `VersionFunction` reads generated JSON configuration values at runtime.
- Action: Opened and followed the skill; chose a focused unit-level test because the behavior is configuration reading without persistence, provider calls, queues, or HTTP host routing. Appended a temp-file JSON loader test without modifying the two existing version tests.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/VersionFunctionTests.cs`.
- Verification evidence: Initial focused run after the appended test passed 3/3, showing issue #581 already made `VersionFunction` read configuration values and that VER-02's remaining gap is startup/publish wiring.
- Limitations: The focused test does not execute the Functions host process; `Program.cs` loader wiring is also checked by grep and publish/package proof commands.

### 2026-06-08 - cloud-architecture-cost-review - VER-02 trigger check

- Agent: Codex worker
- Trigger: GitHub issue #582 touches the Azure Functions CI workflow.
- Action: Opened the skill and reviewed applicability. The selected mechanism keeps the existing Azure Functions package flow, adds no new resource, changes no deployment target, and adds no provider usage or fixed monthly cost.
- Output artifacts: None from this skill beyond this log entry.
- Verification evidence: Readiness docs confirm Azure Functions is the current backend target; no deploy command or resource creation command was run.
- Limitations: Exact provider pricing was not checked because VER-02 introduces no paid resource or cost estimate.

### 2026-06-08 - verification-before-completion - VER-02 final evidence check

- Agent: Codex worker
- Trigger: Preparing final supervised delivery report for GitHub issue #582 after build metadata implementation.
- Action: Opened and followed the skill; ran publish proof, focused version tests, full backend tests, workflow syntax parsing, diff whitespace check, no-hardcoded-SHA scan, and restricted-term scan over changed files before completion claims.
- Output artifacts: Final worker report; `docs/skill-run-log.md`.
- Verification evidence: `dotnet publish backend-dotnet/src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj -c Release -o /tmp/ver-pub /p:CommitSha=deadbeefcafe /p:BuildTimestamp=2026-06-08T00:00:00Z` exited 0 and `/tmp/ver-pub/version.generated.json` contained the override. `dotnet test backend-dotnet/ReplyInMyVoice.sln --configuration Release --filter FullyQualifiedName~VersionFunctionTests` passed 3/3. `dotnet test backend-dotnet/ReplyInMyVoice.sln --configuration Release` passed 595/595.
- Limitations: No live Azure deploy or smoke command was run; the issue explicitly scopes this to local build/package and CI publish wiring.

### 2026-06-08 - resilience-test-generation - HARD-01 API burst rate-limit invariant

- Agent: Codex worker
- Trigger: GitHub issue #584 / HARD-01 adds tests for rate limits, concurrent requests, and rejected-submit no-charge behavior.
- Action: Opened and followed the skill; identified the critical operation as API submit admission, the dependency boundary as SQLite-backed EF persistence, and the invariant as exactly `limit` admitted requests with rejected requests creating no rewrite reservation or used-quota charge. Added deterministic concurrent local tests rather than live endpoint calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiBurstRateLimitTests.cs`; `scripts/load-test/api-burst.mjs`; `plans/rewrite-api-v1/load-test.md`.
- Verification evidence: `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiBurstRateLimitTests` passed 2/2 after fixing a SQLite query-shape issue.
- Limitations: No staging or production load was run; the issue reserves real endpoint load for the owner.

### 2026-06-08 - state-machine-modeling - HARD-01 rate-limit window lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #584 tests the `ApiKeyRateLimitWindow` lifecycle and reset behavior.
- Action: Opened and followed the skill; modeled states as below-limit, at-limit, limited-until-reset, and next-window-open. Events are submit-in-window and submit-in-next-minute. Invariants are one row per key/window, count never exceeds the configured limit, and usage reservations are only created after an admitted submit.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiBurstRateLimitTests.cs`.
- Verification evidence: The reset test proves the first minute reaches count `limit`, one extra same-window submit is limited with no reservation, and the next minute creates a separate window with count 1.
- Limitations: No production state transition or migration was changed; this is regression coverage for the existing limiter.

### 2026-06-08 - data-module-review - HARD-01 limiter and quota persistence

- Agent: Codex worker
- Trigger: GitHub issue #584 touches EF-backed rate-limit windows, usage periods, rewrite attempts, outbox rows, and usage reservations.
- Action: Opened and followed the skill; reviewed `AppDbContext`, `ApiKeyRateLimiter`, `QuotaService`, `ApiKeyRateLimitWindow`, `UsageReservation`, `RewriteAttempt`, and existing rate-limit/quota tests. Kept schema unchanged and added assertions over persisted counters and side effects.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiBurstRateLimitTests.cs`.
- Verification evidence: The burst test asserts one persisted rate-limit window at count `limit`, exactly `limit` rewrite attempts, exactly `limit` usage reservations, exactly `limit` outbox messages, `ReservedCount == limit`, and `UsedCount == 0`.
- Limitations: No migration or index changes were needed; SQLite file-backed fixtures provide local transaction coverage but are not a SQL Server substitute.

### 2026-06-08 - dotnet-backend-testing - HARD-01 EF SQLite burst tests

- Agent: Codex worker
- Trigger: GitHub issue #584 adds C# xUnit backend tests for the API rate limiter and quota reservation invariant.
- Action: Opened and followed the skill; chose a focused EF Core SQLite integration-style test using the real `ApiKeyRateLimiter`, `QuotaService`, and persisted AppDbContext state. Used xUnit and FluentAssertions with the existing test project target framework and no new package references.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiBurstRateLimitTests.cs`.
- Verification evidence: `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiBurstRateLimitTests` passed 2/2, and `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors.
- Limitations: The tests intentionally avoid live HTTP and do not measure worker throughput; the harness runbook covers owner-run staging and production load.

### 2026-06-08 - verification-before-completion - HARD-01 final evidence check

- Agent: Codex worker
- Trigger: Preparing the final supervised delivery report for GitHub issue #584 after adding the burst harness, backend tests, and load-test runbook.
- Action: Opened and followed the skill; reran machine-checkable issue acceptance commands and the broader backend test gate before completion claims.
- Output artifacts: Final worker report; `docs/skill-run-log.md`.
- Verification evidence: `node scripts/load-test/api-burst.mjs --dry-run --url http://example.invalid --key rmv_test_x --concurrency 5 --requests 10` exited 0. `node scripts/load-test/api-burst.mjs --help` exited 0. `node --check scripts/load-test/api-burst.mjs` exited 0. `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiBurstRateLimitTests` passed 2/2. `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors. `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` passed 597/597.
- Limitations: No live staging or production endpoint was called; real load execution remains owner-run as required by HARD-01.
### 2026-06-08 - data-module-review - HARD-02 API key usage anomaly signal

- Agent: Codex worker
- Trigger: GitHub issue #585 / HARD-02 reads existing `ApiKeyUsage` rows and adds usage-count anomaly logic without a new table.
- Action: Opened and followed the skill; reviewed `AppDbContext` indexes, `ApiKeyUsage`, `ApiKeyUsageQueryService`, `ApiKeyService`, API key Functions routes, and the existing SQLite fixture style. Confirmed the query can use the existing `(ApiKeyId, CreatedAt)` index on production providers and needs no EF migration.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyUsageAnomalyService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyUsageAnomalyServiceTests.cs`; `plans/rewrite-api-v1/key-leak-runbook.md`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 40` ran and showed broad existing scan rows; focused anomaly tests passed 3/3 and full backend tests passed 598/598.
- Limitations: No dashboard wiring, EF migration, or live telemetry query was run; HARD-02 scopes this to a structured backend log signal plus runbook.

### 2026-06-08 - dotnet-backend-testing - HARD-02 anomaly service tests

- Agent: Codex worker
- Trigger: GitHub issue #585 / HARD-02 adds C# xUnit coverage for API-key usage-spike classification.
- Action: Opened and followed the skill; used the shared EF Core SQLite fixture and wrote failing tests before production code for spike, steady, and zero-usage cases, including structured log state for the flagged path.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiKeyUsageAnomalyServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyUsageAnomalyService.cs`.
- Verification evidence: Initial focused run failed on missing `ApiKeyUsageAnomalyService`, then `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiKeyUsageAnomalyServiceTests` passed 3/3 after implementation. `dotnet build ReplyInMyVoice.sln -c Release` exited 0. `dotnet test ReplyInMyVoice.sln -c Release` passed 598/598.
- Limitations: Tests do not assert Application Insights ingestion; they assert the service emits structured logger state when the anomaly is flagged.
### 2026-06-08 - dotnet-backend-testing - HARD-03 API input hardening

- Agent: Codex worker
- Trigger: GitHub issue #586 / HARD-03 adds C# xUnit coverage for API malformed input, boundary validation, and error response shape.
- Action: Opened and followed the skill; chose direct Azure Functions tests using the existing SQLite fixture style because the behavior is HTTP validation and response mapping. Added table-driven rewrite input cases, invalid usage `days` cases, and explicit 401/402/409/429 response checks.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiInputHardeningTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiUsageHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiUsageHttpFunctionsTests.cs`.
- Verification evidence: Initial focused run failed on four invalid `days` cases returning 200, then passed 16/16 after the scoped parser update. `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiInputHardeningTests` passed 16/16. Full `dotnet test ReplyInMyVoice.sln -c Release` passed 611/611. `dotnet build ReplyInMyVoice.sln -c Release` exited 0.
- Limitations: No frontend proxy test was added; backend function coverage is sufficient for the issue scope. No deploy, push, or PR command was run.

## 2026-06-08 cloud-architecture-cost-review — Monitoring cost (PostHog + Sentry)

- Skill source: read `agent-skills/cloud-architecture-cost-review/SKILL.md` as fallback (not indexed to the Skill tool this session).
- Trigger: owner asked whether enabling PostHog (analytics) + Sentry (error tracking) for HARD-04 costs money / whether free tiers suffice.
- Usage assumption: replyinmyvoice.com has no real users yet (GA/LAUNCH-01 not opened); events/errors are internal-test volume (hundreds to low-thousands/month).
- Pricing (verified 2026-06 against posthog.com/pricing + sentry.io/pricing): PostHog free = 1M events + 100K exceptions + no monthly fee (pure usage-based); Sentry free Developer = 5K errors + 5M spans + 1 user, permanent; Sentry Team = $26/mo (annual) for multi-user.
- Recommended: both fit the free tier at $0. PostHog alone can cover analytics + error tracking (100K exceptions free = 20x Sentry's 5K, no user cap) -> connect PostHog first, treat Sentry as optional. Connecting keys to the prod Worker is the only owner-gated step (no cost).
- Rejected: Sentry Team $26/mo — unneeded at current scale.
- Limitations: Sentry free caps at 1 user + 5K errors/mo (a noisy bug could exhaust it; spike-protection mitigates); PostHog error tracking is newer than Sentry's. No paid action taken; no key written to prod yet.

## 2026-06-08 system-spec-synthesis — MCP productization spec

- Agent: Claude Code (supervisor)
- Trigger: owner asked to turn the existing rewrite API into a usable MCP service (stdio npm package + remote HTTP server + new `/api/v1/analyze-signal` backend endpoint + `/developers/mcp` page), shipped end-to-end — requires implementation-ready API/job/data contracts before any code.
- Action: Opened and followed the skill. Separated source facts (each with a file path) from explicit [ASSUMPTION]s; produced the full Output Contract (Context … Open Questions) plus a Dynamic Delivery Workflow work breakdown with machine-checkable acceptance per issue.
- Output artifacts: `plans/mcp-productization/REQUIREMENT.md`.
- Verification evidence: cross-references confirmed this session — `ApiKeyAuthResolver.cs`, `QuotaService.cs`, `V1RewriteHttpFunctions.cs:164` (async 202 + Location), `packages/mcp-server` skeleton with uncommitted M9-002 (`plans/codex-exec-M9-002.log`), Entra-lacks-DCR (external research), `wrangler.jsonc` nodejs_compat hosting. No code changed; copy is banned-term-safe (positioning = natural/concise/facts-preserved, never detection).
- Limitations: 5 Open Questions deliberately left for the owner (analyze billing, remote async cap, npm version, mcp path, tone enum) — not silently decided. Hosting compared inline (3 options, all ≈$0); not run as a formal cloud-architecture-cost-review skill. No implementation started, DDW not launched. Remote async cap vs Worker request-duration is an assumption to be proven by MCP-REMOTE tests, not yet measured.

### 2026-06-08 - system-spec-synthesis - REMOTE-595 Streamable HTTP route

- Agent: Codex worker
- Trigger: GitHub issue #595 turns the remote MCP notes and issue brief into an implementation-ready Next route contract for `/api/mcp`.
- Action: Opened and followed the skill at checklist level; read `AGENTS.md`, `CLAUDE.md`, `plans/mcp-productization/issues/REMOTE-streamable-http.md`, `plans/mcp-productization/REQUIREMENT.md`, `app/api/v1/rewrite/route.ts`, `lib/azure-api.ts`, and `packages/mcp-server/src/tools`. Mapped the route to stateless Streamable HTTP, Bearer header auth, existing `/api/v1/rewrite` backend adapter, scoped Origin validation, and local verification gates.
- Output artifacts: `app/api/mcp/route.ts`; `tests/unit/mcp-remote.test.ts`; root `package.json` / `package-lock.json` SDK dependency.
- Verification evidence: Initial focused unit run failed because the route was missing; after implementation `npm run test -- tests/unit/mcp-remote.test.ts` passed 5/5 and `npm run typecheck` exited 0.
- Limitations: No long-form spec document was written because the issue body and repo brief already define the accepted contract. No deploy, push, or PR command was run.

### 2026-06-08 - state-machine-modeling - REMOTE-595 rewrite attempt polling lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #595 requires remote `rewrite_email` to submit an async attempt, poll it, and return a working state plus `attempt_id` when the remote cap is reached.
- Action: Opened and followed the skill; modeled states as submitted, working, succeeded, failed, and remote-cap-working. Events are submit accepted, poll working, poll succeeded, poll failed, and poll cap reached. Invariants are one MCP call maps to one backend submit, terminal success returns rewritten text, backend failure stays an error, and the remote cap returns only the existing attempt id for `get_rewrite_result`.
- Output artifacts: `app/api/mcp/route.ts`; `tests/unit/mcp-remote.test.ts`.
- Verification evidence: The focused remote unit test exercises the cap path with a submitted attempt and repeated working polls, then asserts structured MCP output `{ status: "working", attempt_id: "attempt-remote-1" }`.
- Limitations: The route does not persist state and does not add Durable Objects or sessions; lifecycle state remains in the existing backend attempt.

### 2026-06-08 - resilience-test-generation - REMOTE-595 remote polling and auth guard

- Agent: Codex worker
- Trigger: GitHub issue #595 changes timeout/polling behavior and adds a missing-auth remote route guard.
- Action: Opened and followed the skill; identified the critical operation as remote MCP tool execution over the existing rewrite API and the invariant as no backend call without a valid Bearer header or accepted Origin. Added deterministic route tests for missing auth, cross-origin rejection, shared tool listing, and remote polling cap fallback.
- Output artifacts: `tests/unit/mcp-remote.test.ts`; `app/api/mcp/route.ts`.
- Verification evidence: `npm run test -- tests/unit/mcp-remote.test.ts` passed 5/5. The missing-auth test asserts `401` and `WWW-Authenticate: Bearer`; the Origin guard test asserts no fetch; the polling-cap test uses fake timers and mocked backend responses.
- Limitations: No live MCP client or production API key was used; full remote smoke is left to the supervisor after branch verification.

### 2026-06-08 - ui-browser-testing - FEMCP-597 developer MCP connect page

- Agent: Codex worker
- Trigger: GitHub issue #597 adds a browser-visible `/developers/mcp` page, `/developers` entry link, responsive host config blocks, and source-string pin test updates.
- Action: Opened and followed the project skill; used source-string coverage first, implemented the static page in the existing developer design system, and verified the rendered local HTML path after build gates.
- Output artifacts: `app/developers/mcp/page.tsx`; `app/developers/page.tsx`; `tests/unit/developers-page.test.ts`.
- Verification evidence: `npm run test -- tests/unit/developers-page.test.ts` first failed on the missing `/developers/mcp` page and missing `/developers` link, then passed 3/3 after implementation. `npm run typecheck` exited 0. `npm run lint` exited 0 with one existing warning in `components/account/account-panel.tsx`. `npm run test` passed 64 files / 481 tests. `npm run build` exited 0 and listed `/developers/mcp` as a static route. The required banned-term grep over `app` and `components` printed nothing. A short-lived local Next dev server on port 3002 returned 200 for `/developers/mcp` and the HTML contained the four host names, remote URL, key CTA, 1-credit note, `402`, and top-up copy.
- Limitations: The in-app Browser target `iab` was unavailable in this session, and Playwright Chromium/Chrome-for-Testing could not launch under the macOS sandbox due Mach service permission denial, so desktop/mobile screenshots could not be captured. The first dev server on port 3000 logged `EMFILE` watcher warnings and could not be stopped from the sandbox after stdin closed; the verified local HTML check used a separate short-lived server on port 3002 with polling enabled.
### 2026-06-08 - ui-browser-testing - FEKEYS-598 key manager polish

- Agent: Codex worker
- Trigger: GitHub issue #598 changes the `/developers/keys` UI and adds focused coverage for create, one-time plaintext reveal, masked list rendering, and visible credit balance / pricing CTA.
- Action: Opened and followed the skill; chose CI-friendly unit/component verification because the route is signed-in and the repo has no jsdom/Testing Library dependency. Added a render test using `react-dom/server`, kept backend API routes read-only, and added source pins for the usage summary endpoint plus pricing CTA.
- Output artifacts: `components/developers/api-keys-panel.tsx`; `tests/unit/api-keys-panel.test.ts`; `tests/unit/developer-keys-ui.test.ts`.
- Verification evidence: `npm run test -- tests/unit/api-keys-panel.test.ts` passed 1/1 after the missing-export red run; `npm run test -- tests/unit/developer-keys-ui.test.ts` passed 7/7; `npm run typecheck` exited 0; `npm run test` passed 65 files / 481 tests; `npm run build` exited 0.
- Limitations: No authenticated browser screenshot or Playwright flow was run by this worker; the supervisor can run a signed-in smoke if credentials/session setup are available.

### 2026-06-09 - data-module-review - DDD-12 infrastructure repositories

- Agent: Codex worker
- Trigger: GitHub issue #609 / DDD-12 adds EF Core repository implementations and UnitOfWork over `AppDbContext`.
- Action: Opened and followed the skill; read the DDD-11 Application abstractions, `AppDbContext`, old quota/account/history query paths, entity mappings, and DI registration pattern. Preserved tracked repository queries for UnitOfWork semantics, retained idempotency lookup visibility across soft-deleted attempts, and kept credit selection materialized by user to remain SQLite-compatible.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/*.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories --limit 120` reported only expected quota/idempotency/read-query signals in the new repository methods. Focused repository tests passed 3/3 after implementation.
- Limitations: The new repositories intentionally do not add transactions or retry loops; callers compose them with scoped `AppDbContext` and `IUnitOfWork`, while existing old services remain unchanged.

### 2026-06-09 - dotnet-backend-testing - DDD-12 repository DI and behavior

- Agent: Codex worker
- Trigger: GitHub issue #609 changes C# backend infrastructure and needs Release build/test acceptance.
- Action: Opened and followed the skill; added focused xUnit/FluentAssertions coverage for Application repository DI registrations, shared scoped context persistence through UnitOfWork, soft-delete-aware user lookup, idempotency lookup, and earliest usable rewrite credit selection.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureRepositoryTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj`.
- Verification evidence: Initial focused run failed because `ReplyInMyVoice.Infrastructure.Repositories` was missing. After implementation, `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~InfrastructureRepositoryTests|FullyQualifiedName~AddReplyInMyVoiceInfrastructure_registers_application_repositories"` passed 3/3. `dotnet build ReplyInMyVoice.sln -c Release` exited 0. `dotnet test ReplyInMyVoice.sln -c Release` passed 619/619.
- Limitations: Tests cover repository contracts and DI only; no old `Infrastructure/Services/*`, Functions, Api, or Worker behavior was changed.

### 2026-06-09 - state-machine-modeling - DDD-20 rewrite attempt reservation handlers

- Agent: Codex worker
- Trigger: GitHub issue #610 moves rewrite create/get use cases into Application handlers and changes the rewrite attempt plus usage reservation lifecycle surface.
- Action: Opened and followed the project skill; modeled states as no attempt, pending attempt with pending reservation, existing same-key attempt, conflict same-key attempt, quota blocked, succeeded attempt lookup, and not-found lookup. Events are create command, duplicate create command, conflicting create command, quota exhausted, credit-backed create, and get query by user.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/CreateRewriteAttemptHandler.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/GetRewriteAttemptHandler.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteUseCaseTests.cs`.
- Verification evidence: Used `agent-skills/state-machine-modeling/scripts/state_machine_template.py "Rewrite attempt reservation"` to generate the required transition checklist. Focused tests cover create -> pending reservation/outbox, duplicate -> existing without new side effects, conflict -> no new side effects, quota blocked -> no pending context mutation, credit-backed create, and get -> owner-only success/not-found.
- Limitations: DDD-20 does not switch Functions/API entry points; existing service and job processing lifecycle remain in place for DDD-21 and later issues.

### 2026-06-09 - data-module-review - DDD-20 rewrite create/get persistence

- Agent: Codex worker
- Trigger: GitHub issue #610 changes data access for rewrite attempt creation, usage periods, usage reservations, rewrite credits, and outbox rows.
- Action: Opened and followed the project skill; read the old `RewriteRequestService` create path, `RewriteHttpFunctions` get-by-user path, DDD-11/12 repository contracts, EF entity mappings, and existing quota tests. Added a minimal outbox repository abstraction so the Application handler can keep the old pending job side effect without depending on `AppDbContext`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IOutboxMessageRepository.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/OutboxMessageRepository.cs`; Application rewrite handlers and tests.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 120 backend-dotnet/src` showed expected quota/idempotency signals in the new handler area. A new focused test first failed on a rejected quota path that left an unsaved `UsagePeriod`; after reordering mutations, `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteUseCaseTests` passed 7/7.
- Limitations: No schema or migration changed. The handler uses the current repository and `IUnitOfWork.SaveChangesAsync` surface; it does not add a new transaction/retry abstraction in DDD-20.

### 2026-06-09 - resilience-test-generation - DDD-20 idempotency and quota rejection checks

- Agent: Codex worker
- Trigger: GitHub issue #610 changes idempotent rewrite attempt creation and quota reservation behavior.
- Action: Opened and followed the project skill; generated the local resilience matrix for rewrite attempt reservation and selected SQLite integration tests as the lowest level that proves persistence invariants. Added duplicate request, conflicting request, quota rejection, suspended user, credit-backed quota, and owner-scoped get tests with deterministic clocks and local SQLite.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteUseCaseTests.cs`.
- Verification evidence: `python3 agent-skills/resilience-test-generation/scripts/resilience_matrix.py "Rewrite attempt reservation"` produced timeout, duplicate, partial-success, concurrent request, and malformed-payload rows. The duplicate/conflict tests assert no duplicate attempts, reservations, or outbox messages. The rejected-quota test asserts a later context save cannot persist a partial period.
- Limitations: No live provider, payment, queue, or cloud endpoint was used. Concurrent reservation retries remain owned by the existing service until a later issue expands the Application unit-of-work contract.

### 2026-06-09 - dotnet-backend-testing - DDD-20 rewrite use-case tests

- Agent: Codex worker
- Trigger: GitHub issue #610 adds C# Application handlers and requires SQLite in-memory tests for create/get use cases.
- Action: Opened and followed the project skill; wrote xUnit/FluentAssertions tests first, watched the initial focused run fail because Application Common and Rewrite handler types were missing, then implemented the handlers and repository registration.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteUseCaseTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`.
- Verification evidence: Initial red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteUseCaseTests` failed with missing `Application.Common` and `Application.UseCases.Rewrite` types. Final gates: `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused use-case test passed 7/7; full `dotnet test ReplyInMyVoice.sln -c Release` passed 626/626; handler file existence check passed.
- Limitations: No frontend, deployment, push, or PR command was run.

### 2026-06-09 - data-module-review - DDD-21 Function rewrite shell

- Agent: Codex worker
- Trigger: GitHub issue #611 removes inline EF create/get access from `RewriteHttpFunctions` for the rewrite attempt path.
- Action: Opened and followed the project skill; read the existing Function create/get methods, DDD-20 Application handlers, repository registration, and existing API behavior tests. Confirmed no schema or migration change was needed and the remaining `AppDbContext` usage in the file belongs to out-of-scope history/delete methods.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteHistoryTests.cs`.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs --limit 120` reported no data-risk signals for the updated Function shell. Release build and full backend tests passed.
- Limitations: The old `RewriteRequestService` remains registered and unchanged for later strangler work.

### 2026-06-09 - state-machine-modeling - DDD-21 rewrite attempt shell mapping

- Agent: Codex worker
- Trigger: GitHub issue #611 changes the Function entry point for rewrite attempt creation and lookup, which fronts the rewrite attempt lifecycle.
- Action: Opened and followed the project skill; generated the transition-table skeleton and verified the state model is unchanged: unauthenticated requests reject before persistence, create delegates to the Application handler for pending/existing/succeeded/conflict/quota outcomes, and get delegates owner-scoped lookup to the Application query.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`.
- Verification evidence: `python3 /Users/qc/.codex/skills/state-machine-modeling/scripts/state_machine_template.py "DDD-21 rewrite Function shell"` produced the required state-machine checklist. Existing `RewriteApiTests` passed 32/32, covering validation/auth no-side-effect cases, quota rejection, create/outbox, idempotent retry, conflict, and owner lookup.
- Limitations: DDD-21 changes only Function shelling; it does not add new states, transitions, or persistence rules.

### 2026-06-09 - resilience-test-generation - DDD-21 idempotent Function shell

- Agent: Codex worker
- Trigger: GitHub issue #611 moves the create/get Function path onto Application handlers while preserving idempotency, quota, and no-side-effect behavior.
- Action: Opened and followed the project skill; generated the failure matrix and selected the existing API integration contract as the lowest useful test level because behavior must not change. Reviewed duplicate request, conflicting request, validation/auth rejection, quota rejection, and lookup paths against the unchanged assertions.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteHistoryTests.cs`.
- Verification evidence: `python3 /Users/qc/.codex/skills/resilience-test-generation/scripts/resilience_matrix.py "DDD-21 rewrite Function shell"` produced duplicate, partial-success, concurrent-request, and malformed-payload checklist rows. `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteApiTests` passed 32/32 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 626/626.
- Limitations: No live providers, queue consumers, payment calls, deployment, push, or PR command was run.

### 2026-06-09 - dotnet-backend-testing - DDD-21 RewriteApiTests contract

- Agent: Codex worker
- Trigger: GitHub issue #611 changes C# Azure Functions entry-point code and requires release build plus focused/full .NET test gates.
- Action: Opened and followed the project skill; used existing `RewriteApiTests` as the behavior contract without modifying assertions, updated only the direct `RewriteHistoryTests` Function construction helper required by the constructor injection change, and ran focused tests before the full suite.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteHistoryTests.cs`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` exited 0 with 0 warnings; `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteApiTests` passed 32/32; `dotnet test ReplyInMyVoice.sln -c Release` passed 626/626.
- Limitations: No new test assertions were added because the issue explicitly required preserving the existing behavior contract.
### 2026-06-09 - data-module-review - DDD-01 frontend generated Prisma artifact removal

- Agent: Codex worker
- Trigger: GitHub issue #613 includes removal of frontend generated Prisma artifacts as part of deleting replaced TypeScript rewrite logic.
- Action: Opened and followed the skill at checklist level; confirmed `lib/generated/` and `prisma/` were already absent in this worktree, and limited changes to deleting the listed dead frontend modules/eval scripts plus compile-only import decoupling in retained helper/type files.
- Output artifacts: Deleted dead frontend rewrite/eval modules and their exclusive tests; retained `lib/observability/` and live proxy helpers.
- Verification evidence: `npm run typecheck`, `npm run test`, `npm run build`, `npm run lint`, the DDD-01 dead-code grep, and the banned-term source scan all exited 0. No live imports under `app/` or `components/` were found before deletion.
- Limitations: No Prisma schema, migration, database access service, payment flow, deployment, push, or PR command was changed or run.

### 2026-06-09 - system-spec-synthesis - DDD-40 Account Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #621 and `plans/ddd-restructure/issues/DDD-40-account.md` require converting the existing `AccountService` use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-40 brief, `docs/ddd-migration-playbook.md`, `AccountService.cs`, existing Rewrite handlers, Application abstractions, repositories, and AccountService tests before editing. Scoped goals to add Account handlers/repositories/DTOs only; non-goals were no entry-point switch, no AccountService edit, no schema/migration change, no provider mode or secret wiring change.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Account/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/AccountDtos.cs`; repository abstractions/implementations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/AccountUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the Account use-case namespace and DTO/abstraction surface did not exist. Final release build, focused AccountUseCase tests, full backend tests, file-existence check, diff whitespace check, and touched-file banned-term scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-40 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-40 Account deletion and entitlement lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #621 migrates Account use cases that read subscription/free-credit entitlement state and erase account-owned rewrite/usage/credit/support records.
- Action: Opened and followed the project skill; modeled states as active account, absent account, erased account, paid-entitled account, credit-entitled account, non-entitled account, pending/processing rewrite attempt, finalized/succeeded attempt, pending usage reservation, released reservation, active credit, erased credit, open billing support request, and resolved billing support request. Events are get/create user, find user, summary query, purchase/billing query, API entitlement query, delete command, duplicate delete command, and optional subscription cancellation.
- Output artifacts: `DeleteAccountHandler`, `HasPaidApiEntitlementHandler`, Account projection handlers, and `AccountUseCaseTests`.
- Verification evidence: `python3 /Users/qc/.codex/skills/state-machine-modeling/scripts/state_machine_template.py "DDD-40 account deletion lifecycle"` generated the checklist. Tests cover delete command transitions to erased/canceled user, pending/processing attempt to failed with `account_erased`, reservation to released, credit to erased/zeroed, support request to resolved, duplicate delete with one cancellation call, paid-status entitlement, purchase-credit entitlement, and rejected promo/expired/missing-user entitlement.
- Limitations: Existing entry points still call `AccountService`; the new state transitions are not production-routed until a later strangler issue switches callers.

### 2026-06-09 - data-module-review - DDD-40 Account repository migration

- Agent: Codex worker
- Trigger: GitHub issue #621 changes data access services for AppUser, UsagePeriod, UsageReservation, RewriteAttempt, RewriteCredit, PromoCode, PromoCodeRedemption, StripeInvoice, and BillingSupportRequest through new Application repository interfaces.
- Action: Opened and followed the project skill; read EF entities/mappings, old AccountService queries/mutations, existing repository style, and SQLite tests. Added narrow repository methods only, kept all schema and migration files unchanged, and made `DeleteAccountHandler` commit through `IUnitOfWork.ExecuteInTransactionAsync`.
- Output artifacts: new Account repository abstractions and implementations, `UnitOfWork.ExecuteInTransactionAsync`, `AccountUsagePlanProvider`, and Account handlers/tests.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 80 backend-dotnet/src` completed and reported the expected broad quota/idempotency/read-write signals in the backend tree. `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~AccountUseCaseTests` passed 6/6; full `dotnet test ReplyInMyVoice.sln -c Release` passed 632/632.
- Limitations: The scan is heuristic and broad; no dedicated migration smoke was needed because no schema or migration changed. The existing `AccountService` data path remains in place by design.

### 2026-06-09 - dotnet-backend-testing - DDD-40 AccountUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #621 adds C#/.NET Application handlers and requires SQLite in-memory tests covering all Account handlers.
- Action: Opened and followed the project skill; wrote `AccountUseCaseTests` before production code, watched the focused test fail on missing Account handler/DTO/interface types, then implemented the Application and Infrastructure code. Added DI assertions to `InfrastructureServiceCollectionTests` for the new repositories, handlers, usage-plan provider, and cancellation adapter.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/AccountUseCaseTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~AccountUseCaseTests` failed with missing `ReplyInMyVoice.Application.UseCases.Account` and related types. Final gates: `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused AccountUseCase tests passed 6/6; focused InfrastructureServiceCollection tests passed 13/13; full backend tests passed 632/632.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-42 RewriteJob Application handler contract

- Agent: Codex worker
- Trigger: GitHub issue #623 and `plans/ddd-restructure/issues/DDD-42-rewrite-job.md` require converting the existing rewrite job execution workflow into an implementation-ready Application handler contract across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-42 brief, `docs/ddd-migration-playbook.md`, `RewriteJobProcessor.cs`, existing Rewrite/Quota Application handlers, Application abstractions, repositories, and tests before editing. Scoped goals to add the strangler handler and provider/cost abstractions only; non-goals were no entry-point switch, no old processor edit, no schema/migration change, no provider secret or deployment change.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/RewriteJob/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IRewriteEngineClient.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IRewriteCostLogger.cs`; Infrastructure adapter and cost logger files; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteJobUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the `ReplyInMyVoice.Application.UseCases.RewriteJob` namespace and new abstraction types did not exist. Final release build, focused RewriteJobUseCase tests, full backend tests, file-existence check, diff whitespace check, and changed-file prohibited-term scan passed.
- Limitations: No separate spec document was added because the issue body plus DDD-42 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-42 rewrite job execution lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #623 moves rewrite job execution, including rewrite attempt status and quota reservation transitions, into an Application handler.
- Action: Opened and followed the project skill; modeled states as missing attempt, pending attempt with pending reservation, processing attempt, succeeded/finalized attempt, failed/released attempt, expired terminal attempt, malformed request release, provider failure release, provider timeout release, period-backed quota, and credit-backed quota. Events are process command, terminal redelivery, expiration precheck, malformed request JSON, processing claim, engine success, engine failure result, engine timeout, and engine exception.
- Output artifacts: `ProcessRewriteJobHandler`, `ProcessRewriteJobCommand`, rewrite engine/cost abstractions, and `RewriteJobUseCaseTests`.
- Verification evidence: Tests cover pending -> processing -> succeeded/finalized, pending -> processing -> failed/released on engine failure, and credit-backed pending -> processing -> failed/released with credit refund on engine exception. Existing `RewriteJobProcessorTests` stayed green in the full suite.
- Limitations: The new Application handler is registered but not yet wired to Functions/API/Worker entry points; the old processor remains live for the current runtime path by design.

### 2026-06-09 - data-module-review - DDD-42 rewrite job persistence migration

- Agent: Codex worker
- Trigger: GitHub issue #623 changes data access for rewrite attempt mutation, usage reservation finalization/release, period counters, rewrite credit refunds, and rewrite cost log persistence.
- Action: Opened and followed the project skill; read EF entities/mappings, old `RewriteJobProcessor` and `QuotaService` mutation paths, existing Application quota handlers, repository contracts, and SQLite tests. Added one narrow no-tracking attempt lookup for preclaim snapshots and kept all schema and migration files unchanged.
- Output artifacts: `IRewriteAttemptRepository.GetByIdNoTrackingAsync`; `RewriteAttemptRepository.GetByIdNoTrackingAsync`; `ProcessRewriteJobHandler`; `RewriteCostLogger`; `RewriteJobUseCaseTests`.
- Verification evidence: Focused tests assert final persisted attempt, reservation, period, and credit state after success/failure/error paths. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 644/644.
- Limitations: No migration smoke was needed because no schema or migration changed. The cost logger duplicates the current persistence shape behind the new abstraction so later waves can switch entry points without editing the old processor now.

### 2026-06-09 - resilience-test-generation - DDD-42 provider failure and quota release checks

- Agent: Codex worker
- Trigger: GitHub issue #623 requires handler coverage for engine failure paths and quota-release-on-error behavior.
- Action: Opened and followed the project skill; selected deterministic local fakes over live provider calls. Added tests for valid rewrite success, failed engine result, and thrown engine exception with credit refund, asserting persisted final state instead of only handler return behavior.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteJobUseCaseTests.cs`; `FakeRewriteEngineClient`; `ThrowingRewriteEngineClient`; `FakeRewriteCostLogger`.
- Verification evidence: Red run failed on missing handler/abstraction types. After implementation, `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteJobUseCaseTests` passed 3/3, and full backend tests passed 644/644.
- Limitations: No live OpenAI-compatible provider, writing signal provider, queue consumer, payment provider, cloud endpoint, deployment, push, or PR command was used.

### 2026-06-09 - dotnet-backend-testing - DDD-42 RewriteJobUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #623 adds C#/.NET Application handler tests for rewrite job success, engine failure, and quota release behavior.
- Action: Opened and followed the project skill; wrote `RewriteJobUseCaseTests` before production code, watched the initial focused run fail on missing `ProcessRewriteJobHandler`, command, engine, and cost logger types, then implemented the Application and Infrastructure code. Added DI assertions for the new handler and abstractions.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteJobUseCaseTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`.
- Verification evidence: Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/RewriteJob/ProcessRewriteJobHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused RewriteJobUseCase tests passed 3/3; full backend tests passed 644/644.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, old processor edit, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-43 ApiKey Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #624 and `plans/ddd-restructure/issues/DDD-43-apikey.md` require converting ApiKey service use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-43 brief, `docs/ddd-migration-playbook.md`, `ApiKeyService.cs`, `ApiKeyUsageQueryService.cs`, existing Rewrite handlers, Application abstractions, repositories, and ApiKey tests before editing. Scoped goals to add ApiKey handlers/repositories/DTOs only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret or deployment change.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/ApiKey/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/ApiKeyDtos.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/ApiKeyCredential.cs`; ApiKey repository abstractions/implementations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/ApiKeyUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the ApiKey Application namespace, handlers, and repository types did not exist. Final release build, focused ApiKeyUseCase tests, full backend tests, file-existence check, diff whitespace check, and touched-file restricted-substring scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-43 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-43 ApiKey active, rotated, revoked, and webhook lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #624 migrates ApiKey use cases that change key active/revoked state and webhook state.
- Action: Opened and followed the project skill; modeled states as active key, revoked key, active key with webhook, active key without webhook, missing/non-owned key, and replacement key after rotation. Events are generate command, list query, rotate command, revoke command, set webhook command, clear webhook command, usage summary query, usage series query, and usage recent query.
- Output artifacts: `GenerateApiKeyHandler`, `ListApiKeysHandler`, `RotateApiKeyHandler`, `RevokeApiKeyHandler`, `SetApiKeyWebhookHandler`, `ClearApiKeyWebhookHandler`, usage query handlers, and `ApiKeyUseCaseTests`.
- Verification evidence: Tests cover generate -> active, list with last-30-day counts, set webhook -> active with webhook, clear webhook -> active without webhook, rotate active -> old revoked plus replacement active, non-owner rotate -> null, missing revoke -> false, revoke active -> revoked, and usage query window clamping.
- Limitations: Existing entry points still call the legacy services; the new handlers are registered but not production-routed until a later strangler issue switches callers.

### 2026-06-09 - data-module-review - DDD-43 ApiKey repository migration

- Agent: Codex worker
- Trigger: GitHub issue #624 changes data access services for ApiKeys and ApiKeyUsages through new Application repository interfaces.
- Action: Opened and followed the project skill; read EF entities/mappings, legacy ApiKey service queries/mutations, existing repository style, and SQLite tests. Added narrow repository methods only, kept all schema and migration files unchanged, kept write paths on `IUnitOfWork`, and kept read-only usage queries without `IUnitOfWork`.
- Output artifacts: `IApiKeyRepository`; `IApiKeyUsageRepository`; `ApiKeyRepository`; `ApiKeyUsageRepository`; ApiKey handlers/tests.
- Verification evidence: Focused tests assert final persisted ApiKey and ApiKeyUsage-derived state after generate/list/webhook/rotate/revoke and usage query paths. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 646/646.
- Limitations: No migration smoke was needed because no schema or migration changed. The legacy ApiKey service data path remains in place by design.

### 2026-06-09 - dotnet-backend-testing - DDD-43 ApiKeyUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #624 adds C#/.NET Application handler tests for ApiKey CRUD, webhook, and usage query behavior.
- Action: Opened and followed the project skill; wrote `ApiKeyUseCaseTests` before production code, watched the initial focused run fail on missing ApiKey Application handler/repository types, then implemented the Application and Infrastructure code with SQLite in-memory coverage.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/ApiKeyUseCaseTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiKeyUseCaseTests` failed with missing `ReplyInMyVoice.Application.UseCases.ApiKey` and related types. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/ApiKey/GenerateApiKeyHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused ApiKeyUseCase tests passed 2/2; full backend tests passed 646/646.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-44 Promo Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #625 and `plans/ddd-restructure/issues/DDD-44-promo.md` require converting promo redeem/status use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-44 brief, `docs/ddd-migration-playbook.md`, `PromoService.cs`, existing Rewrite handlers, Application abstractions, repositories, and promo tests before editing. Scoped goals to add the strangler Application handlers only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret or deployment change.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Promo/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/PromoRedeemResultDto.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/PromoStatusDto.cs`; promo repository interface extensions and implementations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/PromoUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because `ReplyInMyVoice.Application.UseCases.Promo`, `RedeemPromoHandler`, and `GetPromoStatusHandler` did not exist. Final release build, focused PromoUseCase tests, full backend tests, handler file-existence check, diff whitespace check, and banned-substring scans passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-44 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-44 promo redemption lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #625 migrates promo redemption/status behavior with validity, cap, already-redeemed, IP-velocity, credit grant, and redemption record states.
- Action: Opened and followed the project skill; modeled states as missing/invalid code, inactive/future code, expired code, redeemable code under cap, cap-reached code, user already redeemed for code, IP-velocity blocked request, applied redemption, active promo credit, and promo status eligible/not eligible. Events are redeem command, status query, code normalization failure, IP count check, atomic count increment success/miss, redemption insert, and duplicate redemption race.
- Output artifacts: `RedeemPromoHandler`, `GetPromoStatusHandler`, promo DTOs, repository extensions, and `PromoUseCaseTests`.
- Verification evidence: Tests cover success, cap reached, already redeemed, expired, IP-velocity blocked, status before redeem, and status after redeem. Full backend tests passed 652/652.
- Limitations: The new handlers are registered but not wired to Functions/API/Worker entry points; the old `PromoService` remains live by design.

### 2026-06-09 - data-module-review - DDD-44 promo persistence migration

- Agent: Codex worker
- Trigger: GitHub issue #625 changes data access for promo code lookup, redemption records, IP velocity counts, atomic promo count increment, and promo credit grants.
- Action: Opened and followed the project skill; read EF promo entities/mappings, legacy `PromoService` persistence flow, existing repository style, and SQLite tests. Added narrow repository methods only, kept all schema and migration files unchanged, kept the raw SQL atomic increment inside Infrastructure, and kept Application handlers free of `AppDbContext`.
- Output artifacts: `IPromoCodeRepository.GetByIdAsync`, `GetByCodeAsync`, `TryIncrementRedemptionCountAsync`; `IPromoCodeRedemptionRepository` add/query/count methods; `IRewriteCreditRepository.AddAsync`; matching repository implementations; `PromoUseCaseTests`.
- Verification evidence: Focused tests assert final persisted promo count, rewrite credit, and redemption state for success, cap reached, already redeemed, expired, and IP-velocity blocked paths. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 652/652.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing `PromoService` persistence code was not edited.

### 2026-06-09 - resilience-test-generation - DDD-44 promo race and velocity checks

- Agent: Codex worker
- Trigger: GitHub issue #625 requires preserving retry/optimistic-concurrency semantics and IP velocity defense while moving promo redemption into Application handlers.
- Action: Opened and followed the project skill; selected deterministic SQLite in-memory tests for handler behavior and final state assertions. The handler uses `IUnitOfWork.ExecuteInTransactionAsync` with a bounded retry count and the repository-backed atomic increment to preserve cap correctness.
- Output artifacts: `RedeemPromoHandler`; promo repository extensions; `PromoUseCaseTests`.
- Verification evidence: Red run failed on missing handler namespace/types. After implementation, focused PromoUseCase tests passed 6/6 and full backend tests passed 652/652. Existing `PromoService` and promo concurrency tests remained in the full suite.
- Limitations: No live provider, payment, queue, cloud endpoint, deployment, push, PR, or production smoke command was used.

### 2026-06-09 - dotnet-backend-testing - DDD-44 PromoUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #625 adds C#/.NET Application handler tests for promo redeem and status behavior.
- Action: Opened and followed the project skill; wrote `PromoUseCaseTests` before production code, watched the initial focused run fail on missing Promo Application namespace and handlers, then implemented Application and Infrastructure code with SQLite in-memory coverage.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/PromoUseCaseTests.cs`.
- Verification evidence: Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Promo/RedeemPromoHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused PromoUseCase tests passed 6/6; full backend tests passed 652/652.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-45 PromoAdmin Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #626 and `plans/ddd-restructure/issues/DDD-45-promo-admin.md` require converting PromoAdmin service use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-45 brief, `docs/ddd-migration-playbook.md`, `PromoAdminService.cs`, Rewrite handler templates, Application abstractions, repositories, and admin promo tests before editing. Scoped goals to add the strangler Application handlers only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret, and no deployment change.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/PromoAdmin/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/AdminPromoDtos.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IPromoAdminRepository.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/PromoAdminRepository.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/PromoAdminUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the PromoAdmin Application namespace, handlers, and repository types did not exist. Final release build, focused PromoAdminUseCase tests, full backend tests, handler file-existence check, diff whitespace check, and changed-file safety substring scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-45 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-45 promo admin lifecycle mutations

- Agent: Codex worker
- Trigger: GitHub issue #626 migrates promo admin use cases that create, update, enable, disable, archive, and restore promo-code state.
- Action: Opened and followed the project skill; modeled promo-code states as missing, active, disabled, archived, pending, expired, and exhausted, derived from `ArchivedAt`, `IsActive`, validity window, and global redemption count. Events are create command, update command, set-active command, archive command, restore command, list query, and detail query.
- Output artifacts: `CreatePromoCodeHandler`, `UpdatePromoCodeHandler`, `SetPromoCodeActiveHandler`, `ArchivePromoCodeHandler`, `RestorePromoCodeHandler`, list/detail handlers, and `PromoAdminUseCaseTests`.
- Verification evidence: Tests cover create -> active with audit, duplicate create rejection, update with audit, update missing id -> not found, ordered status projection, detail stats for applied redemptions, and detail missing id -> null. Full backend tests passed 659/659.
- Limitations: Existing Functions/API/Worker entry points still use the legacy service; the new handlers are registered but not production-routed until a later strangler issue switches callers.

### 2026-06-09 - data-module-review - DDD-45 promo admin repository migration

- Agent: Codex worker
- Trigger: GitHub issue #626 changes data access for promo admin list/detail queries, promo-code mutations, and admin audit append through new Application repository interfaces.
- Action: Opened and followed the project skill; read EF promo/admin-audit entities and mappings, legacy `PromoAdminService` persistence flow, existing repository style, and SQLite tests. Added a narrow `IPromoAdminRepository`, kept all schema and migration files unchanged, kept write paths on `IUnitOfWork`, preserved unique-code handling, and kept handlers free of `AppDbContext`.
- Output artifacts: `IPromoAdminRepository`; `PromoAdminRepository`; Application PromoAdmin handlers; `AdminPromoDtos`; `PromoAdminUseCaseTests`.
- Verification evidence: Focused tests assert persisted promo rows and admin audit rows for create/update, no side effects for duplicate create and missing update, and detail stats from redemption/credit rows. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 659/659.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing `PromoAdminService` persistence code was not edited.

### 2026-06-09 - dotnet-backend-testing - DDD-45 PromoAdminUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #626 adds C#/.NET Application handler tests for PromoAdmin create, update, list, and detail behavior.
- Action: Opened and followed the project skill; wrote `PromoAdminUseCaseTests` before production code, watched the initial focused run fail on missing PromoAdmin Application namespace, handlers, and repository types, then implemented Application and Infrastructure code with SQLite in-memory coverage.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/PromoAdminUseCaseTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~PromoAdminUseCaseTests` failed with missing `ReplyInMyVoice.Application.UseCases.PromoAdmin` and related types. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/PromoAdmin/CreatePromoCodeHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused PromoAdminUseCase tests passed 7/7; full backend tests passed 659/659.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-46 Billing Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #627 and `plans/ddd-restructure/issues/DDD-46-billing.md` require converting Billing use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-46 brief, `docs/ddd-migration-playbook.md`, `StripeBillingService.cs`, `TaxTurnoverService.cs`, Rewrite handler templates, Application abstractions, repositories, and billing tests before editing. Scoped goals to add the strangler Application handlers only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret, no deployment change, and no live payment action.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Billing/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/BillingDtos.cs`; Application billing abstractions; Infrastructure adapters/registrations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/BillingUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the Billing Application namespace, handlers, and abstractions did not exist. Final release build, focused BillingUseCase tests, full backend tests, handler file-existence check, diff whitespace check, and touched-path restricted-substring scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-46 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-46 billing subscription and payment paths

- Agent: Codex worker
- Trigger: GitHub issue #627 migrates billing use cases that read subscription/customer state, cancel subscriptions, issue refunds, list paid provider payments, and compute tax turnover warnings.
- Action: Opened and followed the project skill; modeled states as missing user, user without customer, user with customer, user without subscription, user with active subscription id, local payment missing, local payment present, provider payment listed, turnover below warning threshold, and turnover at or above warning threshold. Events are create checkout command, create portal query, cancel command, refund command, paid-payment list query, and turnover report query.
- Output artifacts: Billing Application command/query handlers, billing DTOs, Stripe client abstraction, turnover notifier/settings abstractions, repository read extensions, and `BillingUseCaseTests`.
- Verification evidence: Tests cover checkout user creation, portal customer use, subscription cancellation, no-subscription no-op, refund success, refund missing-payment not found, provider paid-payment listing, and turnover warning notification. Full backend tests passed 667/667.
- Limitations: Existing Functions/API/Worker entry points still use the legacy services; the new handlers are registered but not production-routed until a later strangler issue switches callers.

### 2026-06-09 - data-module-review - DDD-46 billing repository reads

- Agent: Codex worker
- Trigger: GitHub issue #627 changes data access for AppUser checkout/portal/cancel reads, payment-intent refund validation, and rolling turnover report purchase-credit reads.
- Action: Opened and followed the project skill; read EF entities/mappings, legacy billing and turnover persistence behavior, existing repository style, and SQLite tests. Added narrow `IRewriteCreditRepository` read methods only, kept all schema and migration files unchanged, kept handlers free of `AppDbContext`, and preserved the old turnover service's SQLite-safe in-memory `DateTimeOffset` window filtering shape.
- Output artifacts: `IRewriteCreditRepository.GetByUserIdAndPaymentIntentIdAsync`; `IRewriteCreditRepository.ListPurchaseCreditsForTurnoverAsync`; matching `RewriteCreditRepository` implementations; Billing handlers/tests.
- Verification evidence: Focused tests assert no provider refund call when local payment is missing, correct provider refund request for an existing local payment, and turnover totals from local purchase credits. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 667/667.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing billing service persistence code was not edited.

### 2026-06-09 - dotnet-backend-testing - DDD-46 BillingUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #627 adds C#/.NET Application handler tests for Billing checkout, portal, cancel, refund, paid-payment list, and tax turnover behavior.
- Action: Opened and followed the project skill; wrote `BillingUseCaseTests` before production code, watched the initial focused run fail on missing Billing Application namespace, handlers, and abstractions, then implemented Application and Infrastructure code with SQLite in-memory coverage and deterministic fakes.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/BillingUseCaseTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~BillingUseCaseTests` failed with missing `ReplyInMyVoice.Application.UseCases.Billing` and related types. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Billing/CreateCheckoutSessionHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused BillingUseCase tests passed 8/8; full backend tests passed 667/667.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-47 Stripe reconciliation Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #628 and `plans/ddd-restructure/issues/DDD-47-stripe-reconciliation.md` require converting the Stripe reconciliation use case into an Application handler across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-47 brief, `docs/ddd-migration-playbook.md`, `StripeReconciliationService.cs`, existing Rewrite/Billing handler templates, Application abstractions, repositories, and reconciliation tests before editing. Scoped goals to add the strangler Application handler only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret, no deployment change, and no live payment action.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeReconciliation/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/StripeReconciliationDtos.cs`; Application reconciliation abstractions; Infrastructure repository/adapters/registrations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeReconciliationUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the StripeReconciliation Application namespace, handler, DTOs, and abstractions did not exist. Final release build, focused StripeReconciliationUseCase tests, full backend tests, handler file-existence check, diff whitespace check, and touched-file restricted-substring scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-47 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - data-module-review - DDD-47 payment grant snapshot repository

- Agent: Codex worker
- Trigger: GitHub issue #628 changes data access for reconciliation-time purchase grant snapshot loading through a new Application repository interface.
- Action: Opened and followed the project skill; read EF `RewriteCredit` usage, legacy reconciliation grant-loading logic, existing repository style, and SQLite test conventions. Added a narrow read-only `IPaymentGrantRepository`, kept all schema and migration files unchanged, preserved the old SQLite-safe in-memory `DateTimeOffset` filtering shape, and kept the Application handler free of `AppDbContext`.
- Output artifacts: `IPaymentGrantRepository`; `PaymentGrantRepository`; Stripe reconciliation Application handler and tests.
- Verification evidence: Focused tests assert the handler requests normalized payment intent ids, performs no unit-of-work save, and produces no alert for a clean match. Full backend tests passed 671/671.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing `StripeReconciliationService` persistence code was not edited.

### 2026-06-09 - dotnet-backend-testing - DDD-47 StripeReconciliationUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #628 adds C#/.NET Application handler tests for Stripe reconciliation discrepancy behavior.
- Action: Opened and followed the project skill; wrote `StripeReconciliationUseCaseTests` before production code, watched the initial focused run fail on missing StripeReconciliation Application namespace, handler, DTOs, and abstractions, then implemented Application and Infrastructure code with deterministic fakes for the Stripe reconciliation client and alerter.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeReconciliationUseCaseTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~StripeReconciliationUseCaseTests` failed with missing reconciliation Application types. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeReconciliation/ReconcileStripeHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused StripeReconciliationUseCase tests passed 4/4; full backend tests passed 671/671.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-48 Stripe event Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #629 and `plans/ddd-restructure/issues/DDD-48-stripe-event.md` require converting Stripe event use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-48 brief, `docs/ddd-migration-playbook.md`, `StripeEventService.cs`, existing Rewrite/Billing handler templates, Application abstractions, repositories, and Stripe event tests before editing. Scoped goals to add strangler Application handlers only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret, no deployment change, and no live payment action.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeEvent/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/StripeWebhookPayloadDto.cs`; Stripe event Application abstractions; Infrastructure repository/notifier adapters/registrations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeEventUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the StripeEvent Application namespace, handlers, notifier abstraction, and repository type did not exist. Final release build, focused StripeEventUseCase tests, full backend tests, handler file-existence check, and touched-file restricted-substring scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-48 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-48 Stripe event and payment-grace lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #629 migrates Stripe event idempotency, webhook dispatch, subscription/payment-grace transitions, invoice upsert, credit grant/revocation, and grace batch use cases.
- Action: Opened and followed the project skill; modeled `StripeEvent` states as `Processing`, `Failed`, and `Processed`; AppUser payment-grace states as active paid access, past-due grace, recovered active, expired inactive, and terminal subscription states; events are try-mark-processed, begin processing, duplicate/in-flight replay, failed sync, checkout completed, subscription update/delete, invoice failed/succeeded/paid/finalized, charge refund/dispute, grace expiry, and grace reminder.
- Output artifacts: Application StripeEvent command/handler set, `IStripeEventRepository`, `IStripeEventNotifier`, repository extensions, and StripeEvent use-case tests.
- Verification evidence: Tests cover duplicate TryMark, checkout replay, invoice failed transition into grace, subscription active transition, a two-row grace-expiry batch drain, and a reminder batch ordering regression. Full backend tests passed 677/677.
- Limitations: Entry points still call the legacy service until a later strangler issue switches callers; no production routing changed.

### 2026-06-09 - data-module-review - DDD-48 Stripe event persistence and repositories

- Agent: Codex worker
- Trigger: GitHub issue #629 changes data access for StripeEvents, AppUsers, RewriteCredits, and StripeInvoices through Application repository interfaces.
- Action: Opened and followed the project skill; read EF mappings, unique keys, row-version concurrency tokens, legacy Stripe event persistence flow, existing repository style, and SQLite test conventions. Added narrow repository methods for event lock rows, Stripe user lookup, grace batches, checkout credit lookup, refund credit lookup, and invoice upsert. Kept all schema and migration files unchanged and kept handlers free of `AppDbContext`.
- Output artifacts: `IStripeEventRepository`; `StripeEventRepository`; extensions to `IAppUserRepository`, `IRewriteCreditRepository`, and `IStripeInvoiceRepository`; matching Infrastructure implementations.
- Verification evidence: Focused tests assert one processed event row for TryMark, one purchase credit for checkout replay, persisted invoice upsert on failed invoice, state changes for batched grace expiry, and due reminder selection before batch limiting. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 677/677.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing `StripeEventService` persistence code was not edited.

### 2026-06-09 - resilience-test-generation - DDD-48 Stripe event replay and post-commit side effects

- Agent: Codex worker
- Trigger: GitHub issue #629 touches Stripe webhook replay, idempotency locking, transactional state sync, and post-commit notification/cancellation side effects.
- Action: Opened and followed the project skill; built the failure matrix around duplicate event, in-flight event, missing checkout user retry, invoice grace transition, post-commit notifier fake, and batch processing. Implemented deterministic SQLite tests and hand-written fakes rather than live provider calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeEventUseCaseTests.cs`; post-commit side-effect abstractions and fakes.
- Verification evidence: Red run failed on missing Application types; final focused test command passed 6/6, including duplicate TryMark, checkout replay, invoice failed grace entry, subscription entitlement sync, bounded grace-expiry batch drain, and reminder due-row selection. Full backend tests passed 677/677.
- Limitations: No live Stripe endpoint, notification provider, billing portal, or cancellation provider was called; side-effect behavior is proven through deterministic fakes and Infrastructure adapter registration.

### 2026-06-09 - dotnet-backend-testing - DDD-48 StripeEventUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #629 adds C#/.NET Application handler tests for Stripe event idempotency, webhook dispatch, invoice/subscription paths, and a batch handler.
- Action: Opened and followed the project skill; wrote `StripeEventUseCaseTests` before production code, watched the initial focused run fail on missing StripeEvent Application namespace, handlers, DTOs, notifier abstraction, and repository types, then implemented Application and Infrastructure code with SQLite in-memory coverage and deterministic fakes for post-commit work.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/StripeEventUseCaseTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~StripeEventUseCaseTests` failed with missing StripeEvent Application types; an added reminder ordering regression then failed with processed count 0 before the repository batch filter was corrected. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/StripeEvent/ProcessStripeWebhookHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused StripeEventUseCase tests passed 6/6; full backend tests passed 677/677.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-49 Admin Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #630 and `plans/ddd-restructure/issues/DDD-49-admin.md` require converting primary Admin use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-49 brief, `docs/ddd-migration-playbook.md`, `AdminService.cs`, existing Rewrite/PromoAdmin handler templates, Application abstractions, repositories, and Admin tests before editing. Scoped goals to the primary handlers only: user list, user detail, stats, grant credits, and delete user. Non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret, no deployment change, and no live payment action.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Admin/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/AdminDtos.cs`; `IAdminUserRepository`; `IAdminStatsRepository`; Infrastructure admin repositories/registrations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/AdminUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the Admin Application namespace, handlers, repositories, DTOs, and abstractions did not exist. Final release build, focused AdminUseCase tests, full backend tests, handler file-existence check, diff whitespace check, and touched-file restricted-substring scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-49 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-49 Admin credit grant and user erase transitions

- Agent: Codex worker
- Trigger: GitHub issue #630 migrates Admin credit grant and user deletion mutations into Application handlers with explicit success, forbidden, and not-found outcomes.
- Action: Opened and followed the project skill; modeled credit grant as target-user-exists to admin credit available plus audit row, and delete as active user to erased user plus audit row. Illegal transitions are missing user, already erased user, and admin self-delete. The delete transition preserves related-table cleanup for attempts, usage periods, reservations, credits, promo redemptions, and billing support requests.
- Output artifacts: `GrantCreditsHandler`; `DeleteAdminUserHandler`; `AdminUserRepository.EraseUserAsync`; `AdminUseCaseTests` cases for grant success/not-found and delete success/forbidden/not-found.
- Verification evidence: Focused AdminUseCase tests passed 15/15 and full backend tests passed 685/685.
- Limitations: Entry points still call the legacy service until a later strangler issue switches callers; no production routing changed.

### 2026-06-09 - data-module-review - DDD-49 Admin repositories and persistence invariants

- Agent: Codex worker
- Trigger: GitHub issue #630 changes EF data access for Admin projections and mutations through new Application repository interfaces.
- Action: Opened and followed the project skill; read EF mappings, row-version concurrency tokens, legacy Admin query/mutation logic, existing repository style, and SQLite test conventions. Added narrow `IAdminUserRepository` and `IAdminStatsRepository` abstractions, kept all schema and migration files unchanged, preserved SQLite-safe in-memory ordering/filtering where DateTimeOffset translation matters, and kept handlers free of `AppDbContext`.
- Output artifacts: `AdminUserRepository`; `AdminStatsRepository`; Application Admin DTOs and repository interfaces; Admin handler tests.
- Verification evidence: Data risk scan ran against `backend-dotnet/src` and reported broad existing risk signals; no new schema or migration files were added. Focused tests assert credit plus audit write, delete erase plus audit write, no side effects on not-found/forbidden paths, and aggregate read projections. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 685/685.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing `AdminService` persistence code was not edited.

### 2026-06-09 - dotnet-backend-testing - DDD-49 AdminUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #630 adds C#/.NET Application handler tests for Admin user list pagination/filtering, user detail, stats, grant credits, and delete user behavior.
- Action: Opened and followed the project skill; wrote `AdminUseCaseTests` before production code, watched the initial focused run fail on missing Admin Application namespace, handlers, DTOs, repositories, and abstractions, then implemented Application and Infrastructure code with SQLite in-memory coverage and deterministic provider fakes.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/AdminUseCaseTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~AdminUseCaseTests` failed with missing Admin Application types. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Admin/GrantCreditsHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused AdminUseCase tests passed 15/15; full backend tests passed 685/685.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-50 BillingSupport Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #631 and `plans/ddd-restructure/issues/DDD-50-billing-support.md` require converting BillingSupport create/list use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-50 brief, `docs/ddd-migration-playbook.md`, `BillingSupportService.cs`, existing Rewrite handler templates, Application abstractions, repositories, and BillingSupport-related tests before editing. Scoped goals to add strangler Application handlers only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret, no deployment change, and no live payment action.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/BillingSupport/*`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/BillingSupportRequestResultDto.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Common/BillingSupportRequestResponseDto.cs`; `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/IBillingSupportRepository.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/BillingSupportRepository.cs`; handler/repository registrations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/BillingSupportUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the BillingSupport Application namespace and handlers did not exist. Final release build, focused BillingSupportUseCase tests, full backend tests, handler file-existence check, Application layering scan, touched-file restricted-substring scan, and standard frontend copy guard passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-50 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-50 BillingSupport request status guard

- Agent: Codex worker
- Trigger: GitHub issue #631 requires preserving the duplicate-open-request guard while creating BillingSupport requests.
- Action: Opened and followed the project skill; modeled `BillingSupportRequest` states as `Open` and `Resolved`. Event list: create request, reject create while an open request exists, list by user, and admin resolve in the existing legacy path. Allowed transition for this issue is no request or resolved-only history to new `Open`; illegal transition is creating another `Open` request for the same user. Persistence implication: the guard and insert run inside `IUnitOfWork.ExecuteInTransactionAsync` with serializable isolation and no schema change.
- Output artifacts: `CreateBillingSupportRequestHandler`; `IBillingSupportRepository.HasOpenRequestForUserAsync`; `BillingSupportRepository.HasOpenRequestForUserAsync`; duplicate-open SQLite test.
- Verification evidence: `CreateBillingSupportRequestAsync_rejects_duplicate_open_request_without_side_effects` passed and asserted `InvalidRequest` plus one persisted request. Focused BillingSupportUseCase tests passed 4/4 and full backend tests passed 689/689.
- Limitations: No new database uniqueness constraint or migration was added because the issue explicitly prohibited schema changes; entry points still call the legacy service until a later strangler issue switches callers.

### 2026-06-09 - data-module-review - DDD-50 BillingSupport repository and persistence invariants

- Agent: Codex worker
- Trigger: GitHub issue #631 changes EF data access for BillingSupport create/list through a new Application repository interface.
- Action: Opened and followed the project skill; read EF mappings, existing BillingSupport entity/status/type enums, existing account deletion repository dependency, legacy BillingSupport persistence flow, and SQLite test conventions. Added a narrow `IBillingSupportRepository` and matching `BillingSupportRepository`, kept the existing `IBillingSupportRequestRepository` untouched for account deletion, kept all schema and migration files unchanged, and kept handlers free of `AppDbContext`.
- Output artifacts: `IBillingSupportRepository`; `BillingSupportRepository`; Application BillingSupport DTOs/handlers; BillingSupport handler tests; DI registration assertions.
- Verification evidence: Data risk scan ran against `backend-dotnet/src` and reported broad existing risk signals; no new schema or migration files were added. Focused tests assert successful create, duplicate-open no-side-effect path, empty list, and user-scoped newest-first list. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 689/689.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing `BillingSupportService` persistence code was not edited.

### 2026-06-09 - dotnet-backend-testing - DDD-50 BillingSupportUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #631 adds C#/.NET Application handler tests for BillingSupport create/list behavior.
- Action: Opened and followed the project skill; wrote `BillingSupportUseCaseTests` before production code, watched the initial focused run fail on missing BillingSupport Application namespace and handlers, then implemented Application and Infrastructure code with SQLite in-memory coverage. Updated the existing Infrastructure service-collection smoke test to assert the new repository and handlers are registered.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/BillingSupportUseCaseTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/InfrastructureServiceCollectionTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~BillingSupportUseCaseTests` failed with missing BillingSupport Application types. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/BillingSupport/CreateBillingSupportRequestHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused BillingSupportUseCase tests passed 4/4; full backend tests passed 689/689.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - system-spec-synthesis - DDD-51 WebhookOutbox Application use-case contract

- Agent: Codex worker
- Trigger: GitHub issue #632 and `plans/ddd-restructure/issues/DDD-51-webhook-outbox.md` require converting WebhookOutbox timer batch use cases into Application handlers across Application, Infrastructure, and tests.
- Action: Opened and followed the project skill at implementation-contract level; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-51 brief, `docs/ddd-migration-playbook.md`, the legacy webhook/outbox dispatcher services, Rewrite handler templates, Application abstractions, repositories, and existing dispatcher tests before editing. Scoped goals to add strangler Application handlers only; non-goals were no entry-point switch, no legacy service edit, no schema/migration change, no provider secret, no deployment change, and no live payment action.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/WebhookOutbox/*`; `IWebhookDeliveryRepository`; Application webhook sender and outbox message handler abstractions; Infrastructure repository/adapters/registrations; `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs`.
- Verification evidence: Initial focused red test failed because the WebhookOutbox Application namespace, handlers, sender/router abstractions, and repository types did not exist. Final release build, focused WebhookOutboxUseCase tests, full backend tests, handler file-existence check, and touched-file restricted-substring scan passed.
- Limitations: No separate spec document was added because the GitHub issue plus DDD-51 brief were already the authoritative implementation spec, and the delivery wave asked to stay strictly inside issue scope.

### 2026-06-09 - state-machine-modeling - DDD-51 webhook delivery and outbox message dispatch lifecycle

- Agent: Codex worker
- Trigger: GitHub issue #632 migrates webhook delivery and outbox message batch lifecycle handling with claim, dispatch, retry, and terminal failure transitions.
- Action: Opened and followed the project skill; modeled `WebhookDelivery` states as `Pending`, `InProgress`, `Delivered`, and `Failed`, and `OutboxMessage` states as `Pending`, `Processing`, `Sent`, and `Failed`. Events are claim due row, successful external dispatch, failed external dispatch, retry delay elapsed, max attempts reached, and duplicate/concurrent timer claim.
- Output artifacts: `DispatchDueWebhooksHandler`; `DispatchDueOutboxHandler`; claim/mark repository methods; SQLite tests for success, failed-attempt back-off, and concurrent claim idempotency for both lifecycles.
- Verification evidence: Focused WebhookOutboxUseCase tests passed 6/6 and full backend tests passed 695/695.
- Limitations: Entry points still call the legacy dispatcher services until a later strangler issue switches callers; no production routing changed.

### 2026-06-09 - data-module-review - DDD-51 WebhookOutbox repositories and persistence invariants

- Agent: Codex worker
- Trigger: GitHub issue #632 changes EF data access for `WebhookDeliveries` and `OutboxMessages` through Application repository interfaces.
- Action: Opened and followed the project skill; read EF mappings, row-version concurrency tokens, existing indexes, legacy transaction/claim code, repository style, and SQLite DateTimeOffset handling. Added narrow claim/mark repository methods, preserved serializable claim transactions through `IUnitOfWork`, kept all schema and migration files unchanged, and kept handlers free of `AppDbContext`.
- Output artifacts: `IWebhookDeliveryRepository`; `WebhookDeliveryRepository`; extensions to `IOutboxMessageRepository`; `OutboxMessageRepository` claim/mark methods; widened retryable transaction race detection in `UnitOfWork`.
- Verification evidence: Focused tests assert claim writes lock owner/lease, success clears lock and marks terminal success, failure clears lock and schedules retry with the existing two-second first delay, and a second handler cannot dispatch an already claimed row. `dotnet build ReplyInMyVoice.sln -c Release` exited 0 and full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No migration smoke was needed because no schema or migration changed. Existing `WebhookDispatcherService` and `OutboxDispatcherService` files were not edited.

### 2026-06-09 - resilience-test-generation - DDD-51 dispatcher retry and concurrent claim coverage

- Agent: Codex worker
- Trigger: GitHub issue #632 touches retry back-off, provider failures, queue publish failures, and concurrent timer claim behavior.
- Action: Opened and followed the project skill; built the failure matrix around HTTP non-2xx, outbox handler exception, max-attempt terminalization through existing repository rules, duplicate timer claim while first dispatch is still running, and SQLite-backed persistence. Implemented deterministic fakes for webhook sending and outbox message handling rather than live HTTP, queue, or cloud calls.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs`; `IWebhookDeliverySender`; `IOutboxMessageHandler`; Infrastructure adapter and concrete outbox message handler.
- Verification evidence: Red run failed on missing Application types; final focused test command passed 6/6, including webhook success/failure/concurrent claim and outbox success/failure/concurrent claim. Full backend tests passed 695/695.
- Limitations: No live webhook endpoint, Service Bus, Azure worker, or deployment command was used; resilience behavior is proven through deterministic fakes and SQLite state assertions.

### 2026-06-09 - dotnet-backend-testing - DDD-51 WebhookOutboxUseCaseTests

- Agent: Codex worker
- Trigger: GitHub issue #632 adds C#/.NET Application handler tests for WebhookOutbox claim, dispatch, back-off, and concurrency behavior.
- Action: Opened and followed the project skill; wrote `WebhookOutboxUseCaseTests` before production code, watched the initial focused run fail on missing WebhookOutbox Application namespace, handlers, sender/router abstractions, and repository types, then implemented Application and Infrastructure code with SQLite in-memory coverage and deterministic fakes.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs`.
- Verification evidence: Red run: `dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~WebhookOutboxUseCaseTests` failed with missing WebhookOutbox Application types. Final gates: `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/WebhookOutbox/DispatchDueWebhooksHandler.cs` exited 0; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused WebhookOutboxUseCase tests passed 6/6; full backend tests passed 695/695; touched-file restricted-substring scan returned no matches.
- Limitations: Git commit was attempted but blocked by sandbox permissions because the worktree git metadata lives outside the writable root; no push, PR, deploy, entry-point switch, legacy service edit, schema change, migration, new package, or live payment command was run.

### 2026-06-09 - data-module-review - DDD-60 account and promo Function strangler shell

- Agent: Codex worker
- Trigger: GitHub issue #645 switches consumer-facing Account and Promo HTTP Functions from legacy Infrastructure service calls to Application handlers that own account, billing-support, and promo persistence paths.
- Action: Opened and followed the project skill; read the DDD-60 issue and brief, `AGENTS.md`, `CLAUDE.md`, the target Function classes, Application Account/BillingSupport/Promo handlers from the integration branch, legacy service behavior, API tests, and handler tests. Kept schema/migrations unchanged, kept old service classes and DI registration intact, and preserved HTTP response record shapes while routing mutations and queries through Application handlers.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AccountHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoHttpFunctions.cs`; updated direct `AccountHttpFunctions` test construction in `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; focused `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~AccountServiceTests|FullyQualifiedName~PromoApiTests|FullyQualifiedName~PromoServiceTests"` passed 42/42; full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695; changed-file restricted-substring scan returned no matches.
- Limitations: No schema, migration, old service deletion, deployment, push, PR, or live payment action was performed. Billing-support notification sending remains in the Function wrapper to preserve existing API behavior because the Application create handler only persists the request.

### 2026-06-09 - state-machine-modeling - DDD-62 billing and Stripe webhook Function shell

- Agent: Codex worker
- Trigger: GitHub issue #647 switches billing checkout, billing portal, and Stripe webhook HTTP Function entry points to Application handlers while preserving response contracts.
- Action: Opened and followed the project skill; modeled checkout states as unauthenticated, invalid SKU, rate-limited, delegated, success, configuration failure, and provider failure; portal states as unauthenticated, delegated, missing customer, success, and provider failure; Stripe event states as absent, processing, processed, and failed. Events are authenticated checkout request, SKU validation, checkout velocity check, portal request, verified webhook request, duplicate event, sync success, and sync failure. Allowed transitions keep pre-handler validation in the Function and move only delegated billing/webhook work to Application handlers. Illegal transitions are handler invocation before auth/SKU/rate-limit success, duplicate event reprocessing after processed status, and persistence mutation on rejected webhook input. Persistence implications: no schema or migration changed; existing Application repositories and serializable Stripe event transaction path own `AppUsers`, `StripeEvents`, `RewriteCredits`, and `StripeInvoices`.
- Output artifacts: `BillingHttpFunctions`; `StripeWebhookFunction`; raw Stripe event to Application DTO adapter; updated API test helpers.
- Verification evidence: Release build passed; focused billing/webhook/service filter passed 49/49; full backend suite passed 695/695.
- Limitations: Existing old services and DI registration were intentionally retained for later cleanup. No new lifecycle states or database constraints were added.

### 2026-06-09 - data-module-review - DDD-62 billing and Stripe webhook persistence path

- Agent: Codex worker
- Trigger: GitHub issue #647 changes the HTTP shell persistence path for checkout user upsert, portal customer lookup, Stripe event idempotency, invoice sync, and rewrite-credit grants by routing through Application handlers.
- Action: Opened and followed the project skill; read function shells, Application billing and Stripe event handlers, old Infrastructure services, repository registrations, EF-backed API/service tests, and DI setup. Confirmed owned data remains in existing repositories and handlers; no `AppDbContext` query was added to the migrated Function methods; old services and registrations stayed in place.
- Output artifacts: `BillingHttpFunctions` now calls `CreateCheckoutSessionHandler` and `CreatePortalSessionHandler`; `StripeWebhookFunction` now calls `ProcessStripeWebhookHandler`; test fakes register the Application billing client boundary; webhook direct-function tests construct the Application handler with SQLite repositories.
- Verification evidence: `git diff --check` exited 0; changed-file restricted-substring scan returned no matches; `dotnet build ReplyInMyVoice.sln -c Release` exited 0; focused filter passed 49/49; full backend tests passed 695/695.
- Limitations: The raw Stripe event adapter is necessary because the merged Application handler accepts a parsed DTO while the Function receives provider JSON. No schema, migration, new package, deployment, push, PR, or live payment action was performed.

### 2026-06-09 - resilience-test-generation - DDD-62 billing and Stripe webhook failure preservation

- Agent: Codex worker
- Trigger: GitHub issue #647 touches checkout provider failures, checkout velocity limits, Stripe webhook replay/idempotency, invalid webhook input, and failed sync recovery behavior.
- Action: Opened and followed the project skill; reviewed failure matrix rows for checkout timeout, missing checkout config, unknown SKU, rate limit, portal missing customer, missing webhook signature, invalid signed payload, duplicate event, malformed unsigned JSON, and webhook sync failure. Reused existing deterministic fakes and SQLite-backed assertions because the issue required behavior unchanged and existing assertions unmodified.
- Output artifacts: Function shell changes plus test helper wiring that keeps the focused API/service tests exercising the same failure paths through Application handlers.
- Verification evidence: Focused filter passed 49/49, covering billing API/service, webhook API, and Stripe event service behavior; full backend suite passed 695/695.
- Limitations: No live provider, cloud, queue, deployment, or payment command was used. No new resilience assertions were added because this issue was a strangler shell swap with unchanged behavior.

### 2026-06-09 - dotnet-backend-testing - DDD-62 focused billing and webhook gates

- Agent: Codex worker
- Trigger: GitHub issue #647 requires Release build plus focused and full .NET test gates for billing and Stripe webhook behavior.
- Action: Opened and followed the project skill; selected existing service and API tests as the lowest test level that proves response status, provider failure mapping, duplicate webhook handling, and persisted state. Updated only test construction/fake DI helpers needed by the new handler constructor path; no test assertions were changed.
- Output artifacts: Updated `StripeBillingApiTests` fake to implement the Application billing client interface; updated `StripeWebhookApiTests` function helper to construct `ProcessStripeWebhookHandler`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings; focused filter passed 49/49; full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new test cases were added because the issue acceptance explicitly required unchanged behavior and existing assertions unmodified.

### 2026-06-09 - state-machine-modeling - DDD-63 admin Function shell lifecycle preservation

- Agent: Codex worker
- Trigger: GitHub issue #648 switches admin and promo-admin HTTP shell call paths for stateful user deletion, credit grant, promo active/archive/restore, and deferred admin service lifecycles.
- Action: Opened and followed the project skill; treated this as a shell-preservation model rather than a new lifecycle design. Existing allowed transitions remain: unauthorized requests reject before mutation; user delete moves eligible users to erased/canceled with audit; credit grant creates an admin credit plus audit; promo create starts active; promo update changes validated fields; promo disable/enable toggles active state; promo archive sets archived and inactive; promo restore clears archive while leaving active state unchanged. Deferred billing-support, accounting CSV, suspension, and refund transitions remain on `AdminService`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; handler command/query calls; DTO-to-legacy-response adapters; deferred TODO markers.
- Verification evidence: `python3 /Users/qc/.codex/skills/state-machine-modeling/scripts/state_machine_template.py "DDD-63 admin Function shell"` generated the lifecycle checklist; focused admin test filter passed 20/20; full backend suite passed 695/695.
- Limitations: No new states, transition helper, enum, schema, migration, or lifecycle tests were added because this issue required unchanged behavior and existing assertions unmodified.

### 2026-06-09 - data-module-review - DDD-63 admin Function persistence path

- Agent: Codex worker
- Trigger: GitHub issue #648 changes the admin Function data-access route from inline legacy services to Application handlers and repositories while leaving five deferred service paths in place.
- Action: Opened and followed the project skill; reviewed the Function shell, Application Admin and PromoAdmin handlers, repository registration, legacy services, existing admin/promo/refund tests, and DI setup. Kept schema and migrations unchanged, kept legacy admin services registered, registered the refund client adapter, and preserved HTTP response record shapes through explicit adapters.
- Output artifacts: `AdminHttpFunctions.cs`; `ServiceCollectionExtensions.cs`; `AdminHttpFunctionsTestFactory.cs`; updated admin test construction helpers.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs --limit 80` returned no risk rows; the same scan on `ServiceCollectionExtensions.cs` returned no risk rows; `git diff --check` exited 0; changed-diff restricted-substring scan returned no matches; Release build and both test gates passed.
- Limitations: The five explicitly deferred AdminService use-cases remain legacy service calls with TODO markers. No schema, migration, old service deletion, deployment, push, PR, or live payment action was performed.

### 2026-06-09 - dotnet-backend-testing - DDD-63 admin shell regression gates

- Agent: Codex worker
- Trigger: GitHub issue #648 requires Release build, focused admin tests, and full .NET backend tests after changing `AdminHttpFunctions`.
- Action: Opened and followed the project skill; used existing xUnit/FluentAssertions admin tests as characterization coverage because the issue required behavior unchanged and assertions unmodified. Added a test-only factory to construct the new Function handler graph, and updated test setup only.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminHttpFunctionsTestFactory.cs`; updated admin test construction calls.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings; focused admin filter passed 20/20; full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new test cases were added because the issue was a strangler shell replacement with unchanged response contracts.

### 2026-06-09 - system-spec-synthesis - DDD-64 rewrite Function shell

- Agent: Codex worker
- Trigger: GitHub issue #649 / DDD-64 requires an implementation-ready handler shell plan across `RewriteHttpFunctions`, `V1RewriteHttpFunctions`, Application rewrite/account handlers, and backend regression gates.
- Action: Opened and followed the project skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, the DDD-64 brief, target Function files, requested Application handlers via `git show origin/delivery/ddd-restructure`, and relevant tests. Generated the spec skeleton with `agent-skills/system-spec-synthesis/scripts/spec_outline.py DDD-64-rewrite-shell`, then applied the plan in-place rather than writing a separate spec artifact.
- Output artifacts: Handler-shell changes in `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs` and `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`.
- Verification evidence: Spec skeleton command exited 0; Release build passed; focused rewrite/V1 tests passed 40/40; full backend suite passed 695/695.
- Limitations: No architecture, API contract, schema, migration, deployment, or old-service cleanup was added beyond the issue scope.

### 2026-06-09 - state-machine-modeling - DDD-64 rewrite attempt lifecycle preservation

- Agent: Codex worker
- Trigger: DDD-64 routes V1 rewrite creation/result and rewrite history lookups through Application handlers that touch `RewriteAttempt`, usage reservation, quota, idempotency, and sandbox attempt lifecycle paths.
- Action: Opened and followed the project skill; generated the lifecycle checklist with `agent-skills/state-machine-modeling/scripts/state_machine_template.py RewriteAttemptUsageReservation`. Preserved the existing states: pending/processing/succeeded/failed/expired for attempts and pending/finalized/released/expired for reservations. Preserved existing events: submit, repeated idempotency key, provider success/failure, reservation expiry, result poll, history detail, and soft delete. Illegal transitions remain unchanged: cross-user read, mixed sandbox/live result read, quota side effects on rejected input, duplicate reservation on repeated idempotency key, and mutation after result polling.
- Output artifacts: `RewriteHttpFunctions` now uses `FindUserHandler` and `GetRewriteAttemptHandler` where handlers exist; `V1RewriteHttpFunctions` now uses entitlement/create/get/account-summary handlers while unsupported sandbox, usage-write, list, delete, and internal-user lookup paths remain marked with DDD-64 TODOs.
- Verification evidence: Lifecycle template command exited 0; focused rewrite/V1 tests passed 40/40; full backend suite passed 695/695.
- Limitations: No new states, transition helper, enum, schema, migration, or new lifecycle assertions were added because DDD-64 requires unchanged behavior.

### 2026-06-09 - data-module-review - DDD-64 rewrite persistence path

- Agent: Codex worker
- Trigger: DDD-64 changes data-access routing for rewrite history, V1 entitlement, V1 attempt creation, V1 result lookup, and V1 usage summary.
- Action: Opened and followed the project skill; reviewed owned entities (`AppUser`, `RewriteAttempt`, `UsagePeriod`, `UsageReservation`, `RewriteCredit`, `ApiKeyUsage`), existing repositories/handlers, old service behavior, and tests together. Ran `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 40 backend-dotnet/src/ReplyInMyVoice.Functions` and kept schema/migrations unchanged. Expanded `RewriteAttemptDto` through `FromAttempt` so handler-backed result/detail responses preserve existing HTTP view fields and sandbox/live checks.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Application/Common/RewriteAttemptDto.cs`; `RewriteHttpFunctions`; `V1RewriteHttpFunctions`; direct test helper wiring in `RewriteHistoryTests` and `ApiInputHardeningTests`.
- Verification evidence: Data risk scan exited 0 and reported existing risk signals for review; `git diff --check` exited 0; changed-file restricted-substring scan returned no matches; Release build passed; focused tests passed 40/40; full backend tests passed 695/695.
- Limitations: Inline DB remains where no Application handler exists yet: rewrite history list/delete, V1 internal-user lookup, sandbox attempt creation, and API usage write helper. Each remaining path is marked with a DDD-64 TODO.

### 2026-06-09 - resilience-test-generation - DDD-64 V1 idempotency, rate limit, and quota gates

- Agent: Codex worker
- Trigger: DDD-64 changes V1 submit/result/usage code paths that are covered by idempotency, rate-limit, quota, provider-failure, and sandbox/live regression tests.
- Action: Opened and followed the project skill; generated the failure matrix with `agent-skills/resilience-test-generation/scripts/resilience_matrix.py V1RewriteSubmit`. Reused existing deterministic SQLite/fake-provider tests because the issue explicitly requires unchanged behavior and unmodified assertions. Verified duplicate idempotency keys, different-draft conflict, quota exhaustion, usable purchase credit, provider failure no-charge path, rate limiter unavailable, concurrent rate-limit behavior, and sandbox/live result separation through the focused filter.
- Output artifacts: Handler-shell code and test helper construction only; no test assertions changed.
- Verification evidence: Resilience matrix command exited 0; focused filter passed 40/40; full backend suite passed 695/695.
- Limitations: No live provider, payment, cloud, deploy, or network dependency was used. No new resilience tests were added because existing acceptance coverage already exercised the required unchanged behavior.

### 2026-06-09 - dotnet-backend-testing - DDD-64 rewrite shell regression gates

- Agent: Codex worker
- Trigger: DDD-64 requires Release build, focused rewrite/V1 tests, and full .NET backend tests after changing C# Azure Function shells and direct test helper construction.
- Action: Opened and followed the project skill; selected existing xUnit/FluentAssertions API/service tests as characterization coverage because response contracts must remain unchanged. Updated only test construction helpers to supply the new Application handlers; no assertions were modified.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteHistoryTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiInputHardeningTests.cs`.
- Verification evidence: Initial Release build failed on `ApiInputHardeningTests.cs` direct V1 constructor wiring, root cause was constructor signature drift; after updating the helper, `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings. Focused `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~RewriteApiTests|FullyQualifiedName~RewriteHistoryTests|FullyQualifiedName~V1RewriteRateLimitTests|FullyQualifiedName~RewriteRequestServiceTests"` passed 40/40. Full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new tests were added because this is a strangler shell refactor with unchanged behavior and the issue forbids assertion changes.

### 2026-06-09 - state-machine-modeling - DDD-65 timer shell lifecycle preservation

- Agent: Codex worker
- Trigger: GitHub issue #650 switches timer and queue Function shells for outbox dispatch, webhook dispatch, payment grace, Stripe reconciliation, and rewrite job processing to Application handlers.
- Action: Opened and followed the project skill; treated this as lifecycle preservation. State list remains existing persisted states for outbox messages, webhook deliveries, app user payment grace, reconciliation reports, rewrite attempts, and usage reservations. Events remain due timer tick, payment-grace timer tick, reconciliation timer tick, and Service Bus rewrite message. Transition table is unchanged: due pending work is claimed and completed or failed-attempted; expired grace downgrades only eligible users; reconciliation reports the same one-day window; valid rewrite messages process by attempt id. Invariants and illegal transitions remain with the handlers: no duplicate finalization, no mutation for invalid queue payloads, no schema change, and no old service deletion.
- Output artifacts: `OutboxDispatcherTimerFunction.cs`, `WebhookDispatcherTimerFunction.cs`, `PaymentGraceExpiryFunction.cs`, `StripeReconciliationTimerFunction.cs`, and `RewriteJobFunction.cs` now call Application commands/handlers while preserving host inputs and logs.
- Verification evidence: `python3 /Users/qc/.codex/skills/state-machine-modeling/scripts/state_machine_template.py DDD-65-timer-shell` exited 0; Release build passed; focused timer/job-related test filter passed 47/47; full backend suite passed 695/695.
- Limitations: No new states, transition helpers, enums, schema, migrations, or lifecycle assertions were added because the issue requires unchanged behavior and existing assertions unmodified.

### 2026-06-09 - data-module-review - DDD-65 timer shell persistence path

- Agent: Codex worker
- Trigger: GitHub issue #650 changes data-access routing for timer and queue entry points from legacy services to already-registered Application handlers/repositories.
- Action: Opened and followed the project skill; reviewed Function shells, Application handler APIs, DI registrations, legacy service registrations, and existing persistence tests together. Findings: no schema or migration required; old services remain registered; constructor service parameters were removed only where no method in the Function file still used them; handler command inputs preserve timestamp, worker name, batch size, and attempt id.
- Output artifacts: Handler-shell changes in the five scoped Function files only.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 80 backend-dotnet/src/ReplyInMyVoice.Functions/Functions` exited 0 and returned existing broad risk signals for review; `git diff --check` exited 0; touched-file restricted-substring scan returned no matches; Release build and both test gates passed.
- Limitations: The data-risk scan is text-signal based and reports unrelated existing Function risks. No data model, EF migration, service cleanup, deployment, push, PR, or live payment action was performed.

### 2026-06-09 - dotnet-backend-testing - DDD-65 timer shell regression gates

- Agent: Codex worker
- Trigger: DDD-65 requires Release build, focused timer/job-related service tests, and the full .NET backend test suite after changing Azure Function shells.
- Action: Opened and followed the project skill; selected the existing xUnit/FluentAssertions service tests named by the issue as characterization coverage for unchanged behavior. No test files or assertions were changed.
- Output artifacts: No test artifacts changed; production Function shell edits only.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors. Focused `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~OutboxDispatcherTests|FullyQualifiedName~StripeEventServiceTests|FullyQualifiedName~StripeReconciliationServiceTests|FullyQualifiedName~RewriteJobProcessorTests"` passed 47/47. Full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new tests were added because this is a strangler shell replacement with unchanged behavior and the issue requires existing assertions unmodified.

### 2026-06-09 - state-machine-modeling - DDD-66 Worker BackgroundService shell preservation

- Agent: Codex worker
- Trigger: GitHub issue #651 changes Worker BackgroundService entry points for rewrite job processing, outbox dispatch, and expired quota reservation cleanup.
- Action: Opened and followed the project skill; modeled this as lifecycle preservation rather than a new transition design. State list remains existing persisted states: rewrite attempts (`Pending`, `Processing`, `Succeeded`, `Failed`, `Expired`), usage reservations (`Pending`, `Finalized`, `Released`, `Expired`), and outbox messages (`Pending`, `Processing`, `Sent`, `Failed`). Event list remains Service Bus rewrite message, outbox loop tick, and cleanup loop tick. Transition table remains owned by the Application handlers: valid rewrite messages process by attempt id; due outbox messages are claimed and then sent or failed-attempted; expired pending reservations are released from reserved quota and marked expired. Invariants remain no duplicate finalization, no quota side effect for invalid queue payloads, no old service deletion, and no schema change. Illegal transitions remain terminal attempt overwrite, duplicate usage charge, duplicate job enqueue, and mutation from malformed worker input.
- Output artifacts: `ServiceBusRewriteWorker.cs`, `OutboxDispatcherWorker.cs`, and `ExpiredReservationCleanupWorker.cs` now call the Wave-2 Application handlers from their existing per-loop scopes.
- Verification evidence: `python3 agent-skills/state-machine-modeling/scripts/state_machine_template.py DDD-66-worker-shell` exited 0; Release build passed; focused worker-related filter passed 34/34; full backend suite passed 695/695.
- Limitations: No new states, transition helper, enum, schema, migration, or lifecycle assertions were added because the issue requires unchanged behavior and existing assertions unmodified.

### 2026-06-09 - data-module-review - DDD-66 Worker persistence path

- Agent: Codex worker
- Trigger: DDD-66 changes data-access routing for Worker background loops from legacy infrastructure services to already-registered Application handlers/repositories.
- Action: Opened and followed the project skill; reviewed the Worker files, Application handler APIs, DI registrations, old service registrations, and existing persistence tests together. Findings: no P1/P2 data issue found; schema and migrations remain unchanged; old services remain registered; per-loop scopes remain intact; handler command inputs preserve attempt id, current timestamp, worker instance id, and batch size.
- Output artifacts: Handler-shell changes in the three Worker files only.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80 backend-dotnet/src/ReplyInMyVoice.Worker` exited 0 and returned expected review signals for outbox/quota paths; `git diff --check` exited 0; changed-diff restricted-substring scan returned no matches; Release build and both test gates passed.
- Limitations: The data-risk scan is text-signal based and reports review prompts, not failures. No data model, EF migration, service cleanup, deployment, push, PR, or live payment action was performed.

### 2026-06-09 - resilience-test-generation - DDD-66 worker queue/outbox/quota failure preservation

- Agent: Codex worker
- Trigger: DDD-66 touches Service Bus rewrite processing, outbox dispatch loop routing, quota reservation cleanup, and existing redelivery/failure coverage while requiring behavior unchanged.
- Action: Opened and followed the project skill; reviewed the failure matrix for timeout, transient provider/cloud failure, permanent failure, duplicate queue delivery, partial persisted state, concurrent quota mutation, and malformed worker payload. Reused existing deterministic tests because the issue forbids changing assertions and the Application handlers already own the failure behavior.
- Output artifacts: Worker shell changes only; no test artifacts changed.
- Verification evidence: `python3 agent-skills/resilience-test-generation/scripts/resilience_matrix.py DDD-66-worker-shell` exited 0; focused worker-related filter passed 34/34; full backend suite passed 695/695.
- Limitations: No live provider, cloud, queue, deployment, or payment command was used. No new resilience assertions were added because this issue is a strangler shell swap with unchanged behavior.

### 2026-06-09 - dotnet-backend-testing - DDD-66 Worker shell regression gates

- Agent: Codex worker
- Trigger: DDD-66 requires Release build, focused rewrite job/outbox/quota tests, and full .NET backend tests after changing C# Worker BackgroundServices.
- Action: Opened and followed the project skill; selected the existing xUnit/FluentAssertions tests named by the issue as characterization coverage for unchanged behavior. No test files or assertions were changed.
- Output artifacts: No test artifacts changed; production Worker shell edits only.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors. Focused `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~RewriteJobProcessorTests|FullyQualifiedName~OutboxDispatcherTests|FullyQualifiedName~QuotaServiceTests"` passed 34/34. Full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new tests were added because this is a strangler shell replacement with unchanged behavior and the issue requires existing assertions unmodified.

### 2026-06-09 - state-machine-modeling - DDD-67 API rewrite/account shell preservation

- Agent: Codex worker
- Trigger: GitHub issue #652 changes API route entry points for account summary, purchase history, billing history, rewrite attempt creation, rewrite attempt lookup, V1 submit/result, and V1 usage while preserving usage and rewrite-attempt lifecycle behavior.
- Action: Opened and followed the project skill; treated this as lifecycle preservation. State list remains existing persisted states: rewrite attempts (`Pending`, `Processing`, `Succeeded`, `Failed`, `Expired`) and usage reservations (`Pending`, `Finalized`, `Released`, `Expired`). Events remain signed-in account lookup, rewrite submit, repeated idempotency key, V1 live/test submit, V1 result poll, V1 usage lookup, and cross-user lookup rejection. Transition table remains owned by Application handlers where available: create attempt reserves quota and enqueues outbox, get attempt reads only the caller-owned attempt, entitlement checks preserve paid or usable purchase-credit access, and test-key sandbox attempts remain inline with explicit TODO markers.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` route lambdas now call Application handlers for the DDD-67 endpoint set.
- Verification evidence: `python3 agent-skills/state-machine-modeling/scripts/state_machine_template.py DDD-67-api-shell` exited 0; Release build passed; focused issue filter passed 57/57; full backend suite passed 695/695.
- Limitations: No new states, transition helper, enum, schema, migration, or lifecycle assertions were added because DDD-67 requires unchanged behavior and existing assertions unmodified.

### 2026-06-09 - data-module-review - DDD-67 API persistence path

- Agent: Codex worker
- Trigger: DDD-67 changes data-access routing in `ReplyInMyVoice.Api/Program.cs` for account, usage, entitlement, and rewrite attempt paths from old services or inline EF reads to already-registered Application handlers.
- Action: Opened and followed the project skill; reviewed `Program.cs`, Application handler APIs, repositories, old service behavior, DI registration, and existing API/service tests together. Findings: no schema or migration required; old services remain registered; promo/Stripe routes remain out of scope; V1 internal-user lookup, API key auth, sandbox attempt creation, and API usage write stay inline and are marked with DDD-67/68 TODOs.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` only.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80 backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` exited 0 and returned no risk rows; `git diff --check` exited 0; touched-file restricted-substring scan returned no matches; Release build and both test gates passed.
- Limitations: The API shell still keeps direct V1 helper database access where the issue explicitly deferred it. No data model, EF migration, service cleanup, deployment, push, PR, or live payment action was performed.

### 2026-06-09 - dotnet-backend-testing - DDD-67 API shell regression gates

- Agent: Codex worker
- Trigger: DDD-67 requires Release build, focused API/account/rewrite tests, and full .NET backend tests after changing ASP.NET Core Minimal API route lambdas.
- Action: Opened and followed the project skill; selected existing xUnit/FluentAssertions ASP.NET Core API and service tests as characterization coverage for unchanged response contracts and persistence side effects. No test files or assertions were changed.
- Output artifacts: No test artifacts changed; production API shell edit only.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors. Focused `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~RewriteApiTests|FullyQualifiedName~RewriteRequestServiceTests|FullyQualifiedName~AccountServiceTests|FullyQualifiedName~V1RewriteRateLimitTests"` passed 57/57. Full `dotnet test ReplyInMyVoice.sln -c Release` passed 695/695.
- Limitations: No new tests were added because this is a strangler shell replacement with unchanged behavior and the issue requires existing assertions unmodified.

### 2026-06-09 - dotnet-backend-testing - CLEAN-02 static helper relocation gates

- Agent: Codex worker
- Trigger: CLEAN-02 changes C# helper test references and requires focused and full .NET test gates after moving static helpers.
- Action: Opened and followed the project skill; used the existing xUnit/FluentAssertions tests as behavior locks, repointed helper assertions to the new static homes first, and verified the expected missing-helper compile failure before adding production helpers.
- Output artifacts: Test references in account, quota, Stripe event, admin deletion, API key, API key HTTP, API key auth, and API usage HTTP tests now exercise the new helper homes.
- Verification evidence: Initial focused test command failed with missing `AccountUsagePlans`, `ExternalAuthUserId`, `ApiKeyHashing`, and `ApiKeyWebhookUrl` names before production helpers existed. `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings and 0 errors. Focused CLEAN-02 filter passed 70/70. Isolated `V1RewriteRateLimitTests` passed 4/4, and the broader API-key-pepper group passed 85/85.
- Limitations: Full `dotnet test ReplyInMyVoice.sln -c Release` repeatedly failed one out-of-scope V1 concurrent quota reservation test with `DbUpdateConcurrencyException` from `CreateRewriteAttemptHandler`; the final full run passed 695/696. CLEAN-02 did not change that handler because this issue is limited to static-helper relocation. No schema, migration, deployment, push, PR, or payment action was performed.

### 2026-06-09 - state-machine-modeling - CLEAN-02 gate repair rewrite reservation race

- Agent: Codex worker
- Trigger: The verifier failed the full .NET gate on concurrent V1 rewrite submission while creating pending rewrite attempts and quota reservations.
- Action: Opened and followed the project skill; modeled the lifecycle as unchanged states (`Pending` rewrite attempts, `Pending` usage reservations, pending outbox messages) and unchanged events (new idempotency key, repeated idempotency key, conflicting idempotency key, concurrent distinct V1 submissions). Allowed transitions remain create pending attempt plus one pending reservation and one outbox row, return existing for duplicate same request, return conflict for duplicate different request, and return quota-exceeded after retry when the latest reserved count reaches quota.
- Output artifacts: `CreateRewriteAttemptHandler` now runs the existing mutation logic through the retrying serializable unit-of-work path.
- Verification evidence: Exact failing V1 test passed 1/1; `V1RewriteRateLimitTests` passed 4/4; full `dotnet test ReplyInMyVoice.sln -c Release` passed 696/696.
- Limitations: No new statuses, enums, schema, migration, deployment, push, PR, or payment action was performed.

### 2026-06-09 - data-module-review - CLEAN-02 gate repair quota persistence race

- Agent: Codex worker
- Trigger: The verifier failure was an EF Core optimistic concurrency exception while saving `UsagePeriod`, `RewriteAttempt`, `UsageReservation`, and `OutboxMessage` changes.
- Action: Opened and followed the project skill; reviewed the failing stack, `CreateRewriteAttemptHandler`, `IUnitOfWork`/`UnitOfWork`, repositories, existing `ReserveQuotaHandler` retry pattern, and the V1 concurrency test. Finding: the Application rewrite-attempt creation path had the same quota reservation write shape as the retrying quota handler but did not use the retrying transaction overload.
- Output artifacts: `CreateRewriteAttemptHandler` now uses `IUnitOfWork.ExecuteInTransactionAsync(..., IsolationLevel.Serializable, maxAttempts: 3, ...)`.
- Verification evidence: Exact failing V1 test passed 1/1; focused CLEAN-02 filter passed 70/70; full `dotnet test ReplyInMyVoice.sln -c Release` passed 696/696.
- Limitations: No schema, migration, data backfill, repository interface change, deployment, push, PR, or payment action was performed.

### 2026-06-09 - resilience-test-generation - CLEAN-02 gate repair concurrent request recovery

- Agent: Codex worker
- Trigger: The failed gate covered concurrent V1 requests, rate limiting, failed API usage writes, and quota reservation recovery.
- Action: Opened and followed the project skill; used the existing WebApplicationFactory/SQLite V1 concurrency test as the resilience test because it already asserts the failure matrix row for concurrent requests plus partial API usage write failure. The fix retries only retryable transaction races and preserves final state assertions.
- Output artifacts: Production retry wrapper in `CreateRewriteAttemptHandler`; no test assertions changed.
- Verification evidence: Exact failing V1 test passed 1/1; `V1RewriteRateLimitTests` passed 4/4; full `dotnet test ReplyInMyVoice.sln -c Release` passed 696/696.
- Limitations: No live provider, cloud, queue, deployment, push, PR, or payment action was performed.

### 2026-06-09 - dotnet-backend-testing - CLEAN-02 gate repair full suite

- Agent: Codex worker
- Trigger: Supervisor verification failed `dotnet test` for the backend solution after CLEAN-02.
- Action: Opened and followed the project skill; kept the existing xUnit/FluentAssertions/WebApplicationFactory test as the red test, made the minimal production retry change, then ran the exact failing test, the containing class, the CLEAN-02 focused filter, and the full solution suite.
- Output artifacts: `CreateRewriteAttemptHandler.cs` now wraps rewrite attempt creation in the existing retrying unit-of-work transaction.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; exact failing V1 test passed 1/1; `V1RewriteRateLimitTests` passed 4/4; focused CLEAN-02 filter passed 70/70; full `dotnet test ReplyInMyVoice.sln -c Release` passed 696/696.
- Limitations: No tests were disabled, skipped, deleted, or weakened. No deployment, push, PR, or payment action was performed.

### 2026-06-09 - system-spec-synthesis - CLEAN-03 admin use-case migration scope

- Agent: Codex worker
- Trigger: CLEAN-03 converts five deferred admin service behaviors into implementation-ready Application commands/queries, repository contracts, and Function shells.
- Action: Opened and followed the project skill; synthesized the scope as a strangler migration with goals to move billing-support queue, resolve, accounting revenue export, suspension, and refund to Application handlers while keeping the old service and DI registration for Phase-B cleanup. Non-goals: no schema change, no migration, no live payment action, no deploy, no PR.
- Output artifacts: Application command/query/handler files under `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Admin/`, an Application refund port, DTO additions, repository contract additions, Function shell wiring, and new Application handler tests.
- Verification evidence: Initial AdminUseCaseTests run failed on missing handler/port types as expected; later `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 errors, the focused admin filter passed 27/27, and the full backend suite passed 702/702.
- Limitations: The source brief referenced an Application `IStripeRefundClient` file that was not present in this branch, so the port was added using the existing Application refund request/result records and backed by the existing Stripe adapter.

### 2026-06-09 - state-machine-modeling - CLEAN-03 admin lifecycle preservation

- Agent: Codex worker
- Trigger: CLEAN-03 changes admin workflows with persisted transitions: billing-support requests open-to-resolved, user suspension active/suspended toggles, and refund idempotency through audit-log state.
- Action: Opened and followed the project skill. State list: billing support `Open` and `Resolved`; user account suspension as `SuspendedAt == null` or non-null; refund state as no matching refund audit or matching `refund` audit for target/payment/amount. Events: admin queue read, admin resolve, repeated resolve, suspend, unsuspend, refund request, repeated refund request, refund provider failure. Allowed transitions: open support request resolves once with audit; resolved support request remains resolved without duplicate audit; suspend preserves existing `SuspendedAt` when already suspended; unsuspend clears `SuspendedAt`; first refund calls the Application refund port then writes one audit; repeated refund returns the prior audit-backed result without another provider call. Illegal/rejected transitions: missing user, missing payment, invalid amount/currency, unavailable refund port.
- Output artifacts: `ResolveBillingSupportRequestHandler`, `SetUserSuspensionHandler`, `IssueRefundHandler`, repository mutation methods, and tests covering repeated resolve, suspension toggle, refund repeat, and refund provider failure.
- Verification evidence: AdminUseCaseTests passed 21/21; focused admin filter passed 27/27; full backend suite passed 702/702.
- Limitations: No new enum, status column, migration, transition helper, deployment, push, PR, or payment-provider live action was added.

### 2026-06-09 - data-module-review - CLEAN-03 admin persistence migration

- Agent: Codex worker
- Trigger: CLEAN-03 changes EF-backed repository contracts and persistence behavior for admin support requests, audit logs, suspension fields, rewrite-credit revenue export, and refund audit idempotency.
- Action: Opened and followed the project skill; reviewed `AdminService`, `AdminHttpFunctions`, existing Admin handlers, repository interfaces, EF repositories, `IUnitOfWork`, and tests together. Findings: no schema/migration required; mutations stay tracked in repositories and commit through `IUnitOfWork`; refund duplicate detection remains audit-log based; revenue export preserves the old payment-credit filter and SQLite paging behavior.
- Output artifacts: repository method additions in `IAdminUserRepository`, `IBillingSupportRequestRepository`, `IRewriteCreditRepository`, and implementations in `AdminUserRepository`, `BillingSupportRequestRepository`, `RewriteCreditRepository`.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py backend-dotnet/src --limit 40` exited 0 and returned broad existing repo signals; diff restricted-term scan returned no matches; Release build passed; focused admin filter passed; full backend suite passed.
- Limitations: The data-risk scan is broad and reported existing quota/idempotency signals outside this issue. No schema, migration, data backfill, deployment, push, PR, or live payment action was performed.

### 2026-06-09 - resilience-test-generation - CLEAN-03 refund and duplicate admin events

- Agent: Codex worker
- Trigger: CLEAN-03 touches refund idempotency and provider failure behavior while moving refund logic out of the old service.
- Action: Opened and followed the project skill; used deterministic fakes rather than live provider calls. Failure matrix covered duplicate refund request, refund provider timeout/failure, duplicate billing-support resolve, missing user/payment, invalid refund amount/currency, and missing refund port.
- Output artifacts: `IssueRefundHandler` with fake-port Application tests for first refund, duplicate refund returning the prior audit, and provider failure writing no audit; `ResolveBillingSupportRequestHandler` test proving duplicate resolve writes one audit.
- Verification evidence: AdminUseCaseTests passed 21/21; focused admin/refund/suspension/route filter passed 27/27; full backend suite passed 702/702.
- Limitations: No retry policy was added for refund provider failure because the legacy behavior throws and writes no audit on provider failure. No live provider, deployment, push, PR, or payment action was performed.

### 2026-06-09 - dotnet-backend-testing - CLEAN-03 admin handler and Function gates

- Agent: Codex worker
- Trigger: CLEAN-03 adds C# Application handlers, repository methods, Function constructor wiring, and xUnit coverage for five admin use cases.
- Action: Opened and followed the project skill; wrote failing Application tests first, verified the missing-handler compile failure, implemented handlers/repositories/shell wiring, fixed one SQLite assertion query by materializing before ordering, then ran focused and full backend gates.
- Output artifacts: `AdminUseCaseTests` now covers billing-support queue read, resolve, accounting revenue export, suspension, refund with fake Application refund port, and refund provider failure. `AdminHttpFunctionsTestFactory` now constructs the new handler graph.
- Verification evidence: Initial `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~AdminUseCaseTests"` failed with missing `GetBillingSupportQueueHandler`, `ResolveBillingSupportRequestHandler`, `ExportAccountingRevenueHandler`, `SetUserSuspensionHandler`, `IssueRefundHandler`, and Application `IStripeRefundClient`. Final AdminUseCaseTests passed 21/21, focused admin filter passed 27/27, `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 errors, and full `dotnet test ReplyInMyVoice.sln -c Release` passed 702/702.
- Limitations: Build/test commands emitted NU1900 vulnerability-feed warnings because NuGet vulnerability metadata was unavailable, but restore/build/test completed. A local git commit was attempted and failed because the worktree git metadata is outside the writable sandbox. No push, PR, deploy, schema change, or live payment action was performed.

### 2026-06-09 - state-machine-modeling - CLEAN-10 outbox and rewrite lifecycle cleanup

- Agent: Codex worker
- Trigger: CLEAN-10 deletes legacy service code that previously touched outbox dispatch and rewrite-attempt creation lifecycles.
- Action: Opened and followed the project skill as a preservation checklist. State list: outbox `Pending`, `Processing`, `Sent`, `Failed`; rewrite attempt `Pending` plus rejected suspended-user create path; usage reservation `Pending`. Events: due outbox dispatch, handler success, handler failure, max-attempt failure, suspended-user create request, restored-user create request. Allowed transitions stay in the surviving Application handlers/repositories: due outbox claim moves pending to processing, success moves processing to sent, retryable failure moves processing to pending, final failure moves processing to failed, rewrite create for suspended users rejects without persistence, restored users can create one pending attempt with one pending reservation and one outbox row. Illegal transitions remain covered by existing handler tests.
- Output artifacts: deleted the four target service files; migrated the max-attempt outbox assertion into `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/WebhookOutboxUseCaseTests.cs`; repointed shared rewrite assertions in `AdminSuspensionTests.cs` and `RetentionServiceTests.cs` to `CreateRewriteAttemptHandler`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; focused handler filter passed 22/22; `AdminSuspensionTests.SuspendedUserRewriteRejected` passed 1/1; full `dotnet test ReplyInMyVoice.sln -c Release` passed 694/694; all four target service file absence checks passed.
- Limitations: No schema, migration, new state, status enum, deployment, push, PR, or payment action was performed.

### 2026-06-09 - data-module-review - CLEAN-10 legacy service deletion and DI cleanup

- Agent: Codex worker
- Trigger: CLEAN-10 changes EF-backed service/dependency wiring and removes obsolete infrastructure service files and their tests.
- Action: Opened and followed the project skill. Reviewed target services, DI registration, old unit tests, surviving Application handler tests, and shared tests together. Initial src reference check found the tax target still referenced by the legacy `AdminService`; removed that stale dependency by returning the existing Application tax DTO shape so the target file and co-located records could be deleted without introducing schema or migration work.
- Output artifacts: removed exactly the four target DI registrations; deleted the four target service files and four obsolete single-service tests; updated `AdminService.cs` and `AdminHttpFunctions.cs` only to stop compiling against deleted tax records; kept `QuotaService` and `TestCollectionBehavior.cs` unchanged.
- Verification evidence: `grep -rn -w` source checks were run for each target before deletion; post-change target-name scan over `backend-dotnet/src` and `backend-dotnet/tests` returned no deleted service/type references; changed-file policy scan returned no matches; Release build and full backend suite passed.
- Limitations: The branch did not satisfy the tax target's stated zero-reference precondition at first because `AdminService` still referenced it. The cleanup was kept compile-focused and no database, migration, payment, secret, deployment, push, or PR work was performed.

### 2026-06-09 - resilience-test-generation - CLEAN-10 retry and failure coverage preservation

- Agent: Codex worker
- Trigger: CLEAN-10 deletes old tests around outbox retry/failure behavior and shared rewrite create tests, so failure/retry invariants needed coverage preservation.
- Action: Opened and followed the project skill as a failure-matrix checklist. Critical operations reviewed: outbox dispatch handler success, retryable handler failure, final max-attempt failure, concurrent claim, suspended-user rewrite create rejection, restored-user rewrite create success, and first rewrite consent stamping. Dependency boundaries stayed local: EF SQLite test database and deterministic in-memory handlers/fakes.
- Output artifacts: added handler-level max-attempt final-failure coverage in `Application/WebhookOutboxUseCaseTests.cs`; repointed `AdminSuspensionTests.cs` and `RetentionServiceTests.cs` to the surviving Application rewrite handler while preserving final database-state assertions.
- Verification evidence: focused handler filter passed 22/22; exact suspension regression passed 1/1 after fixing stale tracked-context reuse; full `dotnet test ReplyInMyVoice.sln -c Release` passed 694/694.
- Limitations: No new retry policy, external provider call, live cloud dependency, deployment, push, PR, or payment action was added.

### 2026-06-09 - dotnet-backend-testing - CLEAN-10 backend acceptance gates

- Agent: Codex worker
- Trigger: CLEAN-10 deletes C# infrastructure services and obsolete xUnit tests while requiring handler/integration coverage and full backend verification.
- Action: Opened and followed the project skill. Confirmed handler coverage before deleting obsolete unit tests, migrated the one unique outbox max-attempt assertion, switched shared tests to `CreateRewriteAttemptHandler`, ran focused tests first, then full backend suite.
- Output artifacts: modified `WebhookOutboxUseCaseTests.cs`, `AdminSuspensionTests.cs`, and `RetentionServiceTests.cs`; deleted `OutboxDispatcherTests.cs`, `TaxTurnoverServiceTests.cs`, `ApiKeyUsageAnomalyServiceTests.cs`, and `RewriteRequestServiceTests.cs`; retained `QuotaService` tests and xUnit collection behavior.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed; issue focused filter passed 22/22; `dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~AdminSuspensionTests.SuspendedUserRewriteRejected"` passed 1/1; full `dotnet test ReplyInMyVoice.sln -c Release` passed 694/694; four target file absence checks passed.
- Limitations: A local `git add && git commit` checkpoint was attempted but failed because the worktree git metadata lives outside the writable sandbox. No push, PR, deploy, schema change, NuGet change, or payment action was performed.

### 2026-06-09 - state-machine-modeling - CLEAN-11 quota, rewrite job, and promo cleanup

- Agent: Codex worker
- Trigger: CLEAN-11 deletes legacy quota, rewrite-job, promo, and promo-admin infrastructure services while preserving usage reservation, rewrite attempt, and promo redemption lifecycles in Application handlers.
- Action: Opened and followed the project skill as a preservation checklist. State list: usage reservation `Pending`, `Finalized`, `Released`, `Expired`; rewrite attempt `Pending`, `Processing`, `Succeeded`, `Failed`, `Expired`; promo redemption absent or `Applied`. Events: reserve, mark processing, finalize success, release failure, release expired, process job, duplicate job processing, redeem promo, duplicate redemption, promo cap reached, invalid or expired code. Allowed transitions remain in `ReserveQuotaHandler`, `MarkQuotaProcessingHandler`, `FinalizeQuotaSuccessHandler`, `ReleaseQuotaHandler`, `ReleaseExpiredReservationsHandler`, `ProcessRewriteJobHandler`, `RedeemPromoHandler`, and promo-admin handlers. Illegal transitions remain covered by handler tests and surviving API/concurrency tests.
- Output artifacts: retargeted `ExpiredReservationCleanupService` and surviving tests to Application handlers; removed old worker missing-attempt catch for the deleted processor exception; returned Application promo-admin DTOs directly from Functions.
- Verification evidence: exact `grep -rn -w` source checks for all four deleted services returned no hits after deletion; focused CLEAN-11 filter passed 65/65; full `dotnet test ReplyInMyVoice.sln -c Release` passed 653/653; Release build passed with 0 errors.
- Limitations: No new state column, enum, migration, schema change, deployment, push, PR, or payment action was performed.

### 2026-06-09 - data-module-review - CLEAN-11 persistence reference cleanup

- Agent: Codex worker
- Trigger: CLEAN-11 removes EF-backed infrastructure service code and rewrites test setup that mutates usage periods, usage reservations, rewrite attempts, promo credits, promo redemptions, and cost logs.
- Action: Opened and followed the project skill. Reviewed target services, DI registrations, Application handlers, repositories, old unit tests, and surviving tests together. Initial source scan found `ExpiredReservationCleanupService` still depended on the old quota service, so that wrapper now calls `ReleaseExpiredReservationsHandler`. Shared `DbFixture` was extracted before deleting the old quota test file because many unrelated tests depend on it.
- Output artifacts: deleted the four target service files and three obsolete service test files; removed four DI registrations; extracted `DbFixture`; rewired surviving test setup through Application handlers/repositories; kept schema and migrations unchanged.
- Verification evidence: `git diff --check` exited 0; post-change old-service/type scan over `backend-dotnet/src` and tests returned no hits; all four file absence checks passed; Release build and full backend suite passed.
- Limitations: A standalone unused legacy `ReserveRewriteResult` record file remains because it was not one of this issue's delete targets. No migration, data backfill, deployment, push, PR, or live provider/payment action was performed.

### 2026-06-09 - resilience-test-generation - CLEAN-11 handler failure and concurrency coverage preservation

- Agent: Codex worker
- Trigger: CLEAN-11 deletes old service tests around provider failure, repeated processing, quota expiry, promo concurrency, IP velocity, and quota/rate-limit interaction.
- Action: Opened and followed the project skill as a failure-matrix checklist. Critical operations reviewed: quota reservation race, rate-limit admission before quota reservation, expired reservation cleanup, provider failure release, repeated succeeded job processing, promo duplicate redemption, global cap race, invalid/expired promo code, production promo proxy fail-closed, and IP velocity blocking. Deterministic local fakes and SQLite fixtures were used; no live provider endpoints were called.
- Output artifacts: surviving tests now use `ReserveQuotaHandler`, `ReleaseExpiredReservationsHandler`, `FinalizeQuotaSuccessHandler`, `ProcessRewriteJobHandler`, `RedeemPromoHandler`, and direct Function/API paths while preserving final persisted-state assertions.
- Verification evidence: focused CLEAN-11 filter passed 65/65 and full Release suite passed 653/653. Diff restricted-wording scan returned no matches.
- Limitations: No new retry policy, external provider call, live cloud dependency, deployment, push, PR, or payment action was added.

### 2026-06-09 - dotnet-backend-testing - CLEAN-11 backend acceptance gates

- Agent: Codex worker
- Trigger: CLEAN-11 changes C# infrastructure DI, Functions/Worker references, and xUnit/WebApplicationFactory/EF SQLite tests while deleting obsolete service tests.
- Action: Opened and followed the project skill. Compared old service-unit coverage against Application handler tests, extracted shared test support before deleting obsolete tests, migrated surviving setup helpers to handlers, ran the focused issue filter, then the full backend suite.
- Output artifacts: modified `AdminHttpFunctions.cs`, `ExpiredReservationCleanupService.cs`, `ServiceBusRewriteWorker.cs`, `AdminPromoTests.cs`, `ApiBurstRateLimitTests.cs`, `ExpiredReservationCleanupServiceTests.cs`, `PromoConcurrencyTests.cs`, `RewriteApiTests.cs`, and `RewriteCostTrackingTests.cs`; added `DbFixture.cs` and `RewriteProviderFakes.cs`; deleted the four target services plus `QuotaServiceTests.cs`, `RewriteJobProcessorTests.cs`, and `PromoServiceTests.cs`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings/errors; focused issue filter passed 65/65; full `dotnet test ReplyInMyVoice.sln -c Release` passed 653/653; all four target file absence checks passed.
- Limitations: Touched-file count exceeded the practical cap because multiple surviving-subject tests and shared test support still referenced deleted types. No push, PR, deploy, schema change, NuGet change, or payment action was performed.

### 2026-06-09 - dotnet-backend-testing - CLEAN-11 verifier retry

- Agent: Codex worker
- Trigger: The supervisor reported a full `dotnet test` failure in `V1RewriteRateLimitTests` after CLEAN-11.
- Action: Opened and followed the project skill; reproduced the exact test, stress-ran it, ran the adjacent API/rate-limit test filter, then reran the full backend suite from the current worktree.
- Output artifacts: no source or test behavior change was made in this pass because the exact reported test passed locally and the full suite also passed.
- Verification evidence: exact `V1RewriteRateLimitTests.V1_rewrite_submit_enforces_per_key_rate_limit_under_concurrent_usage_write_failures` passed once, then passed 25 consecutive `--no-build` runs; adjacent `V1RewriteRateLimitTests|RewriteApiTests|ApiBurstRateLimitTests` filter passed 38/38; full `dotnet test ReplyInMyVoice.sln -c Release` passed 653/653.
- Limitations: Local test count is 653 because CLEAN-11 deletes three obsolete service test files. The supervisor failure showed 695 tests and source paths outside this worktree, so that result appears to have used a stale or different checkout state. No push, PR, deploy, schema change, NuGet change, or payment action was performed.

### 2026-06-09 - data-module-review - CLEAN-11 verifier retry

- Agent: Codex worker
- Trigger: The reported failure occurred during EF Core persistence for concurrent rewrite attempt creation.
- Action: Opened and followed the project skill; reviewed `CreateRewriteAttemptHandler`, `ReserveQuotaHandler`, `UnitOfWork`, `ApiKeyRateLimiter`, the V1 completion usage writer, and the old quota retry shape from the integration branch.
- Output artifacts: no persistence code change was made because the current handler already uses the same three-attempt reservation race retry shape as the deleted quota service and the failing path passed locally under stress.
- Verification evidence: the current worktree full suite passed 653/653, and the focused rate-limit/API filter passed 38/38.
- Limitations: No schema or migration change was made.

### 2026-06-09 - resilience-test-generation - CLEAN-11 verifier retry

- Agent: Codex worker
- Trigger: The reported failing test covers concurrent V1 requests when API usage write persistence fails.
- Action: Opened and followed the project skill; checked the local failure matrix for database write failure, concurrent requests, rate-limit admission, quota reservation, outbox creation, and final persisted-state assertions.
- Output artifacts: no new test was added because the existing regression test is present and passed repeatedly.
- Verification evidence: exact reported test passed 26 total local runs in this pass, and related API/rate-limit filter passed 38/38.
- Limitations: The external verifier failure could not be reproduced from this worktree.

### 2026-06-09 - state-machine-modeling - CLEAN-11 verifier retry

- Agent: Codex worker
- Trigger: The reported failure involved the V1 submit lifecycle: rate-limit admission, pending rewrite attempt creation, pending reservation creation, and outbox enqueue.
- Action: Opened and followed the project skill as a lifecycle checklist. State list checked: API rate-limit window count, rewrite attempt `Pending`, usage reservation `Pending`, outbox message `Pending`, and usage audit row absent when audit writes fail. Event checked: concurrent V1 submit after rate-limit admission. Invariant checked: admitted requests create exactly one attempt/reservation/outbox row each, rate-limited requests create no quota rows, and audit write failure does not change the response outcome.
- Output artifacts: no lifecycle code change was made after local verification passed.
- Verification evidence: exact test stress run and full backend suite passed from the current worktree.
- Limitations: No new state, enum, schema, deployment, push, or PR work was performed.

### 2026-06-09 - state-machine-modeling - CLEAN-13 Stripe webhook and reconciliation cleanup

- Agent: Codex worker
- Trigger: CLEAN-13 deletes legacy Stripe webhook/reconciliation infrastructure services while preserving webhook event, subscription grace, refund/dispute, and reconciliation lifecycles in Application handlers.
- Action: Opened and followed the project skill as a lifecycle checklist. State list: Stripe event `Processing`, `Processed`, `Failed`; subscription `Active`, `PastDue`, `Inactive`, `Canceled`; payment grace present/cleared; rewrite credit grant/adjusted. Events checked: checkout completed, invoice failed/succeeded/paid, subscription updated/deleted, refund, dispute, duplicate/replayed webhook, and reconciliation timer inputs. Invariants checked: duplicate event idempotency, failed-event retry, no duplicate credit grant, grace expiry/reminder transitions, paid grace preservation for `past_due`, non-paying downgrade, refund clamping, dispute revocation, and read-only reconciliation inputs.
- Output artifacts: migrated old service edge-case assertions into `StripeEventUseCaseTests.cs`; added `StripeEventNotifierTests.cs`; deleted `StripeEventService.cs`, `StripeReconciliationService.cs`, and their obsolete direct test files.
- Verification evidence: focused Stripe filter passed 42/42; full `dotnet test ReplyInMyVoice.sln -c Release` passed 636/636; deleted-class grep checks over `backend-dotnet/src` passed.
- Limitations: No new lifecycle state, enum, schema, migration, deployment, push, PR, or payment action was performed.

### 2026-06-09 - data-module-review - CLEAN-13 Stripe persistence reference cleanup

- Agent: Codex worker
- Trigger: CLEAN-13 removes EF-backed legacy services and test coverage around `StripeEvents`, `RewriteCredits`, `StripeInvoices`, and reconciliation purchase-grant reads.
- Action: Opened and followed the project skill. Reviewed old services, repositories, DI registrations, handler tests, surviving consumers, and old service tests together. Confirmed surviving contracts must move before deleting `StripeReconciliationService.cs`, and confirmed `StripeBillingService`, its statics, provider adapters, notifier, and subscription cancellation service remain untouched.
- Output artifacts: added `StripeReconciliationContracts.cs`; removed only `AddScoped<StripeReconciliationService>()` and `AddScoped<StripeEventService>()`; preserved legacy reconciliation client/alerter aliases; added a DI guard asserting retired services are not registered.
- Verification evidence: relocation build passed before deletion; post-deletion Release build passed; `grep -rq "class StripeBillingService" backend-dotnet/src` and `grep -rq "interface IStripeReconciliationAlerter" backend-dotnet/src` passed; `git diff --check` passed.
- Limitations: No database schema, migration, index, transaction policy, live provider call, deployment, push, or PR change was made.

### 2026-06-09 - resilience-test-generation - CLEAN-13 webhook replay and refund coverage preservation

- Agent: Codex worker
- Trigger: CLEAN-13 retires old service tests that covered duplicate Stripe webhooks, failed-event replay, checkout grant conflicts, refunds, disputes, and notification post-commit effects.
- Action: Opened and followed the project skill as a failure-matrix checklist. Failure/replay rows preserved locally: missing checkout user then replay, duplicate checkout/subscription events, invoice recovery replay, refund before grant then replay, duplicate refund replay, partial refund grant math, consumed-credit clamp, dispute credit revocation, and invalid signature/no-side-effect API tests already in `StripeWebhookApiTests`.
- Output artifacts: migrated relevant old-service assertions into `StripeEventUseCaseTests.cs`; added `StripeEventNotifierTests.cs` for notification boundary behavior that now lives outside the handler; removed obsolete direct service tests after focused coverage passed.
- Verification evidence: initial TDD DI guard failed as expected while legacy services were still registered; migrated event/notifier focused filter passed 25/25 before deletion; issue focused Stripe filter passed 42/42 after deletion; full backend suite passed 636/636.
- Limitations: Malformed raw JSON logging from the deleted service was not reintroduced because parsing now occurs before the handler boundary. No live Stripe endpoint, cloud dependency, deployment, push, PR, or payment action was used.

### 2026-06-09 - dotnet-backend-testing - CLEAN-13 backend acceptance gates

- Agent: Codex worker
- Trigger: CLEAN-13 changes C# infrastructure DI, service files, xUnit tests, EF SQLite handler tests, and full backend acceptance coverage.
- Action: Opened and followed the project skill. Added a failing DI guard first, compared old service-unit assertions against Application handler/API tests, migrated unique handler/notifier coverage, relocated surviving contracts, deleted obsolete service tests, then ran focused and full backend gates.
- Output artifacts: added `StripeReconciliationContracts.cs` and `StripeEventNotifierTests.cs`; modified `StripeEventUseCaseTests.cs`, `InfrastructureServiceCollectionTests.cs`, `ServiceCollectionExtensions.cs`, `plans/decisions-log.md`, and this log; deleted `StripeEventService.cs`, `StripeReconciliationService.cs`, `StripeEventServiceTests.cs`, and `StripeReconciliationServiceTests.cs`.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed after relocation and again after deletion; focused CLEAN-13 filter passed 42/42; full `dotnet test ReplyInMyVoice.sln -c Release` passed 636/636; acceptance grep checks passed; diff-only restricted-wording scan found no added banned substrings.
- Limitations: NuGet vulnerability metadata warnings appeared because the NuGet service index was unavailable, but restore/build/test completed. A local `git add && git commit` checkpoint was attempted and failed because this worktree's git index is outside the writable sandbox. No push, PR, deploy, schema change, NuGet change, or payment action was performed.

### 2026-06-09 - data-module-review - CLEAN-20 service deletion

- Agent: Codex worker
- Trigger: CLEAN-20 removes EF-backed account, API key, API usage, credit expiry, and admin infrastructure services plus obsolete direct service tests.
- Action: Opened and followed the project skill. Reviewed the target service files, DI registrations, handler coverage, integration coverage, repository-backed handler paths, and surviving tests that had used deleted services as setup helpers. Confirmed no schema, migration, index, transaction policy, or repository contract change was needed.
- Output artifacts: deleted the five target service files and five obsolete direct service test files; removed their five DI registrations; moved endpoint response records to function files; repointed surviving setup helpers to Application handlers or direct data seeding.
- Verification evidence: post-delete service-name scan over `backend-dotnet/src` and `backend-dotnet/tests` had no hits; `git diff --check` passed; all five target file absence checks passed.
- Limitations: Initial pre-delete scan found two stale non-target source references, so they were first repointed to existing handlers/helper plumbing before deletion. No database schema, migration, NuGet, deployment, push, PR, or payment action was performed.

### 2026-06-09 - resilience-test-generation - CLEAN-20 coverage preservation

- Agent: Codex worker
- Trigger: CLEAN-20 deletes old service tests that previously covered quota/account setup, API key usage windows, admin refund provider failure, and credit expiry notification boundaries.
- Action: Opened and followed the project skill as a coverage checklist. Preserved the surviving failure and persistence assertions in Application handler tests and integration tests, and repointed admin refund provider-failure coverage from the deleted service to the surviving function/handler path.
- Output artifacts: updated `AdminRefundTests.cs`, `AdminSuspensionTests.cs`, `AdminDeleteUserTests.cs`, `AdminCreditAdjustTests.cs`, `ApiKeyHttpFunctionsTests.cs`, `RewriteApiTests.cs`, and `RewriteHistoryTests.cs`; deleted only the obsolete direct service test files.
- Verification evidence: focused CLEAN-20 acceptance filter passed 56/56; full `dotnet test ReplyInMyVoice.sln -c Release` passed 585/585.
- Limitations: No new resilience behavior was introduced; this pass preserved existing coverage while removing obsolete subjects. No live provider, cloud dependency, deployment, push, PR, schema, or payment action was used.

### 2026-06-09 - dotnet-backend-testing - CLEAN-20 backend acceptance gates

- Agent: Codex worker
- Trigger: CLEAN-20 changes C# service files, dependency injection, function response contracts, and xUnit/EF SQLite tests.
- Action: Opened and followed the project skill. Compared direct service test coverage against Application handler and integration tests, repointed surviving-subject test helpers to handlers/direct seeding, removed obsolete direct service tests, and ran the required backend gates.
- Output artifacts: modified `AccountHttpFunctions.cs`, `AdminHttpFunctions.cs`, `ApiUsageHttpFunctions.cs`, `RewriteHttpFunctions.cs`, `ApiKeyHashing.cs`, `ServiceCollectionExtensions.cs`, and surviving test files; deleted `AccountService.cs`, `ApiKeyService.cs`, `ApiKeyUsageQueryService.cs`, `CreditExpiryReminderService.cs`, `AdminService.cs`, and their five direct test files.
- Verification evidence: `dotnet build ReplyInMyVoice.sln -c Release` passed with 0 warnings/errors; focused CLEAN-20 filter passed 56/56; full `dotnet test ReplyInMyVoice.sln -c Release` passed 585/585; all five target file absence checks passed; post-delete service-name scan had no hits.
- Limitations: A local `git add && git commit` checkpoint was attempted and failed because this worktree's git index is outside the writable sandbox. No push, PR, deploy, schema change, NuGet change, or payment action was performed.

### 2026-06-10 - system-spec-synthesis - frontend commercial redesign plan

- Agent: Claude Code (supervisor, interactive)
- Trigger: Owner asked for a page-by-page frontend redesign plan targeting commercial-grade UX and user-journey rationality.
- Action: Opened and followed the skill. Read AGENTS.md, enumerated all App Router pages, read `plans/site-overhaul/REQUIREMENT.md` + recent shipped-wave plans, ran an Explore-agent audit of every user-facing page (structure, CTAs, states, SEO, copy consistency), and verified three funnel facts in code (buy-intent loss in `components/landing/buy-button.tsx`, hardcoded hero CTA in `components/landing/hero.tsx`, stale "3 lifetime free" claim in `app/terms/page.tsx`).
- Output artifacts: `plans/frontend-commercial-redesign/REQUIREMENT.md` (audit findings F1–F10, journeys J1–J5, per-page issue tables with acceptance criteria, 3-wave rollout, verification plan, open questions Q1–Q4); this log entry.
- Verification evidence: planning-only turn — findings cross-checked against source files cited inline; no build/test run required. Banned-term constraint and contract-test obligations are encoded as acceptance criteria in the plan.
- Limitations: No code changed, no issues created, no delivery wave launched. Wave B copy depends on owner decisions Q1–Q4 (trial-code acquisition, delete-account backend status, dark mode, legal sign-off). Live-page rendering was not browser-verified this turn; `ui-browser-testing` is scheduled per wave instead.

### 2026-06-10 - ui-browser-testing - App Shell V1 (FE-S1) build + verification

- Agent: Claude Code (supervisor, interactive)
- Trigger: Owner approved the FE-S1 shell at the visual checkpoint and authorized building the signed-in App Shell, passing review, merging to main, and deploying.
- Action: Followed the skill. Hand-built the signed-in app shell — `app/app/layout.tsx` (one auth gate + shell data), `components/app/shell/*` (topbar, progressive-disclosure sidebar, mobile drawer with focus trap, quota pill, account menu, shared primitives EmptyState/Skeleton/UpsellCard/SectionCard, CSS-module token styling), suppressed the marketing header/footer on `/app/*` via `ChromeGate`. Made every nav target real: surfaced the previously-dark `me/rewrites` history API (new same-origin proxy + `history-list.tsx` with loading/empty/error/delete states), relocated the developer console to `/app/keys|usage` (reused `DeveloperDashboard` with a new `initialTab` prop; `/developers/keys` → 301), built a real `/app/connect` page, consolidated billing into the Account page. Added branded `not-found`/`error`/`global-error` pages and corrected the `/terms` "3 lifetime free" claim to the trial-code model. Verified the shell in the browser preview (a temporary mock-data harness, since `/app` needs an Entra session) across desktop consumer + developer views, mobile collapsed, mobile drawer, and the account menu; deleted the harness before commit.
- Output artifacts: shell components under `components/app/shell/`, `components/app/history-list.tsx`, `components/chrome-gate.tsx`; new routes `app/app/{layout,history,keys,usage,connect}` and `app/api/me/rewrites/**`; `app/{not-found,error,global-error}.tsx`; edits to `app/app/page.tsx`, `app/app/account/page.tsx`, `app/developers/keys/page.tsx`, `app/layout.tsx`, `app/terms/page.tsx`, `components/developers/developer-dashboard.tsx`, `next.config.ts`; tests `tests/unit/app-shell.test.ts` (+ updated `developer-keys-ui.test.ts`).
- Verification evidence: 5 preview screenshots reviewed (consumer/developer/mobile/drawer/account-menu); `npx tsc --noEmit` clean; `npx vitest run` 291/291 pass; `npm run build` + `npm run cf:build` (OpenNext) succeed; banned-term scan over changed paths clean; no `.cs` changes (no DB migration).
- Limitations: The live `/app` shell (auth-gated) and the real history fetch are only verifiable post-deploy; a live smoke is planned after the prod deploy. The shell is "V1" — Usage/Connect reuse existing surfaces and Billing is consolidated into Account; the richer per-page split (FE-S4/S5/S6) and Waves A/C/D remain queued.

### 2026-06-10 - ui-browser-testing - same-origin fix + public redesign slice (autonomous)

- Agent: Claude Code (supervisor, interactive → autonomous while owner out)
- Trigger: Owner's logged-in screenshots showed Account ("Cross-origin request rejected") + History ("couldn't load") broken; then owner authorized an autonomous run to finish verifiable tasks and deploy.
- Action: Followed the skill. Root-caused the live bug — `requireSameOrigin` 403'd same-origin GET in prod because browsers omit the Origin header on safe methods (`isAllowedOrigin(null, prod)→false`); the Account route had been an orphan so it surfaced only when the shell linked it. Fixed `requireSameOrigin` to treat absent-Origin GET/HEAD as same-origin (writes stay strict) + added `tests/unit/http-same-origin.test.ts`. Then built the session-free-verifiable redesign slice: landing auth-aware CTAs (`app/page.tsx` reads session → Hero/ClosingCta swap CTA), homepage metadata, and a `/developers` "Two ways to integrate" hub (REST API + MCP equal cards + shared-key strip). Verified public pages in local preview (hub screenshot, signed-out CTA, home title) and post-deploy via curl.
- Output artifacts: `lib/http.ts`, `tests/unit/http-same-origin.test.ts` (PR #684); `app/page.tsx`, `app/developers/page.tsx`, `components/landing/hero.tsx`, `components/landing/closing-cta.tsx` (PR #685); `app/sitemap.ts` (PR #686). All merged to main + deployed.
- Verification evidence: `/api/me` + `/api/me/rewrites` now return 401 (was 403) live; homepage `<title>` + og:title live; `/developers` "Two ways to integrate" + shared-key strip live (count 2); all public pages 200; sitemap lists `/developers/*`; tsc + 295 unit + build + cf:build green on each; 3 cloudflare-worker deploys SUCCESS, dotnet-azure no-op.
- Limitations: Deferred (unverifiable without an owner session, or design-judgment): buy-intent-through-auth OAuth round-trip, OG image (CF ImageResponse risk), workspace/shell fusion, richer shell pages (per-source usage / key-aware connect / billing-account split), full physical /developers/api split. The authenticated shell + Account/History real-data render still want an owner logged-in check (expected to work post same-origin fix).

### 2026-06-10 - ui-browser-testing - signed-in console redesign (owner feedback pass)

- Agent: Claude Code (supervisor, interactive)
- Trigger: Owner reviewed the live shell and directed a redesign: History clicks did nothing and showed no draft-vs-rewrite; the Rewrite page embedded a redundant history list and felt cramped; Account didn't look like a management console and showed "Inactive" for free users; Developer mode must live in the left nav for everyone, default off, and upsell instead of pretending to be enabled.
- Action: Followed the skill. Rebuilt the three pages in one console style: History rows now expand to a side-by-side draft vs rewrite comparison (server detail fetch, AI-signal %s, copy + delete); the Rewrite workspace fills the widened 1280px shell with the shared page-header style and its embedded localStorage history list was removed (history lives only at /app/history; retention copy updated); Account became stat cards (Email / Plan / Rewrites left / Period ends) + purchases table + billing support + danger zone, with planLabelForStatus mapping free accounts to "Free" (never "Inactive"); the Developers sidebar group is now always visible with the Developer-mode toggle deleted, and Keys/Usage/Connect render a Pro/API feature-list upsell (NZ$19.90/mo CTA) for non-subscribers via isDeveloperTierStatus. Verified all four views (rewrite/history-expanded/account/upsell) desktop + mobile in a mock-data preview harness (deleted before commit).
- Output artifacts: PR #687 (main `6051643`) — components/app/shell/* (nav model, primitives incl. DeveloperUpsell, stat/history/danger styles), components/app/history-list.tsx, components/app/rewrite-workspace.tsx, components/account/account-panel.tsx, app/app/{layout,account,keys,usage,connect}; tests updated (workspace-copy, app-shell, account-receipts, developer-keys-ui).
- Verification evidence: preview screenshots of all four views; tsc clean; vitest 298/298; build + cf:build green; banned-scan clean; cloudflare-worker deploy SUCCESS; live smoke (public 200, /app + /app/history → 307 sign-in, /api/me 401 guard intact). Also deleted untracked "* 2.tsx" filesystem-duplicate artifacts that were breaking local tsc.
- Limitations: Authenticated rendering with real data still needs the owner's logged-in pass (auth-gated). Pro-tier console view (real DeveloperDashboard inside the gate) unchanged this round; funnel items (buy-intent through auth, checkout banners, first-run onboarding) remain queued.

### 2026-06-10 - ui-browser-testing - pricing v3 (dynamic Sonnet workflow + supervisor visual verify)

- Agent: Claude Code supervisor (Opus) orchestrating a dynamic Workflow with Sonnet worker agents.
- Trigger: Owner asked to redesign /pricing per PLAN §11b "directly with a dynamic workflow, Sonnet writes the code."
- Action: Launched a Workflow (implement→verify→fix) with model:'sonnet' agents scoped to the pricing files + contract tests; agents read PLAN §11b, applied PF1-4 truth fixes + the developer card + signed-in awareness, updated pricing-* tests, and ran tsc/vitest. Verify agent passed first try (298 tests, banned clean). Supervisor then visually verified the public page in preview (desktop tall viewport + mobile) and CAUGHT two visual defects the green-tests Sonnet pass missed: the new full-width `.plan-pro` developer card was unstyled and its name/price inherited light-on-light color (ghost text), and `.pricing-session-hint` was unstyled. Supervisor fixed both in app/globals.css (full-width `.plan-pro` card spanning the 2-col grid + dark name/price + 2-col feature list + `.pricing-session-hint` pill), re-verified in preview.
- Output artifacts: PR #688 (main `c17f121`) — app/pricing/page.tsx, components/landing/pricing-{comparison,faq,trust}.tsx (Sonnet), app/globals.css (supervisor CSS fix), pricing-* contract tests; PLAN §11b.
- Verification evidence: preview screenshots desktop+mobile (dev card reads correctly post-fix); tsc + 298 unit + build + cf:build green; live smoke — /pricing 200, "AI Signal" present + "Tone check" gone, "local history"/"tone preset" claims gone, "REST API + MCP" + "#pro" + "Free (trial code)" present; cloudflare-worker deploy SUCCESS, dotnet-azure no-op.
- Limitations: buy-intent-through-auth OAuth round-trip (FE-B1/B2) excluded — auth-flow risk, needs an owner-session test. Signed-in pricing rendering (account hint / Pro "manage billing" swap) verified structurally + signed-out path in preview; the live signed-in view wants an owner logged-in pass. Reinforced lesson: Sonnet-workflow UI output passes tests but leaves visual/layout gaps — the supervisor preview-verify+fix step is required, not optional.

### 2026-06-11 - ui-browser-testing - FE-OPT M0-1 loading skeletons

- Agent: Codex worker
- Trigger: Issue #689 adds browser-visible App Router loading states for `/app/*` and `/admin`, plus the `/app` shell error fallback.
- Action: Opened and followed the project skill. Read the app shell, shell primitives/CSS, the existing admin promo-code loading pattern, target pages, and Playwright setup. Added a shared app-shell skeleton helper using existing shell primitives, route-specific loading files, and a branded `/app` error boundary.
- Output artifacts: `components/app/shell/shell-skeleton.tsx`, new loading/error route files under `app/app/**` and `app/admin/loading.tsx`, `components/app/shell/shell.module.css`, and this log entry.
- Verification evidence: pre-change file-presence check failed as expected; post-change required file checks passed; restricted-wording scan over `app components public lib` passed; `npm run typecheck`, `npm run build`, and `npm run test` passed.
- Limitations: Browser preview was attempted against a local Next dev server and mock API, but Chromium launch was denied by the macOS sandbox and WebKit was not installed. The attempted preview left local test listeners on `127.0.0.1:3210` and `127.0.0.1:45935`; the harness denied both session input and PID signals, so they could not be stopped from this worker turn. No deploy, push, PR, backend, schema, auth, billing, or payment-provider changes were made.
