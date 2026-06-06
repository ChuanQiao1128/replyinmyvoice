# RFX-08: SDK robustness + OpenAPI accuracy (FIX-16, FIX-19, FIX-23) — CANARY

**Tier:** 2 · **Owner:** Codex · **Depends on:** none
Detailed findings: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#16, #19, #23).

## Context
- SDK: `packages/sdk/src/index.ts` (currently published as `replyinmyvoice-api@0.1.0`), `packages/sdk/package.json`, `packages/sdk/README.md`. Root vitest = `tests/unit/**/*.test.ts`; SDK tests live in `tests/unit/sdk.test.ts`.
- Spec: `public/openapi.json` + the live contract in `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs`.

## Changes required
1. **SDK (FIX-16):** add an optional `idempotencyKey?: string` to `RewriteOptions`/`submitRewrite` and send it as the `Idempotency-Key` header so a retried submit is safe; validate the submit response shape (throw `RimvApiError{code:"invalid_response"}` if `id` missing, instead of polling `/rewrite/undefined`); add real backoff to the poll loop (do not poll at zero delay immediately; honour `pollIntervalMs` with a small initial wait); add a `LICENSE` file (MIT) to `packages/sdk/` and include it in `files`. Bump `version` to `0.1.1`.
2. **OpenAPI (FIX-19):** make `public/openapi.json` match the live API — rewrite job `id` is a **UUID** (format: uuid; drop `rw_123` examples); remove the `rewrite_failed` example error code (impl emits `engine_*`/real codes, never `rewrite_failed`); do NOT document `X-RateLimit-*` headers on the 401 responses (the unauth path doesn't emit them); reconcile `additionalProperties`/min-max-after-trim with actual behavior; remove the duplicate `/api/v1/openapi` path doc (keep `/api/v1/openapi.json`); fix the `RewriteResult` discriminator (add `mapping` or drop the discriminator).
3. **periodEnd nullable (FIX-23):** make `periodEnd` nullable in both the SDK `UsageResponse` type and `public/openapi.json` (server returns null for sandbox/free).

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` + `npm run test` green (update/extend `tests/unit/sdk.test.ts` + `tests/unit/openapi-spec.test.ts`: assert idempotency header sent, invalid-response throws, `periodEnd` nullable, openapi id is uuid + no `rw_123`/`rewrite_failed`).
- [ ] `cd packages/sdk && npm run build` succeeds; `package.json` version is `0.1.1`; `LICENSE` present + in `files`.
- [ ] Banned-term grep clean.

## Do NOT
- Do NOT `npm publish` (the supervisor republishes after deploy). Do NOT change the wire protocol (still async submit+poll). Do NOT invent endpoints/fields.
