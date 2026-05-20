# AGENTS.md instructions for /Users/qc/Desktop/CloudFlare

## Project

Project name: **Reply In My Voice**

Domain: `replyinmyvoice.com`

## Codex And Claude Code Skill Policy

This project uses real, reusable agent skills stored in:

```text
/Users/qc/Desktop/CloudFlare/agent-skills
/Users/qc/.codex/skills
/Users/qc/.claude/skills
```

Use the matching skill before coding, planning, or reviewing when a task matches one of these triggers. If the current agent session has not indexed a newly created skill yet, read the project source skill at `agent-skills/<skill-name>/SKILL.md` and follow it as the fallback.

### Required Skill Triggers

Use `system-spec-synthesis` when:

```text
The task asks for a system spec, architecture summary, implementation-ready requirements, API/data/job contract design, Azure backend planning, or converting loose product notes into an executable engineering plan.
```

Use `resilience-test-generation` when:

```text
The task changes or tests retries, timeouts, rate limits, provider failures, Stripe webhook replay, Azure Service Bus redelivery, OpenAI/Sapling failures, quota races, idempotency, or recovery behavior.
```

Use `state-machine-modeling` when:

```text
The task changes or reviews subscription states, free/paid quota states, usage reservations, rewrite attempts, queue jobs, webhook lifecycle, deployment lifecycle, or any multi-step workflow with statuses and transitions.
```

Use `data-module-review` when:

```text
The task changes or reviews Prisma schema, Entity Framework models, migrations, data access services, usage counters, idempotency tables, transactions, indexes, or persistence invariants.
```

Use `claude-heavy-planning-handoff` when:

```text
The task is broad enough to span more than three modules or services, requires architecture planning before implementation, changes Azure/backend/auth/billing/queue architecture, or should be routed from Codex to Claude Code for heavy planning.
```

### Routine Skill Routing Rules

Use these project-level aliases when discussing the Agent Studio workflow:

```text
requirements-to-system-plan = system-spec-synthesis
resilience-test-writer = resilience-test-generation
state-machine-and-data-model-review = state-machine-modeling + data-module-review
cloud-readiness-review = deployment readiness checklist until a dedicated skill exists
```

For any non-trivial development run, follow this routine:

```text
1. Requirements and system planning:
   Use system-spec-synthesis before implementation when the task changes architecture, API contracts, data models, deployment flow, or multi-module behavior.

2. State and data correctness:
   Use state-machine-modeling and data-module-review before changing quota, subscription, webhook, usage reservation, rewrite attempt, queue job, EF Core/Prisma schema, migrations, or persistence invariants.

3. Resilience and failure tests:
   Use resilience-test-generation before or during implementation whenever the change touches retries, timeouts, provider failures, idempotency, webhook replay, queue redelivery, quota races, concurrent requests, or recovery behavior.

4. Cloud/deployment readiness:
   Before CI/CD, Azure, Cloudflare, database migration, or production-readiness changes, run a cloud-readiness review using README.md, docs/manual-setup.md, docs/dotnet-azure-blocker-preflight.md, docs/business-qa-and-deploy-result.md, and the relevant skills above. Do not claim a dedicated cloud-readiness-review skill was used unless that skill is created and followed later.

5. Claude Code handoff:
   Use claude-heavy-planning-handoff only for broad planning or architecture-heavy work that should be routed from Codex to Claude Code. Codex remains responsible for local implementation, tests, commits, CI/CD setup, and deployment unless the user says otherwise.

6. Evidence rule:
   In final answers, run docs, resume bullets, and interview notes, only claim a named skill was used if the agent explicitly opened/followed that skill or produced its required output. If the agent only followed the same idea manually, describe it as a checklist or workflow, not as a used skill.

7. Learning rule:
   When a reusable lesson is found during rewrite quality, quota correctness, webhook replay, deployment, or resilience work, update the relevant project docs before finishing, especially docs/rewrite-strategy-memory.md, docs/business-qa-and-deploy-result.md, or the active run target document.
```

### Claude Code CLI Automation

Codex can call the local Claude Code CLI for architecture-heavy planning when the user explicitly asks for Claude involvement or when `claude-heavy-planning-handoff` applies.

Verified local capability:

```text
Date checked: 2026-05-19
Claude Code CLI: 2.1.143
Non-interactive command mode: claude -p / --print works
Smoke result: Codex launched Claude Code and received a JSON result successfully
```

Default automation pattern:

```text
1. Codex reads project context and prepares a sanitized handoff prompt.
2. Codex must not include secret values, .env contents, API tokens, private keys, or raw credential notes in the prompt.
3. For first-pass planning, prefer disabling Claude tools and passing summarized context:
   claude -p --tools "" --permission-mode dontAsk --no-session-persistence --max-budget-usd 0.20 --output-format json "<sanitized planning prompt>"
4. Save any material Claude planning output under docs/claude-planning-result-<topic>.md or another clearly named docs file.
5. Codex reviews the result, maps it back to repo constraints, and remains responsible for implementation, tests, commits, CI/CD, and deployment.
```

Safety rules:

```text
Do not use Claude CLI automation for small edits that Codex can finish directly.
Do not use --dangerously-skip-permissions for this project.
Do not let Claude read or print .env.local, .dev.vars, globalapikey/, Azure credentials, Stripe secrets, Clerk secrets, OpenAI keys, Sapling keys, or GitHub secrets.
Use --max-budget-usd for non-interactive Claude calls.
If Claude CLI auth fails, budget is exceeded, or non-interactive mode fails, fall back to writing a handoff document only.
Only claim Claude Code participated if a Claude CLI call was actually run or a Claude handoff/result document was produced.
```

### Skill Smoke Verification

These five skills are expected to be used during real development, but not every small edit needs all five. Full backend, quota, billing, queue, deployment, or resilience runs should normally use the relevant planning, state/data, resilience, and cloud-readiness routines; Claude Code involvement is reserved for broad planning or explicit user requests.

Latest local smoke result:

```text
Date checked: 2026-05-19

system-spec-synthesis:
  Passed. scripts/spec_outline.py produced the required implementation-spec headings.

resilience-test-generation:
  Passed. scripts/resilience_matrix.py produced timeout, retry, duplicate, partial-success, concurrency, and malformed-payload failure rows.

state-machine-modeling:
  Passed. scripts/state_machine_template.py produced states, events, transitions, invariants, and illegal-transition sections.

data-module-review:
  Passed after optimization. scripts/scan_data_risks.py now excludes generated/build/vendor output by default and supports --limit / --include-generated.

claude-heavy-planning-handoff:
  Passed. scripts/build_handoff_brief.py produced a sanitized handoff brief, and Codex successfully called local Claude Code non-interactively with claude -p.
```

### Interview Demo Prompts

Use these exact prompts to demonstrate the skills without touching production systems:

```text
Use system-spec-synthesis to turn the current Azure backend migration notes into an implementation-ready system spec.
Use resilience-test-generation to design tests for the rewrite request flow when OpenAI fails after quota reservation.
Use state-machine-modeling to model subscription status, free quota, paid quota, and rewrite reservation lifecycle.
Use data-module-review to review the quota, usage reservation, and Stripe event persistence model in this repo.
Use claude-heavy-planning-handoff to prepare a Claude Code planning brief for migrating the rewrite backend to Azure App Service, Azure Service Bus, and a .NET worker.
```

## Current Rewrite-Quality Long-Run Target

The next autonomous rewrite-quality/product run must use this target document as the source of truth:

```text
/Users/qc/Desktop/CloudFlare/docs/unified-fact-rewrite-long-run-target.md
```

It is backed by the detailed implementation plan:

```text
/Users/qc/Desktop/CloudFlare/docs/superpowers/plans/2026-05-19-unified-fact-preserving-rewrite.md
```

When the user explicitly starts this long autonomous run, prioritize the current rewrite-quality target over the older .NET/Azure backend target. The run goal is to remove user-facing scenario selection, reduce visible tones to Warm/Direct, make draft-only usage first-class, implement unified fact extraction and fact gates, evaluate at least 60 known samples with at least 40 draft-only cases, fix all known failures, update learning docs, push, deploy to Cloudflare, verify active backend workflows, and run remote smoke tests.

Do not deploy as final while any known evaluation sample fails. Ordinary bad rewrite outputs, low evaluation pass rate, model failures, provider errors, build/test failures, and deployment command errors are not stop conditions; investigate, fix, document learning, rerun evaluation, and continue until the target document's completion criteria or stop conditions are reached.

Latest unified rewrite-quality result before the v2 strategy change:

```text
Date: 2026-05-19
Evaluation cases: 66
Draft-only cases: 44
Customer-usable pass count: 66/66
Fact preservation or unsupported-addition failures: 0
Final selected rewrites worse than draft: 0/66
Strict signal pass count: 42/66
```

Latest rewrite strategy decision on 2026-05-19:

