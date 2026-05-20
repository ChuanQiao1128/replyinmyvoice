# Clean Final Quality Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent fact/reviewer/meta language from leaking into final emails, improve first-click success by adding a clean-final gate and bounded internal repairs, then verify, push, and deploy.

**Architecture:** Keep the current fact-reconstruct pipeline. Add a deterministic clean-final checker before any rewrite is returned, then route clean-final failures through targeted repair or strong escalation using the same no-charge quality gate. Sapling remains a reference gate only; sentence scores are used internally for targeted repair and are not shown to users.

**Tech Stack:** Next.js 15 App Router, TypeScript, Vitest, OpenAI role-based models, Sapling writing signal, Cloudflare Workers/OpenNext, Neon/Prisma.

---

## Problem Statement

The current pipeline can lower the Naturalness Check signal, but it can still return a sentence such as:

```text
The May 8 client handover is referenced.
```

This is not a fact loss and not an unsupported amount/name/date, so the current fact gate can miss it. It is also not guaranteed to be caught by Sapling because Sapling only provides a writing signal, not product-specific sendability judgment.

The next version must reject or repair final emails that contain internal analysis language, extracted-fact labels, reviewer-note wording, or awkward isolated fact sentences.

## Product Rule

One user click may run multiple bounded internal attempts, but the user should receive either:

- a fact-safe, send-ready rewrite that passes the Naturalness Check quality bar; or
- a no-charge quality failure.

Do not create an unbounded loop. Do not tell the model to optimize for a detector or a specific Sapling score. Do not expose sentence-level Sapling data to users.

## Files To Modify

- Modify: `lib/rewrite-pipeline/checks.ts`
  - Add deterministic clean-final checks.
  - Return machine-readable issues such as `meta_language:fact_reference` and `awkward_sentence:isolated_fact_label`.

- Modify: `lib/rewrite-pipeline/model.ts`
  - Tighten reviewer, finalizer, targeted repair, and escalation prompts so generated output must not include internal analysis wording.
  - Add an optional LLM sendability check only if deterministic checks are not enough in tests.

- Modify: `lib/rewrite-pipeline/pipeline.ts`
  - Run clean-final checks before returning any passed response.
  - Route clean-final failures through targeted repair first, then strong escalation, then fallback.
  - Include clean-final failures in `candidateSignals.reason` and in repair notes.

- Modify: `lib/rewrite-pipeline/types.ts`
  - Add a clean-final issue type only if the implementation needs structured issue metadata beyond strings.

- Modify: `tests/unit/rewrite-pipeline-checks.test.ts`
  - Add deterministic tests for meta/reviewer/fact-label leakage.

- Modify: `tests/unit/rewrite-pipeline.test.ts`
  - Add pipeline tests proving a Sapling-passing rewrite is still rejected or repaired if clean-final checks fail.

- Modify: `tests/unit/rewrite-pipeline-model.test.ts`
  - Add prompt-contract tests proving finalizer/repair/escalation prompts explicitly forbid internal note leakage.

- Modify: `docs/rewrite-strategy-memory.md`
  - Promote the Priya billing regression lesson.

- Modify: `docs/rewrite-learning-system.md`
  - Document the clean-final gate as part of production quality learning.

- Modify: `docs/scenario-evaluation-results.md`
  - Record the post-fix focused eval result.

## Task 1: Add Deterministic Clean-Final Checks

**Files:**
- Modify: `lib/rewrite-pipeline/checks.ts`
- Test: `tests/unit/rewrite-pipeline-checks.test.ts`

- [ ] **Step 1: Write failing tests for meta-language leakage**

Add tests that fail until the checker rejects these phrases:

```ts
expect(
  deterministicCheck(input, facts, "The May 8 client handover is referenced.", styleCard)
    .issues,
).toContain("meta_language:fact_reference");

expect(
  deterministicCheck(input, facts, "Based on the provided context, the issue appears to be billing.", styleCard)
    .issues,
).toContain("meta_language:provided_context");

expect(
  deterministicCheck(input, facts, "The source says there are 18 active seats.", styleCard)
    .issues,
).toContain("meta_language:source_reference");
```

- [ ] **Step 2: Run the targeted test and confirm failure**

Run:

```bash
npm test -- tests/unit/rewrite-pipeline-checks.test.ts
```

Expected before implementation: at least one new test fails because the issue is not reported.

- [ ] **Step 3: Implement deterministic patterns**

Add a helper in `checks.ts`:

```ts
function detectMetaLanguage(text: string) {
  const patterns: Array<[RegExp, string]> = [
    [/\b(?:is|was|were)\s+(?:referenced|mentioned|provided|included|stated)\b/i, "meta_language:fact_reference"],
    [/\bbased on (?:the )?(?:provided )?(?:context|information|details)\b/i, "meta_language:provided_context"],
    [/\b(?:the )?(?:source|draft|original|input|facts?|context)\s+(?:says|states|mentions|indicates|notes)\b/i, "meta_language:source_reference"],
    [/\bextracted facts?\b/i, "meta_language:extracted_facts"],
    [/\breviewer notes?\b/i, "meta_language:reviewer_notes"],
  ];

  return patterns
    .filter(([pattern]) => pattern.test(text))
    .map(([, issue]) => issue);
}
```

