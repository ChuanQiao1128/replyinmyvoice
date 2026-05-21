---
name: dotnet-backend-testing
description: Use when adding, changing, reviewing, or explaining C#/.NET backend tests, xUnit tests, ASP.NET Core API integration tests, EF Core transaction tests, provider fakes, webhook tests, queue/worker tests, or CI dotnet test coverage.
---

# .NET Backend Testing

Use this skill to choose and implement the right test level for C#/.NET backend work. Prefer tests that prove persisted state and API behavior, not only mocked calls.

## Workflow

1. Identify the behavior and invariant under test.
2. Choose the lowest test level that proves it:
   - domain/service unit test for pure logic
   - EF Core SQLite integration test for transactions, indexes, idempotency, and counters
   - `WebApplicationFactory` test for ASP.NET Core routing, auth decisions, validation, and response codes
   - worker/service test for queues, outbox, retries, and provider failures
3. Write the failing test before production changes when fixing a bug.
4. Use deterministic fakes for Stripe, OpenAI, Sapling, Service Bus, and other external providers.
5. Assert final state, not just returned status.
6. Run focused tests first, then the broader backend suite.

## Project Defaults

Use what this repo already proves:

```text
xUnit
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing / WebApplicationFactory
EF Core SQLite test database
hand-written fakes/stubs for providers
dotnet test
coverlet.collector
Swagger/OpenAPI via Swashbuckle
GitHub Actions running dotnet test
```

Do not claim Moq, NSubstitute, WireMock.Net, Testcontainers, Postman collections, SonarQube, or load-testing tools unless the repo actually adds and verifies them.

## Boundary With UI Browser Testing

Do not use this skill as evidence for browser behavior, screenshots, responsive layout, visual review, or Playwright UI flows. Use `ui-browser-testing` for those checks. Use both skills when a .NET/API change also affects browser-visible behavior.

## Test Selection

| Scenario | Preferred test |
| --- | --- |
| service logic without persistence | xUnit service test |
| quota counters, reservation finalization, idempotency keys | EF Core SQLite integration test |
| controller/minimal API validation and HTTP status | `WebApplicationFactory` API test |
| Stripe webhook replay or malformed event | API or service integration test with local JSON payload |
| provider failure, malformed AI output, no-charge path | service/worker test with deterministic fake provider |
| queue redelivery, outbox retry, worker finalization | worker/service test with fake publisher/provider |
| migration/index/unique constraint behavior | EF Core integration or migration smoke test |

## Required Assertions

- Validation/auth failures create no usage, attempt, reservation, or billing side effect.
- Duplicate requests/events do not create duplicate attempts, jobs, counters, or charges.
- Successful rewrite finalizes exactly one reservation and increments used quota once.
- Provider/server/quality failures release or expire quota according to product rules and do not charge.
- Terminal states cannot be overwritten by late success, retry, or redelivery.
- API tests verify response status and persisted database state.

## Commands

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Quota
dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Rewrite
```

Use `npm test -- <path>` only for the Next/TypeScript path. Do not use it as evidence that .NET backend tests pass.

## Resume Evidence Rule

When producing resume bullets or interview notes, say what is proven by the repo:

```text
xUnit, FluentAssertions, ASP.NET Core integration tests, WebApplicationFactory, EF Core SQLite tests, GitHub Actions
```

Only add tools like Moq, Testcontainers, WireMock.Net, or Postman after adding real artifacts and verification evidence.