```text
Implement fact_reconstruct as the default production rewrite workflow:
extract facts -> classify scenario -> load style card -> generate 3 candidates -> review -> finalize -> deterministic and LLM fact gates -> Sapling Naturalness Check gate -> one bounded strong-model escalation -> quality failure with no charge if still below the quality bar.
```

For future rewrite work, Sapling is a final reference gate, not prompt input and not a model optimization target. A user-visible successful rewrite must pass fact gates and the Naturalness Check rule: if the draft is above `NATURALNESS_THRESHOLD` (default 40%), the rewrite must be at or below that threshold; if the draft is already at or below the threshold, the rewrite must not raise the signal. If Sapling is unavailable in the fact-reconstruct production route, return a quality-failure response and do not charge usage.

Latest rewrite strategy update on 2026-05-20:

```text
The rewrite engine must be treated as a bounded adaptive rewrite-agent orchestrator, not a fixed prompt chain. Long support, policy, refund, cancellation, transfer, options, and eligibility-review replies must be rebuilt into send-ready email structure from extracted facts. Do not accept a candidate just because it preserves facts and lowers the Naturalness Check signal if it is only the original text split into many one-sentence paragraphs, contains broken numbered lists, breaks quoted-summary boundaries, or reads like a mechanical support macro.
```

Required adaptive strategy:

```text
normalize input -> Input Analyzer -> extract facts with critical/supporting/optional importance -> Style / Intent Card -> Initial Strategy Router -> Budget Manager -> generate candidates -> structured reviewer -> fact gates -> structural send-ready gates -> Policy / Intent Gate -> Naturalness Check gate -> Rewrite Quality Strategist Agent -> selected strategy retry -> final gates -> success or quality failure/no charge.
```

For long support-policy messages, the generation strategy must group related facts into natural paragraphs:

```text
1. acknowledge the concrete situation briefly,
2. explain current status,
3. explain available options or policy constraints,
4. ask for the user's preferred next step or confirmation,
5. preserve any no-change-without-confirmation constraint.
```

Do not rely on sentence-level repair when the problem is structural. If a candidate has detached numbered-list markers, sentence-per-paragraph formatting, broken quote boundaries, or weak line-split paraphrasing, route it to a full restructure pass from facts instead of repairing one or two sentences.

The rewrite pipeline must include a bounded internal `Rewrite Quality Strategist Agent`. It diagnoses failed candidates using failure kinds such as `fact_loss`, `unsupported_fact`, `broken_numbered_list`, `broken_quote_boundary`, `sentence_per_paragraph`, `line_split_paraphrase`, `support_macro_voice`, `messy_thread_leak`, `quote_or_list_risk`, `signal_not_improved`, and `low_signal_got_worse`, then chooses one allowed next strategy: targeted sentence repair, full structure rewrite, facts-first reconstruct, support-policy/options rewrite, quote/list-safe rewrite, messy-thread cleanup rewrite, strong-model restructure, or quality failure. This agent is the automation layer for eval-driven improvement: during long runs, failed eval cases should produce diagnosis tags, strategy decisions, regression tests, prompt/style-card/routing updates, and strategy-memory notes without waiting for manual website testing from the user.

The Strategy Router must run twice: once before first generation using `InputAnalysis`, and again after failures using gate/reviewer evidence. The runtime loop must include a Budget Manager; adaptive does not mean unlimited retries. For long-run evaluation, expand to a 60-case suite but use staged modes (`smoke`, `focused`, `full`) and record OpenAI/Sapling call counts and estimated cost. The full 60-case suite should run before push/deploy or after major strategy changes, not after every tiny prompt edit.

The next implementation plan for this is:

```text
/Users/qc/Desktop/CloudFlare/docs/superpowers/plans/2026-05-20-adaptive-rewrite-agent-orchestrator.md
```

Current long-run implementation target:

```text
/Users/qc/Desktop/CloudFlare/docs/fact-reconstruct-rewrite-target.md
```

## Previous .NET/Azure Long-Run Target

The next autonomous C#/.NET Azure backend run must use this target document as the source of truth:

```text
/Users/qc/Desktop/CloudFlare/docs/dotnet-azure-full-run-target.md
```

Before starting that long run, also read the latest blocker preflight:

```text
/Users/qc/Desktop/CloudFlare/docs/dotnet-azure-blocker-preflight.md
```

When the user explicitly starts the long autonomous run, do not treat the work as a staged Phase 1 / Phase 2 effort. The AutoRun target is the full end-to-end backend goal in that document: ASP.NET Core API, EF Core/Azure SQL persistence, quota reservation/idempotency correctness, Stripe webhook idempotency and entitlement sync, Azure Service Bus queued rewrite processing, worker deployment, App Service deployment, Application Insights, CI/CD preparation, tests, migrations, and remote smoke verification.

Continue autonomously until the target is complete or a stop condition in `docs/dotnet-azure-full-run-target.md` is reached. Ordinary build errors, test failures, package issues, Azure CLI syntax issues, migration errors, App Service deployment errors, Service Bus setup errors, and worker packaging problems are not stop conditions; investigate, fix, and continue without asking the user.

Core positioning:

> Replies that still sound like you.

Sub-positioning:

> A writing assistant for everyday communication: teacher messages, sales follow-ups, workplace email, and drafts that need your tone.

This is an AI-assisted writing workflow product. It helps users turn rough, generic, or AI-assisted drafts into replies that preserve facts and sound closer to how the user would personally write.

It must not be positioned as a detector-bypass, evasion, or gray-market humanizer product.

## Product Background

This product exists because many real professionals already use AI to draft replies, but raw AI output often sounds stiff, generic, or unlike the sender.

Primary user story:

```text
A teacher needs to reply to students by email.
The teacher may use AI to draft the response.
The raw AI draft may feel artificial, too polished, or unlike the teacher's normal voice.
Students may react badly if the reply feels impersonal.
The product helps the teacher turn the draft into a clearer, warmer, more personal reply while preserving the facts.
```

Second user story:

```text
A salesperson needs to reply to customers, leads, or clients.
The salesperson may use AI to organize context or draft a reply.
The raw AI draft may sound mechanical, corporate, or detached.
The product helps make the reply more natural and relationship-aware without inventing promises, discounts, timelines, meetings, names, or outcomes.
```

Strategic framing:

```text
The product is for real communication replies: teacher messages, sales follow-ups, workplace email, and customer/client responses.
It is not for student essay rewriting, academic misconduct, detector evasion, or hidden bypass workflows.
The core workflow is draft + context -> reply in my voice.
The product should emphasize personal tone, context, fact preservation, and practical send-ready replies.
```

## Confirmed Product Decisions: Usage Limits And Third-Party Writing Signal

These decisions were confirmed before implementation and should be treated as implementation requirements.

### Input Workflow Direction

The app should support a practical email/message reply workflow:

```text
User pastes the email/message they need to reply to.
User may also paste a rough AI-assisted draft, or write a rough draft themselves.
User adds context about what actually happened and facts that must be preserved.
The app rewrites the response so it feels friendlier, more natural, and closer to the user's own voice.
```

Confirmed MVP decision:

```text
Use two main textareas:
1. Message to reply to
2. Rough draft reply

Keep the existing context fields:
- Audience
- Purpose
- What actually happened
- Facts to preserve
```

### Character Limits

The user wants a clear character limit for pasted content.

Confirmed limits:

```text
Message to reply to: max 5000 characters
Rough draft reply: 10 to 5000 characters
Audience: max 300 characters
Purpose: max 500 characters
What actually happened: max 1000 characters
Facts to preserve: max 1000 characters
Combined request cap: 10000 characters
```

Reasoning:

```text
Teachers and sales users may paste a full incoming email.
The app should prevent very large email threads from driving up cost and latency.
The UI should show remaining characters near long textareas.
```

### Free And Paid Usage Limits

The user wants usage limits.

Candidate plan:

```text
Free users: 3 rewrites
Paid users: NZD $9/month in the current sandbox/MVP configuration
Paid quota: 100 rewrites/month
```

Confirmed MVP decision:

```text
Require sign-up before free rewrites.
Give every signed-in user 3 free lifetime successful rewrites.
Paid users get 100 successful rewrites per billing month.
One user click/request on Rewrite counts as one usage attempt, even if the server tries multiple bounded internal rewrite strategies for quality.
Do not count validation errors, auth failures, payment failures, or provider/server errors.
When free users exhaust 3 lifetime attempts, show a hard paywall.
When paid users exhaust 100 monthly attempts, show a hard quota/paywall state.
Show simple remaining-usage text in /app.
```

Database implication:

```text
Add usage tracking beyond the current User model.
Either add monthly counters to User for MVP simplicity, or create a RewriteUsage table for cleaner billing-period resets.
Prefer a RewriteUsage table if time allows.
```

Possible MVP model:

```prisma
model RewriteUsage {
  id          String   @id @default(cuid())
  userId      String
  periodKey   String
  count       Int      @default(0)
  createdAt   DateTime @default(now())
  updatedAt   DateTime @updatedAt

  user        User     @relation(fields: [userId], references: [id], onDelete: Cascade)

  @@unique([userId, periodKey])
}
```

