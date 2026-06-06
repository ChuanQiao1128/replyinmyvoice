# Rewrite Public API — Cross-Review (Workflow × Codex)

- **Date:** 2026-06-06
- **Method:** two independent read-only reviews of the same module, then cross-referenced.
  1. **Dynamic multi-agent Workflow** — 7 dimension reviewers (security/auth, billing/quota, data/migrations, api-contract, webhooks/resilience, frontend/SDK, secrets/observability) + adversarial verification of every high/critical finding. 14 agents, **34 raw findings, 7 high confirmed**.
  2. **Codex** (read-only) — single-pass review of the same scope. **14 findings** (0 critical, 5 high, 5 medium, 4 low).
- **Scope:** the API module — `app/api/v1/*`, `app/api/keys/*`, `app/api/me/*`, `app/developers/*`, `components/developers/*`, `packages/sdk`, `lib/api-observability.ts`; backend `V1RewriteHttpFunctions`, `ApiKey*`, `Quota/Account/StripeEvent/RewriteJobProcessor/Webhook*` services, entities + migrations.

## Headline

- **No CRITICAL / no cross-tenant data breach.** Ownership is scoped by `UserId` on the read paths (no IDOR across accounts); no secret values in source; banned-term gate clean. Both reviewers agree.
- **Both reviewers independently agree on the top 5 issues** (→ high confidence they are real): API has no paid-plan gate, webhook SSRF, webhook duplicate delivery, non-atomic/bypassable rate limit, inconsistent usage math.
- **The review caught two regressions in features just shipped (verified true in code):** the **CSV export is 403-broken in prod**, and **MON-01 telemetry is dropped on Cloudflare Workers** (no `waitUntil`).
- **Root-cause of the deploy cascade bug found (Workflow-only):** the C# tests use **SQLite + EnsureCreated everywhere**, so SQL-Server-only rules (multiple-cascade-paths) and the migrations themselves are **never exercised in CI** — which is exactly why the `WebhookDeliveries` FK bug only surfaced at deploy.

## Cross-reference (where the two reviews agree / diverge)

