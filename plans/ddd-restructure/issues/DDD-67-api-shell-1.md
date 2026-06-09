# DDD-67: Shell Api/Program.cs — rewrite + account + me + usage endpoints (part 1, strangler replace)

## Context
Per docs/ddd-migration-playbook.md, switch the Minimal-API routes in `ReplyInMyVoice.Api/Program.cs`
(1559 lines, split across two issues) from calling the old Infrastructure services inline to
invoking the Wave-2 Application handlers. This issue covers the following endpoint groups ONLY:

- `/api/me` (GET) — account summary
- `/api/me/payments` (GET) — purchase history
- `/api/me/billing/history` (GET) — billing history
- `/api/rewrite` (POST) — create rewrite attempt
- `/api/rewrite-attempts/{attemptId}` (GET) — get rewrite attempt by id
- `/api/v1/rewrite` (POST) — V1 submit rewrite
- `/api/v1/rewrite/{id}` (GET) — V1 get rewrite result
- `/api/v1/usage` (GET) — V1 get usage

**DDD-68 owns the remaining endpoint groups** (`/api/promo/*`, `/api/stripe/*`, `/api/admin` if
any). Do NOT touch those routes in this issue to avoid a collision.

Behaviour is unchanged; existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` (the whole file — study the helper functions
  at the bottom: `ResolveExternalUserId`, `ResolveRequestEmail`, `ResolveApiKeyAuthAsync`,
  `TryWriteV1ApiKeyUsageAsync`, etc.)
- Application/UseCases/Account/GetAccountSummaryQuery.cs + GetAccountSummaryHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/GetPurchaseHistoryQuery.cs + GetPurchaseHistoryHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/GetBillingHistoryQuery.cs + GetBillingHistoryHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Rewrite/CreateRewriteAttemptCommand.cs + CreateRewriteAttemptHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Rewrite/GetRewriteAttemptQuery.cs + GetRewriteAttemptHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/GetOrCreateUserCommand.cs + GetOrCreateUserHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/HasPaidApiEntitlementQuery.cs + HasPaidApiEntitlementHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteRequestServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/V1RewriteRateLimitTests.cs`

## Constraints
- In Program.cs Minimal API, handlers are resolved from DI via parameter injection in the route
  lambda or via `app.Services` during startup. Use parameter injection in the lambda — add the
  handler as a parameter alongside the existing `HttpRequest httpRequest` / `CancellationToken`.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP `AccountService`, `RewriteRequestService` registrations. The V1 route has complex
  inline helpers (`ResolveApiKeyAuthAsync`, `CreateV1SandboxAttemptAsync`, `TryWriteV1ApiKeyUsageAsync`,
  `SetV1RateLimitHeaders`) that use `AppDbContext` directly. Replace only the Application-level
  calls (quota check, create attempt, get attempt, usage lookup) in the three V1 lambdas. Leave
  the inline `AppDbContext`-backed helpers (`ResolveApiKeyAuthAsync`, `TryWriteV1ApiKeyUsageAsync`,
  `CreateV1SandboxAttemptAsync`) unchanged for now and mark them with
  `// TODO(DDD): still uses inline db — DDD-67/68`.
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required (endpoint by endpoint)

1. `GET /api/me`: replace `accountService.GetOrCreateAccountSummaryAsync(externalUserId, email, ...)` with
   `GetAccountSummaryHandler` called with `new GetAccountSummaryQuery(externalUserId, email)`.
   Add `GetAccountSummaryHandler` as a lambda parameter.

2. `GET /api/me/payments`: replace `accountService.GetPurchaseHistoryAsync(externalUserId, email, ...)` with
   `GetPurchaseHistoryHandler` called with `new GetPurchaseHistoryQuery(externalUserId, email)`.
   Add `GetPurchaseHistoryHandler` as a lambda parameter.

3. `GET /api/me/billing/history`: replace `accountService.GetBillingHistoryAsync(externalUserId, email, ...)` with
   `GetBillingHistoryHandler` called with `new GetBillingHistoryQuery(externalUserId, email)`.
   Add `GetBillingHistoryHandler` as a lambda parameter.

4. `POST /api/rewrite`: replace `accountService.GetOrCreateUserAsync(...)` + `AccountService.GetUsagePlan(user, ...)` + `rewriteRequestService.CreateAttemptAsync(...)` with:
   - `GetOrCreateUserHandler` called with `new GetOrCreateUserCommand(externalUserId, email)` for the user;
   - `AccountService.GetUsagePlan(user, configuration)` is a static helper — keep it;
   - `CreateRewriteAttemptHandler` called with the appropriate `CreateRewriteAttemptCommand`.
   Add `GetOrCreateUserHandler`, `CreateRewriteAttemptHandler` as lambda parameters.

5. `GET /api/rewrite-attempts/{attemptId}`: replace the inline `db.AppUsers.SingleOrDefaultAsync` +
   `db.RewriteAttempts.AsNoTracking().SingleOrDefaultAsync` with
   `GetRewriteAttemptHandler` called with `new GetRewriteAttemptQuery(attemptId, externalUserId)`.
   If the handler returns a result keyed by externalUserId (not internal user id), adjust accordingly.
   Add `GetRewriteAttemptHandler` as a lambda parameter.

6. `POST /api/v1/rewrite`: the lambda currently calls `db.AppUsers` (inline), `accountService.HasPaidApiEntitlementAsync`,
   and `rewriteRequestService.CreateAttemptAsync`. Replace:
   - `accountService.HasPaidApiEntitlementAsync(user.Id, now, ...)` with
     `HasPaidApiEntitlementHandler` / `new HasPaidApiEntitlementQuery(user.Id, now)`;
   - `rewriteRequestService.CreateAttemptAsync(...)` with
     `CreateRewriteAttemptHandler` / appropriate command;
   - Leave `ResolveApiKeyAuthAsync` and sandbox path unchanged (TODO comment as noted above).
   Add `HasPaidApiEntitlementHandler`, `CreateRewriteAttemptHandler` as lambda parameters.

7. `GET /api/v1/rewrite/{id}`: replace `db.RewriteAttempts.AsNoTracking().SingleOrDefaultAsync(...)` with
   `GetRewriteAttemptHandler` / `new GetRewriteAttemptQuery(id, auth.UserId.Value)`.
   Keep `IsV1SandboxAttempt` check and `MapV1RewriteResult` helper unchanged.
   Add `GetRewriteAttemptHandler` as a lambda parameter.

8. `GET /api/v1/usage`: replace `accountService.GetOrCreateAccountSummaryAsync(user.ExternalAuthUserId, user.Email, ...)` with
   `GetAccountSummaryHandler` / `new GetAccountSummaryQuery(user.ExternalAuthUserId, user.Email)`.
   The `db.AppUsers` lookup to get `user.ExternalAuthUserId` from `auth.UserId` stays unchanged
   (TODO comment). Add `GetAccountSummaryHandler` as a lambda parameter.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~RewriteApiTests|FullyQualifiedName~RewriteRequestServiceTests|FullyQualifiedName~AccountServiceTests|FullyQualifiedName~V1RewriteRateLimitTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT touch the promo, stripe, or billing endpoints in Program.cs — those belong to DDD-68.
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
