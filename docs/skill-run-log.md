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
- Verification evidence: Official Sapling docs confirmed sentence/token-level detector outputs and false-positive/false-negative cautions; project docs confirmed Sapling unavailability is a quality-failure/no-charge condition in the fact-reconstruct route.
- Limitations: No live provider failure test, rate-limit simulation, or alternate detector integration was run.

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