### Third-Party Writing Signal

The user wants to connect **Sapling** as the third-party writing signal provider to measure how AI-like or natural the text appears before and after rewriting. Confirm the exact Sapling API docs and response schema before implementation.

Important framing:

```text
This signal is useful as a reference for naturalness and writing quality.
It must not become the sole success metric.
It must not be marketed as a detector bypass feature.
It must not claim guarantees.
It should be combined with fact preservation, tone quality, and user review.
```

Recommended internal terminology:

```text
third-party writing signal
AI-likeness reference signal
naturalness reference
before/after writing signal
```

Avoid user-facing language that suggests:

```text
guaranteed human writing
detector evasion
undetectable output
score hacking
```

Potential implementation:

```text
1. Run third-party signal on the rough draft before rewrite.
2. Generate one rewrite candidate using the user's context.
3. Run third-party signal on the rewritten candidate.
4. If the signal remains too high, gets worse, or the rewrite is too generic, automatically try another bounded internal strategy for the same user request.
5. Select based on a composite score:
   - facts preserved
   - tone matches Warm or Direct
   - reply is concise and send-ready
   - third-party signal materially improves when possible
   - no invented promises, timelines, discounts, names, policies, or outcomes
```

Recommended cap:

```text
Start with a bounded set of internal rewrite strategies per user request in MVP.
The production MVP cap is up to 3 initial strategies plus up to 2 targeted repairs per user request, charged as 1 user usage attempt.
Development/evaluation scripts may try more strategies per sample while searching for better prompts and scoring.
Do not create an unbounded loop chasing a third-party score.
```

Core optimization goal:

```text
Lowering the AI-like signal is a core product goal, similar to how a compression tool should noticeably reduce file size.
The product should not merely display before/after percentages; it must actively search for rewrite strategies that lower the signal while preserving facts and tone.
During development, if the AI-like signal reduction is not satisfactory, automatically test other prompts, rewrite strategies, and scoring approaches until the before/after results improve meaningfully.
Internal development target, not user-facing copy:
- average signal reduction of at least 30 points across the representative sample set
- most rewritten samples below 50% AI-like signal
```

Mandatory R&D target:

```text
For launch-quality Naturalness Check work, the internal R&D target is mandatory:
- average AI-like signal reduction of at least 30 points
- majority of fully measured evaluated rewrites below 50%

Do not count a run as target-met if the writing signal provider returns unavailable scores for any evaluated sample.
If the provider rate-limits or fails, document it in docs/optimization-notes.md and keep the best last complete measured run.
```

Development evaluation loop:

```text
Create a local evaluation set with representative teacher, sales, workplace, and client/customer reply samples.
For each sample, record:
- draft Sapling AI-like signal
- rewrite Sapling AI-like signal
- signal delta
- facts preserved
- tone quality
- whether output became too casual, too generic, too salesy, or invented details

Use this eval set to compare rewrite strategies during development.
Do not accept the first prompt if the measured reduction is weak.
If the target is not met, keep running alternative prompts, strategy ordering, and composite scoring until results improve or a hard provider/cost limit is documented.
```

Superseded production rewrite strategy selected on 2026-05-18:

```text
Use a bounded measured workflow in /api/rewrite:
1. OpenAI plain email-thread note using the user's supplied context.
2. Deterministic thread/facts-first rewrite passes when the first pass remains above 50% AI-like signal or improves by less than 30 points.
3. Targeted repair when a candidate is worse, still too high, too short, or missing critical facts.
4. Best-available safety when strict gates cannot pass but a complete candidate exists.
5. Guaranteed facts-first fallback when all measured candidates are incomplete.

The fallback rewrite pass must use only request-provided facts and must not contain sample-specific hardcoded facts such as dates, times, people, product quantities, invoice details, or policy outcomes.

The fallback is intentionally implemented as an internal rewrite pass/subroutine, not a separate external agent, so cost and latency remain bounded. Reconsider a dedicated rewrite subagent only if future evaluation shows the bounded workflow cannot meet the target on expanded samples.

Current complete measured result:
- samples evaluated: 26
- long cases: 10
- long customer-support cases: 5
- average AI-like signal reduction: 60 points
- rewrites below 50% AI-like signal: 20/26
- final selected rewrites worse than draft: 0/26
- Priya billing/proration regression: passed at 89% -> 0%
- Priya live 100% -> 100% regression: fixed at 100% -> 0% by preserving `finance manager` in facts-first fallback
- target met: yes
```

This 2026-05-18 strategy is retained as history only. The active production route should use `fact_reconstruct` as documented near the top of this file.

### Rewrite/Repair Strategy Memory

The rewrite engine should accumulate measured strategy learning over time.

Source of truth:

```text
/Users/qc/Desktop/CloudFlare/docs/rewrite-strategy-memory.md
/Users/qc/Desktop/CloudFlare/docs/rewrite-learning-system.md
```

This document records the current internal design for:

```text
Rewrite Agent
Repair Agent
Strategy Memory Agent
diagnosis tags
repair playbook
scenario-specific lessons
promotion rules for moving a strategy into production
```

Future rewrite quality work must update this document when evaluation or real QA discovers a reusable lesson.

Implemented learning-system direction:

```text
Request-time learning:
- Each rewrite request runs a bounded diagnose -> rewrite -> measure -> repair -> select loop.
- Bad measured candidates are repaired or rejected.
- Quality-gate failures are not charged as successful usage.

Offline learning:
- Successful rewrites and quality-gate failures are stored as internal learning samples when REWRITE_LEARNING_LOG_ENABLED is not false.
- Run `npm run memory:rewrite` to create docs/rewrite-memory-digest.md from stored learning samples.
- The digest summarizes patterns and recommendations without printing user-submitted text.
```

Important rule:

```text
Do not silently train or self-modify production prompts from private user content.
Use stored internal learning samples, curated eval cases, internal QA, and aggregate telemetry to propose changes.
The Strategy Memory Agent may propose prompt/rule changes, but stable lessons must be promoted through docs, tests, evaluation, and code review.
```

Product design direction:

```text
The system should become better over time by preserving measured rewrite/repair lessons.
This is the product advantage over a simple GPT rewrite prompt:
diagnosis -> targeted rewrite -> measurement -> repair -> gated selection -> documented strategy memory.
```

Possible new env vars:

```env
WRITING_SIGNAL_PROVIDER=sapling
SAPLING_API_KEY=
WRITING_SIGNAL_TIMEOUT_SEC=10
```

Confirmed decisions:

```text
Show the before/after Sapling signal to users.
Run the Sapling signal for free users too.
One user Rewrite request counts as one usage attempt.
If the system tries multiple internal strategies for that same request, it still consumes only one usage attempt.
If the user manually clicks Try again after receiving a result, that is a new request and consumes another usage attempt.
```

Confirmed user-facing label:

```text
Use "Naturalness Check" for the before/after Sapling signal.
Explainer copy:
A third-party reference signal that helps compare how natural the draft and rewrite feel. It is not a guarantee; review the reply before sending.
```

Updated MVP decision:

```text
Show a cautious before/after "Naturalness Check".
Do not show it as a guarantee.
Do not optimize solely for this score, but treat meaningful signal reduction as a core product objective.
Charge one usage attempt per user Rewrite request.
Automatically run bounded internal optimization strategies when the first candidate does not reduce the AI-like signal enough.
If the user manually clicks Try again, that is a new usage attempt.
For the current `fact_reconstruct` production route, if the third-party signal provider is missing or fails, return a quality-failure/no-charge response instead of a successful rewrite. The UI may show a neutral "Signal unavailable" quality state, but the attempt must not be charged as a successful rewrite.
```

### Product Focus

Confirmed MVP focus:

```text
General reply assistant.
The landing page and examples should explicitly include teacher and sales scenarios, but the product should not be limited to only those two.
Also include workplace email and customer/client replies.
All user-facing website and app UI copy should be in English.
```

## Confirmed UI/UX Decisions

Visual direction:

```text
Warm Writing Desk
Warm, calm, professional, writing-focused.
Use off-white paper-like backgrounds, dark readable text, soft borders, subtle note/paper metaphors, and restrained warm accents.
Avoid heavy futuristic AI-tool styling, large purple/blue gradients, and aggressive marketing visuals.
```

Landing page:

```text
Include a preset interactive demo.
The demo must not call OpenAI or Sapling.
The demo must not consume usage.
Demo scenarios:
- Teacher message
- Sales follow-up
- Workplace email
- Client/customer reply
```

App workspace:

```text
Use a two-column desktop layout.
Left column: Message to reply to, Rough draft reply, context fields, tone selector, Rewrite button.
Right column: rewritten output, Naturalness Check, change summary, risk notes, copy button, Try again, local history.
Mobile layout stacks vertically.
```

Naturalness Check:

