# DDD-64: Shell remaining RewriteHttpFunctions + V1RewriteHttpFunctions onto Application handlers (strangler replace)

## Context
Per docs/ddd-migration-playbook.md, complete the strangler replace for the two rewrite function
classes. `RewriteHttpFunctions.cs` already has `CreateRewriteAttempt` and `GetRewriteAttempt`
shelled (Wave 1 / DDD-21); this issue covers the remaining three methods in that file
(`ListMyRewriteAttempts`, `GetMyRewriteAttempt`, `DeleteMyRewriteAttempt`) which still use inline
`db` (AppDbContext) and `accountService`. It also covers `V1RewriteHttpFunctions.cs` (772 lines),
which calls `db`, `accountService`, and `rewriteRequestService` inline.
Behaviour is unchanged; existing tests stay green with assertions unmodified.
Mirror the already-shelled Functions/RewriteHttpFunctions.cs (create/get).

**Size note:** V1RewriteHttpFunctions.cs is 772 lines and contains substantial inline logic
(sandbox-attempt creation, rate-limit header helpers, usage-write helpers) in addition to the two
primary `[Function]` endpoints (`V1SubmitRewrite`, `V1GetRewriteResult`) and one usage endpoint
(`V1GetUsage`). Shell all three `[Function]` endpoints. If any private helper method still has
inline `db` access after the endpoint swap, leave it or refactor it to avoid breaking the build;
add a `// TODO(DDD): remaining V1 inline db — DDD-64` comment rather than leaving the build red.

Read first:
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`
- Application/UseCases/Rewrite/GetRewriteAttemptQuery.cs + GetRewriteAttemptHandler.cs
  (already shelled; study the pattern) — via `git show origin/delivery/ddd-restructure:...`
- Application/UseCases/Account/FindUserQuery.cs + FindUserHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/HasPaidApiEntitlementQuery.cs + HasPaidApiEntitlementHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/GetOrCreateUserCommand.cs + GetOrCreateUserHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- Application/UseCases/Account/GetAccountSummaryQuery.cs + GetAccountSummaryHandler.cs
  (via `git show origin/delivery/ddd-restructure:...`)
- `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteHistoryTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/V1RewriteRateLimitTests.cs`
- `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteRequestServiceTests.cs`

## Constraints
- Inject the Application handlers via DI (already registered in Wave 2). In each migrated method,
  build the Command/Query and call the handler; remove inline DbContext queries on those paths.
- Behaviour/response contract UNCHANGED. Do NOT modify any test assertions.
- KEEP the old service classes (`AccountService`, `RewriteRequestService`) and their DI
  registration. Remove a constructor param only if no remaining method in the file uses it
  (note: `CreateRewriteAttempt` and `GetRewriteAttempt` in `RewriteHttpFunctions` have already
  been shelled; do not regress them).
- No DB schema change / no new EF migration. TFM net8.0, no new NuGet.
- No banned terms (humanizer|bypass|undetect|detector|evade).

## Changes required

### RewriteHttpFunctions.cs — remaining three methods

Current fields: `AppDbContext db`, `AccountService accountService` (plus already-migrated handlers)

1. `ListMyRewriteAttempts` (`GET /me/rewrites`): currently calls `accountService.FindUserAsync(...)` then
   performs an inline `db.RewriteAttempts` paginated query. Replace with:
   - `FindUserHandler` called with `new FindUserQuery(authUser.ExternalAuthUserId)` to get the user id;
   - an Application handler for listing rewrite history if one exists (check
     Application/UseCases/Rewrite for a list/history handler); if no such handler exists in Wave 2,
     leave the inline `db` query in place for this method only and add a
     `// TODO(DDD): no list-history handler yet — DDD-64` comment.
2. `GetMyRewriteAttempt` (`GET /me/rewrites/{attemptId}`): currently calls `accountService.FindUserAsync`
   then inline `db.RewriteAttempts`. Replace the `FindUserAsync` call with `FindUserHandler`.
   For the rewrite-attempt lookup, if a targeted Application handler exists use it; otherwise leave
   the inline `db` query in place with the same TODO comment style.