| # | Finding | Codex | Workflow | Verified | Severity |
|---|---|---|---|---|---|
| 1 | **No paid-plan/quota gate on the API** — a free/Inactive account's key can call `/api/v1/rewrite`; `ApiKey.PlanTier`/`MonthlyQuota` are dead fields; leftover promo/trial credits are spendable via B2B API | ✅ HIGH | ✅ HIGH | ✅ both + live E2E | **HIGH** |
| 2 | **Webhook SSRF** — `TryNormalizeWebhookUrl` accepts any http/https incl. loopback/RFC1918/`169.254.169.254`; sender follows redirects | ✅ HIGH | ✅ HIGH (verified) | ✅ both | **HIGH** |
| 3 | **Webhook duplicate delivery** — 30s claim lock < HTTP timeout (100s default), 30s timer → slow receiver gets re-sent | ✅ HIGH | ✅ HIGH (verified) | ✅ both | **HIGH** |
| 4 | **Rate limit non-atomic/bypassable** — limiter does `COUNT(*)` over `ApiKeyUsage` rows that are written AFTER the response inside a swallow-all `catch{}`; concurrent bursts pass; degrades open under load | ✅ HIGH | ✅ MED | ✅ both | **HIGH** |
| 5 | **Long `Idempotency-Key` → 500** — header stored unvalidated, `RewriteAttempt.IdempotencyKey` capped at 120 (OpenAPI says 255) → SQL throws on SaveChanges | ✅ HIGH | — | codex | **HIGH** |
| 6 | **CSV export broken in prod** — export is `<a download>` GET (no `Origin` header) but the route runs `requireSameOrigin` → **403 in production** | — | ✅ HIGH (verified) | ✅ verified by me | **HIGH (regression)** |
| 7 | **MON-01 telemetry dropped on Workers** — `void captureApiEvent(...)` is a floating promise with no `ctx.waitUntil` → cancelled after response on Cloudflare | — | ✅ MED | ✅ verified by me | **HIGH (regression)** |
| 8 | **Missing index on `ApiKeyUsage.RequestId`** — webhook enqueuer + lookups table-scan a high-write table per async rewrite | (part of #9) | ✅ HIGH | ✅ workflow | **HIGH** |
| 9 | **Webhook ownership via best-effort `ApiKeyUsage`** — swallowed usage-write suppresses the webhook or misattributes it to a poll row; no `ApiKeyId` of record on the attempt | ✅ MED | ✅ MED | ✅ both | **MED** |
| 10 | **Usage math inconsistent** — `Used` omits consumed credits + in-flight reservations while `Quota`/`Remaining` include them → `Quota − Used ≠ Remaining` (the `2/1/2` you saw) | ✅ MED | ✅ MED | ✅ both | **MED** |
| 11 | **CI never exercises SQL Server / migrations** — all 15 DB fixtures use `UseSqlite` + `EnsureCreatedAsync`; the cascade rule + EF migrations are untested → deploy-time surprises | — | ✅ HIGH | ✅ workflow | **MED (process)** |
| 12 | **Usage `days`/series unbounded** — `?days=` accepts any int; summary/series load all rows with no DB-side date bound → memory/500 | ✅ MED | ✅ MED | ✅ both | **MED** |
| 13 | **v1 Next proxy errors return framework 500** — `app/api/v1/*` rethrow Azure fetch failures instead of `{error:{code,message}}` (502) | ✅ MED | — | codex | **MED** |
| 14 | **Sandbox isolation one-directional** — a live key can read sandbox (`rmv_test_`) attempts of the same user; env check only blocks test→live | — | ✅ MED | ✅ workflow | **MED** |
| 15 | **CSV formula injection** — `=,+,-,@` cell prefixes not neutralized in exports | ✅ LOW | ✅ MED | ✅ both | **MED** |
| 16 | **SDK gaps** — can't send `Idempotency-Key` (retry double-charge); no response-shape validation; fixed-interval (not backoff) polling; no `LICENSE` in tarball | ✅ MED | ✅ LOW | ✅ both | **LOW–MED** |
| 17 | **`API_KEY_PEPPER` silent-degrade** — missing pepper logs once and proceeds (unpeppered); unsalted single-pass SHA-256; no rotation path | ✅ LOW | ✅ LOW | ✅ both | **LOW** |
| 18 | **`requireSameOrigin` missing on GET `/api/me`, `/api/keys`, `/api/me/payments`** | ✅ LOW | — | codex | **LOW** |
| 19 | **OpenAPI drift** — id is GUID not `rw_123`; `rewrite_failed` example code never emitted; `X-RateLimit-*` documented on 401 but not sent; `additionalProperties:false` vs server ignores unknowns; trim-vs-raw length; `/openapi` duplicates `/openapi.json`; discriminator has no `mapping` | ✅ LOW | ✅ LOW×6 | ✅ both | **LOW** |
| 20 | **Webhook resilience nits** — claim txn has no serialization-retry; redirects followed; poison delivery (missing data) throws every tick, never terminal; HMAC has no timestamp (replay) | — | ✅ MED×3/LOW | ✅ workflow | **MED–LOW** |
| 21 | **`RowVersion` is a client-managed Guid** (not DB rowversion) → silent lost updates if a writer forgets to bump it | — | ✅ MED | ✅ workflow | **LOW** |
| 22 | **Provider hard-failure** leaves the attempt `Processing` + quota reserved until the 15-min sweep; redelivery never retries the provider | — | ✅ LOW | ✅ workflow | **LOW** |
| 23 | **`periodEnd` nullable mismatch** — SDK/OpenAPI type it required, server returns null (sandbox) | ✅ LOW | — | codex | **LOW** |

---

## Fix backlog (what a delivery workflow should fix), prioritized

### P0 — correctness / security / revenue (fix before promoting the API)
- **FIX-01 API plan gate (revenue):** at `/api/v1/rewrite` submit, reject keys whose account lacks API-eligible **paid** quota (enforce SPEC decision 5); decide whether leftover promo/trial credits count; wire `ApiKey.PlanTier`/`MonthlyQuota` or delete them. *(#1)*
- **FIX-02 Webhook SSRF:** in `TryNormalizeWebhookUrl` block loopback/link-local/RFC1918/ULA/`100.64/10`/metadata after DNS resolution; require https; register the webhook `HttpClient` with `AllowAutoRedirect=false` + a connect-time IP re-check (anti DNS-rebind) + short timeout. *(#2)*
- **FIX-03 Webhook exactly-once-ish delivery:** add an `InProgress`/renewable-lease state with lease > HTTP timeout, set the webhook HTTP timeout < lease, and add a stable event-id/idempotency header. *(#3)*
- **FIX-04 Atomic rate limit:** replace COUNT-over-best-effort-rows with an atomic fixed-window counter (single UPDATE/RETURNING or distributed store) checked-and-incremented before processing; do **not** swallow the usage-write that feeds it — fail closed. *(#4)*
- **FIX-05 Idempotency-Key validation:** validate length (≤ the 120-char column) → 400 on overflow (or hash to a bounded value); align OpenAPI (it says 255). *(#5)*

### P1 — shipped regressions + correctness/perf
- **FIX-06 CSV export 403 (regression):** make the Usage/Billing "Export CSV" buttons `fetch()` the CSV as a blob and trigger download (fetch sends `Origin` → passes same-origin), or add a Referer fallback in `requireSameOrigin` for same-origin GETs. *(#6)*
- **FIX-07 Telemetry on Workers (regression):** run `captureApiEvent` via the OpenNext/Workers `waitUntil` (e.g. `getCloudflareContext().ctx.waitUntil(...)`) so post-response telemetry actually flushes; today it is dropped. *(#7)*
- **FIX-08 Index `ApiKeyUsage.RequestId`** (+ consider uniqueness) — stop table-scanning on every async rewrite. *(#8)*
- **FIX-09 Webhook origin source-of-truth:** persist the submitting `ApiKeyId` on `RewriteAttempt`/`UsageReservation` and enqueue webhooks from it (not from the swallowed `ApiKeyUsage` row). *(#9)*
- **FIX-10 Usage math:** make `remaining = quota − used − reserved` consistent, or return a per-source breakdown (period vs credits). *(#10)*
- **FIX-11 CI SQL-Server gate (root cause):** add a CI step that runs the EF migrations against SQL Server (or `dotnet ef migrations script` + a SQL-Server-backed smoke) so cascade-path/migration errors are caught before deploy — not on `main`. *(#11)*
- **FIX-12 Bound usage `days`/series:** cap `days` (e.g. ≤ 90) + 400 on invalid; push the date filter into the DB query. *(#12)*
- **FIX-13 v1 proxy error shape:** the `app/api/v1/*` routes should return `{error:{code:"proxy_request_failed"}}` 502 on backend failure, not a framework 500. *(#13)*
- **FIX-14 Symmetric sandbox isolation:** reject when `IsSandboxAttempt(attempt) != auth.IsTest` on the read path. *(#14)*

### P2 — hardening / contract / SDK polish
- **FIX-15** CSV formula-injection neutralization (prefix `= + - @ TAB CR` cells). *(#15)*
- **FIX-16** SDK: add `idempotencyKey` option, validate the submit response shape, real polling backoff, ship a `LICENSE`. *(#16)*
- **FIX-17** `API_KEY_PEPPER`: fatal-if-missing in production; consider HMAC-SHA256(pepper) / per-key salt + a documented rotation path. *(#17)*
- **FIX-18** Apply `requireSameOrigin` to GET `/api/me`, `/api/keys`, `/api/me/payments` (or document the exemption). *(#18)*
- **FIX-19** OpenAPI accuracy pass: id = UUID, drop `rw_123`/`rewrite_failed`, fix the 401 rate-limit-header claim, `additionalProperties`/trim/length, dedupe `/openapi`, discriminator `mapping`. *(#19)*
- **FIX-20** Webhook resilience: serialization-retry on the claim txn, terminal state for poison deliveries, HMAC timestamp for replay-bounding. *(#20)*
- **FIX-21** Consider DB-generated `rowversion` (or assert every writer bumps the Guid token). *(#21)*
- **FIX-22** Provider hard-failure: release the reservation immediately (don't wait for the 15-min sweep) + bounded provider retry. *(#22)*
- **FIX-23** Make `periodEnd` nullable in SDK + OpenAPI. *(#23)*

---

## Notes on review quality
- **Agreement = signal:** the 5 items both reviews independently flagged (FIX-01..05) are the highest-confidence and should lead any fix wave.
- **Complementarity:** Codex was stronger on **contract/runtime edge cases** (Idempotency-Key 500, proxy error shape, same-origin GET); the Workflow was stronger on **systemic/process** issues (CI never tests SQL Server, missing index, the two shipped regressions, webhook resilience depth).
- **Suspects A & B confirmed** by both: API has no free-tier gate; free-tier usage math is inconsistent.
