# API-03: POST /api/v1/rewrite — asynchronous submit (key-authed)

**Tier:** 1 (prereq) · **Owner:** Codex · **Depends on:** API-02

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §API and Job Contracts (submit), §Input validation, §Error codes.
- **Mirror the existing Entra submit endpoint, which already returns `202 + attemptId`:**
  `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs:27` (`CreateRewriteAttempt`).
- Reuse:
  - `ApiKeyAuthResolver.ResolveUserIdAsync` (API-02).
  - `AccountService.GetUsagePlan(user, configuration)` → `AccountUsagePlan(Scope, PeriodKey, QuotaLimit)` (`AccountService.cs:360`).
  - `RewriteRequestService.CreateAttemptAsync(userId, idempotencyKey, RewriteRequest, periodKey, quotaLimit, now, ct)` → `ReserveRewriteResult` (Kinds: `Created`/`Existing`/`Conflict`/`QuotaExceeded`) (`RewriteRequestService.cs:15`).
  - `RewriteRequest(MessageToReplyTo?, RoughDraftReply, Audience?, Purpose?, WhatHappened?, FactsToPreserve?, Tone)` — for v1 set `RoughDraftReply=draft`, `Tone="warm"`, the rest `null`.
  - Find the `AppUser` by the key's `userId` (the key already identifies the owner) instead of `GetOrCreateUserAsync` (which is Entra-keyed).
  - `FunctionHttpResults.Problem(...)` for error responses.

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets in source. Do NOT push/touch `main`.
- **This endpoint is ASYNCHRONOUS. It MUST NOT run the engine inline.** It reserves + enqueues via `CreateAttemptAsync` (which writes the outbox) and returns `202`. The worker processes the job.

## Changes required
1. **New `V1RewriteHttpFunctions`** (`backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`), function `SubmitRewrite` `[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rewrite")]`:
   1. `userId = await ApiKeyAuthResolver.ResolveUserIdAsync(...)`; `null` → `401 { error:{ code:"invalid_key" } }`.
   2. parse JSON body `{ "draft": string }`; malformed JSON / missing `draft` / trimmed length < 10 → `400 invalid_request`.
   3. word cap: `draft` split on whitespace `> 300` tokens OR `draft.Length > 2400` → `400 input_too_long` (checked BEFORE any reservation).
   4. load `AppUser` by `userId`; `plan = GetUsagePlan(user, configuration)`.
   5. `idempotencyKey` = request header `Idempotency-Key` if present, else a generated GUID string.
   6. `result = CreateAttemptAsync(user.Id, idempotencyKey, new RewriteRequest(null, draft, null, null, null, null, "warm"), plan.PeriodKey, plan.QuotaLimit, now, ct)`.
   7. map: `QuotaExceeded` → `402 quota_exhausted`; `Conflict` → `409 idempotency_conflict`; `Created`/`Existing` → `202 { id: result.AttemptId, status:"processing" }` with header `Location: /api/v1/rewrite/{id}`.
   8. write an `ApiKeyUsage` row (`Endpoint="v1/rewrite"`, status code, latency ms) — best-effort, must not fail the request.
2. All non-2xx bodies use shape `{ "error": { "code": "...", "message": "..." } }`.

## Acceptance (machine-checkable)
- [ ] Integration/xUnit: valid key + valid draft → `202` with a GUID `id`; a `UsageReservation(Pending)` exists for the attempt; `UsagePeriod.UsedCount` is unchanged (reserved, not yet charged).
- [ ] 301-word draft → `400 input_too_long` with NO reservation created; a 2401-char whitespace-free draft → `400`.
- [ ] missing/invalid key → `401`; an account with no period quota and no usable credit → `402` with NO reservation.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT call `RewriteJobProcessor.ProcessAsync` inline (async only).
- Do NOT implement the GET result endpoint (API-04), rate limiting (API-08), or the Next.js route (API-05).
- Do NOT add a same-origin check (the API key is the trust boundary). Do NOT expose `tone` or other engine inputs.