3. `DeleteMyRewriteAttempt` (`DELETE /me/rewrites/{attemptId}`): same pattern — replace
   `accountService.FindUserAsync` with `FindUserHandler`; keep inline `db` soft-delete only if no
   Application handler covers it yet.
4. Remove `AppDbContext db` from the constructor if it is no longer referenced by any method after
   the migrations above; otherwise retain it.

### V1RewriteHttpFunctions.cs — three [Function] endpoints

Current constructor: `AppDbContext db, IApiKeyRateLimiter rateLimiter,
  AccountService accountService, RewriteRequestService rewriteRequestService`

The file contains substantial private helpers (sandbox attempt creation, usage write, rate-limit
headers). Those helpers reference `db` directly. Shell the three `[Function]` methods first; the
helpers may still call `db` internally — that is acceptable as long as the build stays green.

1. `V1SubmitRewrite` (`POST /v1/rewrite`): This method contains inline `db.AppUsers` lookup and calls
   `accountService.HasPaidApiEntitlementAsync(...)` and `AccountService.GetUsagePlan(...)` and
   `rewriteRequestService.CreateAttemptAsync(...)`. Replace:
   - the inline `db.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == auth.UserId.Value, ...)` user lookup
     with — if no Application handler exists for resolving a user by internal id, leave it in place
     with a TODO comment;
   - `accountService.HasPaidApiEntitlementAsync(user.Id, now, ...)` with
     `HasPaidApiEntitlementHandler` called with `new HasPaidApiEntitlementQuery(user.Id, now)`;
   - `AccountService.GetUsagePlan(user, configuration)` is a static helper — keep calling it as-is;
   - `rewriteRequestService.CreateAttemptAsync(...)` with `CreateRewriteAttemptHandler` called with
     `new CreateRewriteAttemptCommand(user.Id, idempotencyKey, rewriteRequest, plan.PeriodKey, plan.QuotaLimit, now, auth.ApiKeyId)`.
   The sandbox path (`auth.IsTest == true`) currently calls a private helper `CreateSandboxAttemptAsync`
   which uses `db` directly. If an Application handler for sandbox attempts does not exist in Wave 2,
   leave the sandbox path unchanged and mark it with a TODO comment.
2. `V1GetRewriteResult` (`GET /v1/rewrite/{id}`): currently calls `db.RewriteAttempts` directly.
   Replace with `GetRewriteAttemptHandler` called with `new GetRewriteAttemptQuery(id, auth.UserId.Value)`.
   The existing sandbox-vs-live check via `IsV1SandboxAttempt(attempt)` is view logic — keep it
   after the handler returns the attempt.
3. `V1GetUsage` (`GET /v1/usage`): currently calls `accountService.GetOrCreateAccountSummaryAsync(...)`.
   Replace with `GetAccountSummaryHandler` called with
   `new GetAccountSummaryQuery(user.ExternalAuthUserId, user.Email)`.
   The inline `db.AppUsers` lookup to resolve `user.ExternalAuthUserId` from `auth.UserId` must
   remain unless an Application handler for "get user by internal id" exists.
4. Adjust constructor: add `HasPaidApiEntitlementHandler`, `CreateRewriteAttemptHandler`,
   `GetRewriteAttemptHandler`, `GetAccountSummaryHandler`; retain `AppDbContext db` if private
   helpers still use it (sandbox creation, usage write); drop `AccountService` /
   `RewriteRequestService` only if no method or helper in the class still references them.

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~RewriteApiTests|FullyQualifiedName~RewriteHistoryTests|FullyQualifiedName~V1RewriteRateLimitTests|FullyQualifiedName~RewriteRequestServiceTests"` exits 0
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0

## DO NOT
- Do NOT change test assertions — behaviour is unchanged.
- Do NOT delete the old service classes or their DI registration (later cleanup wave).
- Do NOT change DB schema or add EF migrations.
- Do NOT add banned terms (humanizer|bypass|undetect|detector|evade) or use @ts-ignore/eslint-disable or gut tests.
- Do NOT push, open a PR, or touch main.
