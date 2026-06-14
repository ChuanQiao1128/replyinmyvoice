# Production-Hardening Wave — Master Spec

> Created 2026-06-13. Goal: take the C#/Azure backend to commercial production-grade and demonstrate mid/senior architecture capability, **without** depending on the current rewrite method (which will be replaced). Source review: [../interview/backend-system-design-review.md](../interview/backend-system-design-review.md). Designs were produced and adversarially verified by a multi-agent workflow over the real code; every design cites `file:line`.

## Scope & framing

Two halves, deliberately separated:

1. **Freeze the rewrite engine as a swappable black box** (HARD-01). The engine's *internals* are out of scope — they're being rewritten. What we harden is the **boundary**: one interface (`IRewriteEngineClient`), one result DTO shape, one error-code set, one DI seam, and contract tests so a future engine drops in by satisfying the contract — system unchanged.
2. **Bring the surrounding system to production grade** (HARD-02 … HARD-14): billing durability, async correctness, resilience, rate limiting, identity, observability, deploy safety, data hygiene, HTTP hardening. All of these live *outside* the engine and survive the swap by construction.

### The engine-swap contract (the load-bearing decision)

The audit found the boundary already exists but **leaks** in four places that would silently break on a swap. The minimal stable contract every future engine MUST honor:

