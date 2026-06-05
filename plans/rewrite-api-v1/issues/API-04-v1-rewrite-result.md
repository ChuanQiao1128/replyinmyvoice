# API-04: GET /api/v1/rewrite/{id} — fetch result (key-authed, owner-only)

**Tier:** 1 (prereq) · **Owner:** Codex · **Depends on:** API-02

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §API and Job Contracts (result), §State and Error Handling.
- **Mirror the existing Entra poll endpoint:** `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs:125` (`GetRewriteAttempt`), which reads a `RewriteAttempt` and returns `RewriteAttemptResponse(AttemptId, Status, ResultJson, ErrorCode)`.
- `ResultJson` shape to parse for mapping: `{ rewrittenText, naturalness:{ draftAiLikePercent, rewriteAiLikePercent }, ... }` (validator at `RewriteJobProcessor.cs:534`).
- `RewriteAttemptStatus` enum: `Pending, Processing, Succeeded, Failed, Expired`.
- Reuse `ApiKeyAuthResolver` (API-02). Add this function to the `V1RewriteHttpFunctions` file created in API-03.

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets. Do NOT push/touch `main`.

## Changes required
1. Function `GetRewriteResult` `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rewrite/{id:guid}")]`:
   1. `userId = await ApiKeyAuthResolver.ResolveUserIdAsync(...)`; `null` → `401 invalid_key`.
   2. load `RewriteAttempt` where `Id == id && UserId == userId` (AsNoTracking); not found / not owned → `404`.
   3. map status to a `200` body:
      - `Pending`/`Processing` → `{ id, status:"processing" }`
      - `Succeeded` → parse `ResultJson` → `{ id, status:"succeeded", rewrittenText, signal:{ draft: naturalness.draftAiLikePercent, rewrite: naturalness.rewriteAiLikePercent } }`
      - `Failed`/`Expired` → `{ id, status:"failed", error:{ code: ErrorCode ?? "engine_unavailable", message: <human string> } }`
2. `401`/`404` bodies use the `{ "error": { "code", "message" } }` shape.

## Acceptance (machine-checkable)
- [ ] Integration/xUnit: a `Pending` attempt → `processing`; a `Succeeded` attempt with valid `ResultJson` → `succeeded` + `rewrittenText` + `signal.draft`/`signal.rewrite` populated from `naturalness`; a `Failed` attempt → `failed` + `error.code`.
- [ ] An attempt owned by a DIFFERENT user → `404` (never leak another user's attempt). Missing/invalid key → `401`.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT change the submit endpoint (API-03) or the worker/`RewriteJobProcessor`.
- Do NOT return another user's attempt under any condition.
