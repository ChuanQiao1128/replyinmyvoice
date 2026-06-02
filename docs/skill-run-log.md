# Skill Run Log

This append-only log records evidence that the reusable project skills were actually opened, followed, smoke-tested, or used during Reply In My Voice development.

Source of truth for future entries:

```text
AGENTS.md -> Codex And Claude Code Skill Policy -> Skill run log rule
```

Tracked skills:

```text
system-spec-synthesis
resilience-test-generation
state-machine-modeling
data-module-review
dotnet-backend-testing
ui-browser-testing
cloud-architecture-cost-review
claude-heavy-planning-handoff
```

## Logging Rules

- Add one entry per skill per run.
- Log future usage when an agent opens, follows, smoke-tests, or uses a tracked skill.
- If a tracked skill should have applied but was missed, add a gap entry and describe the correction.
- Do not log secrets, `.env.local` values, API tokens, private keys, credential file contents, or raw private user text.
- Do not backfill guessed historical runs. Historical entries are allowed only when backed by an existing artifact.
- Use absolute dates.

## Entry Template

```text
### YYYY-MM-DD - <skill-name> - <short task label>

- Agent: Codex | Claude Code | Other
- Trigger: <why this skill applied>
- Action: <opened/followed/smoke-tested/used/gap>
- Output artifacts: <files, docs, tests, plans, or command output references>
- Verification evidence: <commands run, tests passed, doc section, or result summary>
- Limitations: <anything not proven, unavailable, skipped, or no-charge/no-secret note>
```

## Entries

### 2026-06-02 - ui-browser-testing - PROMO-12 promo browser and preview checks

- Agent: Codex
- Trigger: PROMO-12 adds Playwright coverage for signup, login, promo redeem, trial rewrite consumption, paywall state, and Worker-preview smoke coverage for browser-visible routes.
- Action: Opened and followed the UI/browser workflow; added a scoped promo full-loop e2e spec, a Worker-preview smoke script, and checklist links from launch gates to browser and route tests.
- Output artifacts: `tests/e2e/promo-full-loop.spec.ts`; `scripts/promo-preview-smoke.ts`; `tests/unit/promo-preview-smoke.test.ts`; `plans/promo-launch-checklist.md`.
- Verification evidence: `PROMO_PREVIEW_PORT=8794 PROMO_AZURE_MOCK_PORT=45940 npm run smoke:promo-preview` passed; `npm run test` passed; focused promo route/unit tests passed. Playwright browser commands were attempted with a local Chromium cache and failed before page execution because Chromium could not register its macOS Mach rendezvous service in this sandbox.
- Limitations: Browser screenshot/flow evidence could not be captured in this worker sandbox due the Chromium launch boundary. No secrets, cookies, or credential values were logged.

### 2026-06-02 - dotnet-backend-testing - PROMO-12 launch checklist backend gate mapping

- Agent: Codex
- Trigger: PROMO-12 requires the launch checklist to map promo trial, cap, proxy, Turnstile, and admin gates to passing backend tests.
- Action: Opened and followed the .NET backend testing workflow; reviewed existing promo, account, quota, proxy, Turnstile, and admin test names and mapped them to the five launch gates without changing backend code.
- Output artifacts: `plans/promo-launch-checklist.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 445 tests; NuGet vulnerability metadata warnings were present, but restore/build/test completed successfully.
- Limitations: No new .NET tests were added for this issue because the dependent PROMO issues already provided the backend lock tests. No secrets were logged.

### 2026-06-02 - resilience-test-generation - PROMO-12 race and preview smoke coverage

- Agent: Codex
- Trigger: PROMO-12 verifies global cap race behavior, proxy trusted-IP fail-closed behavior, Turnstile handling, and repeated promo redeem outcomes before deploy handoff.
- Action: Opened and followed the resilience workflow; connected existing concurrency, route, and admin audit tests to the launch checklist and added preview smoke cases for invalid, expired, and already-used redeem outcomes with IP forwarding verification.
- Output artifacts: `scripts/promo-preview-smoke.ts`; `tests/unit/promo-preview-smoke.test.ts`; `plans/promo-launch-checklist.md`.
- Verification evidence: `PROMO_PREVIEW_PORT=8794 PROMO_AZURE_MOCK_PORT=45940 npm run smoke:promo-preview` passed and observed three Azure mock redeem calls with forwarded client IP; `npm run test` passed; `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed.
- Limitations: The smoke script uses a local Azure mock behind Worker preview and does not deploy or touch production infrastructure. No secrets were logged.

### 2026-06-02 - data-module-review - PROMO-01 promo EF schema

- Agent: Codex
- Trigger: PROMO-01 adds EF Core entities, indexes, concurrency tokens, check constraints, and a migration for promo codes and redemptions.
- Action: Opened and followed the data-module workflow; reviewed uniqueness, FK shape, delete behavior, indexed lookup columns, check constraints, and migration output.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCode.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCodeRedemption.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602080020_AddPromoCodes.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602080020_AddPromoCodes.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.
- Verification evidence: `dotnet build backend-dotnet/ReplyInMyVoice.sln` passed; `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj` passed 401/401; idempotent EF SQL script generation passed and contained the promo tables, checks, unique indexes, and no `RewriteCreditId` FK on promo redemptions.
- Limitations: No local SQL Server target was available in this worker environment, so database update was verified through EF script generation plus SQLite schema tests. No secrets were logged.

### 2026-06-02 - state-machine-modeling - PROMO-01 redemption status

- Agent: Codex
- Trigger: PROMO-01 adds persisted promo redemption status with `Applied` and `Reversed` values.
- Action: Opened and followed the state workflow at schema scope; kept the issue to persisted states only and used the unique `(PromoCodeId, UserId)` index as the duplicate-apply backstop.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/PromoCodeRedemption.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoCodeSchemaTests.cs`.
- Verification evidence: Focused `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter PromoCodeSchemaTests --no-restore` passed 4/4; full backend test command passed 401/401.
- Limitations: PROMO-01 intentionally adds no redemption service, transition function, reversal behavior, quota behavior, or consumption logic. No secrets were logged.

### 2026-06-02 - dotnet-backend-testing - PROMO-01 SQLite schema tests

- Agent: Codex
- Trigger: PROMO-01 requires xUnit SQLite tests for unique promo code, unique redemption per user and code, and check constraints.
- Action: Opened and followed the .NET backend testing workflow; wrote the failing promo schema tests first, then implemented the EF entities and mapping; also reused the repo's no-cookie WebApplicationFactory client pattern so existing API tests run in this sandbox.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoCodeSchemaTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeBillingApiTests.cs`.
- Verification evidence: Initial focused promo test failed before implementation because promo entity types were missing; after implementation, focused promo tests passed 4/4. Full `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj` passed 401/401.
- Limitations: NuGet vulnerability metadata lookup warned because `https://api.nuget.org/v3/index.json` was unreachable, but restore/build/test artifacts were available. No secrets were logged.

### 2026-05-25 - system-spec-synthesis - Corrected smoke 10 eval and pipeline split

- Agent: Codex
- Trigger: User provided a two-step implementation contract separating eval harness/report reliability from rewrite pipeline behavior.
- Action: Opened/followed the planning workflow and kept Step A and Step B as separate implementation/commit scopes.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `tests/unit/openai-output.test.ts`; `lib/fact-extraction.ts`; `lib/rewrite-pipeline/checks.ts`; `lib/rewrite-pipeline/model.ts`; `lib/rewrite-pipeline/pipeline.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`; `tests/unit/rewrite-pipeline.test.ts`; `docs/eval-runs/single-input-smoke-10-eval-harness-v2.md`; `docs/eval-runs/single-input-smoke-10-pipeline-fix-v1.md`; `docs/eval-runs/single-input-smoke-10-comparison.md`.
- Verification evidence: Step A committed as `45bb57d`; Step B focused tests passed with `npm test -- tests/unit/rewrite-pipeline-checks.test.ts tests/unit/fact-extraction.test.ts tests/unit/rewrite-pipeline.test.ts`; provider smoke 10 completed in smoke mode only with customer pass 8/10.
- Limitations: Did not expand cases 011-100, did not run focused/full mode, and did not run the old dual-input provider eval. No secrets or provider payloads were logged.

### 2026-05-25 - resilience-test-generation - Semantic judge retry and eval resume

- Agent: Codex
- Trigger: Corrected smoke 10 previously failed on a DeepSeek semantic judge timeout, requiring retry/resume behavior.
- Action: Opened/followed the resilience workflow; added bounded semantic judge retry/backoff and per-case progress checkpoint/resume to the eval runner, with tests using mock providers.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `docs/eval-runs/single-input-smoke-10-eval-harness-v2.md`.
- Verification evidence: Focused Step A tests passed with `npm test -- tests/unit/fact-extraction.test.ts tests/unit/eval-scenarios-corpus.test.ts tests/unit/openai-output.test.ts`; Step A smoke wrote a progress checkpoint and final report without rerunning completed cases.
- Limitations: Retry behavior was verified with mock-provider tests and the smoke run, but no forced live provider outage was induced. No raw provider payloads or credentials were logged.

### 2026-05-25 - state-machine-modeling - Rewrite candidate/fallback gate lifecycle

- Agent: Codex
- Trigger: Step B changed the multi-step rewrite lifecycle: fact extraction, candidate generation, candidate selection, finalization, deterministic gates, policy gates, repair/escalation, fallback, and quality failure.
- Action: Opened/followed the state workflow to keep transitions bounded and explicit; candidate selection now checks reviewer, deterministic, and policy gates before selecting, and fallback must pass fact plus naturalness gates before success.
- Output artifacts: `lib/rewrite-pipeline/pipeline.ts`; `lib/rewrite-pipeline/checks.ts`; `tests/unit/rewrite-pipeline.test.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`.
- Verification evidence: `npm test -- tests/unit/rewrite-pipeline-checks.test.ts tests/unit/fact-extraction.test.ts tests/unit/rewrite-pipeline.test.ts` passed 94/94.
- Limitations: This did not change quota, billing, persistence, or async queue states. No secrets were logged.

### 2026-05-25 - data-module-review - Persistence scope check for rewrite eval work

- Agent: Codex
- Trigger: The task touched rewrite/eval workflows and required checking whether persistence invariants or data modules were involved.
- Action: Opened/reviewed the data-module workflow and ruled out persistence changes for this scoped eval/rewrite pipeline patch.
- Output artifacts: No Prisma, EF Core, migration, transaction, usage-counter, idempotency, or database-access files were changed for this Step A/Step B work.
- Verification evidence: Scoped implementation touched eval scripts, rewrite pipeline modules, tests, and docs only; no data-module test or migration was needed.
- Limitations: This is a negative scope check, not a database review. Existing unrelated data/quota test failures, if present in full `npm test`, were not fixed here. No secrets were logged.

### 2026-05-25 - system-spec-synthesis - Two-field rewrite contract implementation

- Agent: Codex
- Trigger: User approved implementing the two-field frontend/backend contract, 400-word draft cap, new eval parser/grader, and next test strategy.
- Action: Used the previously synthesized contract to implement scoped changes across frontend validation, .NET API validation, eval parsing, and strategy docs.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `lib/rewrite-limits.ts`; `lib/rewrite-word-count.ts`; `lib/validation.ts`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteRequestLimits.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/Program.cs`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Focused frontend/parser/eval unit tests passed, .NET `RewriteApiTests` passed, C# eval tool build passed, and `npm run lint` passed. `npm run typecheck` was attempted but is blocked by a pre-existing missing `stripe` package import in `lib/stripe.ts`.
- Limitations: Did not run live provider eval in this turn. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - dotnet-backend-testing - Two-field rewrite API word limit

- Agent: Codex
- Trigger: Backend behavior changed to accept the frontend two-field request shape and reject drafts over 400 words without creating usage or attempts.
- Action: Added ASP.NET API integration coverage before implementation, then updated shared .NET rewrite request limits and both ASP.NET/Functions validation paths.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteRequestLimits.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter RewriteApiTests --no-restore` passed 11/11; broader `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter Rewrite --no-restore` passed 42/42.
- Limitations: NuGet vulnerability metadata checks warned because `https://api.nuget.org/v3/index.json` could not be reached, but restore/build/test artifacts were available. No secrets were logged.

### 2026-05-25 - ui-browser-testing - Rewrite workspace word counter

- Agent: Codex
- Trigger: Frontend rewrite workspace changed from character-count display to 400-word draft limit display and submit gating.
- Action: Updated the existing single-input workspace without adding extra context fields; verified the signed-out browser route loads cleanly and identified that authenticated `/app` workspace visual verification requires a signed-in session.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `lib/rewrite-word-count.ts`; `tests/unit/workspace-copy.test.ts` verification.
- Verification evidence: `npm test -- tests/unit/workspace-copy.test.ts tests/unit/rewrite-email-eval-cases.test.ts tests/unit/validation.test.ts tests/unit/eval-scenarios-corpus.test.ts tests/unit/openai-compatible.test.ts tests/unit/rewrite-pipeline-model.test.ts` passed 61/61; Playwright browser check reached `/sign-in?redirect_to=%2Fapp` with no console errors or failed requests.
- Limitations: Could not visually inspect the authenticated workspace without a valid local signed-in session. No credentials, cookies, or tokens were logged.

### 2026-05-25 - cloud-architecture-cost-review - Staged provider eval strategy implementation

- Agent: Codex
- Trigger: User wants the next provider test window to target sub-30% or sub-20% third-party writing signal scores without unbounded provider spend.
- Action: Updated the DeepSeek adaptive strategy and rewrite memory docs to require staged smoke/focused/full eval, answer-key isolation, provider call reporting, and hard fact/forbidden-claim bars.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: No new cloud resources or fixed-cost infrastructure were introduced. The recommended next command uses smoke mode first and keeps full 100-case runs as a later gate.
- Limitations: Exact provider prices were not quoted because no fixed budget estimate was requested and no official pricing pages were consulted. No secrets were logged.

### 2026-05-25 - system-spec-synthesis - Two-field rewrite contract and eval strategy

- Agent: Codex
- Trigger: User clarified that the product frontend should only send `roughDraftReply` and `tone`, asked what backend/frontend/eval changes are needed, and requested a strategy before implementation.
- Action: Opened and followed the skill to structure the contract, non-goals, rollout order, verification, and open questions.
- Output artifacts: Analysis in the current Codex thread; no frontend, backend, eval, or product code changes.
- Verification evidence: Reviewed the current frontend submit payload, Next proxy, .NET rewrite request contract, .NET validation, model prompt assembly, rewrite engine fact extraction, and eval parser/grader boundaries.
- Limitations: No implementation or live rewrite evaluation was run in this planning pass. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - cloud-architecture-cost-review - Rewrite eval threshold and provider-cost strategy

- Agent: Codex
- Trigger: User wants the new corpus used to drive rewrite quality toward sub-30% or sub-20% third-party writing signal scores, which affects DeepSeek/OpenAI-compatible and Sapling evaluation call volume.
- Action: Opened and followed the skill to keep the proposed strategy staged and bounded rather than repeatedly running full 100-case provider evals.
- Output artifacts: Analysis in the current Codex thread; no cloud resources, deploy commands, or provider configuration changes.
- Verification evidence: Identified that parser/test changes have no fixed infrastructure cost, while semantic grading plus rewrite evaluation adds variable provider calls. Recommended smoke/focused/full progression and call-count reporting before full runs.
- Limitations: Exact provider pricing was not quoted because no fixed budget estimate was requested and no official pricing pages were consulted. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - ui-browser-testing - Two-field rewrite workspace verification plan

- Agent: Codex
- Trigger: User clarified the frontend should keep only the current two-field rewrite payload and asked what needs to change.
- Action: Opened and followed the skill to identify browser-visible checks needed if the word-limit UI or submit behavior changes later.
- Output artifacts: Analysis in the current Codex thread; no UI code changes.
- Verification evidence: Reviewed the rewrite workspace submit payload and current client-side character-limit gating. Planned future verification around word counter, disabled state, submit payload, error state, desktop/mobile layout, console, and network behavior.
- Limitations: No dev server, Playwright, Codex Browser, screenshots, or live browser verification were run because this turn was analysis-only. No secrets or credentials were logged.

### 2026-05-25 - dotnet-backend-testing - Two-field rewrite API verification plan

- Agent: Codex
- Trigger: User asked to adjust the backend around the frontend's two-field rewrite contract and word-count limits.
- Action: Opened and followed the skill to identify required xUnit/API coverage for future .NET backend changes.
- Output artifacts: Analysis in the current Codex thread; no .NET code changes.
- Verification evidence: Reviewed `RewriteRequest`, Azure Functions validation, ASP.NET API validation, `RewriteApiTests`, and existing tests that already use two-field JSON payloads for quota/job flows.
- Limitations: No backend tests were added or run in this planning pass. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - cloud-architecture-cost-review - Semantic judge eval cost gate

- Agent: Codex
- Trigger: The proposed semantic fact judge for the new 100-case rewrite corpus would add LLM provider calls to evaluation runs.
- Action: Opened and followed the skill as a pre-implementation cost gate; reviewed the DeepSeek adaptive rewrite strategy, fact-reconstruct target, and manual setup context.
- Output artifacts: Analysis in the current Codex thread; no cloud resource, deployment, or product code changes.
- Verification evidence: Confirmed no new fixed cloud infrastructure is required for the corpus/parser/grader work. Identified variable usage-cost risk from one additional semantic judge model call per evaluated email case, on top of existing rewrite pipeline and Sapling calls.
- Limitations: Did not quote exact provider prices because no exact budget was requested and no provider pricing page was consulted. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - system-spec-synthesis - New 100-case rewrite eval corpus analysis

- Agent: Codex
- Trigger: User replaced `docs/rewrite-email-eval-cases-100.md` and asked for an implementation-impact analysis before code changes.
- Action: Opened and followed the skill to separate source facts, assumptions, component impact, rollout order, and verification needs.
- Output artifacts: Analysis in the current Codex thread; no product code changes.
- Verification evidence: Validated the new corpus has 100 `### Case` entries, required inline fields and `####` sections, non-empty `must_keep` and `must_not_claim` lists, and no banned positioning terms. Ran `npm run test -- tests/unit/rewrite-email-eval-cases.test.ts`, which failed at the expected old-parser boundary.
- Limitations: Did not implement parser, test, semantic grader, or rewrite-engine changes in this analysis pass. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-25 - replyinmyvoice-rewrite - New eval corpus rewrite impact review

- Agent: Codex
- Trigger: User asked whether the new 100-case corpus requires rewrite agent or engine changes.
- Action: Opened the project rewrite skill and used it as context for evaluating corpus, naturalness, fact-preservation, and rewrite-engine boundaries.
- Output artifacts: Analysis in the current Codex thread; no product code changes.
- Verification evidence: Reviewed the new corpus schema, current parser/test/eval runner usage, and product request path around `factsToPreserve`, `messageToReplyTo`, `whatHappened`, and fact extraction.
- Limitations: Did not call the MCP rewrite tool or run live rewrite quality evaluation. No secrets, `.env.local` values, API tokens, private keys, or credentials were logged.

### 2026-05-24 - dotnet-backend-testing - Azure Functions bare API audience auth

- Agent: Codex
- Trigger: Production Google login still returned to `/sign-in` after callback success, and Azure `/api/me` continued returning 401. The remaining likely token-validation boundary is Entra access-token audience shape.
- Action: Opened and followed the skill; added a focused xUnit regression that resolves both `api://<api-client-id>` and bare `<api-client-id>` from the configured API scope, then updated `FunctionAuthResolver` so either valid Entra audience form is accepted while keeping the required scope/role gate.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new `ResolveAudiences_accepts_api_uri_and_bare_api_client_id_from_scope` regression failed before implementation because the resolver did not expose/derive the bare API client id audience. After the fix, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter FunctionAuthResolverTests --no-restore` passed 9/9 and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 76/76.
- Limitations: Codex still cannot perform the owner's live Google login in this session. No access-token values, cookies, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - ui-browser-testing - Entra login token-shape diagnostics

- Agent: Codex
- Trigger: Production Google login still redirected back to `/sign-in` after the CIAM metadata-issuer fix, and Cloudflare tail continued to show Azure `/api/me` returning 401 after `/auth/callback` succeeded.
- Action: Opened and followed the skill; added safe Worker-side diagnostics that log only access-token shape on Azure auth rejection: summarized audience form, issuer host, scope names, role count, and whether stable identity claims exist. The diagnostic intentionally avoids logging token values, email, name, or raw private claims.
- Output artifacts: `lib/azure-api.ts`; `tests/unit/azure-api.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/azure-api.test.ts` failed before the diagnostic helper existed, then passed after implementation. `npm test -- tests/unit/azure-api.test.ts tests/unit/entra-auth.test.ts tests/unit/middleware.test.ts`, `npm run typecheck`, `npm run lint`, and `npm run cf:build` passed.
- Limitations: This is diagnostic instrumentation, not a final auth fix. It needs one post-deploy owner login retry to capture the safe token profile. No raw access tokens, cookies, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - dotnet-backend-testing - Azure Functions CIAM metadata issuer auth

- Agent: Codex
- Trigger: Production Google login still returned to `/sign-in`; Cloudflare logs showed `/auth/callback` succeeded and `/app` had an access token, but Azure Functions `/api/me` still returned 401.
- Action: Opened and followed the skill; added a focused xUnit regression proving Entra External ID CIAM alias authority must accept the canonical issuer published by the discovery metadata, then updated `FunctionAuthResolver` to include the metadata issuer in JWT `ValidIssuers`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new `ResolveValidIssuers_accepts_ciam_metadata_issuer_for_alias_authority` regression failed before implementation because the resolver lacked metadata issuer support. After the fix, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter FunctionAuthResolverTests --no-restore` passed 8/8 and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 75/75.
- Limitations: Codex cannot perform the owner's live Google login in this session; no access-token values, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - ui-browser-testing - Entra login Azure 401 redirect loop

- Agent: Codex
- Trigger: The production browser-visible login flow still redirected back to `/sign-in` after Google callback.
- Action: Opened and followed the skill; used Cloudflare production tail evidence to trace the browser flow through `/auth/callback`, `/app`, and the Azure account-summary request, then compared Worker Entra config with Azure Functions app settings and the CIAM discovery document.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Production tail showed `/auth/callback` succeeded, `/app` loaded, and `azure_account_summary_auth_rejected { status: 401 }` immediately preceded the `/sign-in` redirect. CIAM discovery returned a canonical metadata issuer host different from the configured tenant-subdomain authority, matching the backend issuer-validation failure fixed in this run.
- Limitations: No authenticated browser screenshot or signed-in `/app` Playwright run was possible without the owner's Google session. No secrets or raw token values were logged.

### 2026-05-24 - dotnet-backend-testing - Azure Functions mapped Entra scope auth

- Agent: Codex
- Trigger: Production Google login reached `/app`, but Cloudflare logs showed Azure `/api/me` returned 401 after access-token retrieval, requiring a .NET Functions auth fix and xUnit regression coverage.
- Action: Opened and followed the skill; added a focused xUnit regression for `JwtSecurityTokenHandler`'s inbound-mapped Entra scope claim and updated `FunctionAuthResolver` to accept the mapped Microsoft scope URI alongside raw `scp`, `scope`, and role claims.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FunctionAuthResolverTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new `HasRequiredScopeOrRole_accepts_inbound_mapped_scope_claim` test failed before the resolver fix, then `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter FunctionAuthResolverTests --no-restore` passed 7/7.
- Limitations: Live signed-in Google login must be retried by the owner after deployment. No token values, `.env.local` values, API tokens, private keys, or provider secrets were logged.

### 2026-05-24 - ui-browser-testing - Entra callback access-cookie redirect loop

- Agent: Codex
- Trigger: Owner reported that production Google sign-in still returned to `/sign-in` after `/auth/callback` redirected to `/app`; this is a browser-visible auth redirect and cookie persistence flow.
- Action: Opened and followed the skill; used production request logs, route/source tracing, and focused Playwright auth-gate verification. Changed callback/password/signup session writes to attach cookies directly to the outgoing `NextResponse.cookies` object, added signed metadata for access-token cookie chunks, reduced callback Set-Cookie churn, and added safe auth-boundary logging for `/app` account-summary failures.
- Output artifacts: `app/auth/callback/route.ts`; `app/api/auth/password/route.ts`; `app/api/auth/signup/start/route.ts`; `app/api/auth/signup/resend/route.ts`; `app/api/auth/signup/verify/route.ts`; `lib/entra-auth.ts`; `lib/azure-api.ts`; `tests/unit/entra-auth.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/entra-auth.test.ts tests/unit/middleware.test.ts` passed 18/18; full `npm test` passed 95/95; `npm run typecheck`, `npm run lint`, standalone `npm run build`, standalone `npm run cf:build`, and `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` passed.
- Limitations: Codex cannot complete the owner's real Google account login inside this session. The deployed fix still needs a production retry from a real browser, and no token values, `.env.local` contents, or provider secrets were logged.

### 2026-05-24 - cloud-architecture-cost-review - C# rewrite real-provider optimization

- Agent: Codex
- Trigger: The task uses real DeepSeek and Sapling provider calls and prepares Azure/Cloudflare production deployment, so it affects provider spend and cloud deployment posture.
- Action: Opened and followed the cost gate; kept the existing Azure Functions Consumption + Azure SQL + Service Bus architecture, avoided new paid infrastructure, and used staged real-provider evals before the final full run.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/`; `docs/rewrite-eval-results/20260524-034340-csharp-rewrite-full.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Final full C# eval completed 100/100 provider success with 133 model calls and 206 Sapling calls; no new Azure resources were created by the implementation.
- Limitations: Real provider usage incurred variable DeepSeek/Sapling API cost. Exact provider billing was not fetched, and no secrets were logged.

### 2026-05-24 - system-spec-synthesis - C# rewrite eval and gate scope

- Agent: Codex
- Trigger: The task changes multi-module C# rewrite behavior, provider contracts, eval reporting, and deployment readiness.
- Action: Opened and followed the planning workflow; scoped the implementation to the C# rewrite provider path, eval runner, gates, prompt strategy, and regression tests without changing the hosting architecture.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/FactReconstructRewriteProvider.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiCompatibleRewriteModelClient.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`.
- Verification evidence: Final eval report `docs/rewrite-eval-results/20260524-034340-csharp-rewrite-full.md` records 100/100 successful measured rewrites and 100/100 below 50% signal.
- Limitations: This run optimizes the C# rewrite engine path; it does not migrate unrelated learning/admin/API-key datastore workstreams.

### 2026-05-24 - resilience-test-generation - rewrite provider retries and gates

- Agent: Codex
- Trigger: The implementation changes Sapling unavailable handling, provider retry behavior, naturalness gate recovery, and deterministic fact/unsupported-judgment gates.
- Action: Opened and followed the resilience workflow; added retry tests for transient writing-signal unavailability and gate regression tests for amount/count preservation and unsupported workplace judgments.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/FactReconstructRewriteProviderTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteEngineCoreTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/FactReconstructRewriteProvider.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 71/71 tests; the final real-provider eval had zero provider failures.
- Limitations: Retry coverage is unit-level plus real-provider eval evidence; it does not simulate every Sapling HTTP status separately.

### 2026-05-24 - dotnet-backend-testing - C# rewrite provider tests

- Agent: Codex
- Trigger: The task adds and changes C#/.NET provider behavior, xUnit tests, and a C# console eval tool.
- Action: Opened and followed the .NET testing workflow; added focused xUnit coverage for Sapling retries, attempt-history retries, exact fact gates, number-word normalization, thousands amounts, membership payment/date preservation, and unsupported judgment labels.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/FactReconstructRewriteProviderTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteEngineCoreTests.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/`.
- Verification evidence: `dotnet build backend-dotnet/ReplyInMyVoice.sln --no-restore -maxcpucount:1` passed; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 71/71 tests.
- Limitations: Browser-visible UI did not change in this run, so no UI/browser skill was used.

### 2026-05-23 - ui-browser-testing - students v2 landing and workspace nudge

- Agent: Codex
- Trigger: The task changes the `/students` landing page, workspace output UI, responsive layout, browser-visible routing, and local verification expectations.
- Action: Opened and followed the skill; selected focused unit/source guards plus attempted local browser verification for `/students` after implementation.
- Output artifacts: `app/students/page.tsx`; `app/globals.css`; `app/sitemap.ts`; `app/app/page.tsx`; `components/app/rewrite-workspace.tsx`; `tests/unit/students-v2-page.test.ts`; `tests/unit/workspace-copy.test.ts`; `tests/unit/pricing-auth-visual-system.test.ts`; `vitest.config.ts`; `.gitignore`; `docs/skill-run-log.md`.
- Verification evidence: `npm run prisma:generate`, `npm run typecheck`, `npm run lint`, and full `npm test` passed. The restricted-term scan over `app`, `components`, `public`, and `lib` returned no matches. Local dev-server startup was attempted for browser verification, but both `0.0.0.0:3000` and `127.0.0.1:3000` failed with sandbox `listen EPERM`.
- Limitations: No browser screenshot or live responsive browser pass was possible because this sandbox cannot bind a local HTTP port. A fallback `npm run build` was attempted and failed before route compilation because DNS to `fonts.googleapis.com` is unavailable for `next/font`; no secrets, deployment, Stripe, schema, middleware, auth, or rewrite-pipeline changes were made.

### 2026-05-23 - data-module-review - M1-007 Entra user id migration

- Agent: Codex
- Trigger: M1-007 changes the Prisma `User` model and adds a database migration for the Entra migration window.
- Action: Opened and followed the skill; reviewed the owned `User` table, existing migrations, auth identifier context, uniqueness invariant, and migration safety before editing.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260523090000_add_user_entra_user_id/migration.sql`; `plans/clerk-to-entra-user-backfill.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` ran and the scoped migration is additive only: nullable `entraUserId` plus a unique index.
- Limitations: Application lookup changes and the actual production backfill are intentionally left to later M1 issues. No secret files, provider dashboards, live money, git, or GitHub actions were touched.

### 2026-05-22 - web-design-engineer - M4-012 landing/header/footer refresh

- Agent: Codex
- Trigger: M4-012 is a browser-visible landing, header, and footer visual refresh.
- Action: Opened and followed the skill; reviewed the aborted implementation, preserved the practical workflow-led direction, fixed mobile overflow, and recorded design decisions plus five-dimension self-critique.
- Output artifacts: `plans/frontend-redesign-design-brief.md`; landing components; `components/site-header.tsx`; `components/site-footer.tsx`; `tailwind.config.ts`; `docs/skill-run-log.md`.
- Verification evidence: Final self-critique average is 7.8/10 for this scoped landing pass; desktop and mobile Playwright checks loaded `/` successfully with no console errors, failed requests, or horizontal overflow.
- Limitations: M4-012 is one scoped pass. M4-013 through M4-015 remain responsible for pricing/auth, `/app`, and final average score >= 8.0 across the full frontend.

### 2026-05-22 - state-machine-modeling - stale git index lock repair lifecycle

