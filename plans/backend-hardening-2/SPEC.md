# Backend hardening wave 2 — close the remaining system-design findings

Source of findings: `plans/interview/ARCH-OPTIMIZATION-2026-06-14.md` (re-verified against `e751bed`).
Goal: take the remaining code-level architecture findings to **production grade**, fully tested.

## Hard constraints (every issue)

- **Integration branch only**: base = `delivery/backend-hardening-2`, never `main`. One PR per issue.
- **Banned terms** (CI grep guard, halt on match): `humanizer | bypass | undetect | detector | evade`.
- **Secrets**: never print/commit; validate env at runtime in the handler, not at import.
- **Rewrite engine stays a swappable black box**: do NOT change `IRewriteEngineClient` contract / `ResultJson` shape / error-code set (HARD-01 froze it).
- **Tests**: keep the existing **799/799** green; every behavioral change adds/updates xUnit tests. Run `dotnet test backend-dotnet/ReplyInMyVoice.sln` clean.
- **No prod-unsafe migrations**: any EF migration must be additive and safe against existing LIVE Azure SQL data (see ISSUE-DATA-CHECK).

## Issues

### Structural

**STRUCT-01 — Consolidate to a single HTTP host (retire the shadow `ReplyInMyVoice.Api`)** (finding #1, #25)
- Current: `src/ReplyInMyVoice.Api/Program.cs` (~1,736 lines) re-maps the full production endpoint set but only `ReplyInMyVoice.Functions` is deployed (`.github/workflows/dotnet-azure.yml` deploy job ships only Functions). HARD-14 `HttpHardeningMiddleware` is wired only in Functions; Api never got it.
- Change: demote `ReplyInMyVoice.Api` to a **non-published local dev/Swagger harness** (stop publishing it in CI) OR delete it. Move the EF migration `--startup-project` off `Api` onto a design-time factory in `Infrastructure` (closes #25 — CI gate project == deploy migration project). Remove the now-dead publish step.
- Acceptance: CI no longer publishes Api as a deploy artifact; `dotnet ef` migrations run via Infrastructure design-time factory; full test suite green; no production route served by Api.

**STRUCT-02 — Lift duplicated HTTP-glue into the Application layer** (finding #1 remainder)
- Current: identical literal validation/error strings duplicated across `Api/Program.cs` and `Functions/V1RewriteHttpFunctions.cs` (e.g. "A draft of at least 10 characters is required.", codes `rate_limit_unavailable`/`input_too_long`).
- Change: move shared request validation + V1 error/result mapping into a single Application-layer component; both hosts (or the one surviving host) call it. One implementation of each rule.
- Acceptance: the duplicated literals exist in exactly one place; tests pin the validation rules.

**STRUCT-03 — Route V1 inline EF through repositories** (finding #3)
- Current: `Functions/V1RewriteHttpFunctions.cs` injects `AppDbContext` and does inline EF (user lookup, sandbox-attempt create, `ApiKeyUsage` write); `Auth/ApiKeyAuthResolver.cs` reads/writes `AppDbContext` directly. 5 `TODO(DDD)-64` markers.
- Change: route user lookup via `IAppUserRepository.GetByIdAsync`; sandbox create via `IRewriteAttemptRepository`. **Add the missing ports**: `IApiKeyUsageRepository.AddAsync` (currently read-only) and an ApiKey `LastUsedAt` write method. Remove `AppDbContext` injection from the HTTP/auth layer.
- Acceptance: no `AppDbContext` reference in `Functions/` HTTP handlers or `ApiKeyAuthResolver`; behavior preserved; tests green.

### Concurrency / data

**DATA-ROWVERSION — Auto-stamp optimistic-concurrency token** (finding #5)
- Current: 25 entities carry `Guid RowVersion`; stamped by hand in ~63 sites across 32 files; `SaveChangesAsync` override does not stamp; no guard.
- Change: add `IConcurrencyStamped` marker; in the existing `AppDbContext.SaveChangesAsync` override, stamp `RowVersion = Guid.NewGuid()` for every `Modified` entity implementing it; remove the manual assignments. Add an architecture/unit test asserting a Modified stamped entity always gets a fresh RowVersion.
- Acceptance: manual `.RowVersion = Guid.NewGuid()` assignments removed (except raw-SQL paths that must set it inline); new test passes; full suite green.

**DATA-CHECK — DB CHECK constraints for money/quota invariants** (finding #7)
- Current: only `PromoCodes` has CHECK constraints; `RewriteCredits`/`UsagePeriods` have none. Invariants enforced only in C#.
- Change: new EF migration adding `CK_RewriteCredits_Consumed_Range ([AmountConsumed] >= 0 AND [AmountConsumed] <= [AmountGranted])` and `CK_UsagePeriods_Counts_NonNegative ([UsedCount] >= 0 AND [ReservedCount] >= 0)`. **Do NOT** add `UsedCount+ReservedCount <= QuotaLimit` (credit-overflow deliberately exceeds base quota).
- Acceptance: migration is additive and prod-data-safe (verify no existing row violates before merge); applies cleanly on the CI mssql:2022 container; tests cover a violating write being rejected.

**DATA-EXC — Centralized SQL exception classifier** (finding #8)
- Current: `UnitOfWork.IsRetryableDbUpdateRaceException` string-matches `"serialization"/"deadlock"/"3960"/"IX_..."`; no `SqlException.Number` path; tests all SQLite.
- Change: a single `IDbExceptionClassifier` keyed on `SqlException.Number` (1205 deadlock, 2627/2601 unique, 3960 snapshot) + `DbUpdateConcurrencyException` type, with a SQLite fallback. Keep dual-provider support.
- Acceptance: classification no longer depends on hard-coded index names; tests cover both provider signals.

**DATA-DBOPTS — CommandTimeout + pending-migration startup assertion** (finding #9)
- Change: set a sensible `CommandTimeout` (reconciliation paging can exceed 30s default); evaluate `AddDbContextPool`; add a startup assertion via `GetPendingMigrationsAsync` (log/fail fast on drift). Acceptance: timeout configurable; startup logs pending-migration state; suite green.

**DATA-EXPIRY — credit-expiry reminder: claim-before-send** (finding #13)
- Current: `SendCreditExpiryRemindersHandler` sends THEN marks; no leader guard; can double-send on concurrent runs.
- Change: claim-before-send (RowVersion-guarded UPDATE to mark "reminder sent" wins the row before sending) or reuse the outbox `LockedUntil` lease; add a filtered index; add a double-run test asserting at-most-once.
- Acceptance: concurrent double-run sends at most one reminder per credit; test proves it.

### Observability

**OBS-CORR — Propagate correlation id HTTP→Service Bus→Worker** (finding #20)
- Current: `RewriteJob(Guid AttemptId)` has no correlation/traceparent; publisher sets no `CorrelationId`/`ApplicationProperties`; consumers read only AttemptId. The outbox row already has `CorrelationId` but the dispatch handler drops it.
- Change: add `CorrelationId` (+ W3C `traceparent`) to `RewriteJob`; HTTP ingress accepts `X-Correlation-Id` → outbox `CorrelationId` → SB `message.CorrelationId`/`ApplicationProperties["traceparent"]`; consumer restores `Activity` + `ILogger` scope.
- Acceptance: a correlation id set at HTTP ingress appears in Worker logs for the same job; test asserts round-trip.

**OBS-LOG — Structured logging in quota lifecycle handlers** (finding #22)
- Change: inject `ILogger` into the 5 quota handlers (reserve/finalize/release/mark/expire) + webhook ingest; one log line per transition with a reservation+attempt-id scope. Acceptance: each transition logs with scope; no secrets logged.

**OBS-OPTIONS — Validated Options pattern + Key Vault** (finding #23)
- Current: ~111 `configuration[...]` index lookups; no `IOptions`/`Bind`/`ValidateOnStart`; bad values silently clamped; no Key Vault in-app.
- Change: bind provider/rewrite/health knobs into Options classes with DataAnnotations + `ValidateOnStart` (misconfig fails loudly at boot); add `AddAzureKeyVault(DefaultAzureCredential)` continuing HARD-10 managed identity. Acceptance: misconfigured option fails startup with a clear error; tests cover validation.

**OBS-EXMW — Global exception middleware (HTTP triggers only)** (finding #15)
- Change: wrap `next()` in `HttpHardeningMiddleware.Invoke` (or a dedicated middleware) to map uncaught exceptions on **HTTP triggers** to a coded error envelope + correlation id; **rethrow for Service Bus / timer triggers** (preserve redelivery — do NOT swallow). Remove per-handler `catch{throw;}` boilerplate. Acceptance: an uncaught exception on an HTTP endpoint returns a coded envelope, not a raw 500; SB/timer still redeliver; tests cover both.

### API

**API-SCOPE — Enforce or retire `ApiKey.Scope`** (finding #14)
- Current: `ApiKey.Scope` persisted, never read/enforced; only rotate copies it forward.
- Change (decision: ENFORCE): parse the JSON scope array in `ApiKeyAuthResolver`, carry it on `ApiKeyAuthResult`, enforce at the V1 boundary (`V1SubmitRewrite`/`V1GetUsage`), mirroring the shipped `IsTest` pattern; default scope = full so existing keys are unaffected; add negative-path test.
- Acceptance: a key lacking a required scope is rejected at V1; existing keys (default full) unaffected; tests cover allow + deny.

**API-ENVELOPE — Converge the public error envelope** (finding #16)
- Change: public/V1 surface emits one consistent envelope `{error:{code,message,requestId}}`; fold correlation id into the body; keep first-party ProblemDetails but add stable machine `code` everywhere. Acceptance: V1 endpoints share one envelope shape; tests pin it.

**API-401 — 401 taxonomy + `WWW-Authenticate`** (finding #17)
- Change: auth resolver distinguishes NoToken/Expired/Invalid; emit RFC6750 `WWW-Authenticate: Bearer error="invalid_token"` and log the subtype (no token contents). Acceptance: expired vs malformed produce distinct logged subtypes + a `WWW-Authenticate` header; test covers it.

**API-DLQ — Dead-letter poison messages on the live consumer** (finding #11)
- Current: `RewriteJobFunction` throws-to-abandon on unprocessable input (no immediate DLQ); `host.json` has no `maxDeliveryCount`.
- Change: dead-letter unprocessable input immediately with a reason code; set `maxDeliveryCount`. Acceptance: a poison message is dead-lettered (not infinitely redelivered); test covers it.

### Testing

**TEST-JWT — JWT negative-path tests** (finding #19)
- Current: `FunctionAuthResolverTests` inject a ClaimsPrincipal, bypassing real validation; no test pins signature/audience/expiry rejection.
- Change: extract the OIDC `ConfigurationManager` behind an injectable seam; add tests (local RSA + JWKS test double): wrong signing key → reject, wrong audience → reject, expired → reject.
- Acceptance: 3 negative-path tests pass exercising real `ValidateToken`.

**TEST-SQLSERVER — Behavior tests on SQL Server (Testcontainers)** (finding #26)
- Current: 28 `UseSqlite`, 0 `UseSqlServer` behavioral tests; prod is Azure SQL.
- Change: add a trait-gated Testcontainers (`mssql`) layer re-running the 4 highest-risk suites (quota race, idempotency UNIQUE, webhook replay, RowVersion concurrency). SQLite stays the fast inner loop.
- Acceptance: the 4 suites pass on real SQL Server in CI under a trait; default fast run unaffected.

**TEST-TIME — Deterministic retry timing test** (finding #29)
- Change: thread the existing `TimeProvider` into `ProviderHttpResilienceHandler`; replace wall-clock `BeOnOrAfter(...AddMilliseconds(500))` assertions with direct RetryDelay assertions. Acceptance: no wall-clock timing assertion remains in the resilience tests.

## Out of scope (owner decision — not code-fixable here)

- **#24 Blue-green / staging-slot deploy + rollback automation**: Consumption Linux Functions don't support slots; needs Premium/EP **paid** plan (`AZURE_ALLOW_PAID_RESOURCES`) + dashboard. Documented, not auto-shipped.
- **#18 API `Sunset`/`Deprecation` headers**: low value until a v2 exists; optional follow-up.

## Merge-to-prod sequencing (after integration branch is verified)

1. Review every PR on `delivery/backend-hardening-2`; full suite green on the integration branch.
2. Validate DATA-CHECK migration against LIVE Azure SQL data (no existing violations) BEFORE merge.
3. Merge integration → main in a controlled push; watch CI build-test + sqlserver-migration + deploy; verify `/api/health` + `/api/version` (commitSha) green; `wrangler rollback` ready.