```text
Show Sapling before/after as an AI-like signal percentage.
Lower is better.
Example:
Naturalness Check
AI-like signal
Draft: 78%
Rewrite: 32%
Change: -46 pts
Reference signal only. Review the reply before sending.
```

Display rules:

```text
Use soft progress bars and labels such as High AI-like signal, Lower AI-like signal, Signal unavailable.
Do not display pass/fail.
Do not imply 0% means guaranteed human writing.
Do not use "detector score" in user-facing UI.
```

Privacy and history:

```text
Local history must include a Clear history button.
Add an input-area privacy reminder:
Avoid pasting passwords, payment details, or highly sensitive personal information.
```

Character limits:

```text
Message to reply to: max 5000 characters.
Rough draft reply: 10 to 5000 characters.
Audience: max 300 characters.
Purpose: max 500 characters.
What actually happened: max 1000 characters.
Facts to preserve: max 1000 characters.
Combined request cap: 10000 characters.
Show remaining characters near long textareas.
```

Account/settings scope:

```text
Do not create a separate /account page for MVP.
Show account-adjacent controls inside /app:
- Subscription status
- Usage remaining
- Manage billing
- Clear history
```

## Important Local Files

Work only inside this folder unless the user explicitly says otherwise:

```text
/Users/qc/Desktop/CloudFlare
```

Key files:

```text
/Users/qc/Desktop/CloudFlare/replyinmyvoice_requirements.md
/Users/qc/Desktop/CloudFlare/.env.local
/Users/qc/Desktop/CloudFlare/local-env.md
/Users/qc/Desktop/CloudFlare/AGENTS.md
```

Sensitive Cloudflare credential file:

```text
/Users/qc/Desktop/CloudFlare/globalapikey/globalapikey.md
```

Do not print, quote, summarize, commit, or expose secret values from `.env.local` or `globalapikey/globalapikey.md`.

## Current External Setup

Domain:

```text
replyinmyvoice.com
```

Current holding page:

```text
https://replyinmyvoice.com
```

Cloudflare Pages project already exists:

```text
replyinmyvoice
```

GitHub remote to use:

```bash
git remote add origin git@github.com:ChuanQiao1128/replyinmyvoice.git
```

GitHub SSH has already been checked successfully for user:

```text
ChuanQiao1128
```

Stripe sandbox webhook has already been created for:

```text
https://replyinmyvoice.com/api/stripe/webhook
```

Webhook events:

```text
checkout.session.completed
customer.subscription.created
customer.subscription.updated
customer.subscription.deleted
```

## Environment

Local env file:

```text
/Users/qc/Desktop/CloudFlare/.env.local
```

Environment setup notes:

```text
/Users/qc/Desktop/CloudFlare/local-env.md
```

`.env.local` has been checked and is ready. Required variables are present and filled:

```env
NEXT_PUBLIC_APP_URL
NODE_ENV
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY
CLERK_SECRET_KEY
NEXT_PUBLIC_CLERK_SIGN_IN_URL
NEXT_PUBLIC_CLERK_SIGN_UP_URL
NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL
NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL
DATABASE_URL
DIRECT_URL
OPENAI_API_KEY
OPENAI_MODEL
STRIPE_SECRET_KEY
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY
STRIPE_PRICE_ID
STRIPE_WEBHOOK_SECRET
WRITING_SIGNAL_PROVIDER
SAPLING_API_KEY
WRITING_SIGNAL_TIMEOUT_SEC
ALLOW_DEV_SUBSCRIPTION_BYPASS
CLOUDFLARE_ACCOUNT_ID
CLOUDFLARE_API_TOKEN
LAUNCH_CONFIRMED
EVAL_MAX_PROMPT_ITERATIONS
EVAL_MAX_WALLCLOCK_MINUTES
```

Never hardcode secrets. Always read secrets from `process.env`.

Builds must not fail only because production secrets are absent. Validate required secrets at runtime in the route or server action that uses them.

## Deployment Decision

The requirements file has been aligned to Cloudflare deployment.

Because this MVP requires API routes, Stripe webhooks, Clerk auth, OpenAI calls, and Prisma/Neon access, do not use static Next export as the production architecture.

Confirmed production target:

```text
Next.js App Router + Cloudflare Workers/OpenNext
Worker name: replyinmyvoice-app
Verify first on the workers.dev URL before any production-domain cutover.
```

The existing Cloudflare Pages holding page should remain live until the full app is built, tested, and ready to cut over.

Do not modify the existing `replyinmyvoice` Pages project's Custom Domain, and do not modify `replyinmyvoice.com` DNS records during normal autonomous development.

Production-domain cutover is gated by:

```env
LAUNCH_CONFIRMED=true
```

If `LAUNCH_CONFIRMED` is missing or `false`, deploy and verify only the independent Worker (`replyinmyvoice-app`) on its `workers.dev` URL. Then document the final domain cutover steps in `docs/manual-setup.md` and stop before changing the live domain.

If Cloudflare custom-domain conflicts appear during deployment, preserve the working holding page and document the final cutover step instead of breaking the live domain mid-build.

Preflight finding:

```text
CLOUDFLARE_API_TOKEN is required for Wrangler deployment in this non-interactive local environment.
The user has added a scoped API token to `.env.local`.
Do not print or expose the token.
```

Latest pre-development checks:

```text
GitHub SSH auth: ok
Git remote origin: git@github.com:ChuanQiao1128/replyinmyvoice.git
Remote branch heads: none found, likely empty repository
Node.js: v24.9.0
npm: 11.6.0
Wrangler: 4.92.0
Neon DATABASE_URL: ok
Neon DIRECT_URL: ok
OpenAI model lookup: ok
OpenAI tiny generation request: ok
Clerk secret key: ok
Stripe price lookup: ok, unit_amount=900, currency=nzd, interval=month
Stripe sandbox NZD price accepted by user for testing
Stripe webhook endpoint/events: ok
Sapling aidetect API: ok
Cloudflare raw API / zone active: ok
Cloudflare non-interactive deploy token: set
Wrangler whoami with API token: ok
Cloudflare Pages API with token: ok, replyinmyvoice project found
Cloudflare zone read with token: ok
Cloudflare Workers scripts API with token: ok
Cloudflare DNS records read with token: ok
```

## Product Requirements Summary

Read the full source of truth before implementation:

```text
/Users/qc/Desktop/CloudFlare/replyinmyvoice_requirements.md
```

Required routes:

```text
/
/pricing
/sign-in/[[...sign-in]]
/sign-up/[[...sign-up]]
/app
/api/rewrite
/api/stripe/checkout
/api/stripe/portal
/api/stripe/webhook
```

Landing page must include:

```text
Hero
Use cases
How it works
Example before/after panel
Pricing
FAQ
Footer
```

Authenticated app page must include:

```text
Rewrite workspace for signed-in inactive/free users while free lifetime quota remains
Paywall for signed-in inactive/free users only after free lifetime quota is exhausted
Rewrite workspace for active/trialing subscribers with paid billing-period quota
Message to reply to textarea
Rough draft reply textarea
Audience field
Purpose field
What actually happened field
Facts to preserve field
Tone selector with exactly Warm and Direct
Rewrite button
Before panel
After panel
Change summary panel
Risk notes panel
Copy button
Local history of last 5 rewrites
```

LocalStorage key:

```text
rimv.rewrite.history.v1
```

Do not persist rewrite history in the database.

## User-Facing Copy Constraints

Never use these terms or concepts in user-facing copy, metadata, route labels, marketing sections, UI docs, or visible product text:

```text
AI detection bypass
detector bypass
undetectable
humanizer
evade detection
bypass filters
trick detectors
```

Allowed direction:

```text
natural writing
personal tone
context-aware replies
sounds like you
preserve facts
clearer replies
warmer replies
direct replies
```

Before completion, run:

