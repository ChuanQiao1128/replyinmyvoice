## Context

- Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: `/Users/qc/Desktop/CloudFlare/backend-dotnet` (.NET 8 isolated Azure Functions). Base branch: `delivery/backend-hardening-2` (NOT main). Solution: `backend-dotnet/ReplyInMyVoice.sln`, currently 799 passing tests.
- Wave spec section OBS-EXMW (finding #15): map uncaught exceptions on HTTP triggers to a coded error envelope + correlation id; rethrow for Service Bus / timer triggers so redelivery/retry is preserved.
- Read first (ground every change in these):
  - `backend-dotnet/src/ReplyInMyVoice.Functions/Http/HttpHardeningMiddleware.cs` — the `Invoke(FunctionContext, FunctionExecutionDelegate)` method. HTTP path resolves a correlation id, stores it in `http.Items["CorrelationId"]` and the `X-Correlation-Id` response header, opens an `ILogger` scope, then calls `await next(context)` at line 45 (currently unguarded). The non-HTTP path is the early `if (http is null) { await next(context); return; }` at lines 24-29. `WritePayloadTooLargeAsync` (lines 129-137) is the existing pattern for writing a JSON envelope straight to `http.Response`.
  - `backend-dotnet/src/ReplyInMyVoice.Functions/Http/FunctionHttpResults.cs` — `Problem(title, detail, statusCode, errorCode?)` already emits `{error:{code,message}}`; `DefaultErrorCode(500)` returns `"internal_error"`.
  - `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs` — the ONLY `ServiceBusTrigger` (line 15); it rethrows on bad input (lines 27, 32). Must keep propagating.
  - `backend-dotnet/tests/ReplyInMyVoice.Tests/HttpHardeningMiddlewareTests.cs` — existing test conventions (FluentAssertions, `DefaultHttpContext` factory, `ReadResponseJson`).

## Constraints

- Banned terms (CI grep guard, halt on match): `humanizer | bypass | undetect | detector | evade` — never as a substring in code, names, comments, or test data.
- Do NOT change `IRewriteEngineClient` / `ResultJson` shape / the error-code set (engine is a frozen black box).
- No secret values in tracked files; the 500 envelope MUST NOT leak the raw exception message or stack trace to the client (log details server-side via the injected `ILogger`; return a generic message).
- Keep the existing 799 tests green; every behavioral change adds xUnit tests.
- Touch only the files in scope (<=3). Do NOT edit `Program.cs` (middleware is already wired at line 21).
- Base on `delivery/backend-hardening-2`. The worker must never push, open a PR, or touch `main`.

## Changes required

1. In `HttpHardeningMiddleware.cs`, wrap ONLY the HTTP-trigger `await next(context)` call (the one inside the `using var scope` block, line 45) in a try/catch. On a caught exception:
   - Log the exception at Error level via the injected `ILogger` (the correlation id is already in the logging scope).
   - If `http.Response.HasStarted` is false, write a `500` coded JSON envelope identical in shape to `FunctionHttpResults.Problem(...)` for status 500: `{error:{code:"internal_error",message:<generic, non-leaking>}}`, set `StatusCode = 500`, `ContentType = "application/json"`, and (re)set the `X-Correlation-Id` header. Do NOT include the exception message/stack in the body.
   - If `http.Response.HasStarted` is true, rethrow (cannot safely overwrite a partially-sent response).
   - Do NOT catch on the non-HTTP early-return path (lines 24-29) — leave it exactly as-is so SB/timer exceptions propagate.
2. Add a small static helper for the 500 body to keep it testable and consistent with `BuildPayloadTooLargeJson`, e.g. `HttpHardeningMiddleware.BuildInternalErrorJson(string correlationId)` returning the serialized `{error:{code:"internal_error",message,...}}`. Reuse `FunctionHttpResults.DefaultErrorCode(500)` (= `"internal_error"`) for the code so the constant lives in one place; if a new public helper on `FunctionHttpResults` is cleaner (e.g. `InternalError()`), add it there and mirror the shape in the middleware (the existing `PayloadTooLargeJson_MatchesFunctionHttpResultsCodedEnvelope` test is the precedent for keeping the two in sync).
3. Add tests in `HttpHardeningMiddlewareTests.cs`:
   - HTTP trigger whose `next` throws → response is `500`, `application/json`, body `error.code == "internal_error"`, body does NOT contain the thrown exception's message text, and `X-Correlation-Id` header is present. Drive this through the real `Invoke(...)` with a `FunctionContext` whose `GetHttpContext()` returns a `DefaultHttpContext` and a `FunctionExecutionDelegate` that throws. If constructing `FunctionContext` is impractical, factor the catch logic into an internal/static method (e.g. `WriteInternalErrorAsync(HttpContext, string correlationId, CancellationToken)`) and test that directly, plus assert the envelope shape matches `FunctionHttpResults`.
   - Non-HTTP path: a `FunctionExecutionDelegate` that throws on the `GetHttpContext() is null` branch propagates the exception out of `Invoke` (assert with `await invoke.Should().ThrowAsync<...>()`) and writes no envelope — proving redelivery is preserved.
   - Envelope-shape parity test: `BuildInternalErrorJson(...)` (or the new helper) root has exactly `error` with `code == "internal_error"`, matching `FunctionHttpResults.Problem(... 500 ...)`.

## Acceptance

- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~HttpHardeningMiddlewareTests` passes (HTTP-throw → coded 500 envelope with correlation id; non-HTTP throw propagates; envelope parity).
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` is fully green (>= 799 tests, plus the new ones).
- `grep -RniE "humanizer|bypass|undetect|detector|evade" backend-dotnet/src/ReplyInMyVoice.Functions/Http backend-dotnet/tests/ReplyInMyVoice.Tests/HttpHardeningMiddlewareTests.cs || true` prints nothing.
- `grep -n "catch" backend-dotnet/src/ReplyInMyVoice.Functions/Http/HttpHardeningMiddleware.cs` shows the new catch is on the HTTP path only (inside the `using var scope` block), and the `if (http is null)` early-return path at lines 24-29 has no try/catch around its `next(context)`.

## DO NOT

- Do NOT wrap, swallow, or alter the non-HTTP (`GetHttpContext() is null`) path; SB/timer exceptions must keep propagating (preserve Service Bus redelivery and timer retry).
- Do NOT change `IRewriteEngineClient`, `ResultJson`, the error-code set, or `Program.cs` middleware wiring.
- Do NOT remove per-handler `catch` blocks in the HTTP functions in this issue (boilerplate sweep is separate; the middleware is the net beneath them).
- Do NOT leak the raw exception message or stack trace in the response body. Do NOT add endpoints, migrations, dependencies, or secrets. Do NOT introduce banned terms.
- Do NOT push, open a PR, or touch `main`. Work only on `delivery/backend-hardening-2`.