# Unified Fact-Preserving Rewrite Long-Run Target

Date: 2026-05-19

## Execution Result

Latest completed run on 2026-05-19:

```text
Evaluation cases: 66
Draft-only cases: 44
Customer-usable pass count: 66/66
Fact preservation or unsupported-addition failures: 0
Final selected rewrites worse than draft: 0/66
Strict signal pass count: 42/66
Average AI-like signal drop: 50 points
```

The run split customer-usable pass from strict Sapling signal pass. Strict signal misses remain optimization work, but the release gate for known samples is now fact preservation, no unsupported facts, no quality failure, and no worse selected signal.

## Purpose

This document defines the next long autonomous development run for **Reply In My Voice**.

The run is not planning-only. Once the user explicitly starts the long run, the agent should continue until the rewrite-quality target is implemented, tested, learned from, deployed, and remotely verified, or until a stop condition in this document is reached.

This document is the AutoRun target. The detailed implementation plan is:

```text
/Users/qc/Desktop/CloudFlare/docs/superpowers/plans/2026-05-19-unified-fact-preserving-rewrite.md
```

## Primary Goal

Make the core rewrite product reliable for the simplest user workflow:

```text
Optional context/message + required draft -> lower AI-like signal, preserved facts, no unsupported additions, natural Warm or Direct rewrite.
```

The next run must focus on the core rewrite experience, not new billing, Stripe, Azure infrastructure, or extra product features.

## Required Product Changes

- Remove user-facing scenario selection from the main `/app` workflow.
- Keep `Context or message` optional.
- Keep `Draft to rewrite` required.
- Keep the product reply-focused, but make draft-only usage a first-class tested path.
- Replace the four visible tones with exactly:
  - `Warm`
  - `Direct`
- Default tone must be `Warm`.
- `Direct` means less padding and fewer softeners, not fewer facts.
- Homepage examples may continue to mention teacher, sales, workplace, and client/customer scenarios as marketing examples.
- Do not advertise cover letters as a core workflow until the reply workflow is stable.

## Required Rewrite Architecture

The rewrite engine must use this flow:

```text
1. Normalize input.
2. Extract required facts from all user-provided text.
3. Infer internal mode only for style/risk guardrails.
4. Generate Warm or Direct rewrite candidates.
5. Check fact preservation and unsupported fact additions.
6. Repair candidates with explicit missing/unsupported facts.
7. If needed, compare escalation model behavior in learning mode.
8. Run deterministic/plain-language cleanup as final safety.
9. Return a fact-preserving result or a non-charged quality failure when no safe result exists.
10. Log learning samples and update strategy memory.
```

Important rule:

```text
Fact extraction is unified. Do not decide which facts matter based on scenario.
```

Internal scenario/mode inference may still exist for guardrails such as teacher tone, customer-support risk, sales promise risk, and workplace clarity. It must not be used to drop or ignore facts.

## Required Fact Gate

The system must extract and preserve at least these categories when present:

- people and names
- roles
- dates
- deadlines
- money/amounts
- counts
- tasks
- ordered steps
- constraints
- promises and non-promises
- policy limits
- signoffs/sender identity
- quoted or must-keep phrases

Candidate selection must reject or repair:

- missing required facts
- unsupported new facts
- invented names, dates, amounts, meetings, policies, outcomes, discounts, refunds, approvals, or deadlines
- malformed output
- over-compressed output that stops answering the situation
- `100% -> 100%` weak fallback treated as clean success

## Testing And Learning Goal

The next run must use testing as a learning loop, not a one-time pass.

For every failed sample:

```text
1. classify the failure
2. record it
3. compare primary model, repair strategy, escalation model, and deterministic cleanup where useful
4. decide whether the root cause is strategy, fact extraction, final selection, model capability, or provider failure
5. add or update regression tests
6. update rewrite strategy memory
7. fix code/prompt/fallback/model routing
8. rerun the full evaluation suite
```

Testing results must become durable project learning. Update the relevant docs:

```text
docs/optimization-notes.md
docs/rewrite-strategy-memory.md
docs/rewrite-learning-system.md
docs/scenario-evaluation-results.md or a new dated evaluation result doc
AGENTS.md only for stable future-development rules
```

## Required Evaluation Bar

The system cannot honestly guarantee perfect success for arbitrary future user input, provider outages, or ambiguous drafts.

The required release gate for this run is concrete:

```text
Minimum seed evaluation cases: 40
Minimum final evaluation cases before deploy: 60
Minimum draft-only cases before deploy: 40
Known-suite customer-usable pass rate before deploy: 100%
Unresolved known failures before deploy: 0
Final selected rewrites worse than draft: 0
Fact preservation failures: 0
Unsupported fact additions: 0
```

