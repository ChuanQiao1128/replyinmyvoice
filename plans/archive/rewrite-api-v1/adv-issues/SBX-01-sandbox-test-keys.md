# SBX-01: Sandbox test keys (rmv_test_) — stubbed, no quota/engine/charge

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Live keys are `rmv_live_` (see `ApiKeyService.cs` `ComputeHash` + key generation; `ApiKeyAuthResolver`; `ApiKey` entity). v1 handlers: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/V1RewriteHttpFunctions.cs` (submit reserves quota via `QuotaService.ReserveAsync` → outbox → engine; poll reads the attempt). Keys UI: `components/developers/api-keys-panel.tsx`; keys endpoints `ApiKeyHttpFunctions` / `app/api/keys/*`.
- Goal: a **sandbox** key so a developer can wire and test the full submit→poll→usage flow **without consuming paid quota, without calling the engine, and without any charge** — it returns a deterministic stub.

## Changes required
1. **Test-key type**: support generating keys with prefix `rmv_test_`. Add a persisted `IsTest` boolean column to `ApiKey` (default false) + EF migration; set it true when a test key is created. `ApiKeyAuthResolver` returns the resolved key including `IsTest` (hashing/lookup unchanged — same pepper+SHA-256).
2. **Keys UI + endpoint**: allow creating a **test key** (e.g. a "Create test key" control / a `test: true` flag on the create call) and show test keys clearly labeled (`rmv_test_••••<Last4>`, badge "Test").
3. **Submit `POST /api/v1/rewrite` for a test key**: short-circuit to a STUB — do **NOT** call `QuotaService.ReserveAsync`, do **NOT** enqueue the outbox, do **NOT** call the engine. Persist (or synthesize) a test attempt that is immediately resolvable and return `202 { id, status:"processing" }` (or a recognizably test id).
4. **Poll `GET /api/v1/rewrite/{id}` for a test attempt**: return a deterministic `succeeded` with a fixed sandbox `rewrittenText` (clearly a sandbox example) and a fixed `signal:{draft,rewrite}`. Owner-scoped as usual.
5. **`GET /api/v1/usage` for a test key**: return a sandbox usage shape (e.g. `scope:"test"`) without reading/altering real `UsagePeriod`/credits.
6. Rate limiting still applies to test keys (so sandbox can't be abused).

## Acceptance (machine-checkable)
- [ ] xUnit: a `rmv_test_` key submit→poll returns the canned `succeeded` result AND the user's `UsagePeriod.UsedCount`/`ReservedCount` and credits are **unchanged** (zero quota effect); no outbox row / no engine call for the test path.
- [ ] xUnit regression: a `rmv_live_` key still reserves + charges exactly 1 on success (LIVE PATH UNCHANGED); revoked/expired still 401.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` + `npm run test` green (update pinned keys-panel copy tests).
- [ ] Banned-term grep clean.

## Do NOT
- Do NOT alter the live (`rmv_live_`) reserve→outbox→engine→finalize path in any way.
- Do NOT consume quota, grant credits, call the engine, or charge for test keys.
- Do NOT weaken live-key auth or the pepper hashing. Do NOT log plaintext keys or the pepper.