Append those issues to the existing deterministic issue list.

- [ ] **Step 4: Verify the targeted test passes**

Run:

```bash
npm test -- tests/unit/rewrite-pipeline-checks.test.ts
```

Expected: all tests in the file pass.

## Task 2: Make Pipeline Repair Clean-Final Failures Before Returning

**Files:**
- Modify: `lib/rewrite-pipeline/pipeline.ts`
- Test: `tests/unit/rewrite-pipeline.test.ts`

- [ ] **Step 1: Write a failing pipeline test**

Mock the first final email to pass Sapling but fail deterministic clean-final checks:

```text
The May 8 client handover is referenced.
```

Expected behavior:

- the pipeline must not return that text;
- it must attempt repair or escalation;
- the returned text must not contain `is referenced`;
- `candidateSignals` must include a rejected issue reason related to clean-final quality.

- [ ] **Step 2: Run the targeted test and confirm failure**

Run:

```bash
npm test -- tests/unit/rewrite-pipeline.test.ts
```

Expected before implementation: the pipeline returns the Sapling-passing text too early.

- [ ] **Step 3: Refactor return paths through a quality helper**

Add a small helper inside `pipeline.ts` or near existing gate helpers:

```ts
function finalQualityPasses({
  deterministicSafe,
  factCheck,
}: {
  deterministicSafe: boolean;
  factCheck: LlmFactCheckResult;
}) {
  return factCheckSafe({
    deterministicSafe,
    llmResult: factCheck,
  });
}
```

Then ensure every successful return path has already run:

```ts
const deterministic = deterministicCheck(input, facts, candidateText, styleCard);
const factCheck = await llmFactCheck({ facts, finalEmail: candidateText, config });

if (!finalQualityPasses({ deterministicSafe: deterministic.safe, factCheck })) {
  // repair, escalate, fallback, or throw no-charge quality failure
}
```

This must apply to:

- initial final candidate
- targeted repair result
- strong escalation result
- deterministic fallback result

- [ ] **Step 4: Pass deterministic issues into repair notes**

When escalation or targeted repair runs after clean-final failure, include deterministic issues such as:

```ts
reviewNotes: [
  ...review.required_edits,
  ...review.risk_notes,
  ...deterministic.issues,
  ...factCheck.issues,
  ...factCheck.required_repairs,
]
```

- [ ] **Step 5: Verify pipeline tests pass**

Run:

```bash
npm test -- tests/unit/rewrite-pipeline.test.ts
```

Expected: the new regression test passes and existing pipeline tests still pass.

## Task 3: Tighten Model Prompt Contracts

**Files:**
- Modify: `lib/rewrite-pipeline/model.ts`
- Test: `tests/unit/rewrite-pipeline-model.test.ts`

- [ ] **Step 1: Write prompt-contract tests**

Assert the finalizer, targeted repair, and escalation prompts include these prohibitions:

```text
Do not mention extracted facts, reviewer notes, source text, original input, provided context, or internal analysis.
Do not write sentences such as "X is referenced", "the source says", or "the facts indicate".
```

- [ ] **Step 2: Run the model prompt tests and confirm failure**

Run:

```bash
npm test -- tests/unit/rewrite-pipeline-model.test.ts
```

Expected before implementation: the exact prompt-contract assertions fail.

- [ ] **Step 3: Update prompt text**

Add the prohibition to:

- `finalizeCandidate`
- `diagnoseHighRiskSentences`
- `repairHighRiskSentences`
- `escalateCandidate`

The wording must preserve the existing compliance boundary:

```text
Do not mention third-party scoring tools, scores, thresholds, detectors, or internal analysis.
```

- [ ] **Step 4: Verify model prompt tests pass**

Run:

```bash
npm test -- tests/unit/rewrite-pipeline-model.test.ts
```

Expected: all prompt-contract tests pass.

## Task 4: Add Priya Regression Coverage

**Files:**
- Modify: `tests/unit/rewrite-pipeline-checks.test.ts`
- Modify: `tests/unit/fact-extraction.test.ts` only if required facts are missing
- Modify: `docs/rewrite-strategy-memory.md`

- [ ] **Step 1: Add the Priya clean-final regression**

Use a compact version of the real failure:

```text
The May 8 client handover is referenced. If those three contractor accounts were not removed after the May 8 client handover, they may still be included as active users.
```

Expected:

- the first sentence is rejected;
- the second sentence is allowed.

- [ ] **Step 2: Add fact preservation checks for the Priya scenario**

The required facts must include:

- `Priya`
- `three temporary contractor accounts`
- `first week of May`
- `18 active seats`
- `15 regular seats`
- `NZD $126`
- `prorated seat charge`
- `May 8 client handover`
- `will not make any changes unless explicitly asked`