- Agent: Codex
- Trigger: The overnight supervisor entered a repair-inbox crash loop because `stash_dirty_worktree` could not write the git index while a stale `.git/index.lock` existed.
- Action: Opened and followed the skill; modeled the supervisor dirty-worktree lifecycle with an additional recovery transition that clears stale index locks before stash operations instead of repeatedly failing the same repair item.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `scripts/analyze_rewrite_quality.py`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh`, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts tests/unit/analyze-rewrite-quality.test.ts`, `npm run typecheck`, `npm run lint`, and full `npm run test` passed.
- Limitations: This repair covers local supervisor/git recovery and local Python compatibility only; it does not change provider dashboard, live money, npm publish, or deployment state.

### 2026-05-22 - ui-browser-testing - M4-012 landing visual refresh

- Agent: Codex
- Trigger: M4-012 changes the landing page, shared header, shared footer, responsive layout, browser-visible navigation, and visual polish.
- Action: Opened and followed the skill; selected desktop and mobile browser verification for `/`, with console/network and layout checks after implementation.
- Output artifacts: `plans/frontend-redesign-design-brief.md`; `components/site-header.tsx`; `components/site-footer.tsx`; `components/landing/hero.tsx`; `components/landing/interactive-demo.tsx`; `components/landing/trust-panel.tsx`; `components/landing/use-cases.tsx`; `components/landing/how-it-works.tsx`; `components/landing/pricing.tsx`; `components/landing/faq.tsx`; `components/landing/closing-cta.tsx`; `tailwind.config.ts`; `plans/task-status.json`.
- Verification evidence: `npm run lint`, `npm run typecheck`, and full `npm run test` passed. Playwright loaded `/` at desktop 1440x1100 and mobile 390x1000 with HTTP 200, no console errors, no failed requests, and no horizontal overflow after fixing the mobile hero/workflow grid.
- Limitations: No auth-gated `/app` workflow, payment flow, live provider call, deployment, or production-domain smoke test is in scope for this landing/header/footer issue.

### 2026-05-22 - state-machine-modeling - supervisor dirty-worktree lifecycle

- Agent: Codex
- Trigger: The overnight supervisor allowed a `needs_human`/repair flow to carry dirty M4-011 files into a later M6-004 repair branch and commit them together.
- Action: Opened and followed the skill; modeled the relevant lifecycle as clean main -> issue branch -> Codex status -> commit/stash/block, with illegal transitions for returning to main or committing when dirty files are undeclared.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh`, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts`, `npm run typecheck`, and `npm run lint` passed. Focused supervisor tests cover branch-before-task writes, no-status preservation, `needs_human`/abort stashing, and the `files_changed` guard that blocks mixed commits.
- Limitations: This covers the shell supervisor lifecycle only; it does not validate GitHub Actions, Cloudflare DNS reachability, or frontend visual quality.

### 2026-05-22 - system-spec-synthesis - supervisor rescue split

- Agent: Codex
- Trigger: The mixed PR #199 needed to be split into implementation-ready scopes without losing M4-011 partial work or M6-004 provider evidence.
- Action: Opened for routing and applied the spec workflow to separate source facts, non-goals, state handling, rollout, and verification in the existing frontend follow-up and custom-domain documents.
- Output artifacts: `plans/frontend-redesign-followups.md`; `plans/custom-domain-attach.md`; `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The rescue split keeps UI source changes out of the supervisor/provider repair branch and records follow-up scopes for the redesign instead of retrying one oversized unattended issue.
- Limitations: The partial UI implementation from the aborted run is preserved separately and is not claimed as complete in this process repair.

### 2026-05-22 - cloud-architecture-cost-review - M6-004 provider-blocker repair

- Agent: Codex
- Trigger: The M6-004 repair item reviews Cloudflare custom-domain verification for `replyinmyvoice.com` after a provider/DNS blocker.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Kept the selected path to read-only Workers domains API verification plus formal-domain smoke from a networked shell, and rejected deploys, DNS changes, dashboard mutation, secret changes, npm publish, and live-money actions.
- Output artifacts: `plans/codex-worker-inbox.md`; `plans/custom-domain-attach.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: Secret-free DNS and curl checks still fail before reaching Cloudflare: `api.cloudflare.com`, `replyinmyvoice.com`, and `example.com` resolve as `ENOTFOUND` from this sandbox.
- Limitations: Current live custom-domain attach state remains unverified until a networked shell runs the documented Cloudflare API and formal-domain smoke checks. No exact pricing lookup was needed because no paid resource or provider-spend action was selected.

### 2026-05-22 - state-machine-modeling - M6-004 repair lifecycle

- Agent: Codex
- Trigger: The repair changes a persisted inbox item lifecycle status for a Cloudflare verification blocker.
- Action: Opened and followed the skill; modeled the repair item transition as `in_progress -> not_actionable` on confirmed sandbox DNS failure, with the allowed next external event being a networked rerun of the documented verification commands.
- Output artifacts: `plans/codex-worker-inbox.md`; `plans/custom-domain-attach.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: The inbox item now records the terminal not-actionable reason, and `plans/custom-domain-attach.md` records states, events, a transition table, invariants, illegal transitions, persistence implications, a test checklist, and exact networked commands.
- Limitations: This lifecycle note covers the repair queue only; it does not verify live Cloudflare state.

### 2026-05-22 - resilience-test-generation - M6-004 provider-blocker routing

- Agent: Codex
- Trigger: The repair item mentions a provider blocker, so the resilience skill was opened to check whether provider-failure testing guidance applied.
- Action: Opened for routing; final scope stayed on read-only Cloudflare DNS/API verification and repair queue classification, without changing retries, timeouts, quota, idempotency, webhook replay, queue redelivery, or recovery behavior.
- Output artifacts: `plans/codex-worker-inbox.md`; `plans/custom-domain-attach.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: Secret-free DNS and curl checks reproduced sandbox reachability failure before any Cloudflare response was returned.
- Limitations: No provider timeout fake, rate-limit test, live Cloudflare API response, or retry behavior test was added because no application resilience behavior changed.

### 2026-05-22 - cloud-architecture-cost-review - M6-004 custom domain check

- Agent: Codex
- Trigger: M6-004 reviews Cloudflare Worker custom-domain attach state for `replyinmyvoice.com` and `replyinmyvoice-app`.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Selected read-only Workers domains API verification and formal-domain smoke checks; rejected deploys, DNS changes, secret changes, and paid-resource creation for this issue.
- Output artifacts: `plans/custom-domain-attach.md`; `docs/preflight-report.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `curl` to the Workers domains API failed before returning Cloudflare metadata because `api.cloudflare.com` could not be resolved from this shell. Formal-domain `curl` checks also failed before reaching the site because `replyinmyvoice.com` could not be resolved.
- Limitations: Current live attach state remains unverified in this sandbox. No secret values were printed or written, no deploy ran, no DNS state changed, and `.env.local` was not modified.

### 2026-05-22 - cloud-architecture-cost-review - M6-001 secret diff retry

- Agent: Codex
- Trigger: Repair item `REPAIR-20260522180011` reviews Cloudflare Worker production secret-name configuration for `replyinmyvoice-app`.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Selected the read-only `wrangler secret list` retry path; rejected secret pushes, deploys, dashboard mutation, and paid-resource changes for this repair.
- Output artifacts: `plans/worker-secret-diff.md`; `plans/codex-worker-inbox.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `XDG_CONFIG_HOME=/private/tmp/replyinmyvoice-wrangler-config npx --no-install wrangler secret list --name replyinmyvoice-app --format json` failed before returning Worker metadata because Cloudflare API DNS resolution is unavailable. A direct DNS lookup returned `ENOTFOUND` for `api.cloudflare.com` and `dash.cloudflare.com`.
- Limitations: The live Worker secret-name list remains unavailable, so `present-in-both`, `missing-in-worker`, and `missing-in-local` are still not authoritative. No secret values were printed or written, no secrets were pushed, no deploy ran, and no provider dashboard state changed.

### 2026-05-19 - system-spec-synthesis - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/spec_outline.py` produced the required implementation-spec headings, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - resilience-test-generation - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/resilience_matrix.py` produced timeout, retry, duplicate, partial-success, concurrency, and malformed-payload failure rows, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - state-machine-modeling - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/state_machine_template.py` produced states, events, transitions, invariants, and illegal-transition sections, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - data-module-review - Skill smoke verification

- Agent: Codex
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/scan_data_risks.py` excluded generated/build/vendor output by default and supported `--limit` / `--include-generated`, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling was smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-19 - claude-heavy-planning-handoff - Skill smoke verification

- Agent: Codex with local Claude Code CLI
- Trigger: Project skill smoke verification for the five reusable Agent Studio skills.
- Action: Smoke-tested.
- Output artifacts: `AGENTS.md` skill smoke verification section.
- Verification evidence: `scripts/build_handoff_brief.py` produced a sanitized handoff brief, and Codex successfully called local Claude Code non-interactively with `claude -p`, as recorded in `AGENTS.md`.
- Limitations: This proves the skill tooling and Claude CLI handoff path were smoke-tested. It does not prove a real development task automatically triggered the skill.

### 2026-05-21 - skill-run-log - Evidence logging policy installed

- Agent: Codex
- Trigger: User requested a run log so future use of the five tracked skills leaves evidence.
- Action: Added logging policy and append-only run log.
- Output artifacts: `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The `AGENTS.md` routine skill routing rules now require appending to this file whenever a tracked skill is opened, followed, smoke-tested, or used.
- Limitations: This is a logging-policy entry, not a tracked skill run. It does not retroactively prove historical automatic triggers.

### 2026-05-21 - state-machine-modeling - Current code state lifecycle review

- Agent: Codex
- Trigger: User explicitly requested running the `state-machine-modeling` skill to inspect current code state lifecycles.
- Action: Opened and followed the skill; used `agent-skills/state-machine-modeling/scripts/state_machine_template.py` to generate a review skeleton; reviewed subscription, quota/reservation, rewrite attempt, Stripe webhook, outbox, and rewrite job state paths.
- Output artifacts: Review findings delivered in the Codex thread; no production code changed.
- Verification evidence: Ran `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` with 34/34 passing tests; ran `npm test -- tests/unit/quota.test.ts tests/unit/stripe-webhook-events.test.ts` with 7/7 passing tests.
- Limitations: This was a static lifecycle review plus existing test run. It did not implement fixes for the identified state-machine gaps.

### 2026-05-21 - state-machine-modeling - State lifecycle fixes

- Agent: Codex
- Trigger: User requested tests and fixes for the state-machine risks found in the current code review.
- Action: Opened and followed the skill; added transition/invariant tests and updated rewrite attempt, usage reservation, subscription, and quota transitions.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/RewriteAttemptStatus.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/Enums/SubscriptionStatus.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/QuotaService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteJobProcessor.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; related .NET tests.
- Verification evidence: First ran `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` and observed 5 expected failing tests; after fixes, the same command passed with 38/38 tests.
- Limitations: This fixed the .NET backend state-machine defects. It did not introduce a full Prisma/Next rewrite-attempt reservation table.

### 2026-05-21 - data-module-review - Quota and webhook persistence fixes

- Agent: Codex
- Trigger: The state fixes changed EF Core entities, enum values, quota counters, subscription persistence, and data-access services.
- Action: Opened and followed the skill; ran `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80`; reviewed owned tables/entities and service mutations before changing persistence behavior.
- Output artifacts: Same code and test files listed in the state lifecycle fixes entry; `docs/business-qa-and-deploy-result.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed with 38/38 tests; `npm test -- tests/unit/quota.test.ts tests/unit/stripe-webhook-events.test.ts` passed with 7/7 tests.
- Limitations: No database migration was added because the changed .NET enum values are stored as strings and no schema shape changed.

### 2026-05-21 - resilience-test-generation - Provider, cleanup, quota, and webhook regression tests

- Agent: Codex
- Trigger: The fixes involve provider success after partial failure, expired reservation cleanup, duplicate/terminal events, and paid quota exhaustion.
- Action: Opened and followed the skill; ran `agent-skills/resilience-test-generation/scripts/resilience_matrix.py "rewrite attempt quota reservation lifecycle"`; added deterministic local tests before production fixes.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/ExpiredReservationCleanupServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs`.
- Verification evidence: The new tests failed first under the old implementation, then passed after the fixes; final focused verification passed with 38/38 .NET tests and 7/7 Next quota/webhook tests.
- Limitations: Tests use deterministic local SQLite/fakes and do not hit live Stripe, Azure Service Bus, OpenAI, Sapling, Clerk, or production databases.

### 2026-05-21 - dotnet-backend-testing - Skill created and routed

- Agent: Codex
- Trigger: User requested a reusable testing skill that is added to `AGENTS.md` and automatically triggers for future C#/.NET backend testing work.
- Action: Created project skill and added required routing rules.
- Output artifacts: `agent-skills/dotnet-backend-testing/SKILL.md`; `agent-skills/dotnet-backend-testing/agents/openai.yaml`; `agent-skills/dotnet-backend-testing/references/demo-prompts.md`; `AGENTS.md`; `CLAUDE.md`; `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 /Users/qc/.codex/skills/.system/skill-creator/scripts/quick_validate.py agent-skills/dotnet-backend-testing`; output: `Skill is valid!`.
- Limitations: This proves the skill exists and is routed. Future development tasks must append separate entries when the skill is actually used to write or review tests.

### 2026-05-21 - ui-browser-testing - Skill created and routed

- Agent: Codex
- Trigger: User requested a separate UI/browser testing skill for Playwright, browser flows, screenshots, responsive layout, visual review, and `AGENTS.md` auto-trigger rules.
- Action: Created project skill and added required routing rules.
- Output artifacts: `agent-skills/ui-browser-testing/SKILL.md`; `agent-skills/ui-browser-testing/agents/openai.yaml`; `agent-skills/ui-browser-testing/references/demo-prompts.md`; `agent-skills/dotnet-backend-testing/SKILL.md`; `AGENTS.md`; `CLAUDE.md`; `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 /Users/qc/.codex/skills/.system/skill-creator/scripts/quick_validate.py agent-skills/ui-browser-testing`; output: `Skill is valid!`.
- Limitations: This proves the skill exists and is routed. Future UI/browser tasks must append separate entries when the skill is actually used to write tests, run Playwright, inspect screenshots, or verify browser behavior.

### 2026-05-21 - cloud-architecture-cost-review - Skill created and routed

- Agent: Codex
- Trigger: User requested a reusable architecture and cost review skill to prevent overbuilt or expensive cloud choices such as unnecessary always-on Azure App Service for low-usage workloads.
- Action: Created project skill, added a reusable architecture cost review template script, and added required routing rules.
- Output artifacts: `agent-skills/cloud-architecture-cost-review/SKILL.md`; `agent-skills/cloud-architecture-cost-review/agents/openai.yaml`; `agent-skills/cloud-architecture-cost-review/scripts/cost_review_template.py`; `agent-skills/cloud-architecture-cost-review/references/demo-prompts.md`; `AGENTS.md`; `CLAUDE.md`; `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 /Users/qc/.codex/skills/.system/skill-creator/scripts/quick_validate.py agent-skills/cloud-architecture-cost-review`; output: `Skill is valid!`. Ran `python3 agent-skills/cloud-architecture-cost-review/scripts/cost_review_template.py "Azure App Service vs Azure Functions"` and confirmed it prints the architecture cost review headings.
- Limitations: This proves the skill exists and is routed. It does not quote current Azure prices; future exact cost reviews must verify current official provider pricing before using specific numbers.

### 2026-05-21 - cloud-architecture-cost-review - Azure Functions cold start options

- Agent: Codex
- Trigger: User asked whether the project currently uses Azure Functions and how to handle Azure Functions cold start without wasting money.
- Action: Opened and followed the skill; reviewed current project docs and the Azure Functions .NET isolated worker setup; compared low-cost Consumption, async polling, Flex Consumption always-ready, Premium, and App Service tradeoffs.
- Output artifacts: Answer delivered in the Codex thread; no code or infrastructure changed.
- Verification evidence: Confirmed `docs/dotnet-azure-full-run-result.md` records `replyinmyvoice-func-dev` on Linux consumption plan `Y1`; confirmed `backend-dotnet/src/ReplyInMyVoice.Functions/Program.cs` is a minimal .NET 8 isolated worker startup; consulted Microsoft Learn Azure Functions hosting/cold-start documentation.
- Limitations: No live Azure settings were changed and no current Azure pricing numbers were quoted. Exact cost decisions still require checking current Azure pricing for the selected region and plan.

### 2026-05-21 - cloud-architecture-cost-review - Flex Consumption Always Ready cost estimate

- Agent: Codex
- Trigger: User asked for the monthly cost of Azure Functions Flex Consumption with Always Ready for the current project.
- Action: Opened and followed the skill; checked current Azure Retail Prices API for Functions Flex Consumption in `australiaeast`; estimated baseline always-ready monthly cost for 512 MB, 2 GB, and 4 GB instance sizes.
- Output artifacts: Cost estimate delivered in the Codex thread; no code or infrastructure changed.
- Verification evidence: Azure Retail Prices API returned Flex Consumption `Always Ready Baseline`, `Always Ready Execution Time`, and `Always Ready Total Executions` meters for `australiaeast`; monthly estimate used 730 hours/month and the 2 GB default Flex instance size documented by Microsoft Learn.
- Limitations: NZD conversion is approximate; exact billed amount can vary by Azure account currency, taxes, region availability, plan configuration, actual instance memory, execution duration, and whether always-ready is enabled for only HTTP or multiple function groups.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek provider cost discussion

- Agent: Codex
- Trigger: User asked to discuss switching rewrite model roles to DeepSeek, model-tier allocation, and third-party writing-signal provider options before implementation.
- Action: Opened and followed the skill as a cost and provider gate; checked current DeepSeek official model/base URL/pricing docs and compared provider-call cost risk at the architecture level.
- Output artifacts: Discussion delivered in the Codex thread; no business code, environment files, provider configuration, or infrastructure changed.
- Verification evidence: DeepSeek official docs confirmed `https://api.deepseek.com`, `deepseek-v4-flash`, `deepseek-v4-pro`, thinking-mode controls, and the current v4-pro discount window.
- Limitations: This was a pre-implementation discussion only. No live DeepSeek call, key validation, cost benchmark, or production rollout was performed.

### 2026-05-21 - system-spec-synthesis - Rewrite orchestrator context discussion

- Agent: Codex
- Trigger: User asked to discuss a rewrite-orchestrator fix where failed attempts carry original input, previous rewrites, and failure analysis into the next loop.
- Action: Opened and followed the skill; reviewed `docs/fact-reconstruct-rewrite-target.md`, `docs/superpowers/plans/2026-05-20-adaptive-rewrite-agent-orchestrator.md`, and related strategy-memory notes before proposing the design.
- Output artifacts: Discussion delivered in the Codex thread; no implementation spec file or code changed.
- Verification evidence: Project docs confirmed the existing fact-reconstruct pipeline, adaptive orchestrator plan, attempt/failure-kind concepts, Sapling constraints, and no-charge quality-failure rule.
- Limitations: No formal spec artifact was written because the user requested discussion before implementation.

### 2026-05-21 - state-machine-modeling - Rewrite attempt loop discussion

- Agent: Codex
- Trigger: User asked about carrying each failed rewrite attempt and diagnosis into later rewrite/repair loops, which affects the multi-step rewrite-attempt lifecycle.
- Action: Opened and followed the skill; modeled the proposed loop as a bounded attempt ledger with explicit attempt states, transition evidence, retry decisions, success, and quality-failure terminal behavior.
- Output artifacts: Discussion delivered in the Codex thread; no state-machine code or tests changed.
- Verification evidence: Reviewed the adaptive orchestrator plan's required failure kinds, strategy decisions, budget manager, gates, and quality-failure/no-charge behavior.
- Limitations: This was a design discussion only and did not add typed states, transition helpers, or regression tests.

### 2026-05-21 - resilience-test-generation - Provider and writing-signal discussion

- Agent: Codex
- Trigger: User asked about DeepSeek provider routing, per-role thinking configuration, Sapling feedback usage, and alternate writing-signal providers.
- Action: Opened and followed the skill; framed the future implementation around provider timeouts, unavailable writing-signal responses, retry budget limits, no-charge quality failures, and deterministic/local test coverage.
- Output artifacts: Discussion delivered in the Codex thread; no provider adapter, tests, or eval scripts changed.
- Verification evidence: Official Sapling docs confirmed sentence/token-level signal outputs and false-positive/false-negative cautions; project docs confirmed Sapling unavailability is a quality-failure/no-charge condition in the fact-reconstruct route.
- Limitations: No live provider failure test, rate-limit simulation, or alternate signal integration was run.

### 2026-05-21 - cloud-architecture-cost-review - Ten-attempt DeepSeek rewrite budget discussion

- Agent: Codex
- Trigger: User proposed using DeepSeek v4-pro for all rewrite roles initially, allowing up to 10 rewrite/repair attempts, and using DeepSeek credentials already provided outside the thread.
- Action: Opened and followed the skill as a provider-cost and retry-budget gate; treated 10 attempts as a maximum error-budgeted quality path rather than the default path.
- Output artifacts: Discussion delivered in the Codex thread; no provider adapter, environment file, production code, deployment config, or paid infrastructure changed.
- Verification evidence: Prior official DeepSeek documentation check confirmed model names, base URL, thinking controls, and current discounted v4-pro pricing; this turn also generated the synthetic 100-case eval corpus to support staged testing before production rollout.
- Limitations: No live DeepSeek call, key validation, cost benchmark, or production request budget measurement was performed.

### 2026-05-21 - system-spec-synthesis - Attempt history and 100-case eval discussion

- Agent: Codex
- Trigger: User clarified that repair attempts must carry all previous failed originals and asked for a broad 100-case email evaluation corpus before implementation.
- Action: Opened and followed the skill; framed the future implementation as an attempt ledger plus source-of-truth fact ledger, then used Codex 5.5 CLI in batches to generate the requested synthetic evaluation markdown.
- Output artifacts: `docs/rewrite-email-eval-cases-100.md`; discussion delivered in the Codex thread.
- Verification evidence: Verified the markdown contains 100 case headings, 100 `id` fields, and 100 instances of each required field; verified the file is ASCII-only.
- Limitations: This was not a production implementation. The case corpus is synthetic and still needs conversion into runnable eval fixtures before it can drive automated scoring.

### 2026-05-21 - state-machine-modeling - Ten-attempt rewrite attempt ledger discussion

- Agent: Codex
- Trigger: User specified that failed rewrite attempts must carry original input, each failed rewrite, and each failure analysis into the next loop, with a maximum of 10 attempts.
- Action: Opened and followed the skill; modeled the future rewrite request as a bounded lifecycle with attempt records, failure evidence, strategy transitions, budget exhaustion, success, and quality-failure/no-charge terminal states.
- Output artifacts: Discussion delivered in the Codex thread; no state-machine code or tests changed.
- Verification evidence: The proposed ledger design preserves attempt number, strategy, candidate text, reviewer issues, fact-gate result, structure-gate result, Sapling result, diagnosis, and next strategy decision across retries.
- Limitations: No typed transition function, persistence schema, or regression tests were added in this turn.

### 2026-05-21 - resilience-test-generation - Ten-attempt provider and gate resilience discussion

- Agent: Codex
- Trigger: User's max-10 retry design touches provider failures, Sapling signal use, failed candidate carry-forward, quality-gate failures, and no-charge behavior.
- Action: Opened and followed the skill; framed future tests around bounded retries, malformed model output, provider timeout/rate-limit, Sapling unavailable, fact-gate failure, structural-gate failure, and budget exhaustion.
- Output artifacts: Discussion delivered in the Codex thread; no test files changed.
- Verification evidence: The generated 100-case corpus includes high-risk cases for policy/intent gates, quote/list boundaries, support macro voice, sentence-per-paragraph drafts, broken numbered lists, messy forwarded threads, dense facts, low-signal already-natural drafts, and no-change-without-confirmation constraints.
- Limitations: No deterministic fakes, live provider calls, or automated eval run were executed.

### 2026-05-21 - system-spec-synthesis - Gate rule repair discussion

- Agent: Codex
- Trigger: User asked whether failed rewrite reasons should lead to repairing gate rules.
- Action: Opened and followed the skill; framed gate repair as a source-of-truth distinction between real output failures, false-positive gate failures, false-negative gate misses, and fact-ledger defects.
- Output artifacts: Discussion delivered in the Codex thread; no implementation spec or code changed.
- Verification evidence: Reviewed `docs/rewrite-strategy-memory.md` sections on Adaptive Gate Calibration and Reviewed Fact Ledger Before Rewrite.
- Limitations: No gate rule change, tests, or eval run was performed.

### 2026-05-21 - state-machine-modeling - Gate lifecycle discussion

- Agent: Codex
- Trigger: User asked about changing gate behavior based on failure reasons, which affects the rewrite attempt and quality-gate lifecycle.
- Action: Opened and followed the skill; described gate outcomes as hard block, soft diagnostic, repair route, or quality-failure terminal state.
- Output artifacts: Discussion delivered in the Codex thread; no state-machine code changed.
- Verification evidence: Reviewed project state-machine guidance and existing strategy-memory notes distinguishing hard business facts from soft footer/polite-formula issues.
- Limitations: No typed transition helper, enum, or regression test was added.

### 2026-05-21 - resilience-test-generation - Gate regression discussion

- Agent: Codex
- Trigger: User asked whether failure reasons should drive gate fixes, which requires tests for false positives, false negatives, provider failures, and budget exhaustion.
- Action: Opened and followed the skill; stated that any gate repair must ship with targeted regression cases proving it fixes the observed miss without weakening hard fact protections.
- Output artifacts: Discussion delivered in the Codex thread; no tests changed.
- Verification evidence: Reviewed resilience skill guidance and existing strategy-memory required regressions for soft footer misses versus hard money/date/policy failures.
- Limitations: No deterministic fake, focused test command, or eval command was run.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek attempt-ledger strategy doc

- Agent: Codex
- Trigger: User asked to formalize the DeepSeek v4-pro rewrite strategy, including max 10 attempts and provider routing, before opening a new test window.
- Action: Followed the provider cost and budget gate; recorded v4-pro as the current quality-first model for all rewrite roles, deferred v4-flash until quality stabilizes, and documented non-thinking versus thinking/high reasoning budget use.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The strategy doc records `OPENAI_BASE_URL=https://api.deepseek.com`, all current model roles on `deepseek-v4-pro`, a hard 10-attempt cap, and no-secret handling.
- Limitations: No live DeepSeek call, key validation, provider adapter change, cost benchmark, or production rollout was performed.

### 2026-05-21 - system-spec-synthesis - DeepSeek rewrite strategy doc

- Agent: Codex
- Trigger: User asked to write the current rewrite-orchestrator strategy into a new markdown file and update `AGENTS.md` so a new test window can use it.
- Action: Followed the system-spec workflow; converted the discussion into an implementation-ready strategy covering source-of-truth inputs, runtime pipeline, attempt ledger schema, Sapling usage, gate calibration, evaluation modes, and success criteria.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The new strategy document includes the attempt ledger fields, repair prompt inputs, failure tags, staged 10/40/100 evaluation plan, and references the 100-case corpus path.
- Limitations: This produced strategy documentation only. No code, tests, prompts, provider configuration, or eval fixtures were changed.

### 2026-05-21 - state-machine-modeling - Attempt lifecycle strategy doc

- Agent: Codex
- Trigger: The strategy changes the rewrite-attempt lifecycle by requiring all failed candidate texts and failure analyses to flow into later attempts with a hard max of 10 attempts.
- Action: Followed the state-machine workflow; documented attempt records, transition evidence, budget exhaustion, success, and quality-failure/no-charge terminal behavior.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The strategy doc defines the attempt budget table, required attempt ledger fields, failure tags, and quality-failure behavior when no candidate passes within the budget.
- Limitations: No typed state enum, transition helper, persistence schema, or automated lifecycle tests were added.

### 2026-05-21 - resilience-test-generation - Attempt ledger and gate strategy doc