```bash
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

Remove or rewrite any user-facing occurrences.

## Tech Stack

Use this stack unless implementation proves a narrow compatibility adjustment is required:

```text
Next.js 15 App Router pinned to v15.x
TypeScript
Tailwind CSS v3
Clerk
Neon Postgres
Prisma ORM with Cloudflare-compatible Neon adapter/driver
Stripe Checkout
Stripe Billing Portal
OpenAI API
Cloudflare Workers/OpenNext deployment with @opennextjs/cloudflare
```

Default OpenAI model:

```text
gpt-4o-mini
```

## Database

Use Prisma with Postgres.

Minimum `User` model:

```prisma
model User {
  id                   String   @id @default(cuid())
  clerkUserId          String   @unique
  email                String?
  stripeCustomerId     String?  @unique
  stripeSubscriptionId String?
  stripePriceId        String?
  subscriptionStatus   String   @default("inactive")
  currentPeriodEnd     DateTime?
  createdAt            DateTime @default(now())
  updatedAt            DateTime @updatedAt
}
```

Allowed subscription statuses for app access:

```ts
["active", "trialing"]
```

## Implementation Plan

### Phase 1: Repository And Project Setup

1. Initialize git in `/Users/qc/Desktop/CloudFlare`.
2. Create `.gitignore` before adding files.
3. Ensure these are ignored:

```text
.env
.env.local
.dev.vars
globalapikey/
node_modules/
.next/
.open-next/
.wrangler/
dist/
```

4. Add GitHub remote:

```bash
git remote add origin git@github.com:ChuanQiao1128/replyinmyvoice.git
```

5. Scaffold or create the Next.js app in this folder.

### Phase 2: Base App Structure

Create or update:

```text
app/layout.tsx
app/page.tsx
app/pricing/page.tsx
app/sign-in/[[...sign-in]]/page.tsx
app/sign-up/[[...sign-up]]/page.tsx
app/app/page.tsx
app/api/rewrite/route.ts
app/api/stripe/checkout/route.ts
app/api/stripe/portal/route.ts
app/api/stripe/webhook/route.ts
middleware.ts
```

Create components:

```text
components/site-header.tsx
components/site-footer.tsx
components/landing/hero.tsx
components/landing/use-cases.tsx
components/landing/how-it-works.tsx
components/landing/example-panel.tsx
components/landing/pricing.tsx
components/landing/faq.tsx
components/app/rewrite-workspace.tsx
components/app/paywall-card.tsx
components/app/subscription-status.tsx
components/ui/button.tsx
components/ui/card.tsx
components/ui/textarea.tsx
components/ui/input.tsx
```

Create helpers:

```text
lib/db.ts
lib/users.ts
lib/subscription.ts
lib/stripe.ts
lib/openai.ts
lib/validation.ts
```

### Phase 3: Auth

1. Install Clerk.
2. Add Clerk provider in `app/layout.tsx`.
3. Add Clerk middleware.
4. Public routes:

```text
/
/pricing
/sign-in
/sign-up
/api/stripe/webhook
```

5. Protected routes:

```text
/app
/api/rewrite
/api/stripe/checkout
/api/stripe/portal
```

### Phase 4: Prisma And Neon

1. Add Prisma dependencies and schema.
2. Configure `DATABASE_URL` and `DIRECT_URL`.
3. Generate Prisma client.
4. Add a Prisma database helper compatible with Cloudflare Workers, preferably using an edge-compatible Neon adapter/driver.
5. Add user and subscription helpers.
6. Run migration locally.
7. After OpenNext build, run a Worker preview/runtime DB smoke test.

### Phase 5: Stripe

Implement:

```text
POST /api/stripe/checkout
POST /api/stripe/portal
POST /api/stripe/webhook
```

Checkout requirements:

```text
Authenticated user required
Get or create local User
Get or create Stripe customer
Use STRIPE_PRICE_ID
Subscription mode
Success URL: NEXT_PUBLIC_APP_URL + /app?checkout=success
Cancel URL: NEXT_PUBLIC_APP_URL + /app?checkout=cancelled
Return { url }
```

Webhook requirements:

```text
Use raw request body
Verify Stripe signature in production
Use Cloudflare Workers-compatible async verification if synchronous Stripe verification fails
Webhook handling must be idempotent by Stripe event id
Update local User subscription fields
Treat active/trialing as active
Treat canceled/unpaid/incomplete_expired/deleted as inactive
```

### Phase 6: OpenAI Rewrite API

Implement:

```text
POST /api/rewrite
```

Request shape:

```ts
type RewriteRequest = {
  messageToReplyTo?: string;
  roughDraftReply: string;
  audience?: string;
  purpose?: string;
  whatHappened?: string;
  factsToPreserve?: string;
  tone: "warm" | "direct";
};
```

Response shape:

```ts
type RewriteResponse = {
  rewrittenText: string;
  changeSummary: string[];
  riskNotes: string[];
};
```

Validation:

```text
messageToReplyTo optional, max 5000 chars
roughDraftReply required, 10 to 5000 chars
audience optional, max 300 chars
purpose optional, max 500 chars
whatHappened optional, max 1000 chars
factsToPreserve optional, max 1000 chars
combined request cap 10000 chars
tone must be warm or direct
401 unauthenticated
402 no active/trialing subscription and free lifetime quota exhausted
400 invalid input
500 provider/server error
```

The model must return strict JSON only. If parsing fails, return a safe server error without exposing raw model text.

### Phase 7: UI

Implement a polished, practical SaaS UI with:

```text
Off-white background
Dark readable text
Subtle cards and borders
Friendly but professional tone
Mobile responsiveness
No large component library unless necessary
```

The first viewport must clearly signal:

```text
Reply In My Voice
Replies that still sound like you.
```

### Phase 8: Local History

Use localStorage only:

```text
rimv.rewrite.history.v1
```

Store the last 5 rewrite results with:

```text
original draft
rewritten text
tone
change summary
risk notes
created timestamp
```

Do not write rewrite history to Neon.

### Phase 9: Docs

Create or update:

```text
.env.example
README.md
docs/manual-setup.md
local-env.md
```

Docs must include:

```text
Local setup
Required env vars
Prisma commands
Running dev server
Stripe webhook local testing note
Build/typecheck commands
Cloudflare Workers/OpenNext deployment notes
Manual dashboard tasks
```

### Phase 10: Verification

Run until passing:

```bash
npm run prisma:generate
npm run typecheck
npm run build
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

If tests or builds fail, fix and repeat.

Expected scripts:

```json
{
  "dev": "next dev",
  "build": "next build",
  "typecheck": "tsc --noEmit",
  "lint": "next lint || eslint .",
  "prisma:generate": "prisma generate",
  "prisma:migrate": "prisma migrate dev",
  "prisma:deploy": "prisma migrate deploy",
  "postinstall": "prisma generate"
}
```

### Phase 11: Deploy

1. Build locally.
2. Deploy with Cloudflare-compatible Next.js deployment to Worker `replyinmyvoice-app`.
3. Set production environment variables in Cloudflare.
4. Verify:

```text
replyinmyvoice-app workers.dev URL
/pricing
/sign-in
/app
/api/stripe/webhook
```

5. If `LAUNCH_CONFIRMED=false`, do not change DNS or custom domains. Document the final cutover steps in `docs/manual-setup.md`.
6. If `LAUNCH_CONFIRMED=true`, perform the final production-domain cutover only after Worker verification passes.

### Phase 12: Git

Commit and push in meaningful stages. At minimum, commit after each completed phase:

```text
preflight/docs
project scaffold
auth
database/usage
stripe
rewrite/signal
ui
evaluation
cloudflare verification
```

Push to:

```bash
git@github.com:ChuanQiao1128/replyinmyvoice.git
```

## Completion Criteria

The project is complete only when:

```text
npm run typecheck passes
npm run build passes
Prisma client generation works
Landing page exists and has required sections
Clerk sign-in/sign-up routes exist
/app is protected
/app allows signed-in inactive/free users to use remaining free lifetime quota
/app shows hard paywall only after free quota is exhausted for inactive/free users
/app gates paid billing-period quota for active/trialing subscribers
POST /api/rewrite validates input
POST /api/rewrite rejects unauthenticated users
POST /api/rewrite returns 402 only when signed-in users have neither active/trialing quota nor remaining free lifetime quota
Stripe checkout route is implemented
Stripe portal route is implemented
Stripe webhook route is implemented
Subscription state persists in Postgres
Rewrite history uses localStorage only
.env.example is complete
README.md is updated
docs/manual-setup.md is created
Banned terms are absent from user-facing app code
Production deployment is verified or the remaining cutover blocker is documented clearly
```

## Autonomy Rules

After the user approves the implementation plan, continue autonomously until the goal is complete.

Do not stop for ordinary build errors, type errors, lint errors, package issues, Cloudflare adapter issues, Stripe route bugs, or local testing problems. Fix them and keep going.

Only stop for the user if:

```text
An external dashboard action is impossible from the local environment
GitHub SSH push is denied
Cloudflare API/deploy permission is denied
Clerk/Stripe/Neon/OpenAI credentials are invalid and cannot be worked around
A real paid/live-mode financial action is required
The task would require exposing or committing secrets
```

If blocked by a dashboard-only step, document it in `docs/manual-setup.md` and continue with everything else that can be completed.

## Autonomous Preflight — Must Run Before Coding

Before writing application code, run a preflight check and create `docs/preflight-report.md`.

The report must include:

- Current working directory.
- Git status and current branch.
- Whether the GitHub remote is configured.
- Existing project structure and framework detected.
- Package manager detected.
- Next.js version detected.
- Node and npm versions detected.
- Whether the current holding-page code exists and should be preserved.
- Whether `.gitignore` protects `.env`, `.env.local`, `.dev.vars`, `globalapikey/`, `node_modules/`, `.next/`, `.open-next/`, `.wrangler/`, and `dist/`.
- Required environment variable names present or missing. Do not print secret values.
- Cloudflare deployment target and whether `CLOUDFLARE_API_TOKEN` is available.
- Clerk configuration availability.
- Neon connection availability.
- Stripe price and webhook availability.
- OpenAI model availability.
- Sapling aidetect availability and observed response shape.
- Any blockers and the chosen fallback.

