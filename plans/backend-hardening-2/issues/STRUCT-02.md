## Context

You are refactoring duplicated V1 public-API HTTP-glue in a .NET 8 backend at `/Users/qc/Desktop/CloudFlare/backend-dotnet`. Repo root: `/Users/qc/Desktop/CloudFlare`. Wave spec: `plans/backend-hardening-2/SPEC.md` (issue STRUCT-02). Read these first to ground every change:

- `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` — the LIVE host. V1 validation constants at lines 36-39; duplicated literals/codes at lines 88, 95, 110, 118, 125, 148; `CountWords` at lines 755-778.
- `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` — the shadow host. Same constants at lines 32-35; same literals at lines 277/309/324/338/375/543/634; same `CountWords` at lines 1453-1479.
- `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/` — destination folder; sibling examples `CreateRewriteAttemptHandler.cs`, namespace `ReplyInMyVoice.Application.UseCases.Rewrite`.
- `backend-dotnet/src/ReplyInMyVoice.Application/Common/ApplicationResultKind.cs` — enum used by the result mapping.
- `backend-dotnet/tests/ReplyInMyVoice.Tests/ApiInputHardeningTests.cs` — existing boundary contract test against the Functions host (`InvalidRewriteInputCases`); must keep passing unchanged.

`ReplyInMyVoice.Application.csproj` references only `Domain`, so it is host-agnostic — the correct home for shared validation. Both hosts already reference Application.

## Constraints

- Base branch = `delivery/backend-hardening-2`. NEVER push, open a PR, or touch `main`.
- Banned substrings anywhere (CI grep halts): `humanizer | bypass | undetect | detector | evade`.
- Do NOT change `IRewriteEngineClient`, `ResultJson` shape, or any wire-level code/message/status string (HARD-01 froze the envelope). This is a pure extract-and-reuse refactor: identical bytes on the wire from both hosts.
- Keep the existing 799 tests green; add new tests. No secret values in tracked files.
- Application layer must stay host-agnostic: the new files may reference only `Domain`/BCL — no `Microsoft.AspNetCore.*`, no `IActionResult`, no `AppDbContext`. Return plain result records/enums; each host maps them to its own `IActionResult`/`IResult`.
- Out of scope: inline-EF→repository routing (STRUCT-03) and Api retirement (STRUCT-01). Do not touch `AppDbContext` usage or rate-limit/sandbox/usage-write logic.

## Changes required

1. Create `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/V1ErrorCatalog.cs` (namespace `ReplyInMyVoice.Application.UseCases.Rewrite`): the single owner of every V1 error code + user-facing message as `const string` / static readonly records. Include exactly the existing strings: `invalid_request`+"Request body must be valid JSON.", `invalid_request`+"A draft of at least 10 characters is required.", `input_too_long`+"Draft must be 300 words or fewer and no more than 2400 characters.", `invalid_request`+"Idempotency-Key must be 120 characters or fewer.", and the `rate_limit_unavailable`/`rate_limited`/`quota_exhausted`/`api_requires_paid_plan`/`idempotency_conflict`/`invalid_key`/`rewrite_failed` code+message pairs already present in the two hosts. Expose a small record like `V1Error(string Code, string Message, int StatusCode)`.
2. Create `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/V1RewriteValidation.cs`: the single owner of the bound constants (`MinimumDraftLength=10`, `MaximumDraftWords=300`, `MaximumDraftCharacters=2400`, `MaximumIdempotencyKeyLength=120`), the `CountWords` helper (move it here, internal/public static), and a `ValidateDraft(string? rawDraft)` / `ValidateIdempotencyKey(string? key)` method returning either the trimmed value or the matching `V1Error` from the catalog. Preserve exact semantics: trim draft; reject null/whitespace/`<10`; reject `>2400` chars OR `>300` words; reject idempotency key `>120` chars.
3. Edit `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`: delete the local `MinimumDraftLength`/`MaximumDraftWords`/`MaximumDraftCharacters`/`MaximumIdempotencyKeyLength` constants and the private `CountWords`; replace the inline validation blocks and `Error(...)` literal call sites with calls into `V1RewriteValidation` + `V1ErrorCatalog`, mapping the returned `V1Error` to `FunctionHttpResults.Problem(...)` exactly as today (same code/message/status). Do not change rate-limit/sandbox/usage paths.
4. Edit `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`: delete `V1MinimumDraftLength`/`V1MaximumDraftWords`/`V1MaximumDraftCharacters`/`V1MaximumIdempotencyKeyLength` and the top-level `CountWords`; route its V1 validation + `V1Error(...)` literal call sites through the same Application component, mapping to its `Results.*`/`V1Error` as today. (If STRUCT-01 already removed Api, skip this file — but as of this brief it still exists.)
5. Create `backend-dotnet/tests/ReplyInMyVoice.Tests/V1RewriteValidationTests.cs`: xUnit `[Theory]` pinning the rules directly against `V1RewriteValidation`/`V1ErrorCatalog` — empty/whitespace draft → `invalid_request`; 9-char draft → `invalid_request`; 2401-char draft → `input_too_long`; 301-word draft → `input_too_long`; 121-char idempotency key → `invalid_request`; a valid 10+ char draft → accepted; assert each returned message equals the catalog constant. Mirror the boundary values already in `ApiInputHardeningTests.InvalidRewriteInputCases`.

## Acceptance

- `cd backend-dotnet && test "$(grep -rl 'A draft of at least 10 characters is required.' src | wc -l | tr -d ' ')" = "1"`
- `cd backend-dotnet && test "$(grep -rl 'Draft must be 300 words or fewer and no more than 2400 characters.' src | wc -l | tr -d ' ')" = "1"`
- `cd backend-dotnet && grep -q 'A draft of at least 10 characters is required.' src/ReplyInMyVoice.Application/UseCases/Rewrite/V1ErrorCatalog.cs`
- `cd backend-dotnet && ! grep -nE 'MaximumDraftWords *= *300|MaximumDraftCharacters *= *2400|V1MaximumDraftWords|V1MaximumDraftCharacters' src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs src/ReplyInMyVoice.Api/Program.cs`
- `cd backend-dotnet && ! grep -rniE 'humanizer|bypass|undetect|detector|evade' src/ReplyInMyVoice.Application/UseCases/Rewrite/V1RewriteValidation.cs src/ReplyInMyVoice.Application/UseCases/Rewrite/V1ErrorCatalog.cs`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~V1RewriteValidationTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiInputHardeningTests`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release`

## DO NOT

- Do NOT change the `IRewriteEngineClient` contract, `ResultJson` shape, or any V1 wire code/message/status — refactor only, identical bytes from both hosts.
- Do NOT route inline EF/`AppDbContext` through repositories (STRUCT-03) or retire/move `ReplyInMyVoice.Api` (STRUCT-01).
- Do NOT add `Microsoft.AspNetCore.*` / `IActionResult` / `AppDbContext` references to the Application layer.
- Do NOT introduce banned substrings (`humanizer|bypass|undetect|detector|evade`); no secret values in tracked files.
- Do NOT push, open a PR, or commit to `main`; work only on `delivery/backend-hardening-2`.