- Agent: Codex
- Trigger: The max-10 DeepSeek loop and gate calibration policy touch provider failures, Sapling availability, quality-failure/no-charge behavior, and regression requirements for gate changes.
- Action: Followed the resilience workflow; documented bounded retry behavior, Sapling unavailable handling, hard versus soft gate failures, structural blockers, and paired regression requirements for future gate repairs.
- Output artifacts: `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `AGENTS.md`; `docs/skill-run-log.md`.
- Verification evidence: The strategy doc requires paired gate regressions, no weakened hard fact protections, no successful weak fallback, and staged evaluation using `docs/rewrite-email-eval-cases-100.md`.
- Limitations: No provider timeout fake, Sapling failure test, quality-failure test, or focused eval command was run.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek test window readiness review

- Agent: Codex
- Trigger: User asked whether the new DeepSeek rewrite-quality strategy and 100-case corpus are ready for testing, which affects AI/provider call routing and evaluation cost boundaries.
- Action: Followed the provider cost and budget gate; checked the strategy document, AGENTS routing note, environment examples, model/provider call sites, evaluation scripts, and staged eval assumptions before any live provider calls.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Offline checks confirmed `docs/rewrite-email-eval-cases-100.md` has 100 cases, the strategy document has no non-ASCII characters, common secret patterns were not found in the reviewed docs, targeted rewrite/provider unit tests passed, and `npm run typecheck` passed.
- Limitations: No live DeepSeek, OpenAI, or Sapling call was run. Exact provider pricing was not rechecked because no external paid test was executed. Current code still hard-codes the OpenAI chat completions URL and existing eval scripts do not yet consume the 100-case markdown corpus.

### 2026-05-21 - resilience-test-generation - DeepSeek test window readiness review

- Agent: Codex
- Trigger: User asked whether testing can start for a strategy involving provider routing, max-attempt behavior, Sapling unavailable handling, quality-failure/no-charge semantics, and gate repair regression expectations.
- Action: Followed the resilience review workflow; inspected current provider boundaries, Sapling retry/unavailable behavior, budget-manager limits, strategy-router failure handling, quality-failure tests, and eval reporting before recommending live testing.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Targeted unit tests passed for rewrite budget management, strategy routing, writing-signal behavior, fact-reconstruct model parsing, legacy OpenAI model config, and API quality-failure ordering.
- Limitations: No deterministic DeepSeek fake, OpenAI-compatible base URL test, Sapling timeout eval, max-10 attempt ledger test, or 100-case eval parser was added in this turn.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek test-window implementation gate

- Agent: Codex
- Trigger: User asked to start the next step but stop before live testing, involving DeepSeek/OpenAI-compatible provider routing and AI/provider evaluation budget boundaries.
- Action: Followed the provider cost gate; kept production defaults unchanged, added explicit empty env placeholders, added a test-window-only max-attempt override clamped to 10, and stopped before any paid/live provider call.
- Output artifacts: `lib/openai-compatible.ts`; `.env.example`; `lib/rewrite-pipeline/budget-manager.ts`; `docs/superpowers/plans/2026-05-21-deepseek-test-readiness.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm run lint` passed, `npm run typecheck` passed, and `npm run test` passed with 32 files and 190 tests.
- Limitations: No exact DeepSeek/OpenAI/Sapling pricing was checked because no live paid test was executed. No provider call, deployment, or remote smoke test was run.

### 2026-05-21 - system-spec-synthesis - DeepSeek test-readiness implementation

- Agent: Codex
- Trigger: The task converted the DeepSeek adaptive strategy notes into executable code/test checkpoints across provider routing, eval corpus loading, budget behavior, and attempt-ledger metadata.
- Action: Followed the spec workflow at implementation scope; created a focused implementation plan, mapped the active strategy requirements into code files and tests, and preserved the stop-before-live-testing boundary.
- Output artifacts: `docs/superpowers/plans/2026-05-21-deepseek-test-readiness.md`; `lib/openai-compatible.ts`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; provider/eval/budget/ledger unit tests.
- Verification evidence: Added tests first and observed expected failures, then implemented the minimal code; final verification was `npm run lint`, `npm run typecheck`, and `npm run test` with 190 passing tests.
- Limitations: This prepared local readiness only. It did not run the 100-case live eval, inspect live provider schemas, validate real keys, or deploy.

### 2026-05-21 - state-machine-modeling - Rewrite attempt ledger implementation

- Agent: Codex
- Trigger: The DeepSeek strategy requires a bounded attempt lifecycle with failed candidate evidence, failure analysis, strategy transitions, and a hard maximum of 10 attempts.
- Action: Followed the lifecycle workflow; added a typed attempt-ledger entry, a helper that rejects attempt numbers outside 1-10, metadata on success/failure payloads, and escalation prompts that receive prior failed attempts as negative evidence.
- Output artifacts: `lib/rewrite-pipeline/types.ts`; `lib/rewrite-pipeline/attempt-ledger.ts`; `lib/rewrite-pipeline/pipeline.ts`; `lib/rewrite-pipeline/model.ts`; `tests/unit/rewrite-attempt-ledger.test.ts`; `tests/unit/rewrite-pipeline-model.test.ts`.
- Verification evidence: `tests/unit/rewrite-attempt-ledger.test.ts` verifies ledger shape and hard 10-attempt rejection; `tests/unit/rewrite-pipeline-model.test.ts` verifies escalation prompts include failed candidate text and failure analysis.
- Limitations: The ledger is in response/error metadata and retry prompts, not a persisted database table. No live multi-attempt eval run was executed.

### 2026-05-21 - resilience-test-generation - DeepSeek provider and eval readiness implementation

- Agent: Codex
- Trigger: The implementation touches provider boundary routing, provider-key selection, Sapling/no-charge gate context, staged eval loading, and bounded retry budget behavior.
- Action: Followed the resilience test workflow; added deterministic unit tests with fake fetch for OpenAI-compatible routing and DeepSeek key selection, parser tests for the 100-case corpus, budget override tests, and escalation prompt-history tests.
- Output artifacts: `tests/unit/openai-compatible.test.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; `tests/unit/rewrite-budget-manager.test.ts`; `tests/unit/rewrite-attempt-ledger.test.ts`; `tests/unit/rewrite-pipeline-model.test.ts`; related implementation files.
- Verification evidence: The initial focused test run failed for missing helper/parser/ledger/override behavior; after implementation, `npm run test` passed with 32 files and 190 tests, `npm run typecheck` passed, and `npm run lint` passed.
- Limitations: No live timeout/rate-limit test against DeepSeek, OpenAI, or Sapling was run. Provider failure coverage remains fake-based local unit coverage until the user approves the live test window.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek live smoke test

- Agent: Codex
- Trigger: User explicitly asked to start testing after the DeepSeek test-window readiness work, which creates live DeepSeek and Sapling provider cost exposure.
- Action: Followed the provider cost gate; ran 10-case smoke instead of focused/full, kept all model roles on `deepseek-v4-pro`, used `OPENAI_BASE_URL=https://api.deepseek.com`, disabled thinking for ordinary JSON calls, and kept timeout/case scope bounded.
- Output artifacts: `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; DeepSeek request-option changes in `lib/rewrite-pipeline/model.ts` and tests.
- Verification evidence: Final full-context smoke wrote `docs/scenario-evaluation-results.md` with 10 measured cases, 60-point average signal drop, 10/10 rewrites below 50%, 0/10 worse selected rewrites, 2/10 customer-usable passes, and 8 fact-preservation or unsupported-addition failures.
- Limitations: Exact provider cost was not calculated from billing dashboards. The run was smoke only, not focused or full. No deploy or remote production smoke test was run.

### 2026-05-21 - resilience-test-generation - DeepSeek smoke failure handling

- Agent: Codex
- Trigger: The live smoke hit provider timeout and empty-content failures before completing, then exposed eval harness input-loss defects and no-charge quality failures.
- Action: Followed the resilience workflow; isolated provider health with a minimal DeepSeek request, diagnosed case-level timeout boundaries, added tests for DeepSeek thinking-mode request bodies, fixed the eval harness to preserve context fields, and reran smoke with bounded timeouts.
- Output artifacts: `tests/unit/rewrite-pipeline-model.test.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; `lib/rewrite-pipeline/model.ts`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Minimal DeepSeek health check returned status 200; `extractFacts` diagnostic completed in 14.4 seconds after disabling thinking; final smoke completed 10/10 case boundaries and wrote results. `npm run lint`, `npm run typecheck`, and `npm run test` passed after fixes.
- Limitations: The smoke still fails quality criteria at 2/10 customer-usable. Provider failure tests remain local/fake-based except for this live smoke. No retry/resume support was added to the eval script.

### 2026-05-21 - system-spec-synthesis - Deterministic rewrite gate architecture review

- Agent: Codex
- Trigger: User asked whether the current rewrite architecture has deterministic gates and restrictions similar to an Agent Governance policy gate.
- Action: Opened and followed the skill at discussion scope; reviewed the current rewrite pipeline, quality gates, policy/intent gate, budget manager, validation, quota, observability, and active rewrite strategy docs.
- Output artifacts: Review delivered in the Codex thread; `docs/skill-run-log.md`.
- Verification evidence: Static review confirmed implemented gates in `lib/rewrite-pipeline/pipeline.ts`, `lib/rewrite-pipeline/checks.ts`, `lib/rewrite-pipeline/policy-intent-gate.ts`, `lib/rewrite-pipeline/fact-ledger.ts`, `lib/rewrite-pipeline/budget-manager.ts`, `app/api/rewrite/route.ts`, `lib/quota.ts`, and `lib/observability/rewrite-telemetry.ts`.
- Limitations: No formal spec file, code change, automated test run, live provider call, or deployment was performed in this turn.

### 2026-05-21 - state-machine-modeling - Deterministic rewrite gate lifecycle review

- Agent: Codex
- Trigger: The question maps rewrite attempts, gate failures, retries, budget exhaustion, success, and quality-failure/no-charge behavior to a multi-step lifecycle.
- Action: Opened and followed the skill at review scope; modeled the existing rewrite path as an attempt lifecycle with deterministic preflight, candidate generation, review, fact/structure/policy/naturalness gates, bounded repair/escalation, fallback, success, or terminal quality failure.
- Output artifacts: Review delivered in the Codex thread; `docs/skill-run-log.md`.
- Verification evidence: Static review confirmed an in-memory attempt ledger, hard attempt cap helper, budget approval checks, failure-kind routing, quality-failure terminal path, and no-charge API response.
- Limitations: The attempt ledger is not persisted as a state table, no transition helper was added, and no lifecycle regression tests were run in this turn.

### 2026-05-21 - system-spec-synthesis - Deterministic rewrite gate fixes

- Agent: Codex
- Trigger: User approved starting fixes for the deterministic rewrite gate review findings, including confirmed input limits, context-field contract alignment, and policy gate auditability.
- Action: Opened and followed the skill at implementation scope; converted the review into focused changes across validation, workspace UI, and policy/intent gate metadata.
- Output artifacts: `lib/rewrite-limits.ts`; `lib/validation.ts`; `components/app/rewrite-workspace.tsx`; `lib/rewrite-pipeline/policy-intent-gate.ts`; `lib/rewrite-pipeline/types.ts`; related unit tests.
- Verification evidence: Added failing tests first, then made them pass. Final verification: `npm test` passed with 32 files and 194 tests; `npm run typecheck` passed; `npm run lint` passed.
- Limitations: This did not implement OpenTelemetry/SIEM export or a persisted rewrite-attempt state table.

### 2026-05-21 - ui-browser-testing - Rewrite workspace input contract check

- Agent: Codex
- Trigger: The fix changed the `/app` rewrite workspace form fields, counters, submit payload, and responsive layout.
- Action: Opened and followed the skill; ran focused UI source tests, started the local Next dev server, rendered a temporary local preview route for the authenticated workspace component, checked desktop and mobile screenshots, then deleted the temporary route.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `tests/unit/workspace-copy.test.ts`; temporary preview route was removed before completion.
- Verification evidence: Playwright browser check returned HTTP 200 on desktop and mobile, labels were `Context or message`, `Draft to rewrite`, `Audience`, `Purpose`, `What actually happened`, and `Facts to preserve`; desktop and mobile had no horizontal overflow and no console/page errors. `npm run test:e2e -- tests/e2e/auth-gate.spec.ts` passed with 2/2 tests.
- Limitations: The production `/app` route remains auth-gated, so the visual check used a temporary local preview route with the real `RewriteWorkspace` component and dummy props.

### 2026-05-21 - resilience-test-generation - Rewrite gate fix routing check

- Agent: Codex
- Trigger: The initial repair discussion mentioned quota/no-charge and provider-gate concerns, so the resilience skill was opened to check whether failure-mode test guidance applied.
- Action: Opened for routing; final implementation scope stayed on deterministic input limits, UI contract, and policy metadata, without changing retries, provider failures, quota races, idempotency, webhook replay, or recovery behavior.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Existing quota/no-charge behavior was not changed; full unit suite still passed with 194 tests, including quota and rewrite quality-failure tests.
- Limitations: No new resilience matrix, provider timeout fake, Sapling unavailable test, concurrency test, or quota race test was added because those failure modes were out of scope for this focused fix.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek smoke failure strategy

- Agent: Codex
- Trigger: User asked to think through the current live DeepSeek smoke failures and modification strategy after a provider-backed test window.
- Action: Followed the cost gate at analysis scope; recommended stopping at smoke, avoiding focused/full provider spend, and improving local ledger/gate regressions before more live eval calls.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Reviewed `docs/scenario-evaluation-results.md`, which showed 10 measured smoke cases, 60-point average signal drop, 10/10 below 50% AI-like signal, but only 2/10 customer-usable passes and 8 fact-preservation failures.
- Limitations: No billing dashboard cost calculation, no new provider call, no focused/full eval, and no code change were performed in this strategy-only pass.

### 2026-05-21 - system-spec-synthesis - DeepSeek failure-to-fix strategy

- Agent: Codex
- Trigger: User asked for root-cause thinking and a modification strategy for the current rewrite-quality failures.
- Action: Followed the spec-synthesis workflow at analysis scope; converted smoke evidence into an implementation-ready fix sequence covering fact/constraint ledger, strategy routing, candidate contracts, structural gates, and regression order.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Mapped observed failures from `docs/scenario-evaluation-results.md` to concrete system changes and added a strategy-memory section with root cause, promoted strategy, and required regressions.
- Limitations: No formal implementation plan file or code patch was created in this turn.

### 2026-05-21 - state-machine-modeling - DeepSeek attempt lifecycle failure diagnosis

- Agent: Codex
- Trigger: The failure analysis involved bounded rewrite attempts, failed candidate evidence, repair transitions, terminal `fact_check_failed`, and quality-failure/no-charge behavior.
- Action: Followed the lifecycle workflow at analysis scope; identified that attempts are being spent without a strong enough transition from generic repair to facts-first reconstruct or policy/options rewrite after hard fact misses.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Smoke report showed 9/10 cases using targeted repair, 17 rejected candidate events, and 8 terminal fact failures despite Naturalness Check improvement.
- Limitations: No transition helper, persisted attempt table, or lifecycle test was added in this turn.

### 2026-05-21 - resilience-test-generation - DeepSeek smoke regression strategy

- Agent: Codex
- Trigger: The live smoke failures require regression coverage for hard fact loss, constraint handling, awkward greeting inference, repeated facts, and no-charge terminal failures.
- Action: Followed the resilience test workflow at analysis scope; proposed local regression coverage for failed smoke cases before spending on another provider-backed focused/full run.
- Output artifacts: `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`; strategy summary in the Codex thread.
- Verification evidence: Strategy-memory now requires regressions for failed smoke cases, weird greetings, fact dumps, and `Do not...` constraints as forbidden-claim absence checks.
- Limitations: No new test files were created and no test command was run in this turn.

### 2026-05-21 - system-spec-synthesis - DeepSeek local repair pass 1

- Agent: Codex
- Trigger: User approved the next repair pass after the 10-case DeepSeek smoke showed fact/gate failures.
- Action: Followed the spec workflow at implementation scope; converted the failure diagnosis into concrete changes across deterministic fact extraction, eval expectation parsing, structural gates, and fallback routing.
- Output artifacts: `lib/fact-extraction.ts`; `lib/rewrite-eval-cases.ts`; `lib/rewrite-pipeline/checks.ts`; `lib/openai.ts`; `scripts/eval-scenarios.ts`; related unit tests; `docs/rewrite-strategy-memory.md`.
- Verification evidence: Added failing tests first, then made them pass. Final verification: `npm run test` passed with 32 files and 201 tests, `npm run typecheck` passed, and `npm run lint` passed.
- Limitations: This was local-only. No live DeepSeek/OpenAI/Sapling eval, focused run, full run, deployment, or remote smoke test was executed.

### 2026-05-21 - state-machine-modeling - Rewrite gate transition repair pass 1

- Agent: Codex
- Trigger: The repair changed how failed attempts are prevented from passing through structural and fact-gate states, including terminal quality-failure causes from the smoke.
- Action: Followed the lifecycle workflow at implementation scope; tightened transition conditions by making unsupported greetings, repeated fact dumps, and internal context echoes hard deterministic structure failures before a candidate can reach success.
- Output artifacts: `lib/rewrite-pipeline/checks.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Added tests for `Hi Finance`, `Hi Reopening`, repeated facts, and `the user` context echo; focused and full unit suites passed.
- Limitations: The attempt ledger remains in response/error metadata, not persisted as a database table. No new transition function was introduced.

### 2026-05-21 - resilience-test-generation - DeepSeek local regression repair pass 1

- Agent: Codex
- Trigger: The smoke failures involved provider-backed quality failures, no-charge fact-check exits, overbroad fallback routing, and brittle eval checks.
- Action: Followed the resilience test workflow; added deterministic local regressions for hard fact source selection, forbidden-claim separation, damaged-item and sales-onboarding fallback routing, status-noun greetings, repeated fact dumps, and eval expectation normalization.
- Output artifacts: `tests/unit/fact-extraction.test.ts`; `tests/unit/rewrite-pipeline-checks.test.ts`; `tests/unit/openai-output.test.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; related implementation files.
- Verification evidence: Focused test run passed for the four changed test files, then `npm run test`, `npm run typecheck`, and `npm run lint` passed.
- Limitations: Resilience coverage is local and deterministic. No live timeout, rate-limit, provider-unavailable, or Sapling-unavailable test was run in this repair pass.

### 2026-05-21 - cloud-architecture-cost-review - DeepSeek smoke after local repair pass 1

- Agent: Codex
- Trigger: User asked to run exactly one 10-case provider-backed smoke after local rewrite-quality repairs, which creates DeepSeek and Sapling variable cost exposure.
- Action: Used the project skill fallback at `agent-skills/cloud-architecture-cost-review/SKILL.md`; kept scope to smoke only, rejected focused/full eval for this turn, kept all model roles on `deepseek-v4-pro`, used `OPENAI_BASE_URL=https://api.deepseek.com`, and stopped after 10 cases.
- Output artifacts: `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Smoke command exited 0 and wrote `docs/scenario-evaluation-results.md` with 10 measured cases, 62-point average signal drop, 10/10 below 50%, 0/10 worse selected rewrites, 4/10 customer-usable passes, and 6 fact-preservation or unsupported-addition failures.
- Limitations: Exact provider billing cost was not checked in dashboards. No focused/full eval, deploy, or remote smoke test was run. The global skill path was unavailable, so the project `agent-skills` copy was used.

### 2026-05-21 - resilience-test-generation - DeepSeek smoke after local repair pass 1

- Agent: Codex
- Trigger: User asked for the 10-case smoke and memory update after local fixes for fact-gate and fallback-routing failures.
- Action: Followed the resilience workflow at execution/review scope; ran the bounded smoke once, inspected pass/failure rows, compared failure kinds, and recorded the next regression targets.
- Output artifacts: `docs/scenario-evaluation-results.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Smoke improved from 2/10 to 4/10 customer-usable, while failures remained fail-closed/no-charge with `fact_check_failed`. New residual issues were identified: locked `Do not...` constraints still entering runtime facts, and unsafe `Hi Upgrade` / `Hi Original` recipient restoration.
- Limitations: This was a live smoke, not a local fake test or full regression implementation. No new code fix was applied after the smoke, by user instruction to stop after this 10-case window.

### 2026-05-21 - cloud-architecture-cost-review - GitHub push and Cloudflare deployment

- Agent: Codex
- Trigger: User asked to push the current local work to GitHub and deploy the current project online.
- Action: Used the project skill fallback at `agent-skills/cloud-architecture-cost-review/SKILL.md`; reviewed the existing Cloudflare Worker deployment route, GitHub Actions deployment workflows, project deployment notes, and confirmed this release uses current infrastructure without adding paid cloud resources.
- Output artifacts: Current git commit, GitHub push, Cloudflare deployment command, and `docs/skill-run-log.md`.
- Verification evidence: Pre-deploy checks passed: `npm run test` passed with 201 tests, `npm run typecheck` passed, `npm run lint` passed, `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed with 38 tests, and `npm run cf:build` passed.
- Limitations: Exact Cloudflare and GitHub Actions billing dashboards were not checked. Final live deployment and remote smoke results are reported in the Codex thread, not duplicated with any secret-bearing environment details.

## 2026-05-22 - Codex - data-module-review
- Trigger: User requested Prisma schema update and commit/push/PR for ApiKey and ApiKeyUsage migration.
- Action taken: Opened/followed the data-module-review skill as a fast additive-schema checklist while applying the user-provided schema patch.
- Output artifacts: prisma/schema.prisma updated with User.apiKeys, ApiKey, and ApiKeyUsage models; existing migration SQL staged.
- Verification evidence: grep counted ApiKey model declarations before commit.
- Limitations: No full Prisma validation or test suite run due to explicit fast commit/push/PR request.

### 2026-05-22 - system-spec-synthesis - M2.5-003 diagnosis clustering

- Agent: Codex
- Trigger: The task converted a loose milestone brief into an implementation-ready learning-analysis and persistence change.
- Action: Opened and followed the skill at implementation scope; identified source inputs, mapped the clustering contract to `LearningFinding` rows, and kept the change additive.
- Output artifacts: `lib/learningops/cluster.ts`; `lib/learningops.ts`; `scripts/learningops-run.ts`; `tests/unit/learningops-cluster.test.ts`; `tests/unit/learningops.test.ts`; Prisma schema and migration updates.
- Verification evidence: Red test observed first for the missing cluster module, then focused tests passed; final `npm run lint`, `npm run typecheck`, `npm run test`, and Prisma schema validation with dummy local URLs passed.
- Limitations: No provider-backed evaluation, production database migration apply, deployment, or GitHub operation was run.

### 2026-05-22 - data-module-review - M2.5-003 LearningFinding cluster fields

- Agent: Codex
- Trigger: The task changed Prisma schema, migration SQL, and the LearningOps insert path for persisted findings.
- Action: Opened and followed the skill; ran the data-risk scanner, reviewed owned tables and the mutating script, and used nullable additive columns plus indexes for migration safety.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260522123000_add_learning_finding_cluster_fields/migration.sql`; `scripts/learningops-run.ts`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed; Prisma schema validation with dummy local URLs passed; lint, typecheck, and unit tests passed.
- Limitations: The migration was not applied to a live database in this turn, and no raw learning sample text was inspected or logged.

### 2026-05-22 - data-module-review - M2.5-004 StrategyCandidate structured patches

- Agent: Codex
- Trigger: The task changed LearningOps persistence by adding structured prompt/strategy patch metadata to `StrategyCandidate` rows.
- Action: Opened and followed the skill; reviewed the owned tables and `scripts/learningops-run.ts` mutator, ran the data-risk scanner, and used nullable additive columns plus an index for migration safety.
- Output artifacts: `lib/learningops/candidates.ts`; `lib/learningops.ts`; `scripts/learningops-run.ts`; `prisma/schema.prisma`; `prisma/migrations/20260522124500_add_strategy_candidate_structured_patch_fields/migration.sql`; `tests/unit/learningops-candidates.test.ts`; `tests/unit/learningops.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red focused tests failed for the missing candidate module and missing promotion; after implementation, focused tests passed. Final `npm run lint`, `npm run typecheck`, `npm run test`, banned-term scan, and Prisma schema validation with dummy local URLs passed.
- Limitations: The migration was not applied to a live database. No provider-backed rewrite evaluation, deployment, or GitHub operation was run.

### 2026-05-22 - system-spec-synthesis - M2.5-005 promotion handoff

- Agent: Codex
- Trigger: The task converted a roadmap stub for auto-drafting PRs from promotable `StrategyCandidate` rows into an implementation-ready LearningOps handoff contract.
- Action: Opened and followed the skill at implementation scope; mapped source facts from `plans/current-task.md`, the M2.5 roadmap, existing LearningOps modules, and the runbook into a pure promotion-brief builder and daily-run handoff file.
- Output artifacts: `lib/learningops/promotion-brief.ts`; `scripts/learningops-run.ts`; `tests/unit/learningops-promotion-brief.test.ts`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test first failed on the missing promotion-brief module. After implementation, focused LearningOps tests passed, then `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No GitHub, git, Codex MCP, deployment, provider-backed evaluation, or live database run was executed in this turn.

### 2026-05-22 - resilience-test-generation - M2.5-002 incremental eval

- Agent: Codex
- Trigger: The task changed timeout recovery, provider-failure handling, resumable checkpoints, and per-case persistence for the provider-backed eval script.
- Action: Opened and followed the skill; kept provider calls out of unit tests, added parser-only Vitest coverage first, and implemented per-case append plus atomic progress writes so completed work survives later interruption.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `tests/fixtures/learning-corpus-mini.md`; `plans/issues/M2.5-002.md`; `plans/codex-implementation-prompt.md`; `plans/decisions-log.md`.
- Verification evidence: Focused parser test failed before implementation because the parser export was missing and importing the script ran legacy side effects; after refactor, `npm run test -- --run tests/unit/eval-scenarios-corpus.test.ts` passed.
- Limitations: No real 100-case provider evaluation, deployment, or production data access was run.

### 2026-05-22 - cloud-architecture-cost-review - M2.5-007 Cloudflare Cron LearningOps

- Agent: Codex
- Trigger: The task added a scheduled Cloudflare Worker execution path for daily LearningOps.
- Action: Opened and followed the skill; reviewed `docs/manual-setup.md`, `docs/next-development-brief.md`, existing Cloudflare/OpenNext config, and the LearningOps runbook. Selected the existing Worker Cron Trigger instead of a new Worker, Azure timer, queue, or always-on service.
- Output artifacts: `worker.js`; `wrangler.jsonc`; `lib/learningops/scheduled.ts`; `docs/learningops-runbook.md`; `docs/skill-run-log.md`.
- Verification evidence: `npx wrangler deploy --dry-run --outdir /private/tmp/learningops-worker-dry-run` completed with exit 0 and did not deploy. Required validation also passed with `npm run lint`, `npm run typecheck`, and `npm run test`.
- Limitations: Exact Cloudflare billing dashboards were not checked. The dry run emitted a nonfatal local Wrangler log-file permission warning and no production deployment was run.

### 2026-05-22 - system-spec-synthesis - M2.5-007 scheduled LearningOps contract

- Agent: Codex
- Trigger: The task converted the M2.5-007 roadmap stub into an implementation-ready scheduled job contract.
- Action: Opened and followed the skill at implementation scope; mapped source facts from `plans/current-task.md`, the M2.5 roadmap, `docs/next-development-brief.md`, existing LearningOps analyzer/candidate modules, and the promotion handoff into a shared pipeline.
- Output artifacts: `lib/learningops/run.ts`; `lib/learningops/scheduled.ts`; `scripts/learningops-run.ts`; `worker.js`; `tests/unit/learningops-run.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test first failed on the missing `lib/learningops/run` module. After implementation, focused LearningOps tests passed, then `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No live production database run, GitHub operation, Codex MCP PR drafting session, or deployment was executed.

### 2026-05-22 - state-machine-modeling - M2.5-007 LearningRun statuses

- Agent: Codex
- Trigger: The task changed the `LearningRun` lifecycle by requiring terminal statuses `digest_only`, `docs_only`, `promoted`, and `blocked`.
- Action: Opened and followed the skill; modeled the scheduled run as start-blocked-for-safety, analyze recent samples, persist findings/candidates, then transition to the analysis outcome or back to `blocked` on failure.
- Output artifacts: `lib/learningops.ts`; `lib/learningops/run.ts`; `tests/unit/learningops.test.ts`; `tests/unit/learningops-run.test.ts`; `docs/learningops-runbook.md`; `docs/skill-run-log.md`.
- Verification evidence: Unit tests cover `digest_only`, `docs_only`, `promoted`, and failure-to-`blocked`; focused and full Vitest suites passed.
- Limitations: There is no database enum or check constraint for statuses yet; this remains a typed application invariant over the existing text column.

### 2026-05-22 - data-module-review - M2.5-007 LearningOps persistence

- Agent: Codex
- Trigger: The task changed database writes for `LearningRun`, `LearningFinding`, and `StrategyCandidate` from a CLI-only insert path to a shared scheduled pipeline.
- Action: Opened and followed the skill; reviewed `prisma/schema.prisma`, existing LearningOps migrations, and mutating code, then kept the change schema-compatible by reusing existing columns and indexes.
- Output artifacts: `lib/learningops/run.ts`; `scripts/learningops-run.ts`; `tests/unit/learningops-run.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed; focused LearningOps tests and full `npm run test` passed.
- Limitations: The scanner reported many existing quota/idempotency signals outside this change. No live migration or production database write was run.

### 2026-05-22 - resilience-test-generation - M2.5-007 scheduled LearningOps failure handling

- Agent: Codex
- Trigger: The task introduced scheduled background execution and needed deterministic behavior when sample reads or persistence fail.
- Action: Opened and followed the skill; added a local fake SQL test that forces sample-read failure after the run row is created and asserts the run is marked `blocked`.
- Output artifacts: `lib/learningops/run.ts`; `tests/unit/learningops-run.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test failed before the runner existed; after implementation, `tests/unit/learningops-run.test.ts` passed and the full Vitest suite passed.
- Limitations: No live Cloudflare scheduled invocation, production database outage simulation, or external provider failure was run.

### 2026-05-22 - state-machine-modeling - M2.5-008 StrategyCandidate review statuses

- Agent: Codex
- Trigger: The task added admin approval decisions for the persisted `StrategyCandidate.status` lifecycle.
- Action: Opened and followed the skill; modeled admin review as `proposed` to one of `approved`, `needs_revision`, or `rejected`, with later admin correction allowed between review states and invalid statuses rejected before DB writes.
- Output artifacts: `lib/admin/learning.ts`; `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test first failed on the missing admin learning module; after implementation, `tests/unit/admin-learning.test.ts`, `npm run typecheck`, `npm run lint`, and the full `npm run test` suite passed.
- Limitations: The database column remains free text; allowed review states are enforced in the admin mutation helper rather than a DB enum or check constraint.

### 2026-05-22 - data-module-review - M2.5-008 StrategyCandidate admin mutation

- Agent: Codex
- Trigger: The task added an admin UI action that updates `StrategyCandidate.status` in the database.
- Action: Opened and followed the skill; reviewed the LearningOps tables, existing raw SQL patterns, and the new mutation path. Kept the change schema-compatible, scoped the update by candidate id, and added a focused fake-SQL unit test for the update statement.
- Output artifacts: `lib/admin/learning.ts`; `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed and reported existing broad quota/idempotency signals outside this change. Focused and full Vitest suites passed.
- Limitations: No live production database write was run.

### 2026-05-22 - ui-browser-testing - M2.5-008 admin learning page

- Agent: Codex
- Trigger: The task added a frontend admin page and form controls under `/admin/learning`.
- Action: Opened and followed the skill; identified the admin review flow and implemented a dynamic page with recent runs, finding clusters, evidence refs, candidate details, linked work, and approval controls.
- Output artifacts: `app/admin/learning/page.tsx`; `components/admin/admin-shell.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run typecheck`, `npm run lint`, and full `npm run test` passed. Local browser verification was attempted with `npm run dev -- -p 3010` and `npm run dev -- -H 127.0.0.1 -p 3010`, but the sandbox rejected both bind attempts with `EPERM`.
- Limitations: No authenticated browser screenshot was captured because the local dev server could not bind in this sandbox, and no production admin session or live database was used.

### 2026-05-22 - cloud-architecture-cost-review - M2.5-009 canary rollout

- Agent: Codex
- Trigger: The task chose a runtime feature-flag shape for production rewrite strategy canaries and could have introduced Cloudflare KV or another paid runtime dependency.
- Action: Opened and followed the skill; selected environment-gated routing plus existing `RewriteCostLog` telemetry over a new KV namespace or service. The runtime pauses or ramps canary traffic from database metrics without creating paid infrastructure.
- Output artifacts: `lib/rewrite-pipeline/canary.ts`; `app/api/rewrite/route.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused canary tests passed, then `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No exact Cloudflare pricing lookup was needed because no new paid resource was selected, and no production flag was enabled.

### 2026-05-22 - system-spec-synthesis - M2.5-009 canary contract