Preflight rules:

- Work only inside `/Users/qc/Desktop/CloudFlare`.
- Do not create a second unrelated app folder.
- Inspect the existing project before scaffolding.
- Preserve the current holding-page brand direction unless the existing codebase is unusable.
- Never print, summarize, commit, or expose secret values from `.env.local`, `.dev.vars`, or `globalapikey/`.
- Do not use static Next export.
- Production deployment target is Next.js App Router on Cloudflare Workers using OpenNext.
- If `CLOUDFLARE_API_TOKEN` is missing, continue building the full app, skip destructive deployment/cutover, and document the missing deployment step in `docs/manual-setup.md`.
- If dashboard-only actions are required, document them and keep going.
- If a build fails because of Node 24 compatibility, first try dependency-compatible fixes; if still blocked, document a recommendation to use Node 22 LTS or Node 20 LTS.
- Use Clerk middleware appropriate to the detected Next.js version. For Next.js 15, use `middleware.ts`.
- Stripe webhook signature verification is mandatory in production.
- Local webhook fallback without signature is allowed only when `NODE_ENV !== "production"`.
- Sapling must be called server-side only.
- Convert Sapling `score` from 0..1 to 0..100 if that is the observed response shape.
- In the current `fact_reconstruct` production route, if Sapling fails, return a quality-failure/no-charge response and show `Signal unavailable` if the UI displays the Naturalness Check panel.
- Usage quota must be enforced server-side, never from localStorage.
- Count exactly one successful user rewrite request after a successful response is ready.
- Do not count validation errors, auth failures, payment failures, provider errors, or server errors.
- Continue autonomously after preflight unless a stop condition from `AGENTS.md` is met.

## Final Long-Run Clarifications Before Autonomous Development

### Free Quota Vs Paywall

Signed-in inactive users are allowed to use the rewrite workspace until they have used 3 lifetime successful rewrites. Only show the hard paywall after the free lifetime quota is exhausted. Active/trialing paid users get 100 successful rewrites per billing period.

`POST /api/rewrite` should return:

- `401` when unauthenticated.
- `402` only when the signed-in user has no active/trialing subscription and has exhausted the 3 free lifetime successful rewrites.
- `400` for validation errors.
- `500` for provider/server errors.

### Usage Accounting

Implement quota enforcement server-side only.

Use a transaction for quota checks and increments.

Suggested period keys:

- `lifetime` for signed-in unpaid users.
- `paid:<stripeSubscriptionId>:<billingPeriodEnd>` for active/trialing paid users.

Check quota before provider calls. Charge usage only after a successful rewrite result is ready. Increment exactly once per successful user-visible rewrite request.

Do not count validation errors, auth failures, payment failures, provider failures, Sapling failures, OpenAI failures, or server errors. Do not let localStorage decide usage quota.

### Cloudflare-Compatible Database Access

Do not assume a standard Node/TCP Prisma Client will work in Cloudflare Workers.

Choose and implement one Cloudflare-compatible database access strategy:

- Prisma with an edge-compatible Neon adapter/driver.
- Prisma Accelerate or Prisma Postgres if configured.
- Another documented Cloudflare-compatible Prisma approach.

After OpenNext build, run a Worker preview/runtime DB smoke test. Passing `next build` alone is not enough.

### Next.js And Clerk Version Pinning

Pin Next.js to v15.x. Do not install `next@latest` if it resolves to Next.js 16.

For Next.js 15, use `middleware.ts`.

If a dependency forces Next.js 16, adapt to `proxy.ts` and document the version change.

Use `.nvmrc` value `22`. When creating `package.json`, set `engines.node` to `>=22 <23`.

Do not put database queries, subscription checks, Stripe calls, OpenAI calls, Sapling calls, or heavy logic in middleware/proxy. Use middleware only for lightweight auth route protection. Subscription checks belong in server pages and API routes.

### Stripe Subscription Period Compatibility

When reading Stripe subscription billing periods, prefer:

```ts
subscription.items.data[0].current_period_end
```

Fall back to legacy:

```ts
subscription.current_period_end
```

only if present. Do not assume top-level `current_period_end` exists.

### Stripe Webhook On Cloudflare Workers

Use raw request body for webhook verification.

In production, Stripe webhook signature verification is mandatory.

If synchronous webhook verification fails in Cloudflare Workers, use an async-compatible verification path and Web Crypto compatible Stripe setup.

Webhook handling must be idempotent. Repeated Stripe events must not double-create users or corrupt subscription status.

### Dynamic Runtime Behavior

Mark auth/subscription-dependent pages and routes as dynamic where needed:

- `/app`
- `/api/rewrite`
- `/api/stripe/checkout`
- `/api/stripe/portal`
- `/api/stripe/webhook`

Do not read secret env vars at module import time in a way that breaks build. Validate required secrets inside the handler that uses them.

Do not add `export const runtime = "edge"` to Prisma/Stripe/OpenAI routes unless the implementation is verified in Cloudflare Worker preview.

### Cloudflare/OpenNext Verification

Passing `npm run build` is not sufficient.

Also add and run Cloudflare/OpenNext build and preview commands appropriate to the installed adapter version.

Verify at minimum:

- `/`
- `/pricing`
- `/sign-in`
- `/app`
- unauthenticated `/api/rewrite` rejection
- `/api/stripe/webhook` method/body handling
- one DB smoke test in Worker preview

Keep the current live holding page intact until the Worker deployment is verified.

### Naturalness Evaluation Budget

Use 8-12 representative samples for development evaluation.

Try at most 3 prompt/strategy variants during evaluation.

Honor these env caps when present:

```env
EVAL_MAX_PROMPT_ITERATIONS=5
EVAL_MAX_WALLCLOCK_MINUTES=60
```

The default long-run guardrail is stricter than the env ceiling: run at most 3 full prompt/strategy evaluation rounds before continuing.

Production cap remains bounded: up to 3 initial strategies plus up to 2 targeted repairs per user request.

If the target reduction is not met within the evaluation budget or wall-clock limit, keep the best measured strategy, write measured results and tradeoffs to `docs/optimization-notes.md`, and continue building the product.

Never create an unbounded loop chasing the writing signal.

### User-Facing Terminology

Use `Naturalness Check`, `writing signal`, and `AI-like signal`.

Avoid the banned words in user-facing UI, metadata, and marketing copy.

Prefer naming internal files/functions `writing-signal` rather than `detector` to avoid grep failures.

## Final Pre-Run Patch — Before Long Autonomous Development

### Banned-Term Grep Compatibility

The banned-term grep scans `lib/**` too. Therefore internal prompts, comments, helper names, filenames, and constants must also avoid the grep substrings:

```text
humanizer
bypass
undetect
detector
evade
```

Do not put those substrings in the OpenAI system prompt. Use neutral phrasing such as:

```text
Do not discuss hiddenness, evasion, or whether the reply will pass automated reviews.
```

### Cloudflare/OpenNext Scripts

Do not use plain `wrangler dev` as the main Cloudflare preview command for this Next.js app.

Use:

```json
{
  "cf:build": "opennextjs-cloudflare build",
  "cf:preview": "opennextjs-cloudflare build && opennextjs-cloudflare preview",
  "cf:deploy": "opennextjs-cloudflare build && opennextjs-cloudflare deploy -- --keep-vars"
}
```

Create `wrangler.jsonc` explicitly. Do not rely only on auto-generated config.

Required minimum:

```jsonc
{
  "$schema": "./node_modules/wrangler/config-schema.json",
  "name": "replyinmyvoice-app",
  "main": ".open-next/worker.js",
  "compatibility_date": "2026-05-17",
  "compatibility_flags": ["nodejs_compat"],
  "assets": {
    "directory": ".open-next/assets",
    "binding": "ASSETS"
  }
}
```

Do not remove `nodejs_compat`. Do not set a compatibility date earlier than `2024-09-23`.

Create `open-next.config.ts` using the installed `@opennextjs/cloudflare` configuration helper.

### Environment Handling

Use `.env.local` for local Next/OpenNext development.

Do not create `.dev.vars` with duplicated secrets unless absolutely necessary. If `.dev.vars` is created, it should normally contain only:

```env
NEXTJS_ENV=development
```

Never commit `.env*` or `.dev.vars*`.

Production secrets must be configured in Cloudflare as secrets/runtime variables.

Deploy with `opennextjs-cloudflare deploy -- --keep-vars` so existing Cloudflare dashboard variables are not wiped.

Do not print secret values while setting secrets. Names only.

### Pricing Display

Treat Stripe `unit_amount=900` with `currency=nzd` as `NZD $9/month`.

User-facing copy must say `NZD $9/month`.

Do not display `900 NZD/month`.

In the final report, include Stripe price verification as:

```text
unit_amount=900, currency=nzd, interval=month
```

### Stripe Webhook Event Coverage

Required MVP events:

- `checkout.session.completed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`

Strongly preferred additional events:

- `invoice.paid`
- `invoice.payment_failed`

