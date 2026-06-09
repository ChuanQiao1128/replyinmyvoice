# DDD-42: Migrate RewriteJob use-cases to Application handlers (strangler)

## Context
Per docs/ddd-migration-playbook.md, migrate the RewriteJob use-cases from
Infrastructure/Services/RewriteJobProcessor.cs into Application handlers that depend on repository
interfaces, following the Wave-1 Rewrite template (Application/UseCases/Rewrite). KEEP the old
service in place (strangler add-then-replace; entry-point switch + deletion are later waves).
Read first: backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs,
docs/ddd-migration-playbook.md, Application/UseCases/Rewrite/* (template), Application/Abstractions/*.

RewriteJobProcessor.cs (583L) exposes one public use-case:
- `ProcessAsync(RewriteJob job, CancellationToken ct)` — the full rewrite-execution orchestration:
  marks the attempt as processing, calls the AI provider, persists the result, finalises quota,
  writes a cost log, and handles all failure paths (quota release, error transitions).

This is a command-only context. The handler wraps the full job execution pipeline.

## Constraints
- Handlers depend on repository interfaces + IUnitOfWork, never AppDbContext directly.
- Extend Application/Abstractions + Infrastructure/Repositories with any new repo methods needed,
  mirroring existing style; register handlers (+ new repos) in ServiceCollectionExtensions.
- KEEP the old RewriteJobProcessor untouched. No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- Match existing behaviour: existing tests for RewriteJobProcessor stay green, assertions unchanged.
- The AI provider call is an Infrastructure concern — define an `IRewriteEngineClient` interface in
  Application/Abstractions; the handler calls it, and the Infrastructure implementation wraps the
  real provider (DeepSeek/OpenAI). Do NOT embed HTTP calls in the Application layer.
- Cost logging (`WriteCostLogAsync`) belongs in the handler via a narrow `IRewriteCostLogger`
  abstraction in Application/Abstractions; the Infrastructure implementation can write to whatever
  persistence is currently used.
- The handler must preserve the exact state-machine transitions (processing → succeeded / failed)
  and the quota finalise/release semantics from the old processor.

## Changes required
1. `Application/UseCases/RewriteJob/ProcessRewriteJobCommand.cs` + `ProcessRewriteJobHandler.cs` —
   replicates the full `ProcessAsync` orchestration via repository interfaces and new abstractions.
2. `Application/Abstractions/IRewriteEngineClient.cs` — interface for the AI rewrite call (input
   draft → rewrite output); Infrastructure implements with the existing provider client.
3. `Application/Abstractions/IRewriteCostLogger.cs` — interface for writing the cost log entry;
   Infrastructure implements with the existing persistence path.
4. Extend `IRewriteAttemptRepository` and `IUsageReservationRepository` in Application/Abstractions
   if new query/mutation methods are needed for the job-execution path.
5. Extend Infrastructure/Repositories with matching implementations; register in
   ServiceCollectionExtensions.cs.
6. `ServiceCollectionExtensions.cs` — register `ProcessRewriteJobHandler` and any new repositories/
   clients with `AddScoped`.
7. `backend-dotnet/tests/ReplyInMyVoice.Tests/Application/RewriteJobUseCaseTests.cs` — cover the
   handler: success path, AI provider failure path, and quota-release on error; use fakes for
   `IRewriteEngineClient` and `IRewriteCostLogger`.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteJobUseCaseTests` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/UseCases/RewriteJob/ProcessRewriteJobHandler.cs`

## DO NOT
- Do NOT delete or edit RewriteJobProcessor or any other existing services (strangler — old path stays).
- Do NOT change entry points (Functions/Api/Worker) — later wave.
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) anywhere.
- Do NOT use @ts-ignore/eslint-disable, loosen configs, or gut tests.
- Do NOT push, open a PR, or touch main.