- Agent: Codex
- Trigger: The task converted a roadmap stub into an implementation-ready rollout contract for promoted rewrite strategies.
- Action: Opened and followed the skill at implementation scope; mapped the source facts into a control/canary strategy-version contract, deterministic assignment, signal-distribution comparison, rollout states, and validation plan.
- Output artifacts: `lib/rewrite-pipeline/canary.ts`; `lib/rewrite-pipeline/config.ts`; `lib/rewrite-pipeline/types.ts`; `app/api/rewrite/route.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused test failed first because `lib/rewrite-pipeline/canary.ts` did not exist. After implementation, focused tests, Prisma schema validation, `npm run lint`, `npm run typecheck`, and full `npm run test` passed.
- Limitations: The specific future promoted strategy behavior still has to be implemented in code and labeled with `REWRITE_STRATEGY_CANARY_VERSION` during that release.

### 2026-05-22 - state-machine-modeling - M2.5-009 canary lifecycle

- Agent: Codex
- Trigger: The task introduced a multi-step deployment lifecycle for rewrite strategy canaries.
- Action: Opened and followed the skill; modeled canary state as `off`, `monitoring`, `paused`, `ramping`, and `complete`, with transitions driven by feature flag state, sample maturity, average signal-drop comparison, and ramp thresholds.
- Output artifacts: `lib/rewrite-pipeline/canary.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Unit tests cover default 10% routing, stable assignment, worse-result pause, and better-result ramp. Full lint, typecheck, and Vitest validation passed.
- Limitations: The lifecycle is enforced in typed application code rather than a persisted enum because no new state table was added.

### 2026-05-22 - data-module-review - M2.5-009 canary telemetry query

- Agent: Codex
- Trigger: The task reads production `RewriteCostLog` signal telemetry on the rewrite request path and adds a supporting index.
- Action: Opened and followed the skill; reviewed `RewriteCostLog` schema and raw SQL patterns, kept the rollout on existing telemetry rows, and added an additive `strategyVersion, createdAt` index for the canary comparison query.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260522132500_add_rewrite_cost_log_strategy_version_index/migration.sql`; `lib/rewrite-pipeline/canary.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed with existing broad quota/idempotency findings outside this change. `npx prisma validate` passed with dummy local database URLs, and full lint, typecheck, and Vitest validation passed.
- Limitations: The migration was not applied to a live database in this turn, and no production telemetry query was run.

### 2026-05-22 - system-spec-synthesis - M2.5-005 status verification

- Agent: Codex
- Trigger: The task required converting the sparse M2.5-005 roadmap stub into a concrete implementation decision under the no-git/no-gh Codex protocol.
- Action: Opened and followed the skill at verification scope; checked the existing LearningOps promotion handoff contract, task builder, run pipeline integration, regression tests, and rewrite strategy memory note against `plans/current-task.md`.
- Output artifacts: `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `npm run lint`, `npm run typecheck`, `npm run test`, and the scoped banned-term scan over `app components public lib` passed.
- Limitations: No GitHub, git, Codex MCP, deployment, provider-backed evaluation, or live database run was executed; the implementation intentionally stops at draft promotion-task preparation in this environment.

### 2026-05-22 - state-machine-modeling - M2.5-008 PR link label follow-up

- Agent: Codex
- Trigger: The task still touched the persisted `StrategyCandidate.status` review lifecycle while tightening the admin candidate review display.
- Action: Opened and followed the skill; confirmed the state model remains unchanged. States: `proposed`, `approved`, `needs_revision`, `rejected`, plus existing historical display states. Events: admin review submit for `approved`, `needs_revision`, or `rejected`. Allowed transitions: any current candidate display state can be set to one of the three admin review states by an admin server action. Illegal transitions: blank candidate id or unsupported status is rejected before SQL writes. Invariants: only admin identities can mutate review status, the update is scoped to one candidate id, and display-only linked-work labels do not alter lifecycle state.
- Output artifacts: `lib/admin/learning.ts`; `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The focused admin-learning test was red for missing PR label metadata, then passed after implementation. `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: The database column remains free text; no live admin mutation was run.

### 2026-05-22 - data-module-review - M2.5-008 linked work metadata

- Agent: Codex
- Trigger: The task reads persisted `StrategyCandidate.linkedCommitHash` values into the admin LearningOps review UI.
- Action: Opened and followed the skill; reviewed `prisma/schema.prisma`, `lib/admin/learning.ts`, and the admin unit tests. Findings: no new migration needed; the change derives a display label from the existing nullable linked-work field and does not widen the mutation query. Open questions: none for this scope. Suggested tests: cover GitHub pull-request URL labeling and keep the existing scoped status-update SQL test.
- Output artifacts: `lib/admin/learning.ts`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 agent-skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed and reported existing broad persistence signals outside this change. Focused and full Vitest suites passed.
- Limitations: No production database rows were read or updated.

### 2026-05-22 - ui-browser-testing - M2.5-008 admin linked work label

- Agent: Codex
- Trigger: The task changed visible copy in the `/admin/learning` candidate review panel.
- Action: Opened and followed the skill; chose a focused unit-level UI data contract test because the admin page is auth-gated and data-backed. Attempted local browser verification by starting `npm run dev -- -H 127.0.0.1 -p 3010`.
- Output artifacts: `app/admin/learning/page.tsx`; `tests/unit/admin-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The dev server bind attempt failed with `EPERM` before a browser page could load. `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No authenticated browser screenshot was captured in this sandbox; the PR-label behavior is covered by `tests/unit/admin-learning.test.ts`.

### 2026-05-22 - system-spec-synthesis - commercialization north star

- Agent: Codex
- Trigger: The user asked where to store the final commercial goal and how Claude's scheduled monitor and Codex's execution loop should coordinate without losing context.
- Action: Opened and followed the skill; converted the loose product and automation requirements into a durable north-star spec with goals, non-goals, current system, operating architecture, contracts, state handling, security rules, rollout, verification, and open questions.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/overnight-supervisor.sh`; `plans/codex-implementation-prompt.md`; `plans/overnight-directive.md`; `plans/commercialization-roadmap.md`; `docs/skill-run-log.md`.
- Verification evidence: Documentation diff was reviewed and `git diff --check` passed.
- Limitations: This turn did not run product tests because the change is documentation and supervisor guidance only; the active overnight loop was left running in the original worktree.

### 2026-05-22 - cloud-architecture-cost-review - M2.5-010 canary rollback

- Agent: Codex
- Trigger: The task changed a Cloudflare scheduled LearningOps job and could have introduced a new paid alerting or runtime-state service.
- Action: Opened and followed the skill; kept the rollout on the existing Cloudflare scheduled handler and Neon database, added no KV namespace, queue, always-on worker, or new deployed service, and made Resend/GitHub outbound calls optional via environment configuration.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `lib/learningops/scheduled.ts`; `scripts/learningops-run.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: `npx prisma validate`, `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No exact provider pricing lookup was needed because no new paid infrastructure was selected and no live alert or GitHub API call was made.

### 2026-05-22 - system-spec-synthesis - M2.5-010 rollback contract

- Agent: Codex
- Trigger: The task converted a roadmap stub into a concrete job/data contract for post-promotion strategy rollback.
- Action: Opened and followed the skill at implementation scope; mapped the source requirement into a 50-rewrite rolling-window signal monitor, persisted rollback override, request-time traffic-off decision, admin email alert, GitHub follow-up issue hook, and verification plan.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `lib/rewrite-pipeline/canary.ts`; `lib/learningops/scheduled.ts`; `scripts/learningops-run.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The red focused tests first failed for the missing rollback module and missing request-time rollback override. After implementation, focused canary tests, `npx prisma validate`, `npm run lint`, `npm run typecheck`, and full `npm run test` passed.
- Limitations: The detailed issue brief did not exist yet, so the implementation uses the roadmap requirement and existing canary architecture as the contract.

### 2026-05-22 - state-machine-modeling - M2.5-010 rollback lifecycle

- Agent: Codex
- Trigger: The task changed the promoted-strategy canary lifecycle by adding automatic rollback and alert side-effect states.
- Action: Opened and followed the skill; modeled states as env-enabled `monitoring`, persisted `rollback_open`, side-effect statuses `pending/sent/skipped/failed` and `pending/opened/skipped/failed`, plus future manual `resolved`. Events are 50-write window measured, regression threshold crossed, rollback persisted, alert success/failure/skip, issue success/failure/skip, and future manual resolution.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `lib/rewrite-pipeline/canary.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `tests/unit/rewrite-canary.test.ts`; `docs/learningops-runbook.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Unit tests cover the 50-rewrite signal query, rollback persistence before outbound alerts, failed outbound alerts keeping rollback active, and request-time assignment forcing canary traffic to 0 for unresolved rollback rows.
- Limitations: Manual rollback resolution is represented by nullable `resolvedAt` but no admin UI for resolving rows was added in this issue.

### 2026-05-22 - data-module-review - M2.5-010 RewriteCanaryRollback persistence

- Agent: Codex
- Trigger: The task added persistent rollback state and raw SQL mutations for canary rollback.
- Action: Opened and followed the skill; reviewed `RewriteCostLog`, LearningOps scheduled SQL patterns, and migration safety. Added an additive `RewriteCanaryRollback` table with indexes and a partial unique index enforcing one unresolved rollback per canary strategy and scenario.
- Output artifacts: `prisma/schema.prisma`; `prisma/migrations/20260522143000_add_rewrite_canary_rollback/migration.sql`; `lib/rewrite-pipeline/canary-rollback.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 /Users/qc/.codex/skills/data-module-review/scripts/scan_data_risks.py --limit 80` completed and reported existing broad quota/idempotency signals outside this change. `npx prisma validate`, focused canary tests, and full Vitest passed.
- Limitations: The migration was not applied to a live database, and no production rollback rows were inserted.

### 2026-05-22 - resilience-test-generation - M2.5-010 rollback alerts

- Agent: Codex
- Trigger: The task added outbound admin email and GitHub issue side effects after rollback persistence.
- Action: Opened and followed the skill; generated a failure matrix for the canary rollback monitor, chose deterministic unit fakes, and tested the partial-success invariant that a persisted rollback remains active even if email or issue creation fails.
- Output artifacts: `lib/rewrite-pipeline/canary-rollback.ts`; `tests/unit/rewrite-canary-rollback.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `python3 /Users/qc/.codex/skills/resilience-test-generation/scripts/resilience_matrix.py "canary rollback monitor"` produced the timeout, 5xx, 4xx, duplicate, partial-success, concurrency, and malformed-payload rows. Focused canary rollback tests and full Vitest passed.
- Limitations: No live email provider, GitHub API, production database outage, or concurrent scheduled run was executed.

### 2026-05-22 - cloud-architecture-cost-review - Cloudflare Worker size limit

- Agent: Codex
- Trigger: Cloudflare/OpenNext deploy failed validation because the Worker package exceeded the free-plan size limit and Wrangler reported Prisma WASM artifacts in the package.
- Action: Opened and followed the skill as a deployment/cost gate; compared keeping the existing Cloudflare Worker with corrected packaging, upgrading to a paid Worker size limit, and moving runtime architecture. Selected the existing Worker path with scoped bundling and minification because it avoids new paid infrastructure and preserves the scheduled LearningOps wrapper.
- Output artifacts: `wrangler.jsonc`; `package.json`; `scripts/copy-prisma-wasm.mjs` removed; `README.md`; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm run cf:build` completed successfully. `npx opennextjs-cloudflare deploy -- --dry-run --keep-vars --metafile .open-next/wrangler-bundle-meta-fixed-final.json` completed without uploading, without the previous "Attaching additional modules" table, and reported `Total Upload: 3788.79 KiB / gzip: 1061.84 KiB`.
- Limitations: No exact Cloudflare pricing lookup was needed because the selected fix does not upgrade plans or create paid resources. No production deploy was run in this turn.

### 2026-05-22 - data-module-review - Prisma WASM deploy packaging check

- Agent: Codex
- Trigger: The deploy failure named Prisma WASM artifacts and the repo uses Prisma schema/migrations with direct Neon SQL at Worker runtime.
- Action: Opened the skill and reviewed the Prisma/runtime boundary. Confirmed `prisma/schema.prisma`, `docs/manual-setup.md`, `docs/preflight-report.md`, and `lib/db.ts` document and implement Prisma as schema/migration source of truth while Worker runtime DB access uses Neon directly. Removed the stale Prisma WASM copy step from the Cloudflare build path without changing schema, migrations, data access services, counters, indexes, transactions, or persistence invariants.
- Output artifacts: `package.json`; `scripts/copy-prisma-wasm.mjs` removed; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: `rg` found no remaining build-script references to `copy-prisma-wasm` after the script removal, and the Cloudflare build/dry-run passed with no Prisma WASM duplicate-module warnings.
- Limitations: No live database migration or production database query was run because this was a packaging-only change.

### 2026-05-22 - system-spec-synthesis - Cloudflare deploy-scope check

- Agent: Codex
- Trigger: The initial symptom could have required an architecture or deployment-flow spec if fixing the Worker size limit required moving runtime services.
- Action: Opened the skill and checked the scope against project instructions and deployment docs. No full implementation spec was produced because the selected fix stayed within existing Cloudflare/OpenNext deployment configuration and did not change APIs, jobs, data model, state lifecycle, or multi-module runtime behavior.
- Output artifacts: `README.md`; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: The dry-run deploy proved the existing Worker architecture can package under the limit after scoped config changes; no separate architecture handoff was required.
- Limitations: This was a scope check, not a standalone system specification document.

### 2026-05-22 - ui-browser-testing - M4-002 landing demo samples

- Agent: Codex
- Trigger: The task changes the public landing-page interactive demo samples and Naturalness Check values.
- Action: Opened and followed the skill; identified the `/` landing demo flow, added a focused unit regression for documented sample fixtures and salutation-name consistency, and attempted a focused Playwright browser check.
- Output artifacts: `components/landing/interactive-demo.tsx`; `components/landing/sample-cases.ts`; `tests/unit/landing-demo-samples.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused landing demo unit test passed after a red failure for the missing fixture module. `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: Playwright browser verification could not start in this sandbox because Next.js could not bind either `0.0.0.0:3000` or `127.0.0.1:3000` and returned `EPERM`.

### 2026-05-22 - system-spec-synthesis - Rewrite Quality Analysis and Codex worker handoff

- Agent: Codex
- Trigger: User asked to convert owner-facing rewrite-quality analysis requirements and Claude-to-Codex automation notes into durable project goals and implementation-ready docs.
- Action: Opened and followed the skill; used `spec_outline.py` to confirm the required spec headings; mapped the requirement into a first-version offline report, existing `RewriteCostLog`/`RewriteProviderCall` data sources, safety/privacy rules, report artifacts, verification checks, and a sanitized monitor-to-Codex worker queue.
- Output artifacts: `docs/rewrite-quality-analysis-spec.md`; `docs/commercialization-north-star.md`; `plans/commercialization-roadmap.md`; `plans/issue-board.md`; `plans/issue-manifest.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: Documentation and queue contracts reference existing Prisma telemetry fields and keep the first version offline rather than adding new infrastructure or an admin UI.
- Limitations: This turn added the implementation target and handoff contract. The Python/Pandas report script and real production report artifacts are still assigned to M5 follow-up issues.

### 2026-05-22 - data-module-review - M5 telemetry persistence rescue

- Agent: Codex
- Trigger: Rescuing M5-002 changed `RewriteCostLog`/`RewriteProviderCall` persistence and introduced transaction-based provider-call replacement.
- Action: Opened and followed the skill; ran `scan_data_risks.py --limit 80`; reviewed `RewriteCostLog.requestId` uniqueness, `RewriteProviderCall.costLogId` foreign key behavior, transaction order, and retry/idempotency behavior.
- Output artifacts: `lib/observability/rewrite-telemetry.ts`; `tests/unit/rewrite-telemetry.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The review caught a retry edge case where an upserted request log could keep the old primary key while replacement provider calls used the new log id. The fix updates the cost-log id on request-id conflict before deleting and reinserting provider calls; `RewriteProviderCall_costLogId_fkey` uses `ON UPDATE CASCADE`. Focused telemetry tests, full Vitest, lint, typecheck, and `npm run cf:build` passed.
- Limitations: The transaction was verified with deterministic unit mocks and schema inspection, not against a live production database.

### 2026-05-22 - resilience-test-generation - Provider telemetry failure coverage

- Agent: Codex
- Trigger: M5-002 records AI provider and writing-signal provider success/failure telemetry, including malformed output, timeouts, and unavailable signals.
- Action: Opened and followed the skill; generated a failure matrix for rewrite provider telemetry and cost logging; checked deterministic tests for malformed model output, provider-call success/failure recording, Sapling metadata, and transactional persistence.
- Output artifacts: `tests/unit/openai-output.test.ts`; `tests/unit/rewrite-pipeline-model.test.ts`; `tests/unit/rewrite-telemetry.test.ts`; `tests/unit/writing-signal.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `resilience_matrix.py "rewrite provider telemetry and cost logging"` produced timeout, transient 5xx, permanent 4xx, duplicate, partial-success, concurrent, and malformed-payload rows. Focused provider telemetry tests passed with 52/52 tests; full Vitest passed with 253/253 tests.
- Limitations: Tests use local fakes and do not hit live OpenAI/DeepSeek, Sapling, Neon, Stripe, or Cloudflare.

### 2026-05-22 - state-machine-modeling - M5-004 rewrite failure reason telemetry

- Agent: Codex
- Trigger: The task changed request-level rewrite failure lifecycle labels persisted for analysis.
- Action: Opened and followed the skill; modeled `RewriteCostLog.status` states as `success`, `quality_failed`, and `server_failed`, with terminal request-level `errorCode` reasons of `signal_unavailable`, `naturalness_gate_failed`, `fact_check_failed`, `reviewer_threshold_failed`, `server_failed`, or provider-specific codes. Events are successful rewrite, quality gate rejection, provider signal unavailability, reviewer rejection not rescued by fallback, and unexpected server exception.
- Output artifacts: `lib/rewrite-failure-reasons.ts`; `lib/observability/rewrite-telemetry.ts`; `lib/rewrite-learning.ts`; `lib/rewrite-pipeline/pipeline.ts`; `app/api/rewrite/route.ts`; `tests/unit/rewrite-telemetry.test.ts`; `tests/unit/rewrite-learning.test.ts`; `tests/unit/rewrite-pipeline.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red tests first failed for coarse `quality_gate_failed`, hidden `signal_unavailable`, generic `TypeError`, and unreachable `reviewer_threshold_failed`. After implementation, `npm run lint`, `npm run typecheck`, and `npm run test` passed.
- Limitations: No database migration or live telemetry replay was run; existing rows with older coarse labels are not backfilled by this issue.

### 2026-05-22 - data-module-review - M5-004 failure reason persistence

- Agent: Codex
- Trigger: The task changed persisted `errorCode` values in `RewriteCostLog` and `RewriteLearningSample`.
- Action: Opened and followed the skill; reviewed the write paths for request cost logs and learning samples, then added a shared normalizer so quality failures store specific machine-readable reasons, provider-specific call details stay on `RewriteProviderCall.errorCode`, and generic server exceptions persist as `server_failed`.
- Output artifacts: `lib/rewrite-failure-reasons.ts`; `lib/observability/rewrite-telemetry.ts`; `lib/rewrite-learning.ts`; `app/api/rewrite/route.ts`; `tests/unit/rewrite-telemetry.test.ts`; `tests/unit/rewrite-learning.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `scan_data_risks.py --limit 80` completed and reported existing broad quota/idempotency/wall-clock signals outside this scoped change. Focused red/green tests, `npm run lint`, `npm run typecheck`, `npm run test`, and the scoped banned-term scan passed.
- Limitations: No schema or migration change was required, and no production database rows were inspected or updated.

### 2026-05-22 - system-spec-synthesis - Agent monitor and repair queue contract

- Agent: Codex
- Trigger: The owner identified a design flaw where Claude monitor progress output and Codex repair work were not cleanly separated.
- Action: Opened and followed the skill; converted the loose automation workflow into a concrete contract that separates human progress checkpoints from machine repair queue items, assigns ownership across Claude monitor, shell loop, Codex worker, and human owner, and updates the repo source-of-truth docs.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`; Claude scheduled task `replyinmyvoice-loop-monitor` updated through its local hardlink.
- Verification evidence: Local checks confirmed the Claude scheduled task file and accessible hardlink share the updated size, link count, and mtime; `rg` checks in the isolated clone confirmed the new contract keeps `overnight-progress.md` as human reporting and `codex-worker-inbox.md` as the machine repair queue.
- Limitations: The repo docs were updated in an isolated clone/branch to avoid racing the active overnight loop. The live Claude task file path under `Documents/Claude/Scheduled` remains unreadable through shell because of macOS permissions, but its same-inode accessible hardlink was updated and stat confirmed the scheduled path changed.

### 2026-05-22 - state-machine-modeling - Agent handoff lifecycle

- Agent: Codex
- Trigger: The task changed a multi-step automation lifecycle: monitor observation, safe restart, blocker classification, queueing, repair, and owner-only escalation.
- Action: Opened and followed the skill; modeled the lifecycle as `running`, `stalled`, `repair_queued`, `repairing`, `needs_user`, `money_made`, and `stopped`, with explicit invariants that progress reports are not task queues and Codex worker consumes at most one pending inbox item per run.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The updated docs define the separate progress/inbox channels, allowed restart behavior, owner-only blockers, and worker non-racing rules.
- Limitations: No automated transition test was added because this change is an operational-doc and automation-prompt contract, not application runtime code.

### 2026-05-22 - system-spec-synthesis - Main-loop repair inbox orchestration

- Agent: Codex
- Trigger: The owner identified that a 30-minute Claude monitor plus a separate hourly Codex worker was logically mismatched and asked to implement the better design.
- Action: Opened and followed the skill; converted the workflow into a single-executor contract where the shell loop consumes `plans/codex-worker-inbox.md` before issue-board work, while the scheduled Codex automation becomes a dead-man watchdog only.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`; Codex scheduled automation `replyinmyvoice-codex-worker`; Claude scheduled task `replyinmyvoice-loop-monitor` updated through its local hardlink.
- Verification evidence: Added a focused Vitest regression for repair-inbox ordering and non-user failure queueing; `bash -n plans/overnight-supervisor.sh`, focused Vitest, `npm run lint`, `npm run typecheck`, and `git diff --check` passed in the isolated branch.
- Limitations: The change updates the local supervisor and automation contract. It does not change Claude's schedule cadence because Claude already writes progress and repair queue items; the Claude scheduled task text was only aligned to say the shell loop consumes the queue.

### 2026-05-22 - state-machine-modeling - Main-loop repair lifecycle

- Agent: Codex
- Trigger: The orchestration change moved repair handling from a separate hourly worker into the primary shell loop, changing queue ownership and transition timing.
- Action: Opened and followed the skill; modeled repair states as `pending -> in_progress -> done | not_actionable | waiting_user`, with the invariant that owner-only blockers never enter autonomous repair and a healthy shell loop is the only normal repair executor.
- Output artifacts: `plans/overnight-supervisor.sh`; `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-worker-inbox.md`; `plans/codex-worker-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The supervisor now checks the repair inbox before `find_next_pending_issue`, queues non-user Codex/GitHub/CI failures into the inbox, and the focused test covers the ordering and queueing contract.
- Limitations: The test validates script structure rather than executing real GitHub PR/CI repair flow, to avoid live PR churn during the orchestration edit.

### 2026-05-22 - cloud-architecture-cost-review - M6-001 Worker secret-name diff

- Agent: Codex
- Trigger: M6-001 reviews Cloudflare Worker production secret configuration for `replyinmyvoice-app`.
- Action: Opened and followed the skill as a read-only cloud/deployment cost gate. Selected a read-only `wrangler secret list` name comparison; rejected secret pushes, deploys, dashboard mutation, and paid-resource changes for this issue.
- Output artifacts: `plans/worker-secret-diff.md`; `docs/skill-run-log.md`.
- Verification evidence: `npx wrangler secret list --name replyinmyvoice-app --format json` was attempted and failed before returning names because the sandbox could not resolve Cloudflare API hostnames. `.env.local` was parsed for names only and was not modified.
- Limitations: The live Worker secret list was unavailable, so `present-in-both`, `missing-in-worker`, and `missing-in-local` remain uncomputed until a networked authenticated shell reruns the command.

### 2026-05-22 - cloud-architecture-cost-review - M6-002 Worker secret push

- Agent: Codex
- Trigger: M6-002 would mutate Cloudflare Worker production secrets for `replyinmyvoice-app`.
- Action: Opened and followed the skill as a cloud/deployment cost and approval gate; compared the requested `wrangler secret put` path with the prerequisite M6-001 diff, read-only Wrangler verification, and dashboard/deploy alternatives. Selected no mutation because the missing-secret list is unavailable and Cloudflare API DNS resolution fails in this sandbox.
- Output artifacts: `plans/worker-secret-diff.md`; `plans/issue-board.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `npx --no-install wrangler secret list --name replyinmyvoice-app --format json` failed before returning Worker metadata because the sandbox could not resolve Cloudflare API hostnames. No `wrangler secret put` command was run.
- Limitations: No production Worker secrets were pushed. A networked, authenticated shell must rerun the M6-001 diff first, then push only names listed under `missing-in-worker` without printing values.

### 2026-05-22 - ui-browser-testing - M4-014 handoff recommendation

- Agent: Codex
- Trigger: The current active issue is M4-014 app workspace visual polish, which changes browser-visible `/app` UI and requires desktop/mobile verification.
- Action: Opened the project `ui-browser-testing` skill and used it to shape the recommendation for the next autonomous run: inspect current dirty state first, complete the app workspace polish, then verify with focused UI/browser checks before PR.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: No product verification was run in this turn; this was a handoff/status recommendation only.
- Limitations: Did not inspect or modify M4-014 implementation files, run Playwright, start the dev server, capture screenshots, or create a PR.

### 2026-05-22 - web-design-engineer - M4-014 handoff recommendation

- Agent: Codex
- Trigger: The current active issue is M4-014 app workspace visual polish, a browser-visible app UI design task.
- Action: Opened the `web-design-engineer` skill and used it to shape the recommendation that the next run should continue from existing design tokens and repo patterns, avoid broad redesign scope, and finish with visual/browser evidence.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: No design implementation or critique scoring was performed in this turn; this was a handoff/status recommendation only.
- Limitations: Did not inspect current UI screenshots, change design code, or run final visual QA.

### 2026-05-22 - web-design-engineer - Frontend redesign issue scoping

- Agent: Codex
- Trigger: The owner installed `web-design-engineer` and asked to add an issue that uses it to redesign the currently weak frontend.
- Action: Opened and followed the skill; read the design-direction and critique references; scoped a high-priority M4 issue that requires current-state critique, design declaration, implementation, final five-dimension scoring, and browser verification.
- Output artifacts: `plans/issues/M4-011.md`; `plans/issue-board.md`; `plans/issue-manifest.md`; GitHub issue `https://github.com/ChuanQiao1128/replyinmyvoice/issues/196`; `docs/skill-run-log.md`.
- Verification evidence: The issue explicitly requires `/Users/qc/.codex/skills/web-design-engineer/SKILL.md`, `ui-browser-testing` routing where applicable, final average design score >= 8.0 with no dimension below 7.0, and desktop/mobile browser checks.
- Limitations: This turn added the queued design work; it did not implement the frontend redesign itself.

### 2026-05-22 - cloud-architecture-cost-review - M6-003 Worker preview smoke

- Agent: Codex
- Trigger: M6-003 reviews the Cloudflare Workers `workers.dev` deployment for launch verification.
- Action: Opened and followed the skill as a read-only cloud/deployment check. Selected a direct route smoke against the existing `replyinmyvoice-app` Worker URL; rejected deploys, DNS changes, secret changes, and paid-resource creation for this issue.
- Output artifacts: `docs/preflight-report.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: `curl` and Node `fetch` smoke attempts were blocked before reaching Cloudflare because this sandbox could not resolve DNS for the Worker host, `cloudflare.com`, or `example.com`.
- Limitations: No remote route status was observed. A networked shell must rerun the documented M6-003 `curl` checks.

### 2026-05-22 - ui-browser-testing - M6-003 route smoke workflow

- Agent: Codex
- Trigger: M6-003 is a browser-visible and API route smoke test for `/`, `/pricing`, `/sign-in`, `/app`, `/api/rewrite`, `/api/stripe/webhook`, and `/api/health/db`.
- Action: Opened and followed the skill to identify the user-visible flow and expected route outcomes. Used focused HTTP route checks rather than screenshots because the issue acceptance criteria are status-code based.
- Output artifacts: `docs/preflight-report.md`; `plans/task-status.json`; `docs/skill-run-log.md`.
- Verification evidence: Local code review confirmed `/app` and `/api/rewrite` are protected by middleware, `GET /api/stripe/webhook` returns a health JSON response, and `GET /api/health/db` performs the DB smoke check. Remote HTTP execution was blocked by DNS failure in this sandbox.
- Limitations: No desktop/mobile screenshot or live browser rendering was captured because the task is a route-status smoke and remote DNS was unavailable.

### 2026-05-22 - system-spec-synthesis - M4-011 no-status repair

- Agent: Codex
- Trigger: The repair inbox item for M4-011 reported that Codex did not write `plans/task-status.json` during a broad frontend redesign run.
- Action: Opened and followed the skill; converted the log evidence and supervisor contract into a scoped repair specification that keeps M4-011 as an umbrella item, splits runnable frontend work into smaller follow-ups, and preserves no-status partial work before cleanup.
- Output artifacts: `plans/frontend-redesign-followups.md`; `plans/codex-implementation-prompt.md`; `plans/overnight-supervisor.sh`; `plans/issues/M4-011.md`; `plans/issue-board.md`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added focused Vitest coverage for M4-011 timebox blocking, early status preflight, and no-status work preservation. The current rescue branch also adds dirty-worktree and declared-file guards before restart.
- Limitations: This repair does not implement the frontend redesign. It records the scoped follow-ups needed before that work can safely run unattended again.

### 2026-05-22 - state-machine-modeling - M4-011 supervisor retry lifecycle

- Agent: Codex
- Trigger: The repair changes issue and supervisor lifecycle behavior after a Codex timeout with no status file.
- Action: Opened and followed the skill; modeled the relevant states as `pending`, `in_progress`, `BLOCKED-AUTONOMY`, `ready_to_commit`, and `needs_human`, with events for timeout, reclassification, and scoped follow-up creation.
- Output artifacts: `plans/frontend-redesign-followups.md`; `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: The follow-up document includes state list, event list, transition table, invariants, illegal transitions, persistence implications, and test checklist. Focused Vitest covers M4-011 blocking before task handoff and no-status edit preservation before returning to main.
- Limitations: The state model covers the supervisor retry contract only; it does not alter product UI state or application runtime behavior.

### 2026-05-22 - state-machine-modeling - Supervisor runtime-ledger guard