If Stripe dashboard currently lacks invoice events, do not stop coding. Implement handlers and document adding those events in `docs/manual-setup.md`.

### Quota Concurrency

Do not implement quota as read-count-then-write-count without a guard.

Use an atomic DB transaction that either:

- creates the usage row if missing, then conditionally increments only when `count < quota`
- locks/serializes the usage row before provider calls

If two requests race when only one attempt remains, exactly one may succeed and the other must return `402`.

Do not count validation, auth, payment, OpenAI, Sapling, or server failures.

### Same-Origin Protection

Protected POST routes should enforce same-origin requests:

- `/api/rewrite`
- `/api/stripe/checkout`
- `/api/stripe/portal`

Check `Origin` when present. Allow only:

- `NEXT_PUBLIC_APP_URL`
- localhost development origins

Do not apply this check to `/api/stripe/webhook`; Stripe webhook uses signature verification instead.

### Local History Privacy

Store only:

- rough draft
- rewritten text
- tone
- change summary
- risk notes
- naturalness result
- timestamp

Do not store the full `messageToReplyTo` field in localStorage.

### Migration Safety

Allowed:

- `npx prisma generate`
- `npx prisma migrate dev --name init`
- `npx prisma migrate deploy`

Forbidden unless the user explicitly approves:

- `prisma migrate reset`
- `prisma db push --force-reset`
- dropping tables
- deleting existing production data

`DIRECT_URL` is for migrations only. `DATABASE_URL` is runtime.

### Deployment Cutover

First deploy and test on the Worker preview / workers.dev URL.

Do not delete the existing Cloudflare Pages project.

Do not attach `replyinmyvoice.com` to the Worker until:

- `/` works
- `/pricing` works
- `/sign-in` works
- `/app` auth gate works
- unauthenticated `/api/rewrite` returns `401`
- Stripe webhook method/signature behavior is verified
- one DB smoke test passes in Worker preview

If custom-domain conflict appears, leave the holding page live and document the manual cutover.

### Provider Timeout And Cost Guard

Use `AbortController`/timeouts for OpenAI and Sapling calls.

Use `WRITING_SIGNAL_TIMEOUT_SEC` for Sapling.

Add `OPENAI_TIMEOUT_SEC=25` to `.env.example` if needed.

One production user request using `fact_reconstruct` may perform at most:

- 1 draft writing-signal call
- one fact extraction call
- one scenario classification call
- one three-candidate generation call
- one reviewer call
- one finalizer call
- one LLM fact-check call
- one strong-model escalation call when the first final misses the quality bar
- up to 2 rewrite writing-signal calls

If Sapling times out in the fact-reconstruct production route, return a quality-failure/no-charge response with `naturalness.label = "unavailable"` when available.

If OpenAI fails, do not charge usage.

Do not run the development evaluation loop against unbounded samples.

## Next Phase: Launch Cutover And Quality Target

The next long-running phase is no longer initial MVP construction. It is launch cutover, real-account verification, and Naturalness Check quality improvement.

Required plan file:

- `/Users/qc/Desktop/CloudFlare/docs/launch-cutover-plan.md`

Before executing the next phase, read:

- `/Users/qc/Desktop/CloudFlare/AGENTS.md`
- `/Users/qc/Desktop/CloudFlare/replyinmyvoice_requirements.md`
- `/Users/qc/Desktop/CloudFlare/docs/preflight-report.md`
- `/Users/qc/Desktop/CloudFlare/docs/manual-setup.md`
- `/Users/qc/Desktop/CloudFlare/docs/optimization-notes.md`
- `/Users/qc/Desktop/CloudFlare/docs/launch-cutover-plan.md`
- `/Users/qc/Desktop/CloudFlare/package.json`
- `/Users/qc/Desktop/CloudFlare/wrangler.jsonc`
- `/Users/qc/Desktop/CloudFlare/prisma/schema.prisma`

`.env.local` may be checked only for variable names and presence. Never print secret values.

Updated launch authorization:

- `LAUNCH_CONFIRMED=true` means the code agent is authorized to cut over `replyinmyvoice.com` to the verified Worker during the next phase.
- Keep Stripe in sandbox mode. Do not switch to live Stripe keys or live price IDs.
- Keep the Worker name `replyinmyvoice-app`.
- Do not delete the existing Cloudflare Pages project.
- Preserve rollback instructions.
- Commit and push after each completed phase.

Next phase goals:

1. Re-run launch preflight.
2. Check Clerk and Stripe sandbox dashboard configuration as far as API/local context allows.
3. Deploy the latest Worker and cut over `replyinmyvoice.com`.
4. Verify a real test account flow: register, sign in, free quota, paywall, sandbox checkout, webhook, paid quota.
5. Continue Naturalness Check optimization until the internal target is met:
   - average AI-like signal reduction of at least 30 points
   - most evaluated samples below 50%
6. Push all work to GitHub and leave the branch clean.

## Next Development Phase: Quick Context, Tested Samples, And Commercial UX

Before starting the next product/UI development run, read:

- `/Users/qc/Desktop/CloudFlare/docs/next-development-brief.md`
- `/Users/qc/Desktop/CloudFlare/docs/sample-cases.md` if it exists
- `/Users/qc/Desktop/CloudFlare/docs/optimization-notes.md`

This section supersedes earlier Warm/Direct-only and heavy context-field wording for the next product iteration.

### Workspace UX Direction

Keep the rewrite workspace as a single-page tool surface. Do not convert it into a step-by-step wizard.

Primary inputs remain:

- message/thread to reply to
- rough draft reply

Reduce form burden by grouping secondary controls into a `Quick context` section:

- audience preset
- purpose preset
- what must stay the same chips
- tone preset
- optional extra context

`Extra context` must be collapsed by default. Users should open it only when they need to add additional details.

Audience and purpose should support `Other`. When `Other` is selected, show a custom input. Do not force custom text for normal preset choices.

Rename or position the old `What actually happened` concept as `Extra context`, and the old `Facts to preserve` concept as `What must stay the same`.

### Tone Presets And API Contract

The UI should show more than two tone choices, such as:

- Warm
- Direct
- Professional
- Friendly
- Firm but polite
- Apologetic
- Concise

Pass the selected visible tone preset to the API as `tonePreset` or an equivalent explicit request field. Do not rely only on client-side mapping into `warm` or `direct`.

Keep the existing `tone` field only as compatibility/fallback if useful. The server prompt must receive enough information to reflect the user's selected preset.

### Homepage Samples

Homepage sample cases must come from documented internal testing or evaluation, not arbitrary placeholder copy.

Rules:

- Do not introduce names, dates, prices, numbers, policy details, or commitments that are not in the sample input.
- Do not rewrite `Dear Student` into a random named greeting.
- Use one selected case each for teacher, sales follow-up, workplace email, and client reply.
- Samples should be long enough to feel realistic but still readable on the homepage.
- Naturalness Check values shown on the homepage should be static selected sample values from `docs/sample-cases.md`, not fresh Sapling calls on every page render or local load.

### Sapling Sample Usage And Cost Tracking

The user has subscribed to the Sapling API plan, so the next round can test longer, more realistic emails.

Use longer samples during evaluation:

- 150-300 words for short reply contexts
- 300-600 words for normal workplace/customer contexts
- 600-1,000 words for longer thread or detailed client reply contexts

Create or update `docs/sample-cases.md` during implementation. For each selected case, record:

- category
- incoming context
- rough draft
- rewritten reply
- word count
- estimated character count
- displayed excerpt word count
- displayed excerpt estimated character count
- Sapling call count
- estimated Sapling characters consumed
- draft score
- rewrite score
- score change
- preserved facts checklist
- whether the case is used on the homepage

Add a usage/cost estimate section with:

- total selected sample count
- total evaluation sample count
- total Sapling calls used
- total estimated characters sent to Sapling
- average characters per sample
- notes on any 429/rate/capacity errors

Unavailable Sapling scores must not count as target-met results. If Sapling returns 429 again, stop repeated evaluation calls, keep the best documented results, and continue product work.

### FAQ Layout

Change the FAQ from a two-column card grid to a familiar single-column list or accordion:

- centered max-width column
- question rows separated by light dividers
- concise answers
- optional chevron/plus interaction
- no large repeated FAQ cards

### Commercial Site Defaults

Use these defaults unless the user gives a newer instruction:

- Pricing remains `NZD $9/month`.
- Do not implement annual checkout until a Stripe annual price exists.
- Do not advertise an annual plan in the UI unless checkout can support it.
- Footer should include `Operated by TimeAwake Ltd.`
- Support/contact email is `info@timeawake.co.nz`.
- Add or expose concise Privacy and Terms footer links/pages when practical.
- Privacy/Terms should state that pasted messages and rewritten replies are processed for the request and are not saved to the database.

Sapling is only a third-party reference signal provider for the Naturalness Check. Do not copy Sapling's Pro feature table or market Sapling-specific features such as autocomplete, snippets, domain administration, or chat assist as Reply In My Voice features.

