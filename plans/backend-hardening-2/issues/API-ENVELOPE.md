## Context

Repo root: `/Users/qc/Desktop/CloudFlare`. Backend: .NET 8 Azure Functions under `backend-dotnet/`. Solution: `backend-dotnet/ReplyInMyVoice.sln` (799 tests currently green). Base branch: `delivery/backend-hardening-2` (NEVER `main`).

Issue API-ENVELOPE (finding #16): the V1 public API error envelope is `{error:{code,message}}` and lacks a `requestId`, so callers cannot correlate failures with logs. A correlation id already exists.

Read these first to ground every edit:
- `backend-dotnet/src/ReplyInMyVoice.Functions/Http/FunctionHttpResults.cs` — `Problem(...)`: coded branch (`:16-29`) builds `new { error = new { code, message } }`; ProblemDetails branch (`:31-40`) sets `Extensions["code"]`.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` — V1 coded errors via local `Error(...)` (`:249-250`) and direct `FunctionHttpResults.Problem(...)` (`:288,:306,:324,:350,:394,:412,:429,:471`); `MapRewriteResult` failed branch (`:538-547`). The pre-existing `requestId` param on `CompleteWithUsageAsync` (`:255,:681,:725`) is the ApiKeyUsage attempt id — a DIFFERENT concept, leave it alone.
- `backend-dotnet/src/ReplyInMyVoice.Functions/Http/HttpHardeningMiddleware.cs` — sets `http.Items["CorrelationId"]` (`:32`), echoes `X-Correlation-Id` header (`:33`); `public static string ResolveCorrelationId(HttpRequest)` (`:48`). This is the canonical request id source.
- Tests to extend: `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionHttpResultsTests.cs` (`:10-31` pins the coded envelope), `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiInputHardeningTests.cs` (`AssertContractError` `:338-353`, `CreateV1Request` `:308-327`), `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs` (failed-branch assertions `:562-564`).

## Constraints

- Banned terms anywhere (CI grep, halt on match): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change the `IRewriteEngineClient` contract, `ResultJson` shape, or the error-code SET (HARD-01 frozen). This change only ADDS a `requestId` field.
- Do NOT change the first-party ProblemDetails branch (`FunctionHttpResults.cs:31-40`) or its `Extensions["code"]` contract.
- The new `requestId` must default to omitted so the other 7 `FunctionHttpResults.Problem` callers and their existing tests stay byte-identical.
- No secret values in tracked files; validate env at runtime in the handler (no new env needed here).
- Keep all 799 existing tests green; add the new assertions. Worker must NEVER push, open a PR, or touch `main`.
- Scope: the 5 files listed under "Changes required" only.

## Changes required

1. `FunctionHttpResults.cs` — in the coded branch of `Problem(...)` (`:16-29`), add a new optional parameter `string? requestId = null`. When `requestId` is non-empty, emit `new { error = new { code, message, requestId } }`; when null/empty, keep emitting `new { error = new { code, message } }` exactly as today. Do not alter the ProblemDetails branch.
2. `V1RewriteHttpFunctions.cs` — add a private helper to resolve the envelope correlation id from the request, e.g. `ResolveRequestId(HttpRequest request)` that returns `request.HttpContext.Items["CorrelationId"] as string` when present and non-empty, else `HttpHardeningMiddleware.ResolveCorrelationId(request)` (so it works both behind the middleware and in direct-invocation unit tests). This is SEPARATE from the existing `ApiKeyUsage` `requestId` plumbing — do not merge them.
3. `V1RewriteHttpFunctions.cs` — thread the resolved request id into every V1 coded-error emission: update the local `Error(string code, string message, int statusCode)` helper (`:249-250`) to pass `requestId` into `FunctionHttpResults.Problem(...)`, and update the direct `FunctionHttpResults.Problem(...)` calls in `GetRewriteResult` and `GetUsage` (`:288,:306,:324,:350,:394,:412,:429,:471`) to pass the resolved request id. Resolve the request id once near the top of each of the three function methods (`SubmitRewrite`, `GetRewriteResult`, `GetUsage`).
4. `V1RewriteHttpFunctions.cs` — in `MapRewriteResult` (`:538-547`, the `failed` branch), include `requestId` inside the `error` object so the terminal `v1/rewrite/{id}` failed body matches the envelope. Pass the resolved request id into `MapRewriteResult` from its caller in `GetRewriteResult`.
5. `FunctionHttpResultsTests.cs` — add a test: `Problem` with a `requestId` emits `error.{code,message,requestId}` (root has only `error`); and a test that without `requestId` the shape is unchanged `error.{code,message}`.
6. `ApiInputHardeningTests.cs` — extend `AssertContractError` (`:338-353`) to also assert `error.requestId` is present and non-empty for V1 errors; have `CreateV1Request` (`:308-327`) seed `context.Items["CorrelationId"] = "<known-id>"` (or set the `X-Correlation-Id` request header) so the assertion can check the returned `requestId` equals the known id.
7. `RewriteApiTests.cs` — in the existing V1 `failed`-result assertions (`:562-564` and the expired/provider-failure cases that follow), additionally assert the failed body's `error.requestId` is present and non-empty and matches the `X-Correlation-Id` response header.

## Acceptance

- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~FunctionHttpResultsTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiInputHardeningTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~RewriteApiTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`
- `grep -RniE "humanizer|bypass|undetect|detector|evade" backend-dotnet/src/ReplyInMyVoice.Functions/Http/FunctionHttpResults.cs backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs || true`

## DO NOT

- Do NOT change the first-party ProblemDetails branch or its `Extensions["code"]` contract.
- Do NOT rename/remove any error code or change the `IRewriteEngineClient`/`ResultJson` contract (HARD-01).
- Do NOT modify the other 7 `FunctionHttpResults.Problem` callers (Account/ApiKey/ApiUsage/Stripe/Rewrite/Billing/Admin) — the new arg defaults to omitted; their output must stay identical.
- Do NOT repurpose the existing `ApiKeyUsage.RequestId` plumbing for the envelope request id.
- Do NOT touch files outside the 5 in scope. Do NOT push, open a PR, or touch `main`. Do NOT print/commit secret values.