- Agent: Codex
- Trigger: The M4-013 loop repeatedly stashed ready work because supervisor-maintained ledger files were dirty but not listed in `plans/task-status.json` `files_changed`.
- Action: Opened and followed the skill; modeled the relevant lifecycle as issue branch running -> `ready_to_commit` with declared product files -> runtime ledger dirty -> stage declared files only -> commit/push, with runtime ledgers allowed to remain local supervisor state.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh` and `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` passed. Focused tests cover ignoring supervisor runtime ledgers during `files_changed` validation and staging only declared task files before commit.
- Limitations: This repair covers the supervisor commit boundary; it does not complete or visually verify the preserved M4-013 pricing/auth UI changes.

### 2026-05-22 - web-design-engineer - M4-013 pricing and auth visual alignment

- Agent: Codex
- Trigger: M4-013 changes browser-visible `/pricing`, `/sign-in`, and `/sign-up` UI and explicitly requires `web-design-engineer`.
- Action: Opened and followed the skill; reused the M4-012 ink, paper, clay, sage, mint, sky, `rounded-lg`, and section-band direction; kept auth and billing links unchanged; added focused visual-system regression coverage before implementing.
- Output artifacts: `app/pricing/page.tsx`; `app/sign-in/[[...sign-in]]/page.tsx`; `app/sign-up/[[...sign-up]]/page.tsx`; `components/auth/google-oauth-card.tsx`; `components/ui/button.tsx`; `tests/unit/pricing-auth-visual-system.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `npm test -- tests/unit/pricing-auth-visual-system.test.ts` first failed on the missing M4-012 route/auth/button contracts, then passed after implementation. `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed.
- Limitations: No Stripe, quota, rewrite, provider, telemetry, webhook, infrastructure, or secret behavior was changed.

### 2026-05-22 - ui-browser-testing - M4-013 pricing and auth route checks

- Agent: Codex
- Trigger: M4-013 requires desktop/mobile browser verification for `/pricing`, `/sign-in`, and `/sign-up`, plus signed-out auth redirect behavior.
- Action: Opened and followed the skill; selected route-level browser checks and focused auth redirect verification after the UI implementation; attempted both local Next dev server startup and focused Playwright auth-gate execution.
- Output artifacts: `app/pricing/page.tsx`; `components/auth/google-oauth-card.tsx`; `tests/unit/pricing-auth-visual-system.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed. Signed-out redirect behavior remains covered by `tests/unit/middleware.test.ts`; focused Playwright execution could not start because the local server bind failed before route loading.
- Limitations: Desktop/mobile screenshots were not captured in this sandbox. `npm run dev -- -H 127.0.0.1 -p 3000`, `npm run dev -- -H 0.0.0.0 -p 3001`, and `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` all failed at server startup with `EPERM`; the Codex in-app browser was also unavailable for `iab`.

### 2026-05-22 - state-machine-modeling - Supervisor runtime-only stash guard

- Agent: Codex
- Trigger: After M4-013 merged, the supervisor locally marked it done, then the next iteration stashed supervisor-only runtime ledgers and reverted the local board to `in_progress`.
- Action: Opened and followed the skill; modeled runtime ledger changes as control-plane state that must remain in the worktree across issue starts unless mixed with non-runtime implementation changes.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added a focused test requiring `stash_dirty_worktree` to skip stashing supervisor-only runtime files before any `git stash push`.
- Limitations: This entry covers supervisor lifecycle safety only; it does not change product UI or provider behavior.

### 2026-05-22 - web-design-engineer - M4-014 app workspace visual polish

- Agent: Codex
- Trigger: M4-014 changes the browser-visible `/app` workspace shell and explicitly requires `web-design-engineer`.
- Action: Opened and followed the skill; reused the M4-012 ink, paper, clay, sage, mint, sky, `rounded-lg`, and dense tool-surface direction. Changed only the workspace shell, quota/status strip, paywall presentation, input panel density, tone controls, output rail, and local-history presentation.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `components/app/subscription-status.tsx`; `components/app/paywall-card.tsx`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Added focused visual-system tests first. `npm run test -- tests/unit/workspace-copy.test.ts` failed before implementation on the missing dense workspace shell/status/paywall contracts, then passed after implementation. `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed.
- Limitations: No rewrite, quota, billing, API, telemetry, webhook, provider, infrastructure, pricing, or secret behavior was changed.

### 2026-05-22 - ui-browser-testing - M4-014 app workspace checks

- Agent: Codex
- Trigger: M4-014 requires signed-out `/app` redirect verification, responsive layout checks, console/network review, and any locally available signed-in preview state.
- Action: Opened and followed the skill; selected focused auth-gate/browser checks after implementation and attempted local Next startup, Playwright auth-gate execution, and Codex in-app browser setup.
- Output artifacts: `components/app/rewrite-workspace.tsx`; `components/app/subscription-status.tsx`; `components/app/paywall-card.tsx`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `npm run lint`, `npm run typecheck`, full `npm run test`, and `npm run build` passed. `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` could not reach route execution because the configured dev server exited before loading `/app`.
- Limitations: Desktop/mobile screenshots, console/network inspection, and live signed-out redirect observation were blocked in this sandbox. `npm run dev`, `npx next dev -H 127.0.0.1 -p 3000`, Python `http.server`, and Playwright webServer startup all failed with `listen EPERM` / `Operation not permitted`; the Codex in-app browser also reported `iab` unavailable.

### 2026-05-22 - system-spec-synthesis - Supervisor auto-repair enhancement plan

- Agent: Codex
- Trigger: The owner asked how to enhance automatic repair so Cloud/Claude monitor findings do not need to be manually pasted back into Codex.
- Action: Opened and followed the skill; inspected the supervisor script, repair inbox flow, active M4-015 loop behavior, and automation configuration to convert the loose requirement into an implementation-ready supervisor repair architecture.
- Output artifacts: `plans/STOP-OVERNIGHT.txt`; `docs/skill-run-log.md`.
- Verification evidence: Confirmed from `plans/overnight.log` that M4-015 repeatedly returned `needs_human` for sandbox-blocked browser screenshots, then the supervisor stashed branch-local board updates and selected M4-015 again from main. Added a stop signal so the loop exited cleanly before more retries.
- Limitations: This turn produced the repair design and paused the loop; it did not patch `plans/overnight-supervisor.sh` yet.

### 2026-05-22 - state-machine-modeling - Supervisor terminal-state persistence

- Agent: Codex
- Trigger: The supervisor lifecycle is repeatedly selecting a task after terminal `needs_human` outcomes because the blocked board state is not persisted on main.
- Action: Opened and followed the skill; modeled the required lifecycle fix as persisting terminal issue states (`done`, `BLOCKED-*`, `needs_human`, CI/merge failure) to main-side ledger files before returning to selection.
- Output artifacts: `plans/STOP-OVERNIGHT.txt`; `docs/skill-run-log.md`.
- Verification evidence: `plans/overnight.log` shows M4-015 was run four times after `needs_human` outcomes caused by browser/server sandbox limits; `plans/issue-board.md` on main still showed M4-015 as `pending`.
- Limitations: The state-machine fix is identified but not implemented in the supervisor script in this turn.

### 2026-05-23 - state-machine-modeling - Supervisor terminal-state persistence implementation

- Agent: Codex
- Trigger: The owner approved implementing the supervisor auto-repair enhancement after M4-015 repeatedly reselected due to branch-local terminal state.
- Action: Opened and followed the skill; implemented `persist_issue_terminal_state_on_main` and routed no-status, `needs_human`, abort, and remote-merged-after-local-merge-failure outcomes through main-side ledger persistence.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `docs/skill-run-log.md`.
- Verification evidence: Added regression tests for terminal-state persistence, sandbox browser/server blocker classification, and remote merge success after local merge-command failure. `bash -n plans/overnight-supervisor.sh`, focused supervisor Vitest, and full `npm run test` passed.
- Limitations: Did not restart the overnight loop; `plans/STOP-OVERNIGHT.txt` remains a local ignored stop signal until restart is desired.

### 2026-05-23 - resilience-test-generation - Supervisor recovery regression tests

- Agent: Codex
- Trigger: The fix changes supervisor recovery behavior for repeated `needs_human`, no-status, sandbox browser/server failures, and merge-command partial success.
- Action: Opened and followed the skill; added deterministic local regression tests instead of invoking live GitHub or browser infrastructure.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: The first focused test run failed on the missing terminal-state helper and sandbox classifier; the second red test failed on missing remote-merge recovery. After implementation, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` and full `npm run test` passed.
- Limitations: Tests inspect the supervisor shell contract statically; they do not execute a live GitHub merge or browser server startup.

### 2026-05-23 - test-driven-development - Supervisor auto-repair fix

- Agent: Codex
- Trigger: Implementing a bugfix to prevent repeated supervisor retries and false merge-failure repair items.
- Action: Opened and followed the skill; wrote failing tests before changing `plans/overnight-supervisor.sh`, then implemented the minimal shell changes to pass.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: Red run failed with missing `persist_issue_terminal_state_on_main` and sandbox classifier; second red run failed with missing `gh pr view "$PR_URL" --json state,mergedAt` recovery. Green runs passed focused supervisor tests and the full Vitest suite.
- Limitations: No live loop restart or live PR merge simulation was run in this turn.

### 2026-05-23 - ui-browser-testing - M6-005 production smoke repair

- Agent: Codex
- Trigger: M6-005 requires formal-domain route smoke verification for `replyinmyvoice.com`, including public pages, signed-out `/app` redirect behavior, and browser-visible route health.
- Action: Opened and followed the skill; identified the expected route/status checklist, reproduced the sandbox DNS blocker with secret-free DNS and curl checks, and documented the network-capable rerun prerequisite instead of claiming a smoke pass.
- Output artifacts: `docs/preflight-report.md`; `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Node DNS lookup returned `ENOTFOUND` for `replyinmyvoice.com` and `example.com`; curl returned `Could not resolve host` for both hosts before any HTTP status evidence. Required local validations are run separately for this repair.
- Limitations: No desktop/mobile screenshots, console review, or live route status checks were possible from this sandbox because public DNS resolution failed before reaching the site.

### 2026-05-23 - state-machine-modeling - Supervisor runtime ledger preservation

- Agent: Codex
- Trigger: The supervisor could persist a terminal state on a branch, then hide the board/inbox/STOP ledger with a full-worktree stash and reselect the same issue from main.
- Action: Opened and followed the skill; modeled supervisor runtime files as state that must remain visible across no-status, needs-human, abort, and banned-term transitions while implementation diffs are preserved separately.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added regression coverage for runtime-aware stashing, STOP file preservation, and no-status paths using `stash_dirty_worktree`; `bash -n plans/overnight-supervisor.sh` and focused supervisor tests passed.
- Limitations: The live overnight loop was not restarted in this turn; `plans/STOP-OVERNIGHT.txt` remains the local stop signal until restart is intentional.

### 2026-05-23 - resilience-test-generation - Supervisor stash recovery regressions

- Agent: Codex
- Trigger: The fix changes recovery behavior after Codex no-status, banned-term, dirty-worktree, and sandbox/socket-permission blocker outcomes.
- Action: Opened and followed the skill; wrote deterministic source-contract tests before implementation so runtime ledgers are not stashed away when non-runtime work must be preserved.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: The first focused run failed on five missing behaviors, then passed after implementation. Full `npm run test`, `npm run lint`, and `npm run typecheck` passed.
- Limitations: These are local static contract tests for the shell supervisor; no live GitHub merge or Cloudflare deployment was invoked.

### 2026-05-23 - test-driven-development - Supervisor runtime stash fix

- Agent: Codex
- Trigger: Implementing a bugfix to prevent autonomous supervisor reselection caused by stashing terminal ledger state.
- Action: Opened and followed the skill; wrote failing tests first, verified the failures, then implemented the minimal supervisor changes to make them pass.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: Red run failed in `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` on no-status stash routing, socket-permission classification, STOP preservation, path-scoped stash, and raw stash usage. Green runs passed focused tests, `bash -n`, full Vitest, lint, and typecheck.
- Limitations: Did not run the overnight supervisor itself after patching because the local STOP signal is intentionally present.

### 2026-05-23 - dotnet-backend-testing - M6-007 backend suite blocker classification

- Agent: Codex
- Trigger: M6-007 validation status included .NET backend test coverage and needed classification without claiming tests passed when the sandbox blocks test discovery.
- Action: Opened and followed the skill; treated `dotnet test backend-dotnet/ReplyInMyVoice.sln` as the required backend command and classified the observed MSBuild socket/named-pipe permission issue as an autonomous sandbox blocker.
- Output artifacts: `plans/overnight-supervisor.sh`; `docs/skill-run-log.md`.
- Verification evidence: The supervisor classifier now recognizes `socket permission` and `named-pipe` as `BLOCKED-AUTONOMY`; TypeScript lint, typecheck, and Vitest passed separately.
- Limitations: No .NET backend test pass is claimed here. The backend suite still needs to be rerun in an environment where MSBuild named-pipe/socket creation is allowed.

### 2026-05-23 - state-machine-modeling - M6-007 repair status narrowing

- Agent: Codex
- Trigger: The M6-007 repair changes persisted lifecycle/status records for the issue board and repair inbox after a non-user `BLOCKED-AUTONOMY` outcome.
- Action: Opened and followed the skill; modeled M6-007 as remaining in `BLOCKED-AUTONOMY` with a narrowed engineering prerequisite, while the repair inbox item moves from `in_progress` to `not_actionable` after documenting the prerequisite.
- Output artifacts: `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/m6-validation-report.md`; `plans/task-status.json`.
- Verification evidence: The issue board now records the loopback-listen Playwright blocker, the inbox item includes worker evidence, and the blockers log no longer frames the item as a user decision. `npm run lint`, `npm run typecheck`, and `npm run test` passed after the ledger edits.
- Limitations: The issue is not marked done because `npm run test:e2e` still needs a non-sandboxed runner that permits local server binding.

### 2026-05-23 - dotnet-backend-testing - M6-007 validation scope repair

- Agent: Codex
- Trigger: M6-007 repair referenced a prior `dotnet test` socket failure and required deciding whether .NET backend tests were part of this issue.
- Action: Opened and followed the skill; reviewed the prior VSTest failure evidence and kept the backend test suite classified separately from the M6-007 Node/Next validation brief.
- Output artifacts: `plans/m6-validation-report.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/task-status.json`.
- Verification evidence: Prior M6-007 evidence showed `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo -m:1 /nr:false` built the test assembly, then VSTest aborted while opening its socket server with sandbox permission denied before tests executed. No backend source was touched in this repair. Current repair validation passed `npm run lint`, `npm run typecheck`, and `npm run test`.
- Limitations: .NET backend tests were not rerun in this repair because the issue does not touch `backend-dotnet/` and the prior failure is an environment permission blocker.

### 2026-05-23 - ui-browser-testing - M6-007 Playwright validation

- Agent: Codex
- Trigger: M6-007 includes `npm run test:e2e`, which runs Playwright browser checks.
- Action: Opened and followed the skill; reviewed the prior Playwright failure and reproduced the underlying loopback bind restriction with a minimal Node HTTP server.
- Output artifacts: `plans/m6-validation-report.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/task-status.json`.
- Verification evidence: `npm run test:e2e` exited before tests because the Playwright web server could not listen on `0.0.0.0:3000` with `EPERM`. The minimal Node HTTP server check also failed to listen on `127.0.0.1` with `EPERM`, matching the Playwright failure before browser route assertions could execute.
- Limitations: No screenshots, console review, or browser route assertions were possible because the server bind failed before browser execution.

### 2026-05-23 - state-machine-modeling - M6-008 live webhook verification

- Agent: Codex
- Trigger: The M6-008 repair changes issue-board and repair-inbox lifecycle status for a live Stripe webhook verification task, and the verification itself has webhook delivery, DB event, and subscription-sync states.
- Action: Opened and followed the skill; used `agent-skills/state-machine-modeling/scripts/state_machine_template.py` and documented the M6-008 states as `pending_operator_event`, `event_delivered`, `event_processed`, `subscription_synced`, and `endpoint_only_verified`.
- Output artifacts: `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/m6-validation-report.md`; `tests/unit/overnight-supervisor-status.test.ts`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: `plans/m6-validation-report.md` now separates endpoint delivery, `StripeEvent` persistence, and `User` subscription sync so synthetic events cannot be overclaimed as entitlement verification. The status taxonomy test was updated after its red run showed M6-008 now belongs with the operator-action blocked issues.
- Limitations: No live Stripe event was sent and no production DB query was run because those require operator-controlled live Stripe and production data access.

### 2026-05-23 - data-module-review - M6-008 webhook DB evidence

- Agent: Codex
- Trigger: M6-008 requires production DB evidence for Stripe webhook processing and subscription state update.
- Action: Opened and followed the skill; reviewed `prisma/schema.prisma`, `app/api/stripe/webhook/route.ts`, `lib/stripe-events.ts`, `lib/stripe.ts`, and `tests/unit/stripe-webhook-events.test.ts`; ran `agent-skills/data-module-review/scripts/scan_data_risks.py --limit 40`.
- Output artifacts: `plans/m6-validation-report.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Existing schema has `StripeEvent.id` as the idempotency key with status, mode, attempts, and timestamps; the webhook handler records `processing`, marks `processed` after handler success, marks `failed` on handler error, and updates `User` only when the event maps to an existing user/customer/subscription. The report now requires both `StripeEvent` and `User` evidence for full M6-008 completion.
- Limitations: This was a read-only review and documentation repair; no migration, webhook handler code, live Stripe call, or production DB query was performed.

### 2026-05-23 - state-machine-modeling - M7-003 Sentry dependency blocker

- Agent: Codex
- Trigger: The M7-003 repair changes persisted lifecycle/status records for an implementation issue after a non-user `BLOCKED-AUTONOMY` outcome.
- Action: Opened and followed the skill; modeled M7-003 as moving from `BLOCKED-AUTONOMY` to `BLOCKED-PROVIDER` when npm registry DNS is unavailable, with a return to `in_progress` only after a networked npm runner can generate the `@sentry/nextjs` lockfile.
- Output artifacts: `plans/m7-003-sentry-prerequisite.md`; `plans/issue-board.md`; `plans/codex-worker-inbox.md`; `plans/blockers-log.md`; `plans/task-status.json`.
- Verification evidence: `npm view @sentry/nextjs version --json` failed with `ENOTFOUND registry.npmjs.org`; local `node_modules/@sentry` and npm cache did not contain `@sentry/nextjs`. Full validation is run separately for this repair.
- Limitations: This repair does not implement Sentry source wiring because committing `package.json` without a generated `package-lock.json` would leave the dependency state inconsistent.

### 2026-05-23 - state-machine-modeling - Overnight supervisor hardening

- Agent: Codex
- Trigger: The supervisor lifecycle had a livelock where `preserving_worktree` failure looped back into repair processing forever, and CI `waiting` states could reach merge logic after timeout.
- Action: Opened and followed the skill; modeled the supervisor transitions so repeated stash failures move toward a stop signal, CI pending/unknown states cannot transition to merge, and only one supervisor instance can mutate repo state.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Added static contract tests for NUL status parsing, stash failure stop behavior, CI merge gating, single-instance locking, and token isolation. Focused test initially failed on five missing behaviors, then passed after implementation.
- Limitations: This does not restart the overnight loop; the STOP signal remains the deliberate guard until the hardened supervisor branch is reviewed/merged.

### 2026-05-23 - resilience-test-generation - Overnight supervisor failure-mode coverage

- Agent: Codex
- Trigger: The change touches retries, partial stash creation, CI timeout behavior, duplicate supervisor runs, and credential exposure across process boundaries.
- Action: Opened and followed the skill; built focused local tests for partial success after persistence (`git stash` creates a stash then exits nonzero), repeated failure livelock prevention, CI timeout/no-check handling, concurrent runner prevention, and environment isolation for Codex child workers.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `bash -n plans/overnight-supervisor.sh`, `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts`, full `npm run test`, `npm run lint`, and `npm run typecheck` passed after the fix.
- Limitations: These are local source-contract and unit checks; no live GitHub CI failure was induced and no autonomous loop was restarted.

### 2026-05-23 - test-driven-development - Overnight supervisor hardening

- Agent: Codex
- Trigger: Implementing bugfixes for the overnight supervisor after a live stash livelock.
- Action: Opened and followed the skill; wrote failing regression tests first for the missing hardening behaviors, verified the focused suite failed on the intended five cases, then implemented the minimal script changes.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red run: `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` failed on five missing hardening checks. Green runs: focused test passed 22/22, full Vitest passed 47 files / 286 tests, lint passed, and typecheck passed.
- Limitations: The test suite verifies the supervisor contract statically and through local commands; it does not run a live overnight automation cycle.

### 2026-05-23 - system-spec-synthesis - Model-aware dispatcher design

- Agent: Codex
- Trigger: The user asked whether Claude can assign whole coherent tasks to a strong model while using parallel Codex workers only for independent frontend/backend/docs/test work.
- Action: Opened and followed the skill; synthesized the dispatcher requirements into an implementation-ready specification with context, goals, non-goals, architecture, data model, job contracts, security, rollout, verification, and open questions.
- Output artifacts: `docs/superpowers/specs/2026-05-23-model-aware-dispatcher-design.md`; `docs/skill-run-log.md`.
- Verification evidence: The spec separates strong-model single-owner, domain-parallel, mechanical-parallel, and blocked/manual assignment modes, and includes concrete dispatcher/coordinator/worker contracts.
- Limitations: This turn produced a design/spec only. No dispatcher script, worker execution, PR merge automation, or loop restart was implemented.

### 2026-05-23 - state-machine-modeling - Model-aware dispatcher lifecycle

- Agent: Codex
- Trigger: The dispatcher design introduces lifecycle states for issue assignment, worker execution, PR/CI handling, and coordinator merge decisions.
- Action: Opened and followed the skill; modeled assignment states from `queued` through `merged`, including blocked/stopped states, events, transition table, invariants, illegal transitions, and test checklist.
- Output artifacts: `docs/superpowers/specs/2026-05-23-model-aware-dispatcher-design.md`; `docs/skill-run-log.md`.
- Verification evidence: The spec now forbids `ci_waiting -> merged` without terminal success, forbids worker direct merge, requires one active assignment group per issue, and requires non-overlapping worker-owned paths for parallel groups.
- Limitations: These are design constraints and planned tests, not an implemented state transition helper yet.

### 2026-05-22 - state-machine-modeling - Agent permission and work allocation policy

- Agent: Codex
- Trigger: The owner clarified that unattended execution must consider permissions first and should not split coherent work across agents when a strong model can handle the whole task with less token overhead.
- Action: Opened and followed the skill; modeled the assignment lifecycle as permission gate -> work-allocation gate -> single-owner coherent task or independent parallel split -> execution/monitoring.
- Output artifacts: `docs/commercialization-north-star.md`; `plans/supervisor-handoff.md`; `plans/codex-implementation-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: Documentation now defines autonomous, provider/sandbox, user-only, paid-resource/secret/dashboard, and workspace-race categories; it also defines single-owner default, parallelization criteria, and token-discipline rules including prompt/context-cache efficiency.
- Limitations: This is an operating-contract update only. It does not change the currently running shell loop process until the docs are merged and the loop next reads them.

### 2026-05-23 - state-machine-modeling - Claude monitor watchdog circuit breaker

- Agent: Codex
- Trigger: The owner provided a Claude monitor watchdog/circuit-breaker proposal for detecting stale or stuck shell-supervisor runs and escalating after one failed safe restart.
- Action: Opened and followed the skill; modeled monitor states, events, transition table, invariants, illegal transitions, persistence implications, and a dry-run checklist for the scheduled Sonnet monitor prompt.
- Output artifacts: `plans/monitor-watchdog-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The prompt now separates `stopped`, `healthy`, `suspect`, `stale`, `stuck`, `restarted_once`, and `escalated`, forbids dispatch/source/board mutations, and requires a single safe `screen` restart before escalation.
- Limitations: This creates the prompt document only. It does not install or update the remote Claude scheduled task and does not restart the shell loop.

### 2026-05-23 - resilience-test-generation - Claude monitor watchdog recovery rules

- Agent: Codex
- Trigger: The watchdog proposal changes recovery behavior for stale logs, residual `codex exec` processes, stash growth, repeated error signatures, and duplicate stale triggers.
- Action: Opened and followed the skill; converted the recovery cases into a failure matrix and dry-run checklist covering timeout, duplicate trigger, partial-success, concurrent runner, stash accumulation, and malformed board/status failures.
- Output artifacts: `plans/monitor-watchdog-prompt.md`; `docs/skill-run-log.md`.
- Verification evidence: The prompt limits recovery to one safe restart per failure signature, records state in `codex-supervisor/monitor-watchdog-state.json`, and escalates by touching `plans/STOP-OVERNIGHT.txt` plus writing `codex-supervisor/inbox/emergency-YYYYMMDD-HHMM.md`.
- Limitations: No live monitor trigger was run and no Claude schedule was modified in this turn.

### 2026-05-23 - state-machine-modeling - Overnight supervisor stash success cleanup

- Agent: Codex
- Trigger: The supervisor repair-inbox flow had generated repeated dirty-worktree-stash-failed blocker records and needed a precise recovery state for successful stash preservation versus genuine stash failure.
- Action: Opened and followed the skill; modeled the stash preservation lifecycle as clean/runtime-only, successful preservation, genuine failure, repeated-failure stop, and post-fix cleanup of generated blocker records.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/blockers-log.md`; `plans/codex-worker-inbox.md`; `plans/decisions-log.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused supervisor regression test first failed because the successful stash path lacked an explicit success return, then passed after adding the return. Duplicate generated blocker entries were counted at 1206 and collapsed to one forensic summary.
- Limitations: This does not restart the loop; `plans/STOP-OVERNIGHT.txt` remains user-controlled.

### 2026-05-23 - resilience-test-generation - Overnight supervisor stash recovery regression

- Agent: Codex
- Trigger: The change covers timeout recovery, repeated stash failure handling, partial stash behavior, generated blocker cleanup, and safe restart prerequisites for the long-running supervisor loop.
- Action: Opened and followed the skill; added focused regression coverage for the successful stash path and preserved the existing failure-path coverage for partial stash drop and repeated-failure stop signaling.
- Output artifacts: `plans/overnight-supervisor.sh`; `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `plans/blockers-log.md`; `plans/codex-worker-inbox.md`; `plans/decisions-log.md`; `docs/skill-run-log.md`.
- Verification evidence: Red run: `npm run test -- tests/unit/overnight-supervisor-repair-inbox.test.ts` failed on the missing explicit return. Green run: the same focused suite passed 23/23 after the script change.
- Limitations: No live shell-loop restart, provider call, deploy, dashboard change, live-money action, or secret mutation was performed.

### 2026-05-23 - state-machine-modeling - M9-003 repair unblock

- Agent: Codex
- Trigger: The M9-003 repair required changing persisted issue-board lifecycle states so the supervisor test suite and prior scoped-release decision agreed.
- Action: Opened and followed the skill; modeled issue rows with `pending`, `in_progress`, blocked categories, and `done` states, then applied the allowed `BLOCKED-AUTONOMY` to `pending` transition to M1-007, M1-009, M3-001, M3-002, and M3-005, plus the M9-003 repair transition to `done` after implementation and validation.
- Output artifacts: `plans/issue-board.md`; `docs/skill-run-log.md`; `plans/task-status.json`.
- Verification evidence: Focused supervisor test passed after the board repair, then `npm run lint`, `npm run typecheck`, `npm run test`, and `npm run build --prefix packages/mcp-server` passed.
- Limitations: No git, GitHub, live money, provider dashboard, npm publish, secret, `.env.local`, or `.dev.vars` action was performed.

### 2026-05-23 - state-machine-modeling - Main loop pending-row restart unblock

- Agent: Codex
- Trigger: The overnight loop exited immediately because scoped M1/M3 rows were still persisted as `BLOCKED-AUTONOMY` on `main`, leaving no pending work for the supervisor.
- Action: Opened and followed the skill; modeled the issue-board rows with `pending`, `in_progress`, and blocked states, then applied the allowed `BLOCKED-AUTONOMY` to `pending` transition for M1-007, M1-009, M3-001, M3-002, and M3-005 after confirming each has a scoped issue brief.
- Output artifacts: `plans/issue-board.md`; `plans/overnight-progress.md`; `docs/skill-run-log.md`.
- Verification evidence: Issue-board row checks confirmed all five target rows are `pending`; focused supervisor Vitest passed before restart.
- Limitations: This does not change provider settings, secrets, `.env.local`, `.dev.vars`, live money, or deployment configuration.

### 2026-05-23 - state-machine-modeling - State-agnostic supervisor board test

- Agent: Codex
- Trigger: The overnight loop repeatedly misclassified otherwise completed issue work because a supervisor unit test required scoped board rows to remain `pending` even after those rows advanced to `in_progress`, `done`, or blocked states.
- Action: Opened and followed the skill; modeled issue-board status as a lifecycle field that may move through `pending`, `in_progress`, blocked states, and `done`, then changed the test to validate row presence and table structure instead of one transient status value.
- Output artifacts: `tests/unit/overnight-supervisor-repair-inbox.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Red run reproduced the failure by advancing M1-007, M1-009, and M3-001 in a temporary board state; green runs passed the focused supervisor Vitest against both the advanced temporary state and the restored main board state.
- Limitations: This changes test coverage only. It does not recover the M1-009 or M3-001 stashes, restart the loop, change provider settings, touch secrets, or deploy.

### 2026-05-24 - ui-browser-testing - Students page polish commit

- Agent: Codex
- Trigger: The task changed browser-visible `/students` page spacing and requested desktop/mobile layout review before a local commit.
- Action: Opened and followed the skill; inspected the students-page CSS, fixed the hero preview before/after columns to use content-sized height, attempted Codex Browser and live localhost verification, then used a no-network static Chromium render of the students markup with `app/globals.css` for desktop/mobile layout checks.
- Output artifacts: `app/globals.css`; `docs/skill-run-log.md`.
- Verification evidence: Static render at 1440px and 390px reported no horizontal overflow, no console errors, and `.student-preview .compare-col` computed `min-height: 0px` with content-sized heights. `npm run typecheck`, `npm run lint`, and `npm test` passed under Node 22.13.1; the banned-term scan returned no matches.
- Limitations: The Codex Browser plugin reported no available `iab` backend, and live `localhost:3021` browser/curl access from the sandbox was blocked even though `lsof` showed a listener on port 3021, so the browser layout check used a static CSS render rather than the running dev server.

### 2026-05-24 - ui-browser-testing - Pivot Phase 0 reply-decision copy

- Agent: Codex
- Trigger: The task changed browser-visible landing, pricing, terms, workspace label, and `/students` copy for the reply-decision repositioning.
- Action: Opened and followed the skill; updated user-visible copy only, verified the dead pricing component was unreferenced before deletion, and checked `/` plus `/students` at desktop and mobile sizes with Playwright.
- Output artifacts: `components/landing/*`; `app/students/page.tsx`; `app/pricing/page.tsx`; `app/terms/page.tsx`; `components/app/rewrite-workspace.tsx`; `tests/unit/*copy*.test.ts`; `docs/skill-run-log.md`; `plans/decisions-log.md`.
- Verification evidence: With Node 22.13.1 first on PATH, staged-state `npm run lint`, `npm run typecheck`, `npm run test`, and `npm run build` passed. The banned-term grep returned no matches. Playwright screenshots for `/` and `/students` at 1440px and 390px showed no horizontal overflow, no console errors, and required hero/boundary copy present.
- Limitations: Validation used `git stash --keep-index` to exclude pre-existing unstaged tracked changes in `components/site-header.tsx`, `components/site-footer.tsx`, `docs/skill-run-log.md`, `lib/admin-visible.ts`, and `lib/rewrite-completeness.ts`; those unrelated changes were restored afterward and were not part of this task. No deploy, main merge, Stripe, schema, secret, or provider setting changes were made.

