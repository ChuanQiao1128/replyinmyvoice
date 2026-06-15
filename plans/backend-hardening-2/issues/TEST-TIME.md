## Context

- Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: `backend-dotnet` (.NET 8 / Azure Functions). Base branch = `delivery/backend-hardening-2` (never `main`).
- Wave spec (read your section): `plans/backend-hardening-2/SPEC.md` → **TEST-TIME** (finding #29). Goal: make retry timing deterministic by threading the existing BCL `TimeProvider` into the resilience handler and deleting wall-clock timing assertions.
- Read first (anchors):
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Resilience/ProviderHttpResilienceHandler.cs` — retry loop; `await Task.Delay(delay, cancellationToken)` at **lines 43 and 53**; `RetryDelay(...)` at lines 65-85; `DateTimeOffset.UtcNow` at **line 75** (Retry-After http-date math); ctor is a primary ctor `ProviderHttpResilienceHandler(ProviderCircuitBreaker circuitBreaker)` at line 5.
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/CheckoutVelocityLimiter.cs` — **the pattern to copy**: takes `System.TimeProvider` via ctor (lines 14, 24), defaults to `TimeProvider.System` (line 19-22), reads now via `timeProvider.GetUtcNow()` (line 45).
  - `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs:384-394` — the **only** handler construction site (`AddResilientProviderHttpClient` → `new ProviderHttpResilienceHandler(...)` at lines 390-391).
  - `backend-dotnet/tests/ReplyInMyVoice.Tests/ProviderHttpResilienceHandlerTests.cs:173-205` — `Retry_after_header_still_honored_when_circuit_closed`; wall-clock assertion at **line 204**. All `new ProviderHttpResilienceHandler(breaker)` call sites (lines 28,55,82,108,109,132,164,198) plus `CreateInvoker` helper (line 227).
  - `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteProviderAdapterTests.cs:61-115` — `OpenAiCompatibleRewriteModelClient_retries_429_response_through_registered_named_http_client`; wall-clock assertion at **line 114**; goes through real DI (`AddReplyInMyVoiceInfrastructure`) + a named HttpClient handler.
- Note: `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) is NOT yet referenced by `backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj`, and `TimeProvider` is NOT registered in DI. `FakeTimeProvider.Advance(...)` completes pending `TimeProvider.Delay(...)` tasks, which is what makes the retry wait resolve instantly in tests.

## Constraints

- Banned terms anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient` / `IRewriteModelClient` / `IWritingSignalClient` contracts, `ResultJson` shape, or the error-code set (engine is a frozen swappable black box).
- Keep retry count, base/max backoff (250 ms / 5 s), jitter, and transient classification behavior identical — only the *clock/delay source* changes.
- Keep the existing 799 tests green; ADD deterministic tests. No secret values in tracked files.
- Worker must NEVER push, open a PR, or touch `main`. Work on `delivery/backend-hardening-2`.
- ≤8 files; stay within the scope list.

## Changes required

1. `ProviderHttpResilienceHandler.cs`: add a `TimeProvider` to the handler. Convert the primary ctor to two ctors mirroring `CheckoutVelocityLimiter`: `ProviderHttpResilienceHandler(ProviderCircuitBreaker circuitBreaker) : this(circuitBreaker, TimeProvider.System)` and `ProviderHttpResilienceHandler(ProviderCircuitBreaker circuitBreaker, TimeProvider timeProvider)` (null-check the time provider). Store the field.
2. In `SendAsync`, replace both `await Task.Delay(delay, cancellationToken)` (lines 43, 53) with `await timeProvider.Delay(delay, cancellationToken)` (BCL extension on `TimeProvider`).
3. In `RetryDelay`, make it an instance method (or pass the provider) and replace `DateTimeOffset.UtcNow` (line 75) with `timeProvider.GetUtcNow()` so the Retry-After http-date path is deterministic.
4. `ServiceCollectionExtensions.cs:390-391`: keep the single call site compiling. Simplest: leave it as `new ProviderHttpResilienceHandler(registry.GetOrAdd(name))` (uses the `TimeProvider.System` default) — do NOT register `TimeProvider` in DI unless needed.
5. Test plumbing: add the test-only seam. Either add `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="8.*" />` to `tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj` and use `Microsoft.Extensions.Time.Testing.FakeTimeProvider`, OR add a minimal in-repo `sealed class FakeTimeProvider : TimeProvider` test double under `tests/ReplyInMyVoice.Tests/TestDoubles/` that overrides `GetUtcNow()` and timer creation so `Delay` resolves on `Advance`.
6. `ProviderHttpResilienceHandlerTests.cs`: update `CreateInvoker`/`CreateBreaker` helpers (or add an overload) so a test can pass a `FakeTimeProvider` into the handler. Rewrite `Retry_after_header_still_honored_when_circuit_closed` to inject the fake time provider, drive the retry on a background task, `Advance` time, await, and assert `innerHandler.InvocationCount == 2` and that the response is `OK` — DELETE the `attemptTimes[1].Should().BeOnOrAfter(...)` wall-clock assertion (line 204) and the `attemptTimes`/`DateTimeOffset.UtcNow` tracking. ADD a focused unit test that asserts the computed `RetryDelay` directly (e.g. a `Retry-After: 500ms` header → next attempt fires only after advancing ≥500 ms, and `< 500 ms` advance leaves `InvocationCount == 1`).
7. `RewriteProviderAdapterTests.cs`: in `OpenAiCompatibleRewriteModelClient_retries_429_response_through_registered_named_http_client`, remove the wall-clock assertion (line 114) and the `attemptTimes` list. Assert deterministically that the second attempt only happens after time advances past the `Retry-After` (or simplify the `Retry-After` to a small value and assert `attemptCount == 2` + success without sleeping a real 500 ms). If injecting the fake provider through full DI is awkward, prefer asserting attempt count + success and rely on the new direct-`RetryDelay` unit test in `ProviderHttpResilienceHandlerTests` for the timing guarantee.

## Acceptance

- `cd backend-dotnet && ! grep -rnE "BeOnOrAfter\(.*AddMilliseconds|BeOnOrAfter\(.*AddSeconds" tests/ReplyInMyVoice.Tests/ProviderHttpResilienceHandlerTests.cs tests/ReplyInMyVoice.Tests/RewriteProviderAdapterTests.cs`
- `cd backend-dotnet && ! grep -nE "Task\.Delay|DateTimeOffset\.UtcNow" src/ReplyInMyVoice.Infrastructure/Resilience/ProviderHttpResilienceHandler.cs`
- `cd backend-dotnet && grep -n "TimeProvider" src/ReplyInMyVoice.Infrastructure/Resilience/ProviderHttpResilienceHandler.cs`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter "FullyQualifiedName~ProviderHttpResilienceHandlerTests|FullyQualifiedName~RewriteProviderAdapterTests"`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `cd backend-dotnet && ! grep -rniE "humanizer|bypass|undetect|detector|evade" src/ReplyInMyVoice.Infrastructure/Resilience/ProviderHttpResilienceHandler.cs tests/ReplyInMyVoice.Tests/ProviderHttpResilienceHandlerTests.cs tests/ReplyInMyVoice.Tests/RewriteProviderAdapterTests.cs`

## DO NOT

- Do NOT change retry count, backoff bounds, jitter, transient classification, or the circuit-breaker contract/behavior.
- Do NOT change `IRewriteEngineClient` / `IRewriteModelClient` / `IWritingSignalClient`, `ResultJson` shape, or error codes.
- Do NOT register `TimeProvider` globally in DI unless strictly required; prefer the `TimeProvider.System` ctor default at the single call site.
- Do NOT introduce banned substrings (`humanizer|bypass|undetect|detector|evade`) or commit secret values.
- Do NOT touch files outside the scope list; do NOT modify other hardening-wave code.
- Do NOT push, open a PR, or merge to `main`; base on `delivery/backend-hardening-2` only.