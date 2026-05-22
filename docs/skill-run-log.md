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
