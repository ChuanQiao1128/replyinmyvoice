# GA-01: Official TypeScript SDK (submit+poll behind one call)

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. The public API is **async**: `POST /api/v1/rewrite` → `202 { id, status }` (+ `Location`), poll `GET /api/v1/rewrite/{id}` → `{ id, status }` then `succeeded { rewrittenText, signal:{draft,rewrite} }` or `failed { error:{code,message} }`; `GET /api/v1/usage` → `{ scope, quota, used, remaining, periodEnd }`. Auth: `Authorization: Bearer rmv_live_…`. Errors: `{ error:{ code, message } }` with HTTP 400/401/402/409/429. Spec: `plans/rewrite-api-v1/SPEC.md`.
- Repo is **NOT** an npm workspace (root `package.json` has `private:true`, no `workspaces`). Existing sibling package: `packages/mcp-server`. Root vitest runs `tests/unit/**/*.test.ts` (`vitest.config.ts`).

## Changes required
1. **New self-contained package `packages/sdk/`**: `package.json` (`name "@replyinmyvoice/api"`, `version "0.1.0"`, `type "module"`, `main`/`exports` → `src/index.ts` or a `dist`; **no `prepublishOnly`/auto-publish**), `tsconfig.json`, `src/index.ts`. **Zero runtime dependencies** — use global `fetch`.
   - `createClient({ apiKey: string, baseUrl?: string })` (default baseUrl `https://replyinmyvoice.com`).
   - `client.submitRewrite(draft: string): Promise<{ id, status }>`.
   - `client.getRewrite(id: string): Promise<{ id, status, rewrittenText?, signal?, error? }>`.
   - `client.rewrite(draft, opts?: { pollIntervalMs?: number=1500, timeoutMs?: number=120000 }): Promise<{ rewrittenText, signal }>` — submits, polls until `succeeded`/`failed`/timeout; throws `RimvApiError { code, message, status }` on failure/timeout.
   - `client.getUsage(): Promise<{ scope, quota, used, remaining, periodEnd }>`.
   - All requests send `Authorization: Bearer <apiKey>`; map any non-2xx body `{error:{code,message}}` to a thrown `RimvApiError`.
2. **`packages/sdk/README.md`**: install note (published by the owner later — do NOT publish), one-call quickstart (`await createClient({apiKey}).rewrite(draft)`), auth, error handling, polling/backoff note.
3. **Tests in `tests/unit/sdk.test.ts`** (so root vitest runs them): import the client from `../../packages/sdk/src/index`; mock `globalThis.fetch`; assert submit+poll happy path, error mapping (`401 invalid_key`, `402 quota_exhausted`, `429 rate_limited`), and timeout throws `RimvApiError`.

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` (root) green; `npm run test` (root) green including the new `tests/unit/sdk.test.ts`.
- [ ] `packages/sdk/package.json` has **no runtime `dependencies`** and no auto-publish script.
- [ ] Banned-term grep clean (`humanizer|bypass|undetect|detector|evade`) — describe `signal` only as a naturalness reference.

## Do NOT
- Do NOT run `npm publish` or add an npm `workspaces` field. Do NOT call the real network in tests (mock `fetch`).
- Do NOT break the root build/typecheck/test by wiring `packages/sdk` into the root tsconfig include.