- Implement `IRewriteEngineClient.RewriteAsync(Guid, RewriteRequest, CancellationToken) -> RewriteEngineResult` (or `IRewriteProvider` behind the existing `RewriteProviderEngineClient` adapter).
- **Success `ResultJson`** must contain: `rewrittenText` (non-empty string), `changeSummary` (array), `riskNotes` (array), and `naturalness.draftAiLikePercent` + `naturalness.rewriteAiLikePercent` as integers. *(Missing `naturalness.*` makes the v1 B2B API and result-webhooks report a succeeded attempt as failed — this is the #1 hidden coupling.)* `changePoints` + `label` are wanted by the UI; `optimization.*` is optional analytics.
- **Failure** = return (never throw) `Success=false` + an `ErrorCode`. The error-code set is **open** (consumers tolerate unknown codes via the `engine_unavailable` fallback), but the five quality-gate codes `{quality_signal_unavailable, structure_gate_failed, naturalness_gate_failed, fact_gate_failed, policy_intent_gate_failed}` MUST keep mapping to the consumer "422 / charged:false" path — any *new* quality code must be added to both frontend route sets in the same PR.
- `RewriteEngineResult.ProviderCalls` must be non-empty (else cost logging silently writes nothing). HARD-01 moves capture off the internal `AsyncLocal` onto the explicit result so this survives a swap.
- Startup validation (`ValidateReplyInMyVoiceRuntimeConfiguration`) currently hard-requires `OPENAI`/`DEEPSEEK` + `SAPLING` key names; a new engine adds its own validation entry rather than bypassing it.

This contract is pinned by tests in HARD-01 and documented in `docs/rewrite-engine-contract.md`. **Everything else in this wave is engine-agnostic by design.**

## Shared constraints (apply to EVERY issue)

1. **Secrets by env-var NAME only.** Validate required keys at runtime in the handler/registration that uses them (follow `ValidateReplyInMyVoiceRuntimeConfiguration` in `ServiceCollectionExtensions.cs:361-418`). Never print/commit values.
2. **Do not touch** `LAUNCH_CONFIRMED`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID`, live Stripe data, DNS, or domain config.
3. **Banned substrings** (CI + delivery-gate, diff-scoped over app/components/public/lib and source): `humanizer`, `bypass`, `undetect`, `detector`, `evade`. Use neutral names (`writing-signal`, `resilience`, `guard`). The correct grep is `humanizer|bypass|undetect|detector|evade` — do NOT use a `det.ctor`-style regex (it does not match `detector`).
4. **No-charge-on-failure is a product invariant.** Failed / quality-failed / provider-failed rewrites must never consume quota; preserve every reservation-release path and its tests.
5. **Engine-swap safety.** The rewrite method will be replaced. Touch only the boundary: `IRewriteEngineClient` / `IRewriteProvider` DTOs, the error-code set, DI registration, and the named-HttpClient resilience layer. Never depend on `FactReconstructRewriteProvider` internals or `Domain/RewriteEngine/*` / `Domain/Quality/*`.
6. **Migrations are additive only** (no drops/renames/narrowing); they must pass the `dotnet-azure.yml` SQL Server 2022 container migration gate AND keep SQLite-backed tests working (add an `IsSqlite()` branch for date/SQL arithmetic, cf. `OutboxMessageRepository.ClaimDueAsync`).
7. **House patterns:** raw `configuration["ENV"]` reads with parse-with-default at registration (no `IOptions<T>`); outbox = row written inside the same `UnitOfWork` transaction + `IOutboxMessageHandler` keyed by an Ordinal `MessageType`; xUnit + FluentAssertions; assembly-wide parallelization is DISABLED (new concurrency tests must be serial-safe).
8. **Pinned tests that gate deploy** must be updated in the same PR: backend `AdminRouteMetadataTests`, `InfrastructureServiceCollectionTests`; frontend vitest source-string families `workspace-copy`, `developers-page`, `openapi-spec`, `public-rewrite-api-route`. Name vitest (`tests/unit`) or xUnit tests as acceptance — Playwright e2e is NOT in CI.


## Items, priority, and dependencies

| ID | Title | Priority | Migration | Verdict | Depends on |
|----|-------|----------|-----------|---------|-----------|
| HARD-01 | Engine swap boundary: freeze contract + seal leaks | P0 (land first) | no | revise* | — |
| HARD-02 | Stripe post-commit notifications through outbox | P0 | no | revise* | — |
| HARD-03 | Stripe webhook ingest-then-process | P0 | yes | revise* | HARD-02 (notification outbox types) |
| HARD-04 | Handle missing Stripe lifecycle events | P0 | no | pass | HARD-02 |
| HARD-05 | Reconciliation: close the loop | P0 | yes | pass | HARD-02 |
| HARD-06 | Outbox fast-path dispatch after commit | P1 | no | revise* | — |
| HARD-07 | Provider circuit-breaker state lifetime + fast-fail | P1 | no | pass | — |
| HARD-08 | Layered rate limiting | P1 | yes | pass | — |
| HARD-09 | Atomic conditional UPDATE for quota reserve | P1 | no | pass | — |
| HARD-10 | Managed Identity for SQL + Service Bus | P1 | no | revise* | — |
| HARD-11 | Observability: business metrics + alert pack | P2 | no | revise* | HARD-07 (breaker metric) |
| HARD-12 | CI migration guard: expand/contract | P2 | no | pass | — |
| HARD-13 | Data hygiene: counters, soft-delete filter, sandbox TTL | P2 | no | pass | — |
| HARD-14 | HTTP hardening: size caps, error envelope, correlation | P2 | no | pass | — |

`*` **revise** = the adversarial reviewer found a real but mechanical defect (a broken banned-term regex, a missed test call-site, a version-specific assertion string, an open-vs-closed error-code wording). Each is captured as a "⚠️ Required corrections" block at the top of that issue and OVERRIDES the approach text. None require redesign.

### Suggested delivery sequencing

The items are mostly independent (separate files), so they parallelize well. The only hard ordering:

- **Wave A (foundation):** HARD-01, HARD-02, HARD-07, HARD-09, HARD-12, HARD-13, HARD-14 — no cross-deps, land in parallel. HARD-01 first so the contract tests exist before anyone else touches the rewrite path.
- **Wave B (billing depth, needs HARD-02's notification outbox):** HARD-03, HARD-04, HARD-05.
- **Wave C (latency + edge + identity):** HARD-06, HARD-08, HARD-10.
- **Wave D (observability, wants HARD-07 in place):** HARD-11.

A delivery daemon may run all 14 into the integration branch and merge per-issue as gates pass; the only enforced edges are HARD-02 → {03,04,05} and HARD-07 → HARD-11.

## Frozen external contract (appendix — do not break in any issue)

Captured by the contract audit; reproduced so every worker can self-check.

**Consumer (Entra-auth):** `POST /api/rewrite` requires `X-Idempotency-Key`; body `{messageToReplyTo?, roughDraftReply(10–5000), audience?, purpose?, whatHappened?, factsToPreserve?, tone:"warm"|"direct"}`; returns 200 (idempotent replay) or 202+`Location` with `{attemptId,status,resultJson,errorCode}`; 400/401; 402 ProblemDetails on quota/suspension; 409 idempotency conflict; 500. `GET /api/rewrite-attempts/{id}` → 200|404. `GET /api/me/rewrites` paginated.

**Attempt status enum (frozen PascalCase strings):** `Pending, Processing, Succeeded, Failed, Expired` — Next proxies branch case-sensitively. No rename/renumber/recasing.

**Attempt `errorCode` (public values):** `provider_timeout, provider_failed, provider_json_parse_failed, request_json_parse_failed, reservation_expired, processing_timed_out, account_erased` + engine quality codes `quality_signal_unavailable, naturalness_gate_failed, fact_gate_failed, structure_gate_failed, rewrite_quality_failed` (+ legacy model-client pass-throughs `model_*` / `rewrite_model_failed` — tolerated, see HARD-01). The four gate codes MUST keep mapping to consumer 422 `{code:"quality_gate_failed",charged:false}`.

**v1 B2B (Bearer API key, envelope `{error:{code,message}}`):** `POST /api/v1/rewrite` success is **exactly** HTTP 202 `{id,status:"processing"}` (SDK + MCP hard-fail otherwise). `GET /api/v1/rewrite/{id}` → `{id,status:"processing"|"succeeded"(+rewrittenText,signal{draft,rewrite})|"failed"(+error{code,message})}` (failed is HTTP 200). `GET /api/v1/usage` → `{scope,periodKey,quota,used,remaining,periodEnd}`. v1 codes: `invalid_key`(401), `invalid_request`(400), `input_too_long`(400), `api_requires_paid_plan`(402), `quota_exhausted`(402), `idempotency_conflict`(409), `rate_limited`(429), `rate_limit_unavailable`(503), `rewrite_failed`(500), `not_found`(404) — all documented in `public/openapi.json`, parsed by SDK/MCP. Forwarded headers: `X-RateLimit-Limit/Remaining/Reset`, `Retry-After`, `Location`, `Content-Type` only. Sandbox (test-key) semantics frozen: instant Succeeded with fixed `SandboxResultJson`, idempotency prefix `test:`, usage scope `"test"`.

**Timing budget (frozen-ish):** 15-min reservation TTL; browser polls 30×1.5s after a ~15s server poll; MCP 27 attempts/0.5–2s; SDK 120s default. Hardening must keep typical completion within ~45s of submit or clients report failure on still-running attempts.

## Verification (whole wave)

- Backend: `dotnet test backend-dotnet/ReplyInMyVoice.sln --configuration Release` green (full suite — a narrow `--filter` skips `AdminRouteMetadataTests`).
- Frontend: `npm run typecheck && npm run test` green.
- Banned-term gate: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` returns nothing.
- Migration gate: each `migration:yes` issue passes the `dotnet-azure.yml` SQL Server 2022 container `database update`.
- Infra scripts (HARD-08 WAF rule, HARD-10 MI rollout, HARD-11 alert pack) are **artifacts/docs only** in this wave — not executed against live Azure/Cloudflare. Rollout is a separate, owner-gated step.

## Out of scope (explicit)

- Rewrite engine internals / quality (being replaced separately).
- Executing infra changes against live Azure or Cloudflare (this wave ships code + scripts + docs; cutover is owner-gated).
- Multi-region DR, load-test baselining, and RBAC (named as known gaps in the review; not in this wave).