If a new failure appears during development or manual QA, add it to the known suite before release. Do not deploy as final while any known sample fails.

Definition of a customer-usable pass:

- required facts preserved
- no unsupported facts added
- no malformed output
- tone matches `Warm` or `Direct`
- output is meaningfully different from the input unless the input is already strong
- if Sapling score is available, final output is not worse than the draft
- target is below 50% AI-like signal or at least 30 points lower when feasible
- if signal remains high, output still preserves facts and includes an honest review note

## Model Escalation Target

Current rewrite model:

```text
OPENAI_MODEL=gpt-4o-mini
```

The next run must introduce explicit model tiers:

```env
OPENAI_MODEL_PRIMARY=gpt-4o-mini
OPENAI_MODEL_REPAIR=gpt-4o-mini
OPENAI_MODEL_ESCALATION=gpt-5.4-mini
OPENAI_MODEL_FINAL_STRONG=gpt-5.4
OPENAI_MAX_MODEL_CALLS_PER_REWRITE=3
OPENAI_ENABLE_FINAL_STRONG_MODEL=false
```

Testing/learning mode may compare more attempts to learn root causes. Production behavior must remain bounded:

```text
1. primary rewrite
2. primary repair
3. escalation rewrite/repair only under strict failure conditions
4. deterministic/plain-language cleanup
```

Only promote escalation when repeated failures show strategy/fact-gate fixes are not enough and the escalation model improves the same failure class.

## Required Test Samples

Use the detailed 40 seed samples and at least 20 failure-driven expansion samples from:

```text
docs/superpowers/plans/2026-05-19-unified-fact-preserving-rewrite.md
```

The final suite must include at least 40 draft-only cases because many real users will paste only their own draft.

## Required Verification

Before final deployment, run:

```bash
npm test
npm run typecheck
npm run build
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

Also run:

- local manual smoke for teacher, support, sales, workplace, and draft-only samples
- full evaluation suite with Warm and Direct where required
- learning digest or equivalent learning summary after enough samples exist
- GitHub Actions verification after push
- Cloudflare deployment verification
- active backend workflow verification if still enabled
- remote smoke tests

Remote smoke commands:

```bash
curl -sS -o /dev/null -w '%{http_code} %{url_effective}\n' https://replyinmyvoice.com/
curl -sS -o /dev/null -w '%{http_code} %{url_effective}\n' https://replyinmyvoice.com/pricing
curl -sS -o /dev/null -w '%{http_code} %{redirect_url}\n' https://replyinmyvoice.com/app
curl -sS -w '\n%{http_code}\n' https://replyinmyvoice.com/api/health/db
```

If a separate Azure backend URL is active in the current deployment config, run its health endpoint smoke test and record the result.

## Deployment Goal

When the known-suite quality bar is met:

- commit meaningful changes
- push to GitHub
- wait for GitHub Actions
- deploy to Cloudflare
- verify remote production behavior
- confirm active backend workflow remains green if enabled
- document the evaluation/deployment result

Do not rush deployment before the known-suite pass rate is 100%.

## Stop Conditions

Stop and ask the user only if:

- a required secret is missing, invalid, or cannot be derived from existing local configuration
- GitHub push, GitHub Actions, or repository configuration is denied
- Cloudflare deployment permission is denied
- an active backend deployment permission is denied and no CLI workaround is available
- a real paid/live-mode financial action is required
- a production domain cutover is required
- continuing would expose, print, commit, or otherwise leak secrets
- the user explicitly interrupts or pauses the run

Ordinary build failures, test failures, bad rewrite outputs, low eval pass rate, provider errors, package issues, Cloudflare adapter issues, or deployment command issues are not stop conditions. Investigate, fix, document learning, and continue.

## Secret Handling

Never print, quote, summarize, commit, or expose secret values from:

- `.env.local`
- `.dev.vars`
- `globalapikey/`
- OpenAI keys
- Sapling keys
- Clerk secrets
- Stripe secrets
- Cloudflare tokens
- Azure credentials
- GitHub secrets

Commands may check whether values exist, but output must be boolean/status only.

## Completion Criteria

The run is complete only when:

- product surface is simplified to no visible scenario selector and two tones
- unified fact extraction exists and is used by rewrite quality gates
- draft-only usage is first-class and heavily evaluated
- model escalation is configured and tested in learning mode
- all known evaluation cases pass
- no known fact preservation failures remain
- no known unsupported fact additions remain
- learning docs are updated
- local verification passes
- GitHub Actions pass
- Cloudflare deployment succeeds
- remote smoke passes
- final result and remaining risks are documented