### 2026-05-24 - cloud-architecture-cost-review - C# rewrite backend migration

- Agent: Codex
- Trigger: The owner set the goal to keep Azure Functions Consumption + Azure SQL + Service Bus, move the backend runtime to C#, avoid App Service, then merge and deploy.
- Action: Opened and followed the skill; kept the existing scale-to-zero Azure Functions architecture, explicitly rejected App Service reintroduction, checked Azure app-setting names without printing values, and used GitHub Actions as the Cloudflare deploy path after local wrangler lacked `CLOUDFLARE_API_TOKEN`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; C# provider adapters; Azure proxy routes; `README.md`; `docs/business-qa-and-deploy-result.md`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 60/60; `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`, and `npm run cf:build` passed; Azure Functions deploy passed; Azure `/api/health`, `/api/health/db`, and unsigned `/api/rewrite` auth-boundary smokes passed.
- Limitations: No new paid resources were created. Local Cloudflare deploy could not complete without `CLOUDFLARE_API_TOKEN`; production Cloudflare deploy is expected through GitHub Actions secrets after merge.

### 2026-05-24 - system-spec-synthesis - Public backend C# runtime target

- Agent: Codex
- Trigger: The task converted a loose goal, "backend all C# and services all Azure," into implementation boundaries spanning rewrite, billing/webhook proxies, account/quota reads, deployment, and verification.
- Action: Opened and followed the skill; scoped the implementation to the public runtime path first: C# rewrite provider and adapters, Cloudflare BFF proxies to Azure Functions, Azure account/quota reads, and documented remaining legacy TS cleanup.
- Output artifacts: `app/api/rewrite/route.ts`; `app/api/stripe/checkout/route.ts`; `app/api/stripe/webhook/route.ts`; `app/app/page.tsx`; C# rewrite engine/provider files; tests; `README.md`; `docs/business-qa-and-deploy-result.md`.
- Verification evidence: Frontend and backend tests/builds passed as recorded in `docs/business-qa-and-deploy-result.md`.
- Limitations: Legacy TS rewrite/learningops/observability files remain for historical tests/admin cleanup and were not deleted in this slice.

### 2026-05-24 - state-machine-modeling - Rewrite quality failure and quota lifecycle

- Agent: Codex
- Trigger: Moving rewrite execution to C# affects rewrite attempt states, queued worker processing, quality failures, and quota reservation/finalization behavior.
- Action: Opened and followed the skill; preserved the existing `Pending -> Processing -> Succeeded/Failed/Expired` attempt lifecycle and implemented provider quality failures as failed provider results so `RewriteJobProcessor` releases reservations instead of finalizing quota.
- Output artifacts: `FactReconstructRewriteProvider.cs`; C# rewrite tests; existing `RewriteJobProcessor` integration path.
- Verification evidence: `FactReconstructRewriteProviderTests` cover Sapling unavailable, structure failure, naturalness failure, and successful output; full .NET suite passed 60/60.
- Limitations: No database schema transition was required in this slice.

### 2026-05-24 - data-module-review - Public runtime Prisma dependency removal

- Agent: Codex
- Trigger: The public `/app`, rewrite, Stripe checkout, and webhook paths moved away from TS/Prisma/Neon helpers toward Azure Functions and Azure SQL.
- Action: Opened and followed the skill; removed public route imports of `lib/users`, `lib/quota`, and TS Stripe handlers; changed `/app` to use Azure account summary; removed generated Prisma client type imports from remaining legacy helpers so typecheck no longer depends on generated Prisma client.
- Output artifacts: `app/app/page.tsx`; `app/api/stripe/checkout/route.ts`; `app/api/stripe/webhook/route.ts`; `lib/users.ts`; `lib/quota.ts`; `lib/subscription.ts`; related tests.
- Verification evidence: `npm run typecheck`, `npm test`, and `npm run build` passed; Azure `/api/health/db` returned Azure SQL.
- Limitations: Legacy Prisma/Neon helper files still exist for historical tests and admin/learning cleanup; no data migration or schema deletion was attempted.

### 2026-05-24 - resilience-test-generation - C# provider failure gates

- Agent: Codex
- Trigger: The C# rewrite provider touches external model and Sapling boundaries where timeouts, unavailable signals, malformed output, structure failures, and naturalness failures must not charge quota.
- Action: Opened and followed the skill; added deterministic fakes and tests for Sapling unavailable before model call, structure-gate failure, naturalness-gate failure, and successful result JSON; added HTTP adapter tests for OpenAI-compatible/DeepSeek and Sapling request/response behavior.
- Output artifacts: `FactReconstructRewriteProviderTests.cs`; `RewriteProviderAdapterTests.cs`; provider implementation files.
- Verification evidence: Focused provider tests passed, then full .NET suite passed 60/60.
- Limitations: Live signed-in rewrite was not executed because this Codex session has no authenticated Entra user token.

### 2026-05-24 - dotnet-backend-testing - C# rewrite engine/provider implementation

- Agent: Codex
- Trigger: The task added C#/.NET rewrite-engine logic, provider adapters, DI registration, and worker-facing failure behavior.
- Action: Opened and followed the skill; used xUnit and deterministic fakes, verified RED failures before implementation, then added domain, provider, adapter, and DI tests.
- Output artifacts: `RewriteEngineCoreTests.cs`; `FactReconstructRewriteProviderTests.cs`; `RewriteProviderAdapterTests.cs`; `InfrastructureServiceCollectionTests.cs`; C# domain/infrastructure files.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 60/60 and `dotnet build backend-dotnet/ReplyInMyVoice.sln --no-restore` passed.
- Limitations: Tests use fake model/Sapling clients for deterministic coverage; live provider behavior was limited to app-setting-name checks and unauthenticated Azure smokes.

### 2026-05-24 - claude-heavy-planning-handoff - C# backend migration routing

- Agent: Codex
- Trigger: The requested goal spans multiple services and could qualify for Claude Code heavy planning.
- Action: Opened and followed the skill's routing guidance, but did not call Claude CLI because the owner explicitly asked Codex to proceed with implementation and the first safe public-runtime slice was locally executable.
- Output artifacts: None beyond this log entry.
- Verification evidence: Codex implemented and verified the migration slice directly with the checks listed above.
- Limitations: No Claude handoff document or Claude CLI result was produced, so Claude Code did not participate in this run.

### 2026-05-24 - dotnet-backend-testing - Post-deploy backend verification

- Agent: Codex
- Trigger: The owner asked Codex to keep testing after production deployment, fix issues autonomously, and ensure the C# Azure backend remained deployed successfully.
- Action: Opened and followed the skill; reran the full .NET backend suite after the production merge and rechecked the remote Azure Functions health boundary.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 60/60; `https://replyinmyvoice-func-dev.azurewebsites.net/api/health` returned `{"ok":true,"service":"replyinmyvoice-functions"}` with HTTP 200.
- Limitations: No signed-in live rewrite was executed because this Codex session does not have an authenticated user access token.

### 2026-05-24 - ui-browser-testing - Post-deploy frontend and E2E verification

- Agent: Codex
- Trigger: The owner asked Codex to keep testing after production deployment; the browser-visible Cloudflare Worker, landing page, auth gate, and API proxy behavior needed verification.
- Action: Opened and followed the skill; ran Playwright E2E, investigated the landing-page assertion failure, and updated the test to match the current footer positioning copy.
- Output artifacts: `tests/e2e/commercial-site.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Production `https://replyinmyvoice.com/` returned HTTP 200; `https://replyinmyvoice.com/api/health/db` returned `{"ok":true,"database":"azure-sql"}` with HTTP 200; unsigned production `POST /api/rewrite` returned HTTP 401 with `{"error":"unauthorized"}`. The focused Playwright failure was traced to stale expected copy: the page now says "Built for practical replies..." instead of the previous "Built for real communication workflows".
- Limitations: Browser E2E covers signed-out gates and static commercial pages only; authenticated rewrite UX remains untested without a real user session token.

### 2026-05-24 - ui-browser-testing - Entra Google callback returns to sign-in

- Agent: Codex
- Trigger: Owner reported that Google login successfully reached `/auth/callback` and redirected to `/app`, but `/app` immediately bounced back to `/sign-in`.
- Action: Opened and followed the skill; traced the auth cookie write/read path through `lib/entra-auth.ts`, `middleware.ts`, `/auth/callback`, and existing auth tests. Added a regression test proving the signed `rimv_session` cookie exceeded the browser 4KB limit when it embedded Entra access/refresh tokens, then changed `rimv_session` to persist only the browser gate identity fields while storing the Azure access token in separate signed chunked cookies for server-side Azure proxy calls.
- Output artifacts: `lib/entra-auth.ts`, `tests/unit/entra-auth.test.ts`, `docs/skill-run-log.md`.
- Verification evidence: Watched the new test fail at 8378-byte cookie size before the fix. After the fix, `npm test -- tests/unit/entra-auth.test.ts`, `npm test -- tests/unit/middleware.test.ts`, full `npm test` (94 tests on the current `origin/main` line), `npm run typecheck`, `npm run lint`, `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium`, and a clean standalone `npm run cf:build` all passed. Pushed commit `5b20341` to `main`; GitHub Actions `cloudflare-worker` run `26358606532` and `dotnet-azure` run `26358606522` passed. Production smoke returned `https://replyinmyvoice.com/` 200, signed-out `/app` 307 to `/sign-in`, `/api/health/db` `{"ok":true,"database":"azure-sql"}`, and unsigned `/api/rewrite` 401.
- Limitations: No live production Google login was performed from the owner's browser in this turn. Local direct `npm run cf:deploy` could not deploy without `CLOUDFLARE_API_TOKEN`, so production deploy used the configured GitHub Actions secrets. No secrets, token values, `.env.local` contents, or provider credentials were logged.

### 2026-05-24 - ui-browser-testing - Rewrite workspace pending result crash

- Agent: Codex
- Trigger: Owner showed a production `/app?checkout=cancelled` client-side exception after login and testing the workspace: `Cannot read properties of undefined (reading 'length')`.
- Action: Opened and followed the skill; traced the browser-visible crash to `/app` treating non-success rewrite payloads as successful results. Added response normalization so missing optional UI fields cannot crash the workspace, changed `/api/rewrite` to normalize direct/resultJson success payloads, kept pending attempts distinct with their `attemptId`, added a signed-in attempt polling proxy, and changed the client to keep polling the same attempt instead of creating a new request.
- Output artifacts: `lib/rewrite-response.ts`; `app/api/rewrite/route.ts`; `app/api/rewrite-attempts/[attemptId]/route.ts`; `components/app/rewrite-workspace.tsx`; `tests/unit/rewrite-response.test.ts`; `tests/unit/rewrite-api-quality.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Watched the new focused tests fail before implementation, then pass. `npm test` passed 99/99; `npm run typecheck` passed; `npm run lint` passed; `npx playwright test tests/e2e/auth-gate.spec.ts --project=chromium` passed 3/3; `npm run cf:build` completed and included `/api/rewrite-attempts/[attemptId]`.
- Limitations: Local Playwright verification covered signed-out browser gates and API rejection only; this Codex session still does not have the owner's authenticated production browser session, so the final live signed-in rewrite flow needs owner-side retest after deployment.

### 2026-05-24 - cloud-architecture-cost-review - Rewrite packs pricing analysis

- Agent: Codex
- Trigger: Owner asked for analysis of the latest rewrite-pack pricing scheme, including Stripe fees, GST exposure, AI/provider cost per rewrite, and launch guardrails.
- Action: Opened and followed the skill; reviewed `docs/rewrite-packs-pricing-spec.md`, compared the proposed Free Trial / Quick Pack / Value Pack / Pro/API / Focus Pack economics, and verified current public Stripe NZ card fees plus IRD GST rate/registration-threshold guidance from official sources.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Official Stripe NZ pricing confirmed domestic cards at 2.65% + NZ$0.30, international cards at 3.5% + NZ$0.30, and +2% if currency conversion is required. Official IRD pages confirmed GST registration at NZ$60,000 taxable turnover expectation/actual threshold and GST at 15% for registered businesses.
- Limitations: This was a pricing/cost review only; no code, Stripe products, Stripe Prices, database migrations, or paid resources were created. Real provider cost per rewrite still needs production measurement from rewrite cost telemetry before increasing included rewrite counts.

### 2026-05-25 - dotnet-backend-testing - Rewrite certainty-preservation gate

- Agent: Codex
- Trigger: Owner asked to modify the rewrite engine after a test case showed good AI-signal reduction but a fact risk from changing `seems` into a definite `is due to` claim.
- Action: Opened and followed the skill; wrote a failing xUnit regression test before production changes, then added deterministic uncertainty-preservation handling in the C# rewrite fact gate and prompt guidance.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteEngineCoreTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/FactReconstructRewriteProvider.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/OpenAiCompatibleRewriteModelClient.cs`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: The focused test `FactGate_blocks_uncertainty_strengthening_from_seems_to_is_due_to` first failed because the current gate passed the candidate; after implementation it passed. `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore --filter Rewrite` passed 38/38.
- Limitations: This verifies deterministic gate behavior and rewrite prompt guidance locally; it does not prove a live Sapling/DeepSeek production rewrite score for the exact Emma sample until remote authenticated rewrite smoke runs after deployment.

### 2026-05-25 - resilience-test-generation - Applicability check for certainty-preservation change

- Agent: Codex
- Trigger: The change touches provider-backed rewrite quality gates, so Codex checked whether resilience-test-generation was needed for retries, provider failures, idempotency, or recovery behavior.
- Action: Opened the skill and determined this was a deterministic fact-preservation bugfix, not a retry/timeout/provider-failure behavior change. No resilience matrix was generated.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Existing rewrite provider retry tests remained within the `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore --filter Rewrite` run, which passed 38/38.
- Limitations: No new provider failure scenario was added because the requested behavior is certainty preservation, not dependency failure handling.

### 2026-05-25 - ui-browser-testing - Rewrite-packs pricing cutover (homepage, /pricing, paywall)