### Next Development Reporting

When the next development run finishes, include:

- workspace UX changes completed
- tone preset/API behavior
- sample case source and recorded scores
- Sapling usage/cost estimate summary
- FAQ layout changes
- tests/screenshots run
- GitHub push status

## Workspace Redesign V2 And Diagnosis-Driven Rewrite Engine

This section supersedes earlier Quick context workspace notes.

### Workspace V2

- Remove Quick context from the user-facing workspace.
- Keep the workspace as one page.
- Context or message is optional.
- Draft to rewrite is required.
- Use five broad scenarios:
  - `Blank / custom`
  - `Email or message reply`
  - `Customer support`
  - `Cover letter`
  - `Work update`
- Use four visible tone presets only:
  - `Warm`
  - `Professional`
  - `Friendly`
  - `Concise`
- Results should be vertical:
  - rewritten text
  - Naturalness Check
  - change summary and risk notes
  - collapsed local history

### Scenario Guardrails

Each scenario must carry backend guardrails, not extra user burden:

- Blank / custom: preserve facts and do not add a recipient, relationship, timeline, or promise.
- Email or message reply: answer the actual thread and do not add a name unless provided.
- Customer support: preserve amounts, counts, dates, product names, policy limits, and next steps; avoid new promises or account changes.
- Cover letter: preserve real experience only; do not invent achievements.
- Work update: preserve ownership, deadlines, blockers, status, and requested action.

### Diagnosis-Driven Engine

Production rewrite flow must follow:

1. diagnose draft patterns
2. create rewrite plan
3. targeted rewrite
4. measure writing signal
5. repair if needed
6. select best candidate

Diagnosis tags include stock openings, corporate polish, uniform rhythm, over-explaining, generic transitions, policy memo voice, low specificity, over-safe tone, support template voice, and application cliches.

Selection must prefer candidates that both lower the AI-like signal and preserve critical facts. Critical facts include emails, currency, months/dates, seat/user counts, named timing, product/reporting details, and scenario-specific details discovered in the request.

If a low-signal candidate drops critical facts, repair the candidate by restoring those details before measuring/selecting. Do not charge extra user usage for internal repair.

### Evaluation Record

Create or update `docs/scenario-evaluation-results.md` during development.

The required evaluation set is:

- five scenarios
- at least three cases per scenario
- at least 15 total cases

For each case record:

- scenario
- tone
- diagnosis tags
- rewrite plan
- draft AI-like signal
- rewrite AI-like signal
- change in points
- expected facts
- facts preserved
- missing facts
- pass/fail
- before and after text

The internal quality target is met when the measured set has:

- average AI-like signal reduction of at least 30 points
- a majority of rewrites below 50% AI-like signal

Current documented run in `docs/scenario-evaluation-results.md`:

- 26 cases evaluated
- 26 measured
- 10 long cases of 300+ words
- 5 long customer-support cases of 300+ words
- average AI-like signal drop: 60 points
- 20/26 rewrites below 50%
- 0/26 final selected rewrites worse than draft
- Priya long billing/proration regression passed

## Next Rewrite Quality Fix — No Bad Result Gate

This section is the next required rewrite-engine priority. It exists because real user testing produced long customer-support rewrites where the draft measured around 89% AI-like signal and the rewrite measured 99-100%. That outcome must be treated as a product failure, even if earlier aggregate evaluation targets were met.

### Product Principle

Reply In My Voice must not present a rewrite as successful when the measured AI-like signal gets worse or remains above the current internal quality bar.

The core product value is not a generic rewrite. It is:

1. diagnose why the draft reads like AI-generated writing,
2. create a targeted rewrite plan,
3. produce a candidate rewrite,
4. measure the before/after writing signal,
5. repair the specific failure patterns,
6. return only a fact-safe, structurally send-ready result that improves the signal whenever possible.

If the system cannot produce an improved, fact-safe candidate within the allowed internal attempts, return a quality-failure/no-charge response rather than presenting a weak rewrite as successful.

### Hard Success Criteria For Production Requests

When Sapling/writing-signal scores are available, a user-visible successful rewrite must satisfy the current threshold rule:

- if the draft AI-like signal is above `NATURALNESS_THRESHOLD` (default 40%), the rewrite AI-like signal must be at or below that threshold.
- if the draft AI-like signal is already at or below `NATURALNESS_THRESHOLD`, the rewrite AI-like signal must not be higher than the draft.

Additionally:

- If `rewriteSignal >= draftSignal`, reject that candidate.
- If `draftSignal > NATURALNESS_THRESHOLD` and `rewriteSignal > NATURALNESS_THRESHOLD`, reject or repair that candidate.
- If all internal candidates fail these gates, return a quality-failure/no-charge response.
- Do not label the Naturalness Check as improved when the rewrite score is higher than the draft score.
- Do not charge user usage for provider/server errors or quality-gate failures.

If Sapling is unavailable or times out:

- return a quality-failure/no-charge response in the fact-reconstruct production route,
- show `Signal unavailable` if a Naturalness Check panel is visible,
- do not count the run as target-met in evaluation,
- do not use unavailable-score results to justify deployment quality.

### Required Signal-Aware Repair Flow

The next implementation must upgrade the rewrite workflow from a simple retry to a measured repair loop:

1. Measure the draft.
2. Diagnose the draft with tags such as:
   - stock support opening
   - customer-service macro voice
   - overly balanced structure
   - uniform paragraph rhythm
   - over-explained support wording
   - generic transition phrases
   - over-polished corporate phrasing
   - defensive hedging
   - too much summary-like structure
3. Generate a targeted candidate using scenario guardrails.
4. Measure the candidate.
5. If the candidate fails the hard success criteria, run a repair pass that receives:
   - draft score
   - candidate score
   - diagnosis tags
   - rejected candidate text
   - concrete failure reason
   - required facts to preserve
6. Measure repaired candidate.
7. Select the best candidate that passes quality gates and preserves facts.
8. If no strict passing candidate exists, generate a guaranteed facts-first structured fallback and run the same fact, structural, and Naturalness Check gates.
9. If the guaranteed fallback is still not fact-safe, structurally send-ready, or signal-safe, return a quality-failure/no-charge response rather than presenting a weak rewrite as successful.

Retries must be strategy changes, not blind repeats. For example, if a customer-support reply remains high, the repair should explicitly remove macro-like phrasing such as `I see how this can be confusing`, `From what you described`, `It seems`, `To help clarify`, and `For next steps`, while preserving the needed billing explanation and next step.

### Long-Text Evaluation Requirement

The next development run must expand evaluation beyond the previous 15-case set.

Minimum evaluation before push/deploy:

- At least 25 total measured cases.
- At least 10 long cases of 300-900 words.
- At least 5 long customer-support cases.
- At least 12 long support-policy/options cases before the next rewrite-quality deployment.
- Include the Priya billing/proration case that previously failed with `89 -> 99/100`.
- Include the Daniel course-transfer/refund regression with cohort dates, refund policy, options, quoted-summary risk, and no-change-without-confirmation facts.
- Include at least 3 cases where the first candidate fails and a repair pass must improve it.

For every case, update `docs/scenario-evaluation-results.md` with:

- scenario
- tone
- input word/character count
- diagnosis tags
- rewrite plan summary
- draft AI-like signal
- first candidate AI-like signal
- repair candidate AI-like signal when used
- final selected AI-like signal
- score change
- whether a candidate was rejected and why
- expected facts
- facts preserved
- unsupported facts introduced
- send-ready structural issues such as broken numbered lists, sentence-per-paragraph formatting, broken quote boundaries, and weak line-split paraphrasing
- final decision: pass/fail

### Deployment Gate

Do not push and deploy the next rewrite-engine update unless all of these are true:

- Average measured reduction is at least 30 points.
- At least 70% of measured rewrites satisfy the current 40% Naturalness Check threshold rule.
- 100% of measured cases avoid a final selected rewrite that is worse than the draft when scores are available.
- The Priya long customer-support regression case passes.
- The Daniel long support-policy regression case passes.
- 0 successful eval outputs contain broken numbered lists, sentence-per-paragraph formatting, broken quote boundaries, or weak line-split paraphrasing.
- All rejected-candidate behavior is documented in `docs/scenario-evaluation-results.md`.
- Unit tests cover candidate rejection, repair pass invocation, no-usage-charge on quality failure, and UI rendering for non-improved signal states.

Development evaluation may spend additional OpenAI and Sapling calls to find a reliable strategy. That is acceptable for this phase. However, the production API must remain bounded and must not run an unbounded loop per user request.

Production request cap for the next implementation:

- 1 draft writing-signal call
- up to 3 initial rewrite candidates
- up to 2 targeted repair candidates
- up to 5 rewrite writing-signal calls

If the bounded production loop cannot produce a candidate that passes fact, structural send-ready, and Naturalness Check gates, return a quality-failure/no-charge response. Do not return a weak fallback as a successful rewrite.

## Imported Claude Cowork project instructions