- [ ] **Step 3: Promote the lesson to strategy memory**

Add this lesson to `docs/rewrite-strategy-memory.md`:

```text
Clean-final gate lesson: A sentence can preserve a fact but still be unsendable if it describes the fact as internal analysis, for example "The May 8 client handover is referenced." These sentences must be repaired into direct customer-facing wording or rejected before display.
```

## Task 5: Run Focused Evaluation With Realistic Draft-Only Cases

**Files:**
- Modify: `docs/scenario-evaluation-results.md`
- Modify: eval fixtures only if the current fixture set does not include Priya-style billing/support cases

- [ ] **Step 1: Run unit verification**

Run:

```bash
npm run lint
npm run typecheck
npm test
```

Expected: all pass.

- [ ] **Step 2: Run build verification**

Run:

```bash
npm run build
```

Expected: production build passes.

- [ ] **Step 3: Run focused scenario eval**

Run:

```bash
npm run eval:scenarios
```

Expected target:

- at least 40 measured cases complete;
- no provider-unavailable cases counted as success;
- average AI-like signal reduction remains at least 30 points;
- majority of measured rewrites remain below 50%;
- no final selected rewrite contains clean-final meta-language issues;
- Priya billing/support regression passes.

- [ ] **Step 4: Update evaluation report**

Update `docs/scenario-evaluation-results.md` with:

- measured case count
- average signal reduction
- below-threshold count
- clean-final rejection/repair count
- targeted repair count
- strong escalation count
- Priya regression result

## Task 6: Deploy Only After Gates Pass

**Files:**
- Modify: `docs/rewrite-learning-system.md`
- Modify: `docs/scenario-evaluation-results.md`

- [ ] **Step 1: Run banned-term grep**

Run:

```bash
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

Expected: no user-facing violations.

- [ ] **Step 2: Commit only relevant files**

Run:

```bash
git status --short
git add lib/rewrite-pipeline/checks.ts lib/rewrite-pipeline/model.ts lib/rewrite-pipeline/pipeline.ts lib/rewrite-pipeline/types.ts tests/unit/rewrite-pipeline-checks.test.ts tests/unit/rewrite-pipeline.test.ts tests/unit/rewrite-pipeline-model.test.ts tests/unit/fact-extraction.test.ts docs/rewrite-strategy-memory.md docs/rewrite-learning-system.md docs/scenario-evaluation-results.md
git commit -m "Add clean final rewrite quality gate"
```

Do not stage unrelated `backend-dotnet` dirty files unless the user explicitly asks.

- [ ] **Step 3: Push and wait for CI/CD**

Run:

```bash
git push origin main
gh run list --limit 5 --json databaseId,name,status,conclusion,headSha,url
```

Expected: Cloudflare Worker workflow and related checks pass on the pushed commit.

- [ ] **Step 4: Verify production routes**

Run:

```bash
curl -sS -o /tmp/rimv_home.html -w '%{http_code}\n' https://replyinmyvoice.com/
curl -sS -o /tmp/rimv_pricing.html -w '%{http_code}\n' https://replyinmyvoice.com/pricing
curl -sS -o /tmp/rimv_app.html -w '%{http_code} %{redirect_url}\n' https://replyinmyvoice.com/app
curl -sS -w '\n%{http_code}\n' https://replyinmyvoice.com/api/health/db
curl -sS -X POST https://replyinmyvoice.com/api/rewrite -H 'Origin: https://replyinmyvoice.com' -H 'Content-Type: application/json' --data '{"roughDraftReply":"Hello, this is a test draft.","tone":"warm"}' -w '\n%{http_code}\n'
```

Expected:

- `/` returns `200`
- `/pricing` returns `200`
- `/app` redirects to `/sign-in`
- `/api/health/db` returns `200`
- unauthenticated `/api/rewrite` returns `401`

## Stop Conditions

Do not stop for:

- failing tests
- TypeScript errors
- model JSON shape issues
- Sapling score misses
- poor first candidates
- Cloudflare build/deploy command errors

Stop and ask the user only if:

- OpenAI credentials are invalid and no configured model works;
- Sapling credentials are invalid or unavailable long enough that evaluation cannot run;
- GitHub push permission is denied;
- Cloudflare deploy permission is denied;
- a real paid Stripe live-mode action or real charge is required;
- an operation would expose, print, or commit secrets.

## Completion Criteria

The next run is complete only when:

- clean-final deterministic checks reject internal/meta/reviewer language;
- the Priya regression is covered by tests;
- successful pipeline returns cannot bypass the clean-final gate;
- repair/escalation prompts forbid internal analysis leakage;
- focused eval has no clean-final meta-language outputs;
- `npm run lint`, `npm run typecheck`, `npm test`, and `npm run build` pass;
- docs are updated with the lesson;
- code is pushed to GitHub;
- Cloudflare deployment succeeds;
- `https://replyinmyvoice.com` smoke tests pass.