- Agent: Claude Code
- Trigger: UI/copy cutover replacing the old subscription pricing (Starter NZ$9.90/55, Pro 110, Exam Week Pass, Top-up, Students framing) with the rewrite-packs model across the homepage, /pricing, and the /app paywall; required local webpage verification.
- Action: Skill is not indexed in this Claude Code session, so followed the project source checklist (`agent-skills/ui-browser-testing`) via the harness preview tools. Started the Next.js dev server and verified /pricing (desktop + mobile), homepage hero/stats/pricing block, footer + nav, and the /students -> / redirect; checked console for errors; captured desktop and mobile screenshots.
- Output artifacts: `app/pricing/page.tsx`; `components/landing/{pricing-v2,hero,trust-panel,closing-cta,faq}.tsx`; `components/site-header.tsx`; `components/site-footer.tsx`; `app/sitemap.ts`; `components/app/paywall-card.tsx`; `app/app/page.tsx`; `components/app/rewrite-workspace.tsx`; `components/auth/google-oauth-card.tsx`; `app/terms/page.tsx`; `next.config.ts` (redirects); `tests/unit/{pricing-auth-visual-system,workspace-copy}.test.ts`; removed `app/students/page.tsx`, `app/launch/page.tsx`, `tests/unit/students-v2-page.test.ts`.
- Verification evidence: `npm run typecheck`, `npm run test` (95 passed / 18 files), `npm run build`, and `npm run cf:build` all passed; banned-term grep clean; dev-server preview showed /pricing rendering Quick/Value(Most popular)/Pro·API with graceful "Available soon" gating, homepage pricing block + hero updated, footer/nav free of old model and "Students", and /students + /launch returning to / with no console errors (desktop + mobile screenshots captured).
- Limitations: Local dev-server verification only; the OpenNext/Cloudflare Worker runtime can differ from local (prerendered pages have 500'd in prod before despite local gates passing), so /pricing, the /students redirect, and the /app paywall must be re-verified on the deployed Worker. The /app paywall and workspace nudge were verified via source/contract tests, not an authenticated exhausted-quota browser session. Stripe pack Prices are not yet created, so all paid CTAs are intentionally "Available soon".

### 2026-05-25 - system-spec-synthesis - Single-input draft rewrite eval contract

- Agent: Codex
- Trigger: Owner corrected the rewrite-quality eval target from a dual-input reply eval to the current product-shaped single-input draft rewrite eval.
- Action: Opened and followed the skill, then wrote an implementation-ready spec covering context, goals, non-goals, current system, proposed architecture, data model, runner contract, state/error handling, privacy, rollout, and verification.
- Output artifacts: `docs/single-input-draft-rewrite-eval-spec.md`; `docs/rewrite-email-eval-cases-100.md`; `lib/rewrite-eval-cases.ts`; `scripts/eval-scenarios.ts`; `tests/unit/rewrite-email-eval-cases.test.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `docs/rewrite-strategy-memory.md`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`.
- Verification evidence: `npm test -- tests/unit/rewrite-email-eval-cases.test.ts tests/unit/eval-scenarios-corpus.test.ts` passed 30/30; `EVAL_CORPUS=email-100 npx tsx scripts/eval-scenarios.ts --mode=smoke --limit=0 --output=/tmp/replyinmyvoice-single-input-eval-dry-run.md` loaded 10 smoke cases and exited without provider calls.
- Limitations: Only cases 001-010 are materialized. Provider smoke was intentionally not run after the contract pivot. `npm run typecheck` remains blocked by the existing missing `stripe` package/type declaration in `lib/stripe.ts`.

### 2026-05-25 - cloud-architecture-cost-review - Eval provider-cost gate

- Agent: Codex
- Trigger: The requested rewrite-quality eval path can spend DeepSeek and Sapling calls; the old provider smoke had already run against the wrong dual-input corpus.
- Action: Opened and followed the skill's cost-control posture. Stopped the old eval process, kept this turn to local parser/runner validation, and documented staged provider usage: local validation first, 10-case smoke second, focused/full only after the corpus and judge shape are stable.
- Output artifacts: `docs/single-input-draft-rewrite-eval-spec.md`; `docs/rewrite-email-eval-cases-100.md`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/skill-run-log.md`.
- Verification evidence: Local dry-run used `--limit=0`, loaded 10 smoke cases, wrote `/tmp/replyinmyvoice-single-input-eval-dry-run.md`, and did not require DeepSeek or Sapling calls.
- Limitations: No new cost telemetry was generated because no provider calls were made under the corrected corpus contract.

### 2026-05-25 - cloud-architecture-cost-review - Corrected single-input smoke calibration

- Agent: Codex
- Trigger: Owner asked to run the corrected single-input warm-tone smoke 10 against the real provider path as a calibration run, explicitly limited to the 10 materialized cases.
- Action: Opened and followed the skill's provider-cost gate. Confirmed no eval process was running, reran local tests and lint, performed a `--limit=0` dry-run, then ran only `--mode=smoke --limit=10` against the existing provider environment. Did not run focused/full mode, materialize more cases, or change prompts/scoring/parser/corpus.
- Output artifacts: `docs/eval-runs/single-input-smoke-10-corrected.md`; `docs/eval-runs/single-input-smoke-10-corrected-triage.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/rewrite-email-eval-cases.test.ts tests/unit/eval-scenarios-corpus.test.ts` passed 30/30; `npm run lint` passed; dry-run loaded exactly 10 smoke cases; completed smoke report recorded 10/10 evaluated, 4/10 customer-usable pass, 4/10 strict signal pass.
- Limitations: The first provider attempt aborted on a DeepSeek connect timeout during semantic judging; after a no-credential endpoint probe returned HTTP 401, one identical rerun completed. Raw provider payloads are not logged, so judge-only separation is verified by mapping tests and report behavior rather than request capture. No optimization fixes were made in this run.

### 2026-05-25 - cloud-architecture-cost-review - Admin Mongo/ELK necessity review

- Agent: Codex
- Trigger: Owner asked whether the Reply In My Voice admin console needs MongoDB or ELK.
- Action: Opened and followed the skill; reviewed current manual setup/run-result docs, admin pages, cost/learning metric helpers, EF Core models, and Application Insights wiring. Selected the existing SQL + Application Insights architecture for the production admin console and rejected MongoDB/ELK as duplicate paid/operational infrastructure for the current workload.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Confirmed `/admin` uses request-level rewrite quality/cost/learning telemetry; confirmed `RewriteCostLog`, `RewriteProviderCall`, `LearningRun`, `LearningFinding`, `StrategyCandidate`, and `RewriteLearningSample` are already modeled in SQL/EF or Prisma history; confirmed Application Insights is already configured for API/Functions/Worker observability.
- Limitations: Exact current MongoDB/Cosmos/Elastic pricing was not checked because no numeric monthly run-rate was quoted; this was an architecture necessity review, not an implementation or deployment change.

### 2026-05-25 - data-module-review - Admin Mongo/ELK persistence fit review

- Agent: Codex
- Trigger: The MongoDB question affects persistence boundaries for admin telemetry, learning samples, quota, billing, and rewrite-attempt records.
- Action: Opened and followed the skill; inspected SQL schema ownership and invariants around `RewriteAttempt`, `UsageReservation`, `StripeEvent`, `OutboxMessage`, admin cost logs, provider calls, and learning tables. Determined MongoDB should not become an authoritative store for admin, billing, quota, or rewrite job state.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: SQL schema already has unique/idempotency constraints, row-version concurrency tokens, foreign keys, and indexed admin lookup paths for current admin screens. A helper `scan_data_risks.py --limit 80` run was started but stopped after it did not return promptly in the dirty workspace; manual targeted reads supplied the review evidence.
- Limitations: No code, migrations, tests, or database resources were changed. This does not rule out a future isolated, non-authoritative local Mongo/ELK portfolio demo fed from exported events.

### 2026-05-25 - system-spec-synthesis - Admin console capability and phase review

- Agent: Codex
- Trigger: Owner asked what the management/admin console should include and what order to build it in.
- Action: Opened and followed the skill; converted the loose admin-console question into staged requirements covering actors, current system, data ownership, read/write boundaries, operational modules, state lifecycles, security, rollout, and verification.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Reviewed current `/admin` overview, metric queries, admin auth helper, manual setup notes, Azure cutover docs, and EF Core table mappings for users, quota, rewrite attempts, Stripe events, outbox, cost logs, learning, API keys, and referrals.
- Limitations: This was an analysis/specification response only; no implementation spec file, admin API, UI, migration, or cloud resource was created.

### 2026-05-25 - state-machine-modeling - Admin lifecycle coverage review

- Agent: Codex
- Trigger: The admin console requirements review covers lifecycle-heavy areas: rewrite attempts, usage reservations, Stripe webhook processing, outbox dispatch, subscriptions, credits, canary rollback, and API keys.
- Action: Opened and followed the skill; identified which admin phases must expose state lists, transition history, illegal/stuck states, retryable failures, and recovery actions without sidestepping C#/.NET transition logic.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Confirmed persisted state-bearing tables and indexes in EF Core: `RewriteAttempts`, `UsageReservations`, `StripeEvents`, `OutboxMessages`, `RewriteCredits`, `LearningRuns`, `StrategyCandidates`, `RewriteCanaryRollbacks`, `ApiKeys`, and `ApiKeyUsages`.
- Limitations: No formal state-machine markdown was generated and no transition helper/test changes were made; this was used to order admin-console capabilities.

### 2026-05-25 - cloud-architecture-cost-review - Admin console phased architecture review

- Agent: Codex
- Trigger: Owner asked about admin-console phases, including whether Python should participate and how to avoid unnecessary infrastructure.
- Action: Opened and followed the skill; recommended keeping the production admin on existing Next.js/.NET/Azure SQL/Application Insights foundations, using Python only as an optional read-only analytics/reporting layer, and rejecting always-on duplicate admin stacks until a clear cost-approved need exists.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Current docs show Cloudflare frontend, Azure Functions/.NET API, Azure SQL, Service Bus, and Application Insights as the production target; prior App Service fixed run-rate was removed in favor of Azure Functions consumption.
- Limitations: Exact current cloud prices were not checked because no specific monthly run-rate was quoted and no paid resource change was proposed.

### 2026-05-25 - data-module-review - Admin console data ownership review

- Agent: Codex
- Trigger: Admin-console phase planning touches user, quota, billing, webhook, queue, cost, learning, and API-key persistence invariants.
- Action: Opened and followed the skill; reviewed the SQL/EF table set and current admin query layer to classify which modules should be read-only, which future operator actions require C# API endpoints, and which actions need audit logging.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Current admin metrics query `RewriteCostLog`/`RewriteProviderCall`; EF Core has unique/idempotency indexes and concurrency tokens for the lifecycle-critical tables; manual setup notes say runtime account/quota/rewrite data is now served from Azure SQL through Azure Functions.
- Limitations: No new admin audit-log schema was added; direct SQL write prevention remains a design recommendation until implemented.

### 2026-05-25 - resilience-test-generation - Semantic judge malformed JSON retry

- Agent: Codex
- Trigger: The single-input dev-20 provider validation hit a semantic judge malformed JSON failure, which changes provider-failure retry and fail-closed behavior.
- Action: Opened and followed the skill; generated a resilience matrix for malformed semantic judge JSON, added a unit regression for retrying malformed judge responses, and updated the eval runner to retry semantic judge parsing before failing a case.
- Output artifacts: `scripts/eval-scenarios.ts`; `tests/unit/eval-scenarios-corpus.test.ts`; `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: `npm test -- tests/unit/eval-scenarios-corpus.test.ts` passed after the retry regression was added; later focused unit verification passed with `npm test -- tests/unit/rewrite-pipeline.test.ts tests/unit/eval-scenarios-corpus.test.ts`.
- Limitations: The later v6 provider partial was intentionally stopped after 6/8 cases per the corrected rerun strategy; no final dev-20 pass, merge, or deploy is claimed in this entry.

### 2026-05-25 - cloud-architecture-cost-review - Cloudflare rewrite-quality validation deploy

- Agent: Codex
- Trigger: User requested merge and deploy after the rewrite-quality validation task.
- Action: Opened and followed the skill; reviewed `docs/manual-setup.md`, `docs/business-qa-and-deploy-result.md`, `README.md`, and `docs/next-development-brief.md`; confirmed this deploy should keep the existing Cloudflare Worker/OpenNext frontend plus Azure Functions/Azure SQL backend path.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Ran `python3 agent-skills/cloud-architecture-cost-review/scripts/cost_review_template.py "Cloudflare rewrite-quality validation deploy"` and reviewed the current deployment docs. Selected option: deploy the existing Cloudflare Worker through the existing `npm run cf:deploy` path with `--keep-vars`. Rejected options: no new Cloudflare service, Azure App Service, queue, database, or always-on worker is needed for this code-only validation deploy.
- Limitations: No exact provider pricing was quoted or checked because this task does not create, resize, or switch paid infrastructure; production smoke still needs to run after deploy.

### 2026-05-27 - system-spec-synthesis - Voice + Fidelity quality-track pivot spec

- Agent: Claude Code
- Trigger: Owner decided (2026-05-27) to stop the AI-detection track and formally pivot to a Voice + Fidelity quality track, and asked to turn the module list into next steps (converting loose product notes into an executable engineering plan).
- Action: Opened and followed the skill (workflow + Output Contract headings); re-read AGENTS.md and the C# rewrite engine; produced an implementation-ready spec converting the owner's 6-module notes (ProtectedTermLedger, BoundaryGate, VoiceProfile, MinimalHumanEdit, SendabilityGate, Quality A/B) into contracts, data model, gate chain, rollout, and verification, reusing the eval-only byproducts from the 10-round investigation and demoting Pangram to offline observation only.
- Output artifacts: `plans/voice-fidelity-quality-track-spec.md` (+ this log entry). Detection-track findings recorded in `plans/translation-roundtrip-pilot.md`.
- Verification evidence: Spec includes a Verification Plan (xUnit + Quality A/B + banned-term grep) and Phase-1 acceptance criteria — notably the hardened FidelityJudge must FAIL the three known object/term-drift misses (seat credit→letter of credit, planter→flowerpot, saucer→tea tray). No code shipped; eval-only until reviewed.
- Limitations: Spec leaves explicit Open Questions (voice-sample intake UI/consent/retention, mode exposure, "user preference" metric, Manus prod cost/latency, VoiceProfile EF schema + migration) as product/architecture decisions, not silent assumptions. `data-module-review` and `cloud-architecture-cost-review` flagged before Phase 3 / any paid prod dependency.

### 2026-05-28 - dotnet-backend-testing - Stage 1 EN->ZH ClaimLedger pilot

- Agent: Codex
- Trigger: Owner asked Codex to start Sub-Phase 1.1 from `plans/phase1-claim-ledger-handoff.md`, adding C# eval-tool behavior and tests for Youdao EN->ZH post-checking and drift reporting.
- Action: Opened and followed the skill; added xUnit coverage for deterministic ZH fact survival checks, batched ClaimLedger verdict parsing, empty ClaimLedger warning behavior, model selection, and markdown drift report rendering before implementing the eval-only pilot.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-101348-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (6 tests). Full suite later passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` after adding the new tests. Real provider verification ran the locked 10-case set with `STAGE1_EN_TO_ZH_PILOT=1` and wrote the report above: Youdao calls 10, DeepSeek calls 20, structured claims 89/93 preserved.
- Limitations: No production rewrite path, Stripe, Azure, DNS, or deployment code was changed. Literal fact-anchor survival remains intentionally naive in 1.1 and does not yet count Chinese equivalents for names, weekdays, or sentence-level constraints; those rows are input for the 1.2 repair step.

### 2026-05-28 - system-spec-synthesis - Stage 1 v1 simplification next-step ordering

- Agent: Codex
- Trigger: Owner clarified the Phase 1 v1 shape: no placeholders and no Chinese GPTZero; focus on FactLedger, ClaimLedger, Youdao EN->ZH, ZH fact coverage, minimal Chinese repair, re-check, and safe Chinese intermediate output.
- Action: Opened and followed the skill to translate the clarified requirements into a concrete next-step implementation order for Sub-Phase 1.2 without reopening the architecture.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Reviewed current Sub-Phase 1.1 implementation and report artifact `docs/rewrite-eval-results/20260528-101348-stage1-en-zh-pilot.md`; no code or tests were changed in this planning response.
- Limitations: This entry records planning only. Phase 1.2 minimal Chinese repair implementation, tests, and provider run remain to be done.

### 2026-05-28 - dotnet-backend-testing - Stage 1.2 Chinese minimal repair loop

- Agent: Codex
- Trigger: Owner approved the v1 simplification and asked what to do next; Codex proceeded with Sub-Phase 1.2 implementation in C# eval-tool code.
- Action: Opened and followed the skill; added xUnit coverage for the repair loop, no-op pass path, safe-intermediate report rendering, and claim-regression rejection. Implemented bounded Chinese minimal repair, second post-check, raw/final ZH report output, and a guard that rejects repairs that reduce ClaimLedger survival.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-103956-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused tests passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (10 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (281 tests). Real provider 10-case Stage 1.2 run used Youdao calls 10 and DeepSeek calls 40; final structured claim preservation was 92/92 after one bounded repair pass.
- Limitations: All 10 cases still fail the literal fact-anchor criterion because the current deterministic FactLedger check is intentionally naive and still requires English verbatim anchors for translated names, weekdays, and sentence fragments. Next work should add preserve-mode-aware hard fact extraction/checking before treating fact failures as final Phase 1 failure.

### 2026-05-28 - system-spec-synthesis - Stage 1.3 preserve-mode-aware hard facts

- Agent: Codex
- Trigger: Owner specified Sub-Phase 1.3 requirements: do not strengthen repair, do not add placeholders/GPTZero/back-translation, and instead make hard fact extraction/checking preserve-mode-aware.
- Action: Opened and followed the skill to scope 1.3 as eval-only hard-fact narrowing plus deterministic exact/normalized/alias checking, leaving production rewrite behavior unchanged.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted owner requirements into implementation checkpoints: normalized date/money/percent/count/duration matching, exact acronym matching, alias matching, structured fact check rows, repair-row filtering, and rerun of the same 10 cases.
- Limitations: This log entry records planning for 1.3; implementation evidence is in the following dotnet-backend-testing entry.

### 2026-05-28 - dotnet-backend-testing - Stage 1.3 preserve-mode-aware hard fact checker

- Agent: Codex
- Trigger: Sub-Phase 1.3 implementation adds C# eval-tool behavior and tests for normalized hard-fact matching and repair-row filtering.
- Action: Opened and followed the skill; added xUnit tests for Chinese date equivalence, dropped-day failure, money equivalence/wrong amount, percent and duration equivalence, acronym exact preservation, alias matching, generic product-name failure, normalized-pass repair filtering, and Stage 1 hard-fact ledger narrowing. Implemented `StageOneHardFactLedgerBuilder`, `StageOneHardFactChecker`, structured `ZhFactCheckItem`, match-kind summaries, and additive Domain fields `ExactOrTranslatedAlias` / `AllowedAliases`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-105841-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused tests passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (18 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (289 tests). Real provider 10-case Stage 1.3 run used Youdao calls 10 and DeepSeek calls 40; hard facts narrowed to 111, final hard-fact preservation was 80/111, and final structured claim preservation stayed 92/92.
- Limitations: Alias generation/classification for person, organization, product, and system names is not implemented. Remaining failures are more explainable but not all resolved; the next likely target is approved alias handling rather than stronger repair prompts.

### 2026-05-28 - system-spec-synthesis - Stage 1.4 approved alias and failure classification

- Agent: Codex
- Trigger: Owner scoped Sub-Phase 1.4 as failure attribution plus approved alias boundaries, with explicit non-goals: no placeholders, GPTZero, English back-translation, production rewrite-path edits, or stronger Chinese repair prompt.
- Action: Opened and followed the skill to turn the requirements into implementation checkpoints: `FactFailureKind`, approved/proposed alias separation, failure breakdown reporting, repair-row routing, hard-fact demotion, and rerun of the locked 10-case set.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted the owner requirements into a bounded eval-only plan before implementation; implementation and provider evidence are recorded in the following dotnet-backend-testing entry.
- Limitations: This entry records planning only. It did not create a production alias catalog or real TermLedger.

### 2026-05-28 - dotnet-backend-testing - Stage 1.4 approved alias and failure classification

- Agent: Codex
- Trigger: Sub-Phase 1.4 implementation adds C# eval-tool behavior and xUnit coverage for failure classification, approved alias boundaries, and repair filtering.
- Action: Opened and followed the skill; added focused tests for approved alias pass, proposed/unapproved alias review, generic entity failure, acronym exact translation failure, person-name alias review, Chinese numeral duration normalizer gaps, weekday equivalents, over-extracted phrase handling, capitalized non-name demotion, failure breakdown rendering, and non-actionable repair filtering. Implemented `ZhFactFailureKind`, `RecommendedNextAction`, `ProposedAliases`, failure breakdown report output, repair report filtering, weekday normalization, and Stage 1 hard-fact demotion for obvious capitalized non-name noise.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`; `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-114558-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (27 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (298 tests). Real provider 10-case Stage 1.4 run used Youdao calls 10 and DeepSeek calls 30; hard facts narrowed to 88, final hard-fact preservation was 76/88, final structured claim preservation was 94/94, and all remaining fact failures were `alias_not_approved`.
- Limitations: The approved alias catalog itself is not implemented; `ProposedAliases` are review-only and do not auto-pass. Next work should build a controlled alias catalog / glossary rather than strengthening Chinese repair.

### 2026-05-29 - system-spec-synthesis - Stage 1.5 approved alias catalog effect check

- Agent: Codex
- Trigger: Owner asked to continue to the next step and see the effect after Sub-Phase 1.4 showed all remaining hard-fact failures were `alias_not_approved`.
- Action: Opened and followed the skill to scope 1.5 as an eval-only approved alias catalog, with explicit non-goals: no LLM auto-approval, no placeholders, no GPTZero, no English back-translation, no production rewrite-path changes, and no stronger Chinese repair prompt.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted the next step into implementation checkpoints: approved aliases may pass, proposed/unapproved aliases remain review-only, and the same locked 10-case set must be rerun to compare failure breakdown against 1.4.
- Limitations: This entry records planning only. It does not introduce a persistent production glossary or user-managed alias approval UI.

### 2026-05-29 - dotnet-backend-testing - Stage 1.5 approved alias catalog effect check

- Agent: Codex
- Trigger: Sub-Phase 1.5 implementation adds C# eval-tool behavior and tests for an approved alias catalog.
- Action: Opened and followed the skill; added xUnit tests proving a known approved alias (`Jamie` -> `杰米`) becomes an allowed alias and that an unapproved proposed alias still does not auto-pass. Implemented `StageOneApprovedAliasCatalog` and wired it after Stage 1 hard-fact ledger building.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-123932-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (29 tests). Real provider 10-case Stage 1.5 run used Youdao calls 10 and DeepSeek calls 28; final hard-fact preservation improved to 87/88, final structured claim preservation was 93/93, and pass count improved to 9/10.
- Limitations: The catalog is eval-only and seeded only with aliases observed in the locked 10-case run. The remaining failure is a standalone count `30` extracted from `5:30 p.m.` and translated as `下午五点半`; that should be handled by time normalizer / count extractor tightening, not by alias approval or stronger Chinese repair.

### 2026-05-29 - system-spec-synthesis - Stage 1.6 time normalizer and count extractor tightening

- Agent: Codex
- Trigger: Owner asked to do the next step: time normalizer / count extractor tightening after Sub-Phase 1.5 left only `30` from `5:30 p.m.` as a hard-fact failure.
- Action: Opened and followed the skill to scope 1.6 narrowly: fix the eval-only Stage 1 hard-fact builder so clock-minute fragments are not treated as standalone Count facts, without changing repair prompts, placeholders, GPTZero, English back-translation, or the production rewrite path.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Converted the next step into implementation checkpoints: add a RED test for filtering clock-minute count fragments while preserving real quantities, implement Stage 1-only filtering, rerun focused tests, and rerun the locked 10-case provider set.
- Limitations: This entry records planning only. It does not redesign the production `FactLedgerExtractor` or add a full time entity model.

### 2026-05-29 - dotnet-backend-testing - Stage 1.6 time normalizer and count extractor tightening

- Agent: Codex
- Trigger: Sub-Phase 1.6 implementation changes C# eval-tool hard-fact filtering and tests for clock-minute count artifacts.
- Action: Opened and followed the skill; added a focused xUnit test proving `30` in `5:30 p.m.` is filtered while ordinary quantities such as `18 seats` are preserved. Implemented Stage 1-only clock-minute detection in `StageOneHardFactLedgerBuilder`.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-124707-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD test failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter StageOneEnToZhSafePilotTests` (30 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (301 tests). Real provider 10-case Stage 1.6 run used Youdao calls 10 and DeepSeek calls 26; final hard-fact preservation was 86/86, final failure breakdown was `none`, and all 10 cases passed.
- Limitations: The tightening is eval-only. It filters count facts whose only source occurrence is the minute component of a clock time; it does not add a full `Time` fact type or a production glossary.

### 2026-05-29 - dotnet-backend-testing - Stage 1.7 30-case generalization check

- Agent: Codex
- Trigger: Owner asked to expand from the fixed 10-case set to about 30 wider samples to validate whether the approved alias catalog and count/time tightening generalize.
- Action: Opened and followed the skill; ran a 30-case Stage 1 provider baseline, added focused xUnit coverage for filtering clock-hour count fragments and wider capitalized non-entity words, implemented eval-only hard-fact builder tightening, and updated report rendering so unrepaired failing cases show final drift details.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-130115-stage1-en-zh-pilot.md`; `docs/rewrite-eval-results/20260528-131237-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (33 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (304 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case rerun used Youdao calls 30 and DeepSeek calls 80; final hard-fact failures dropped from 53 to 35, final `value_changed` failures dropped from 2 to 0, and final ClaimLedger preservation was 100% for all 30 cases.
- Limitations: The approved alias catalog remains eval-only and seeded from the locked 10-case set. The 30-case remainder is all `alias_not_approved`, split between real entity aliases and over-extracted single-token entity fragments; no production glossary, placeholders, GPTZero, English back-translation, or stronger Chinese repair prompt was added.

### 2026-05-29 - dotnet-backend-testing - Stage 1.8 hard-fact placement and entity merge

- Agent: Codex
- Trigger: Owner approved the next step after the 30-case expansion showed remaining failures were alias/placement problems rather than Chinese repair problems.
- Action: Opened and followed the skill; added focused xUnit coverage for merging title-case entity fragments, preserving salutation person names, demoting non-entity capitalized words, routing unmatched exact-or-alias entities to alias review, and keeping merged person names as person facts. Implemented eval-only title-case fragment merge and refined exact-or-alias failure classification.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-133028-stage1-en-zh-pilot.md`; `docs/rewrite-eval-results/20260528-133915-stage1-en-zh-pilot.md`; `docs/rewrite-eval-results/20260528-135002-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (37 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (308 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case final rerun used Youdao calls 30 and DeepSeek calls 82; hard facts narrowed to 243, final hard-fact failures dropped to 26, and every remaining final failure was `alias_not_approved` routed to `approve_alias`.
- Limitations: This remains eval-only. It does not create a production glossary, does not auto-approve aliases, and does not introduce placeholders, GPTZero, English back-translation, or stronger Chinese repair prompts. Role-like phrases such as `Senior Support Lead` still need a later TermLedger/translated-equivalent decision.

### 2026-05-29 - dotnet-backend-testing - Stage 1.9 controlled alias catalog

- Agent: Codex
- Trigger: Owner said to continue after Stage 1.8 left only approved-alias review failures on the 30-case set.
- Action: Opened and followed the skill; added focused xUnit coverage proving observed wide-sample person/place aliases pass only after explicit catalog approval, and role/title fragments are demoted from hard FactLedger. Expanded the eval-only alias catalog with reviewed 30-case aliases and kept role-like phrases out of hard facts.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-141031-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (39 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (310 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case run used Youdao calls 30 and DeepSeek calls 84; all 30 cases passed, final hard-fact failures were zero, and final ClaimLedger preservation was 100% for all cases.
- Limitations: Alias approval is still eval-only and case-derived. It does not create a production glossary, alias provenance model, domain scoping, user review UI, placeholders, GPTZero, English back-translation, or stronger Chinese repair prompt.

### 2026-05-29 - dotnet-backend-testing - Stage 1.10 structured alias glossary

- Agent: Codex
- Trigger: Owner asked to continue after Stage 1.9 proved the controlled alias catalog can make the 30-case set pass.
- Action: Opened and followed the skill; added focused xUnit coverage for structured alias entries, unapproved/proposed alias routing, and domain-scope isolation. Replaced the eval-only flat alias dictionary with `StageOneAliasEntry` records carrying alias language, provenance source, approval state, and domain scope while preserving the existing 30-case aliases.
- Output artifacts: `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/StageOneEnToZhSafePilotTests.cs`; `docs/rewrite-eval-results/20260528-142745-stage1-en-zh-pilot.md`; `docs/rewrite-strategy-memory.md`; `docs/skill-run-log.md`.
- Verification evidence: Focused TDD tests failed before implementation, then passed. Focused Stage 1 suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~StageOneEnToZhSafePilotTests"` (41 tests). Full suite passed with `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo` (312 tests). `git diff --check` passed. Source banned-term scan found no matches. Real provider 30-case run used Youdao calls 30 and DeepSeek calls 82; all 30 cases passed, final fact summary remained 241/241 preserved, and final failure breakdown was none.
- Limitations: The structured glossary remains eval-only and in-memory. It does not add a persistent production glossary store, tenant/domain management UI, migration, placeholders, GPTZero, English back-translation, or stronger Chinese repair prompt.

### 2026-05-31 - cloud-architecture-cost-review - Entra External ID tenant clarification

- Agent: Codex
- Trigger: Owner asked why the local Azure CLI tenant `53d7668e-c994-4634-8c4d-ff116a03c0b9` could not be used instead of the configured CIAM tenant, and whether a new tenant was required.
- Action: Opened and followed the skill for an identity architecture/cost gate only; read `docs/manual-setup.md` and `docs/next-development-brief.md`, checked the Azure CLI default tenant and tenant list, then used a read-only Microsoft Graph token for the configured CIAM tenant to inspect the frontend app registration.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: `az account show` reported default tenant `53d7668e-c994-4634-8c4d-ff116a03c0b9`; `az account tenant list` also showed the configured CIAM tenant `614ea821-6ef3-43e2-8613-d4b13fae115d`; Microsoft Graph read of app id `02ffae8e-3d30-42d0-86cd-9b858ab33252` returned display name `Reply In My Voice Frontend`, `isFallbackPublicClient: true`, production/local callback URLs, and beta `nativeAuthenticationApisEnabled: all`.
- Limitations: User-flow and identity-provider reads failed without delegated Graph permissions such as `IdentityUserFlow.Read.All` and `IdentityProvider.Read.All`, so email/password, email OTP, reset, and Google provider status still require portal confirmation or additional Graph consent. No Azure or Entra resources were created or modified.

### 2026-05-31 - cloud-architecture-cost-review - CIAM tenant readiness for email auth E2E

- Agent: Codex
- Trigger: Owner asked whether the existing `info@timeawake.co.nz` accessible tenant can complete the remaining Entra External ID setup and when email-code/logout/reset end-to-end testing can run.
- Action: Opened and followed the skill for an identity architecture/cost gate only; used read-only Graph and native-auth probes against the configured CIAM tenant without creating or modifying resources.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Microsoft Graph `/me` in tenant `614ea821-6ef3-43e2-8613-d4b13fae115d` returned `info_timeawake.co.nz#EXT#@replyinmyvoicecustomers.onmicrosoft.com` as `Member`; `/me/memberOf` returned `Global Administrator`; `/organization` returned display name `Reply In My Voice Customers` and tenant type `CIAM`. Direct native-auth sign-in/reset probes for nonexistent addresses returned `user_not_found` instead of client/tenant/native-auth configuration errors. The production logout endpoint returned 307 to `/` and cleared `rimv_session`, `rimv_oauth`, and access-token cookies.
- Limitations: Registration start was intentionally not triggered because it sends a real verification email. User-flow and identity-provider portal screens were not changed.

### 2026-05-31 - ui-browser-testing - Production auth pages readiness check

- Agent: Codex
- Trigger: Owner asked when email verification, logout, and related auth flows can be tested end to end.
- Action: Opened and followed the skill; inspected the production sign-up, sign-in, and forgot-password pages with Playwright and used non-email-sending API probes for signed-out/native-auth behavior.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Production `/sign-up`, `/sign-in`, and `/forgot-password` each loaded with HTTP 200 and visible email fields; Playwright confirmed page titles and form visibility. Production `/api/auth/signin` with a unique nonexistent address returned `404 {"error":"user_not_found","ok":false}`. Production `/api/auth/reset/start` with a unique nonexistent address returned `404 {"error":"No account for this email. Please create one.","ok":false}`. Browser request failures observed were expected Cloudflare RUM/navigation aborts, not app route failures.
- Limitations: The real sign-up verification-code flow, reset-code flow, authenticated `/app`, and sign-out button flow still require a live mailbox code from the owner and were not completed in this check.

### 2026-05-31 - ui-browser-testing - Dynamic auth E2E workflow completion

- Agent: Codex
- Trigger: Owner asked to run Entra External ID email/password auth E2E through a dynamic GitHub issue workflow with live code handoff, issue closure, and a sentinel monitor.
- Action: Opened and followed the skill; verified production sign-up, `/app` session creation, logout signed-out gate, email/password re-login, forgot-password reset-code completion, and new-password sign-in. Used GitHub issues #356-#362 as the workflow ledger, fixed the discovered CIAM issuer session-minting bug through PR #363, and stopped the heartbeat sentinel after all workflow issues closed.
- Output artifacts: GitHub issues #356-#362; PR #363; `docs/skill-run-log.md`.
- Verification evidence: #357, #358, #359, #360, #361, #356, and #362 were closed with evidence comments. PR #363 merged to main as `a84023a43ede6f04bb891ce2927f4eac6c9e65e0`; main Cloudflare workflow `26707889589` completed build-test and deploy successfully. Runtime verification showed sign-in HTTP 302 followed by `/app` HTTP 200 with a workspace marker, logout HTTP 307 followed by `/app` HTTP 307 to `/sign-in?redirectTo=%2Fapp`, re-login HTTP 302 followed by `/app` HTTP 200, reset verify HTTP 200 with `ok: true`, and new-password sign-in HTTP 302 followed by `/app` HTTP 200.
- Limitations: The run used one owner-accessible test email alias and owner-provided verification codes. Codes, temporary passwords, cookies, and tokens were intentionally not written to GitHub or docs.

### 2026-05-31 - ui-browser-testing - Auth module completion status audit

- Agent: Codex
- Trigger: Owner asked whether the registration/login module was complete and what auth coverage remained untested.
- Action: Opened and followed the skill; rechecked GitHub issue closure, the merged CIAM issuer fix PR, the successful main Cloudflare deployment, and the existing auth unit/E2E test coverage.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: GitHub issues #356-#362 were all `CLOSED` with `COMPLETED` state reason and `verified` label. PR #363 was merged to `main` as `a84023a43ede6f04bb891ce2927f4eac6c9e65e0`. Main Cloudflare workflow `26707889589` completed successfully. Auth coverage was present in `tests/e2e/auth-gate.spec.ts`, `tests/e2e/auth-forgot.spec.ts`, `tests/unit/auth-signup-routes.test.ts`, `tests/unit/auth-signin-route.test.ts`, `tests/unit/auth-reset-routes.test.ts`, `tests/unit/entra-native-auth.test.ts`, `tests/unit/entra-auth.test.ts`, and `tests/unit/auth-rate-limit.test.ts`.
- Limitations: This was a status audit, not a new browser run. Remaining optional coverage includes Google OAuth live E2E, live resend-code paths, live negative-code/password-policy cases, production rate-limit behavior, mobile visual screenshots, session-expiry behavior, cross-browser coverage, and account deletion if that is considered part of auth acceptance.

### 2026-05-31 - ui-browser-testing - Remaining auth coverage issue planning

- Agent: Codex
- Trigger: Owner asked to discuss, before starting, converting the remaining auth coverage gaps into GitHub issues supervised by subagents and a sentinel, with real frontend testing through Playwright MCP.
- Action: Opened and followed the skill for planning only; no GitHub issues, browser runs, account deletions, or sentinels were started.
- Output artifacts: `docs/skill-run-log.md`.
- Verification evidence: Proposed a production-focused issue breakdown for Google OAuth, account cleanup, resend paths, negative auth cases, mobile screenshots, session behavior, rate-limit smoke, and cleanup/ledger closure. The plan requires Playwright MCP/browser evidence, console/network review, issue evidence comments, and no secrets or one-time codes in GitHub.
- Limitations: Planning only. Execution awaits owner approval and clarification on whether the `chuanqiao1128@gmail.com` Google test user should remain registered after the flow or be cleaned up.

### 2026-05-31 - cloud-architecture-cost-review - Temporary SQL access for auth cleanup

- Agent: Codex
- Trigger: Owner approved starting the remaining auth E2E workflow, which required deleting a pre-existing Google test account and checking Azure SQL-backed app state.
- Action: Opened and followed the skill; compared the zero-cost temporary firewall/read-only verification path against unsafe direct production mutations. Added a single temporary Azure SQL firewall rule for the current IP, used it to verify and erase only the old test account through project `AccountService`, then removed the rule.
- Output artifacts: GitHub issue #365; `docs/skill-run-log.md`.
- Verification evidence: Temporary rule `codex-auth-e2e-20260531` was created for the current IP and later deleted. Backend lookup found one matching app user with no Stripe subscription; project `AccountService.DeleteAccountAsync` erased that state with postcheck active old external-auth id count 0. CIAM user delete returned 204, permanent deleted-item delete returned 204, and post-delete lookup returned 404.
- Limitations: The temporary SQL access was used only for this cleanup check. No Azure resources were created or resized, no fixed monthly cost was added, and no connection strings, passwords, tokens, or one-time codes were logged.

### 2026-05-31 - dotnet-backend-testing - Account deletion retry-strategy fix

- Agent: Codex
- Trigger: The auth cleanup exposed a production-path `.NET` bug: `AccountService.DeleteAccountAsync` failed when SQL Server retrying execution strategy was enabled around a user-initiated transaction.
- Action: Opened and followed the skill; added a failing xUnit regression test using a retrying EF execution strategy, then wrapped the account-erasure transaction in `Database.CreateExecutionStrategy().ExecuteAsync`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: New focused test failed before the fix with `The configured execution strategy 'TestExecutionStrategy' does not support user-initiated transactions`; after the fix, the focused test passed. `dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo --filter "FullyQualifiedName~AccountServiceTests"` passed 9 tests. `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo` passed 397 tests.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable, but restore/build/test completed with cached packages.

### 2026-05-31 - ui-browser-testing - Remaining auth coverage dynamic workflow execution

- Agent: Codex
- Trigger: Owner approved starting the remaining auth coverage workflow with GitHub issues, subagent-style/sentinel oversight, and real frontend testing through Playwright MCP/browser automation.
- Action: Opened and followed the skill; executed GitHub issue workflow #364-#375 with production browser checks for Google OAuth self-service sign-up, Google logout/re-login, email sign-up and reset resend paths, negative auth cases, desktop/mobile screenshots, session persistence, protected-route redirects, and bounded rate-limit smoke.
- Output artifacts: GitHub issues #364-#375; PR #376; local screenshot artifacts under `/tmp/rimv-auth-visual-20260531`; `docs/skill-run-log.md`.
- Verification evidence: #365-#373 were closed with evidence comments. Google auth used the correct Entra branch `Use another account -> Sign in with Google -> attribute collection` and reached `/app`. Email sign-up/reset resend flows reached cooldown and resend states, completed with owner-supplied codes, and post-reset email login reached `/app`. Negative tests showed friendly errors for wrong password, weak sign-up value, wrong verification code, and stale flow-cookie calls. Desktop and mobile auth screenshots had no horizontal overflow, clipped controls, unexpected console errors, or failed requests. Session checks preserved `/app` across refresh/navigation and redirected signed-out `/app` to `/sign-in?redirectTo=%2Fapp`. Rate-limit smoke kept normal auth pages at HTTP 200 and returned bounded invalid sign-in attempts as HTTP 401, not 500.
- Limitations: Owner-provided one-time codes and temporary sign-in values were intentionally not recorded in GitHub, docs, or logs. Cross-browser coverage beyond Chromium/Chrome was not part of this issue set.

### 2026-05-31 - cloud-architecture-cost-review - Final auth E2E cleanup verification

- Agent: Codex
- Trigger: Final cleanup for the remaining auth coverage workflow required verifying Azure SQL-backed app erasure without leaving paid or long-lived infrastructure changes.
- Action: Reused the cost-gated temporary-access pattern: deleted test account state through the app UI/account API, hard-deleted matching CIAM users through Microsoft Graph, created a single temporary Azure SQL firewall rule only for cleanup verification, queried for target app-user remnants, and removed the firewall rule.
- Output artifacts: GitHub issue #374; `docs/skill-run-log.md`.
- Verification evidence: App account deletion UI returned to the public home page without delete errors for the verified email alias and the Google test account. Microsoft Graph active deletes and deleted-item hard deletes returned 204 for the matching CIAM users; the post-delete target-user query returned an empty list. Azure SQL verification returned `target_original_rows 0` for the test emails/original Entra object ids and `recent_erased_rows 3` for recent anonymized app-user rows.
- Limitations: The temporary SQL access was only used for cleanup verification and no secrets or connection strings were logged. An older pre-existing `info+rimv-e2e-20260531@timeawake.co.nz` CIAM test user was observed but not touched because it was outside this owner-approved Gmail cleanup scope.

### 2026-06-02 - system-spec-synthesis - Promo-code trial design (replace auto free-3)

- Agent: Claude Code (supervisor, interactive).
- Trigger: Owner asked to replace the automatic 3-free-rewrites model with a redeemable universal promo code (alphanumeric, expires end of Aug 2026), and to first capture the design in an MD doc on a new branch without writing code. This converts loose product notes into an implementation-ready engineering plan (system-spec-synthesis), and also applied the state-machine-modeling and data-module-review lenses for the redemption lifecycle and the new persistence tables.
- Action: Opened and followed the skill's output contract. Grounded the design by reading the live C#/Azure SQL backend (no code changed): `AccountService.GetUsagePlan`/`GetOrCreateAccountSummaryAsync`, `AdminService.GrantCreditsAsync`, `QuotaService` credit-overflow consumption, `RewriteCredit` entity, `AppDbContext` conventions, Functions HTTP route pattern, and the frontend copy + contract tests. Confirmed no existing promo/coupon code and that the C# `Referral` entity is vestigial. Authored a full spec (Context/Goals/Non-Goals/Current System/Architecture/Data Model/API & Job Contracts/State & Error Handling/Security & Privacy/Rollout/Verification/Open Questions + copy & interaction design + Codex implementation checkpoints). Created branch `feat/promo-code-trial`.
- Output artifacts: `plans/promo-code-trial-spec.md`; branch `feat/promo-code-trial`; `docs/skill-run-log.md`.
- Verification evidence: Design only — no code, tests, migrations, or deploys executed. Key architecture claims verified against live source (free "3" is a `UsagePeriod.QuotaLimit`, not a credit; `/api/me` already sums credits into `remaining` with per-source `ExpiresInDays`; redemption can reuse the `RewriteCredit{Source="PROMO"}` grant shape so the consumption/paywall/quota-race path is unchanged). Confirmed no driver/codex process was running and the payment wave was `WAVE_DONE` before branching, to avoid a shared-checkout collision.
- Limitations: Planning only; implementation of source changes is delegated to Codex and awaits owner confirmation of the open inputs (actual code value, exact expiry instant/timezone, granted-credit TTL, global cap, distribution channel). Honest constraint documented in the spec: a single universal code cannot be fully abuse-proofed; per-user-unique codes are the real upgrade path the schema already supports.

### 2026-06-02 - state-machine-modeling - PROMO-02 redemption lifecycle

- Agent: Codex
- Trigger: GitHub issue #428 changes the promo-code and per-user redemption lifecycle with terminal success, duplicate, expired, exhausted, and IP-velocity outcomes.
- Action: Opened and followed the skill; modeled `PromoCode` as redeemable only when active, within the valid window, and below the global cap, and modeled `PromoCodeRedemption` as none -> applied with duplicate apply rejected by the unique index.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoServiceTests --no-restore` passed 11 tests; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 412 tests.
- Limitations: PROMO-02 implements service-level redeem/status only; HTTP endpoint mapping and `/api/me` promo block are left to PROMO-03.

### 2026-06-02 - data-module-review - PROMO-02 promo persistence invariants

- Agent: Codex
- Trigger: GitHub issue #428 changes EF-backed promo redemption persistence, usage-credit grants, idempotency, and global cap accounting.
- Action: Opened and followed the skill; reviewed the existing `PromoCode`, `PromoCodeRedemption`, `RewriteCredit`, `AppDbContext`, `AccountService`, `AdminService.GrantCreditsAsync`, `QuotaService`, and `StripeEventService` transaction patterns before implementing the service.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: New tests assert one `RewriteCredit{Source="PROMO"}` per successful redemption, one applied redemption per user/code, exact `RedemptionCount` under parallel cap pressure, salted IP hash storage, and no second grant on duplicate redeem. Full backend suite passed with 412 tests.
- Limitations: No schema or migration changes were made in PROMO-02 because PROMO-01 already supplied the entities, indexes, and checks.

### 2026-06-02 - resilience-test-generation - PROMO-02 duplicate and race coverage

- Agent: Codex
- Trigger: GitHub issue #428 requires idempotent redemption, concurrent same-user replay protection, exact global-cap behavior under parallel load, and IP velocity handling.
- Action: Opened and followed the skill; wrote SQLite integration tests that assert final persisted state across duplicate calls, same-user parallel calls, N+ parallel cap attempts, IP velocity block/flag behavior, and production missing trusted IP fail-closed behavior.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-02 test class passed 11 tests; full backend suite passed 412 tests. `git diff --check` passed. Changed backend files had no forbidden-substring matches.
- Limitations: SQLite file-backed WAL fixtures were used for local race tests; production SQL Server still relies on its retrying execution strategy and the same atomic conditional update.

### 2026-06-02 - dotnet-backend-testing - PROMO-02 xUnit/SQLite coverage

- Agent: Codex
- Trigger: GitHub issue #428 adds C#/.NET backend service behavior and requires xUnit + SQLite acceptance coverage.
- Action: Opened and followed the skill; added `PromoServiceTests` using existing xUnit, FluentAssertions, EF Core SQLite, deterministic clocks, and local logger capture.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoServiceTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoService.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused run failed before implementation because `PromoService`/result types were missing; after implementation, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoServiceTests --no-restore` passed 11 tests and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 412 tests.
- Limitations: A local git commit was attempted but blocked by sandbox write access to the parent repository's worktree metadata; no push or PR was attempted.

### 2026-06-02 - system-spec-synthesis - PROMO-03 endpoint and account summary contract

- Agent: Codex
- Trigger: GitHub issue #429 and `plans/promo-issues/PROMO-03-user-endpoints.md` require API contract implementation for `POST /api/promo/redeem`, optional status, and `/api/me` promo summary data.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, `plans/promo-issues/PROMO-03-user-endpoints.md`, and `plans/promo-code-trial-spec.md` sections 4, 12, and 13. Converted the requirements into implementation checkpoints: thin HTTP mapper over `PromoService`, proxy-secret guarded IP extraction only, stable promo error codes, unchanged credit consumption math, `/api/me` promo block, and WebApplicationFactory acceptance coverage.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-03 acceptance tests passed 13 tests; full `dotnet test backend-dotnet/ReplyInMyVoice.sln --logger "console;verbosity=normal"` passed 425 tests.
- Limitations: The ASP.NET Core API route mirror was added so existing WebApplicationFactory tests can assert the same contract; the deployed Azure Functions route is also implemented in `PromoHttpFunctions`.

### 2026-06-02 - state-machine-modeling - PROMO-03 redemption HTTP outcomes

- Agent: Codex
- Trigger: PROMO-03 exposes the redemption lifecycle over HTTP and must preserve duplicate, invalid, expired, exhausted, velocity, config-error, and success transitions from `PromoService`.
- Action: Opened and followed the skill; modeled `PromoCode` availability as active-window-cap derived state and `PromoCodeRedemption` as none -> applied, with duplicate apply rejected by the unique index and mapped to `already_redeemed`. Locked illegal/terminal outcomes in endpoint tests instead of adding new state fields.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `PromoApiTests` asserts 401, 400, 403, 422 invalid, 422 expired, 409 already, 409 exhausted, 429, 500 server_config, and 200 success mappings; the full backend suite passed 425 tests.
- Limitations: No new persisted states were added; PROMO-03 only exposes and summarizes the existing PROMO-02 lifecycle.

### 2026-06-02 - data-module-review - PROMO-03 promo summary and credit-label invariants

- Agent: Codex
- Trigger: PROMO-03 changes EF-backed account summary reads, active promo credit accounting, and per-source labels.
- Action: Opened and followed the skill; reviewed `AccountService`, `PromoService`, promo entities, `RewriteCredit`, and `AppDbContext` invariants. Ran `scan_data_risks.py --limit 40 backend-dotnet/src/ReplyInMyVoice.Infrastructure`; implemented read-only promo summary derivation from `PromoCodeRedemptions` plus active `RewriteCredit{Source="PROMO"}` rows and mapped the PROMO source label to `Trial rewrites`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Account service/API tests assert `promo.hasRedeemed`, `eligible`, `trialRemaining`, `trialExpiresAt`, and `Trial rewrites`; full `dotnet test` passed 425 tests.
- Limitations: The scan reported broad pre-existing quota/idempotency signals in infrastructure and migrations; no schema changes were made for PROMO-03.

### 2026-06-02 - resilience-test-generation - PROMO-03 HTTP failure matrix

- Agent: Codex
- Trigger: PROMO-03 tests authentication failures, malformed payloads, proxy/Turnstile gate behavior, duplicate redemption, global-cap exhaustion, IP velocity, and server config failure paths.
- Action: Opened and followed the skill; generated a `promo-redeem` resilience matrix and implemented deterministic WebApplicationFactory tests for malformed JSON, missing verified Turnstile token, repeated requests, exhausted cap, five-redemption IP velocity block, missing IP hash salt, and trusted-IP/XFF handling.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-03 test command passed 13 tests; full backend suite passed 425 tests. Changed backend files had no forbidden-substring matches.
- Limitations: The 403 test represents the backend/API mirror rejecting a request without a verified Turnstile token; the future Next.js proxy issue still owns live Cloudflare Turnstile verification and forwarding.

### 2026-06-02 - dotnet-backend-testing - PROMO-03 WebApplicationFactory endpoint coverage

- Agent: Codex
- Trigger: PROMO-03 requires C#/.NET WebApplicationFactory integration tests for every redeem status code path and `/api/me` promo block serialization.
- Action: Opened and followed the skill; wrote failing WebApplicationFactory and service tests first, then implemented the Functions endpoint, ASP.NET Core API mirror, and account summary changes. Reused xUnit, FluentAssertions, EF Core SQLite, and project header-auth test conventions. Added host-builder reload guards to touched API test classes to avoid local host-factory hangs.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused tests failed before implementation because `AccountSummary.Promo` and `/api/promo/redeem` were missing. After implementation, `timeout 180 dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --filter "FullyQualifiedName~PromoApiTests|FullyQualifiedName~AccountSummaryIncludesPromoBlockAndTrialCreditLabel|FullyQualifiedName~Me_includes_promo_block_and_trial_credit_label" --logger "console;verbosity=normal"` passed 13 tests, and full `dotnet test backend-dotnet/ReplyInMyVoice.sln --logger "console;verbosity=normal"` passed 425 tests.
- Limitations: A local git commit was attempted but blocked by sandbox write access to `/Users/qc/Desktop/CloudFlare/.git/worktrees/issue-429/index.lock`; no push or PR was attempted.

### 2026-06-02 - system-spec-synthesis - PROMO-04 admin promo API contract

- Agent: Codex
- Trigger: GitHub issue #430 and `plans/promo-issues/PROMO-04-admin-endpoints.md` require implementation-ready admin API contracts for promo create/list/detail/update/disable/enable.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the issue body, the PROMO-04 brief, and `plans/promo-code-trial-spec.md` section 7. Converted the requirements into implementation checkpoints: reuse `AdminAccess.RequireAdminAsync`, keep backend-only scope, normalize/store promo codes through the existing EF entities, write `AdminAuditLog` for every promo mutation, and expose aggregate stats with IP hash clusters only.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-04 tests passed 5 tests with `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter AdminPromoTests --no-restore`; full `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 430 tests.
- Limitations: This issue implemented backend endpoints only; the admin UI remains owned by PROMO-10.

### 2026-06-02 - state-machine-modeling - PROMO-04 admin active-state transitions

- Agent: Codex
- Trigger: PROMO-04 changes admin-controlled promo-code lifecycle transitions for disable and enable, while list/detail status is derived from active flag, validity window, and global cap.
- Action: Opened and followed the skill; modeled `PromoCode` status as pending/active/expired/exhausted/disabled derived state and implemented disable/enable as explicit `IsActive` transitions with audit rows. Kept redemptions and credits unchanged by admin list/detail/stat reads.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `AdminPromoTests` asserts non-admin rejection, create, duplicate rejection, disable, and stats payload behavior; full backend suite passed 430 tests.
- Limitations: No new persisted enum state was added because the promo spec defines admin status as derived from existing columns.

### 2026-06-02 - data-module-review - PROMO-04 promo admin persistence and audit

- Agent: Codex
- Trigger: PROMO-04 changes EF-backed promo-code administration, validation, audit persistence, and redemption-stat queries.
- Action: Opened and followed the skill; reviewed `PromoCode`, `PromoCodeRedemption`, `RewriteCredit`, `AdminAuditLog`, `AppDbContext`, `PromoService`, and existing admin audit patterns. Implemented service-layer validation mirroring promo CHECK constraints, duplicate-code rejection before save plus unique-index fallback, audit JSON containing `promoCodeId` and changed field names, and stats derived from applied redemptions plus linked credit consumption.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `scan_data_risks.py --limit 40` ran and reported broad pre-existing quota/idempotency signals, with no PROMO-04-specific blocker found. Focused tests and full backend suite passed.
- Limitations: `AdminAuditLog` has no dedicated promo-code foreign-key column, so PROMO-04 records `promoCodeId` inside `DetailsJson` to reuse the existing audit table without a schema change.

### 2026-06-02 - dotnet-backend-testing - PROMO-04 admin endpoint integration tests

- Agent: Codex
- Trigger: PROMO-04 requires C#/.NET integration tests for admin auth, create/audit, duplicate handling, disable persistence, and privacy-safe stats.
- Action: Opened and followed the skill; wrote failing xUnit tests first against `AdminHttpFunctions`, then implemented `PromoAdminService` and the six admin promo routes. Reused existing `DbFixture`, xUnit, FluentAssertions, EF Core SQLite, and direct Functions invocation patterns.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminPromoTests.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/PromoAdminService.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused test run failed before implementation because `CreatePromoCode`, `DisablePromoCode`, `GetPromoCodeDetail`, and response types were missing. After implementation, `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter AdminPromoTests --no-restore` passed 5 tests and `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 430 tests.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because the remote package vulnerability feed was unavailable, but restore/build/test completed with cached packages.

### 2026-06-02 - resilience-test-generation - PROMO-05 parallel redeem and failure matrix

- Agent: Codex
- Trigger: GitHub issue #431 and `plans/promo-issues/PROMO-05-concurrency-security-tests.md` require adversarial promo redemption tests for duplicate requests, parallel global-cap pressure, invalid code states, proxy trust handling, IP velocity, and validity boundaries.
- Action: Opened and followed the skill; identified `PromoService.RedeemAsync` as the critical operation and `PromoCode.RedemptionCount == COUNT(Applied redemptions)` plus one credit per applied redemption as the invariant. Covered duplicate same-user requests, cap=1 parallel users through `Task.WhenAll`, expired/disabled code states, untrusted proxy headers, production fail-closed behavior, same-IP velocity flag/block thresholds, and inclusive `ValidUntil`.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoConcurrencyTests --no-restore` passed 8 tests; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 438 tests.
- Limitations: This issue adds coverage only. No production code, schema, deploy, payment, or frontend files were changed.

### 2026-06-02 - state-machine-modeling - PROMO-05 promo redeem lifecycle checks

- Agent: Codex
- Trigger: PROMO-05 tests promo-code and promo-redemption lifecycle transitions under duplicate, invalid, expired, disabled, capped, and velocity-limited inputs.
- Action: Opened and followed the skill; modeled states as `PromoCode` pending/active/expired/exhausted/disabled derived from `IsActive`, validity window, and cap, and `PromoCodeRedemption` none -> applied with duplicate apply rejected. Events covered redeem request, duplicate redeem request, code disabled, code expired, cap reached, proxy trust missing, IP threshold reached, and boundary-time redeem. Invariants: an applied redemption creates exactly one PROMO credit, duplicate requests do not create a second credit, cap=1 cannot exceed one applied redemption, expired/disabled codes create no credit, and equality with `ValidUntil` remains redeemable.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: The new tests assert illegal transitions return `AlreadyRedeemed`, `InvalidCode`, `Expired`, `CapReached`, `IpVelocityBlocked`, or `ServerConfig` without extra credits; focused and full backend tests passed.
- Limitations: No new persisted state field or transition helper was added because PROMO-05 is a test-lock issue only.

### 2026-06-02 - data-module-review - PROMO-05 promo persistence invariants

- Agent: Codex
- Trigger: PROMO-05 reviews and test-locks EF-backed promo counters, redemptions, unique user/code behavior, IP hashes, and credit grants under concurrency.
- Action: Opened and followed the skill; reviewed `PromoCode`, `PromoCodeRedemption`, `RewriteCredit`, `AppUser`, `AppDbContext`, `PromoService`, `PromoApiTests`, and existing SQLite fixture patterns. Added file-backed SQLite tests for real parallel transactions and asserted persisted final state after each adversarial path.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Assertions cover `RewriteCredits`, applied `PromoCodeRedemptions`, `PromoCodes.RedemptionCount`, nullable `RedeemIpHash` for untrusted proxy input, and no user/credit/redemption rows on production config failure. Focused and full backend suites passed.
- Limitations: No schema or migration change was made; broad app/lib prohibited-term grep still reports pre-existing matches in `lib/rewrite-eval-cases.ts`, which this issue did not touch.

### 2026-06-02 - dotnet-backend-testing - PROMO-05 concurrency and security xUnit coverage

- Agent: Codex
- Trigger: PROMO-05 requires C#/.NET xUnit + SQLite/retrying execution-strategy coverage for backend concurrency and security cases.
- Action: Opened and followed the project skill; added `PromoConcurrencyTests` with xUnit, FluentAssertions, EF Core SQLite, file-backed SQLite for parallel service tests, and WebApplicationFactory for API proxy trust handling. Kept test helpers local to the new class and used generated per-test config values instead of fixed secret-like placeholders.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoConcurrencyTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `dotnet test backend-dotnet/ReplyInMyVoice.sln --filter PromoConcurrencyTests --no-restore` passed 8 tests; `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore` passed 438 tests; `git diff --check` passed; the new backend test file had no prohibited-term matches.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because the remote package vulnerability feed was unavailable, but restore/build/test completed with cached packages.

### 2026-06-02 - system-spec-synthesis - PROMO-06 free baseline consistency checkpoint

- Agent: Codex
- Trigger: GitHub issue #432 and `plans/promo-issues/PROMO-06-free-baseline-zero.md` require an implementation-ready free-baseline cutover plan across `/api/me`, `ReserveAsync`, EF migration, and account erasure.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-06 issue body, the PROMO-06 brief, and `plans/promo-code-trial-spec.md` sections 4 and 16.1. Converted the requirements into implementation checkpoints: free baseline default zero with `FREE_BASELINE_REWRITES` runtime override, preserve paid quota, document that `ReserveAsync` receives and applies the caller quota instead of trusting stale persisted free-period limits, add a forward-only data migration for `free:lifetime`, and extend account erasure to clear promo redemption IP hashes while retaining rows.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`; `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602091811_FreeBaselineZero.cs`; backend acceptance tests; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-06 test command passed 46 tests; full `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 445 tests after the generated migration pair was finalized.
- Limitations: PR description authoring is handled by the supervisor; the required finding is recorded in this log and final report for the supervisor to carry into the PR.

### 2026-06-02 - state-machine-modeling - PROMO-06 quota and promo-credit states

- Agent: Codex
- Trigger: PROMO-06 changes free quota, promo-credit availability, exhausted/paywall behavior, and account erasure privacy state.
- Action: Opened and followed the skill; modeled account quota states as free-baseline zero, promo credit available, promo credit exhausted, and paid unchanged. Events covered account summary read, rewrite reserve request, promo-credit consumption, old free-period row after migration, and account erase. Invariants: new inactive users have zero remaining without credit, stale free-period rows do not add remaining quota, reserve without credit creates no attempt/reservation/outbox, active PROMO credits are the only trial remaining source, exhausted PROMO credits set `Usage.Exhausted`, and erasure retains redemption records while nulling IP hash data.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Tests assert the allowed and rejected state transitions above; focused tests passed 46/46 and full backend tests passed 445/445.
- Limitations: No new persisted state enum was added; the state remains derived from existing usage-period counters, active credits, and promo redemption rows.

### 2026-06-02 - data-module-review - PROMO-06 UsagePeriods migration and promo erasure

- Agent: Codex
- Trigger: PROMO-06 changes EF migration data, usage counter invariants, and `PromoCodeRedemption` privacy handling during account deletion.
- Action: Opened and followed the skill; reviewed `UsagePeriod`, `RewriteCredit`, `UsageReservation`, `PromoCodeRedemption`, `AppDbContext`, `AccountService`, and `QuotaService` together. Added a generated EF migration pair with SQL-only `Up` that updates `UsagePeriods` rows where `PeriodKey='free:lifetime'` to `QuotaLimit=0`, refreshes `UpdatedAt`, and changes `RowVersion`; `Down` is empty for forward-only behavior. Ran `scan_data_risks.py --limit 40 backend-dotnet`.
- Output artifacts: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602091811_FreeBaselineZero.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/20260602091811_FreeBaselineZero.Designer.cs`; `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FreeBaselineMigrationTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: `FreeBaselineMigrationTests` inspects migration operations for the free-row update and empty `Down`; full backend tests passed 445/445. Touched-file prohibited-term scan returned no matches.
- Limitations: The first scan invocation used multiple roots and returned a usage error; it was rerun successfully with the single `backend-dotnet` root. The scan reports broad pre-existing quota/idempotency signal rows by design.

### 2026-06-02 - resilience-test-generation - PROMO-06 no-credit and stale-row failure matrix

- Agent: Codex
- Trigger: PROMO-06 tests quota reservation behavior around stale free-period rows, no-credit requests, promo-credit availability, and exhausted promo credits.
- Action: Opened and followed the skill; treated `QuotaService.ReserveAsync` plus `/api/rewrite` as the critical operation and `no successful rewrite attempt without period quota or active credit` as the invariant. Added deterministic tests for no-credit rewrite rejection, old free period with two used rewrites rejecting the next request, promo credit preserving success paths, and exhausted promo credit setting account exhaustion.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Focused PROMO-06 tests passed 46/46; full backend tests passed 445/445.
- Limitations: No live provider, payment, queue, or cloud dependency was called; tests use EF SQLite and local WebApplicationFactory paths.

### 2026-06-02 - dotnet-backend-testing - PROMO-06 backend acceptance coverage

- Agent: Codex
- Trigger: PROMO-06 requires C#/.NET tests for `/api/me`, `ReserveAsync`, EF migration behavior, promo-credit account summary, promo exhaustion, and account erasure.
- Action: Opened and followed the skill; wrote failing xUnit/WebApplicationFactory/EF SQLite tests before production changes, then implemented the account service, rewrite caller wiring, EF migration, and adjusted legacy credit-total expectations. Reused xUnit, FluentAssertions, EF Core SQLite, WebApplicationFactory, and existing local fixture patterns.
- Output artifacts: `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AccountApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/QuotaServiceTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/RewriteApiTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/FreeBaselineMigrationTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/AdminCreditAdjustTests.cs`; `backend-dotnet/tests/ReplyInMyVoice.Tests/PromoApiTests.cs`; `docs/skill-run-log.md`.
- Verification evidence: Initial focused test run failed before implementation because `GetUsagePlan` lacked a configuration overload; after implementation, focused tests passed 46/46 and full `dotnet test backend-dotnet/ReplyInMyVoice.sln` passed 445/445.
- Limitations: NuGet vulnerability feed checks emitted `NU1900` warnings because the remote package vulnerability feed was unavailable, but restore/build/test completed with cached packages.

### 2026-06-02 - system-spec-synthesis - PROMO-07 redeem proxy contract

- Agent: Codex
- Trigger: GitHub issue #433 and `plans/promo-issues/PROMO-07-redeem-proxy.md` define a new Next.js BFF API route contract for promo redemption, Turnstile verification, trusted Cloudflare IP forwarding, and runtime secret validation.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-07 issue body, the PROMO-07 brief, and `plans/promo-code-trial-spec.md` sections 8.1, 8.3, and 13. Converted the requirements into checkpoints: same-origin POST, `{code, turnstileToken}` parsing, Turnstile `siteverify` with `cf-connecting-ip`, runtime `TURNSTILE_SECRET_KEY` and `PROMO_PROXY_SHARED_SECRET` checks, Azure bearer forwarding, and response pass-through.
- Output artifacts: `app/api/promo/redeem/route.ts`; `lib/turnstile.ts`; `lib/auth-rate-limit.ts`; `tests/unit/promo-redeem-route.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused red test first failed because `app/api/promo/redeem/route.ts` did not exist; after implementation, `npm run test -- tests/unit/promo-redeem-route.test.ts` passed 7/7, `npm run typecheck` passed, and full `npm run test` passed 302/302.
- Limitations: The optional promo status proxy was not implemented because PROMO-07 marks it optional and acceptance is fully covered by the redeem route. No UI, backend, payment, deployment, or secret file was changed.

### 2026-06-02 - resilience-test-generation - PROMO-07 Turnstile and fail-closed proxy tests

- Agent: Codex
- Trigger: PROMO-07 changes a provider-dependent route and requires tests for blocked/missing Turnstile tokens, production missing secrets, and trusted IP/secret forwarding.
- Action: Opened and followed the skill; treated `/api/promo/redeem` as the critical operation and `no Azure redeem call unless same-origin, configured, captcha-verified, and authenticated` as the invariant. Added deterministic local fetch fakes for Turnstile success and rejection, asserted missing token rejection, asserted production missing secret errors happen before provider calls, and asserted forwarded `X-Client-IP` comes from `cf-connecting-ip` even when `x-forwarded-for` is present.
- Output artifacts: `tests/unit/promo-redeem-route.test.ts`; `app/api/promo/redeem/route.ts`; `lib/turnstile.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused route tests passed 7/7 and full Vitest passed 302/302 with no live Turnstile, Azure, database, or payment dependency calls.
- Limitations: No retry policy was added; provider failure is fail-closed as `invalid_captcha`, matching the issue's proxy gate behavior.

### 2026-06-02 - state-machine-modeling - PROMO-08 redeem UI quota states

- Agent: Codex
- Trigger: PROMO-08 changes `/app` branching for no-redemption, active trial-credit, exhausted trial, and paid quota states.
- Action: Opened and followed the skill; modeled the UI state entity as the `/api/me` account summary. States: new signed-in account with zero remaining and no promo redemption; active promo trial credit; redeemed and exhausted promo trial; paid account with current quota; paid account with exhausted monthly quota. Events: account summary read, successful redeem, redeem error, trial credit consumption, paid quota exhaustion. Allowed transitions: new zero-quota account -> redeem card; redeem success -> workspace; trial credit remaining -> workspace; redeemed zero remaining -> buy paywall; paid exhausted -> billing-management paywall. Illegal transitions: new zero-quota account -> buy paywall; paid exhausted account -> forced redeem card; error response -> workspace. Invariants: trial display uses 3 as the grant size, PROMO source is labeled `Trial rewrites`, and the universal code value is never rendered.
- Output artifacts: `lib/promo-app-state.ts`; `tests/unit/promo-app-state.test.ts`; `app/app/page.tsx`; `docs/skill-run-log.md`.
- Verification evidence: `npm run test -- tests/unit/promo-app-state.test.ts tests/unit/workspace-copy.test.ts` passed 13/13; `npm run typecheck` passed; `npm run test` passed 309/309; `npm run build` passed and listed `/app`.
- Limitations: No backend persistence state was changed; the state remains derived from the existing account summary and promo block.

### 2026-06-02 - ui-browser-testing - PROMO-08 redeem card and responsive flow

- Agent: Codex
- Trigger: PROMO-08 adds a browser-visible redeem form, Turnstile widget, inline error states, success refresh, `/app` empty-state branching, and mobile overflow acceptance.
- Action: Opened and followed the skill; identified the user-visible flow as signed-in `/app` -> redeem card -> inline error or redeem success -> `/api/me` refetch -> workspace trial quota line. Added a focused Playwright spec with a local Azure account-summary mock, signed test session cookies, browser-level redeem response stubs, and a Turnstile stub covering new user, success, inline errors, exhausted trial, and mobile overflow.
- Output artifacts: `components/app/redeem-code-card.tsx`; `app/app/page.tsx`; `tests/e2e/promo-redeem-ui.spec.ts`; `playwright.config.ts`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run typecheck` passed; `npm run test` passed 309/309; `npm run lint` passed; `npm run build` passed. Focused Playwright attempts reached the browser launch phase after production build/start, but local Chromium launch failed before any page loaded because this macOS sandbox denies the browser process Mach-port registration.
- Limitations: Browser assertions are committed and ready for the supervisor/CI environment, but local Playwright execution could not complete in this sandbox. A full guard scan still reports pre-existing copy-guard strings in `lib/rewrite-eval-cases.ts`; the diff-only scan for this issue returned no matches.

### 2026-06-02 - system-spec-synthesis - PROMO-09 signup verification contract

- Agent: Codex
- Trigger: PROMO-09 changes the Entra-native signup API contract and browser-visible signup form by requiring Turnstile verification and listed email-domain rejection before account creation.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-09 issue body, `plans/promo-issues/PROMO-09-signup-hardening.md`, and `plans/promo-code-trial-spec.md` sections 7.3 and 8.3. Converted the requirements into checkpoints: server-side signup-start gate, runtime Turnstile verification, bundled listed-domain checker, signup widget using `NEXT_PUBLIC_TURNSTILE_SITE_KEY` with dev test fallback, and scoped unit/E2E coverage without changing Google sign-in.
- Output artifacts: `app/api/auth/signup/start/route.ts`; `components/auth/google-oauth-card.tsx`; `components/auth/auth-panels.module.css`; `lib/disposable-email-domains.ts`; `lib/disposable-email-domains.json`; `tests/unit/auth-signup-routes.test.ts`; `tests/e2e/auth-gate.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused signup route test failed before implementation for the new gates, then `npm run test -- tests/unit/auth-signup-routes.test.ts` passed 10/10. `npm run typecheck`, `npm run test`, and `npm run build` passed after implementation.
- Limitations: Local commit creation failed because Git worktree metadata is outside the writable sandbox. No push, PR, deploy, payment, or secret-file changes were attempted.

### 2026-06-02 - resilience-test-generation - PROMO-09 Turnstile failure gates

- Agent: Codex
- Trigger: PROMO-09 adds provider-dependent signup protection and fail-closed behavior for missing, rejected, or unavailable Turnstile verification.
- Action: Opened and followed the skill; treated `/api/auth/signup/start` as the critical operation and `no Entra signup call unless email/password are valid, domain is allowed, and Turnstile verifies` as the invariant. Added deterministic local mocks for Turnstile success, failed token verification, and missing runtime config.
- Output artifacts: `tests/unit/auth-signup-routes.test.ts`; `app/api/auth/signup/start/route.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run test -- tests/unit/auth-signup-routes.test.ts` passed 10/10 and asserts that Entra `signupStart` and `signupChallenge` are not called for listed domains, missing tokens, failed verification, or missing verification config.
- Limitations: No live Cloudflare, Entra, database, or payment dependency was called; provider behavior is covered with local fakes. No secrets or token values were logged.

### 2026-06-02 - ui-browser-testing - PROMO-09 signup form verification

- Agent: Codex
- Trigger: PROMO-09 adds a Turnstile widget and browser-visible signup errors for blank verification and listed email domains.
- Action: Opened and followed the skill; added focused Playwright coverage to `tests/e2e/auth-gate.spec.ts` with an in-page Turnstile stub, blank-token disabled-submit check, listed-domain guidance check, and normal signup-start success flow to the email-code step.
- Output artifacts: `components/auth/google-oauth-card.tsx`; `components/auth/auth-panels.module.css`; `tests/e2e/auth-gate.spec.ts`; `docs/skill-run-log.md`.
- Verification evidence: `npm run build` passed. `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/rimv-promo-09-ms-playwright npx playwright test tests/e2e/auth-gate.spec.ts` and `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/rimv-promo-09-ms-playwright npm run test:e2e` reached the browser launch phase but could not run page assertions because this macOS sandbox denies Chromium Mach-port registration.
- Limitations: The Playwright assertions are present for CI/supervisor verification, but local browser execution is environment-blocked. Changed-file guard scan returned no matches; a full repo scan still reports pre-existing copy-guard strings in an unrelated rewrite eval fixture.

### 2026-06-02 - system-spec-synthesis - PROMO-10 admin promo-code console contract

- Agent: Codex
- Trigger: PROMO-10 adds the first `/admin` page plus a thin Next.js admin proxy for the existing C# promo-code admin endpoints.
- Action: Opened and followed the skill; read `AGENTS.md`, `CLAUDE.md`, the PROMO-10 issue body, `plans/promo-issues/PROMO-10-admin-ui.md`, and `plans/promo-code-trial-spec.md` section 7. Converted the requirements into checkpoints: server-side auth/admin gating, same-origin admin mutations, bearer forwarding, create/list/detail/disable/enable proxy routes, duplicate-code field errors, derived list status, and stats sanitization to hash clusters only.
- Output artifacts: `app/admin/promo-codes/page.tsx`; `app/admin/promo-codes/loading.tsx`; `app/api/admin/promo-codes/**/route.ts`; `components/admin/promo-codes-admin.tsx`; `lib/admin-promo-codes.ts`; `lib/admin-promo-proxy.ts`; `tests/unit/admin-promo-codes.test.ts`; `tests/unit/admin-promo-proxy-route.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused tests first failed because the admin helper and proxy routes did not exist; after implementation, `npm test -- tests/unit/admin-promo-codes.test.ts tests/unit/admin-promo-proxy-route.test.ts` passed 12/12. `npm run test`, `npm run typecheck`, `npm run lint`, and `npm run build` passed.
- Limitations: No backend persistence, payment, secrets, deployment, or production branch wiring was changed.

### 2026-06-02 - ui-browser-testing - PROMO-10 admin page browser coverage

- Agent: Codex
- Trigger: PROMO-10 requires browser-visible admin states, desktop/mobile screenshots, duplicate field errors, immediate disable state, and no browser-visible raw IP values.
- Action: Opened and followed the skill; added a focused Playwright spec with a local Azure admin mock, signed test-session cookies, signed-out redirect coverage, non-admin 403 view, admin create/duplicate/stats/disable coverage, mobile and desktop screenshot capture calls, and a direct mock redeem check after disable. Also adjusted Playwright to use one worker so the fixed mock backend port is not raced across files.
- Output artifacts: `tests/e2e/admin-promo-codes.spec.ts`; `playwright.config.ts`; `components/admin/promo-codes-admin.tsx`; `docs/skill-run-log.md`.
- Verification evidence: `npm run test` passed 325/325; `npm run typecheck` passed; `npm run lint` passed; `npm run build` passed. Focused Playwright attempts with an installed temporary Chromium cache reached browser launch after production build/start, but local Chromium failed before page load because this macOS sandbox denies Chromium Mach-port registration.
- Limitations: Playwright assertions and screenshot capture are committed for the supervisor/CI environment, but local browser execution could not complete in this sandbox. Changed-file guard scan returned no matches; a full repo scan still reports pre-existing copy-guard strings in `lib/rewrite-eval-cases.ts`.

### 2026-06-02 - ui-browser-testing - PROMO-11 trial-code copy verification

- Agent: Codex
- Trigger: PROMO-11 changes browser-visible landing, pricing, developers, auth, footer, and workspace quota copy from baseline access language to trial-code redemption language.
- Action: Opened and followed the skill; identified the user-visible flows as `/`, `/pricing`, `/developers`, `/sign-up`, and `/app` quota/nudge copy. Updated source-string contract tests first, observed the focused tests fail on the old copy, then updated the copy surfaces and reran the focused tests green. Started the local Next dev server and checked rendered HTTP responses for the updated copy on the public routes.
- Output artifacts: `components/landing/hero.tsx`; `components/landing/closing-cta.tsx`; `components/landing/pricing-v2.tsx`; `app/pricing/page.tsx`; `components/site-footer.tsx`; `app/developers/page.tsx`; `components/auth/google-oauth-card.tsx`; `app/app/page.tsx`; `components/app/rewrite-workspace.tsx`; `lib/rewrite-eval-cases.ts`; `tests/unit/pricing-auth-visual-system.test.ts`; `tests/unit/workspace-copy.test.ts`; `docs/skill-run-log.md`.
- Verification evidence: Focused copy tests failed before implementation and then passed 11/11. Final `npm run test` passed 326/326; `npm run typecheck` passed; required restricted-term grep returned no matches; stale old-copy phrase scan returned no matches. Dev server returned HTTP 200 for `/`, `/pricing`, `/developers`, and `/sign-up`, and the rendered responses contained the expected trial-code copy.
- Limitations: Local Chromium launch through Playwright failed before page load because this macOS sandbox denied Chromium Mach-port registration, so screenshot-level browser verification could not be completed here. The dev server also reported watcher `EMFILE` warnings in the sandbox, but the checked routes compiled and returned 200.
