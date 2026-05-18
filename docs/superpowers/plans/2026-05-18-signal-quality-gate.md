# Signal Quality Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent Reply In My Voice from returning a rewrite as successful when the AI-like signal increases or fails to improve enough.

**Architecture:** Add a measured quality gate around rewrite candidate selection, then add a targeted repair pass that receives concrete failure reasons. Expand scenario evaluation with long customer-support cases and regression coverage for the Priya billing sample.

**Tech Stack:** Next.js App Router, TypeScript, Vitest, Playwright, OpenAI Chat Completions, Sapling writing-signal API, Cloudflare Workers/OpenNext.

---

### Task 1: Add Quality Gate Types And Tests

**Files:**
- Create: `lib/rewrite-quality-gate.ts`
- Create: `tests/unit/rewrite-quality-gate.test.ts`

- [ ] **Step 1: Write failing tests**

Create tests for:

- candidate passes when rewrite is below 50%
- candidate passes when rewrite is at least 30 points lower
- candidate fails when rewrite is higher than draft
- candidate fails when rewrite is above 50 and reduction is less than 30
- unavailable scores produce a neutral `signal_unavailable` status

- [ ] **Step 2: Run tests to verify failure**

Run:

```bash
npm run test -- tests/unit/rewrite-quality-gate.test.ts
```

Expected: fails because `lib/rewrite-quality-gate.ts` does not exist yet.

- [ ] **Step 3: Implement quality gate module**

Export:

- `evaluateSignalQuality({ draftPercent, rewritePercent })`
- result status values:
  - `pass_below_threshold`
  - `pass_reduction`
  - `fail_worse`
  - `fail_insufficient_reduction`
  - `signal_unavailable`
- `shouldRejectCandidate(result)`
- `shouldRepairCandidate(result)`

- [ ] **Step 4: Run tests**

Run:

```bash
npm run test -- tests/unit/rewrite-quality-gate.test.ts
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add lib/rewrite-quality-gate.ts tests/unit/rewrite-quality-gate.test.ts
git commit -m "test: add rewrite signal quality gate"
```

### Task 2: Add Targeted Repair Strategy

**Files:**
- Modify: `lib/openai.ts`
- Modify: `lib/rewrite-diagnosis.ts`
- Create: `tests/unit/rewrite-repair.test.ts`

- [ ] **Step 1: Write failing repair tests**

Tests must assert:

- a repair prompt receives draft score, candidate score, failure reason, diagnosis tags, and required facts
- customer-support repair instructions explicitly remove macro-like phrasing
- repair instructions preserve billing facts, dates, amounts, user counts, and next steps
- repair does not use banned user-facing terms or banned grep substrings

- [ ] **Step 2: Run tests to verify failure**

```bash
npm run test -- tests/unit/rewrite-repair.test.ts
```

Expected: fails because repair helpers are not implemented.

- [ ] **Step 3: Implement repair helpers**

Add a repair strategy that takes:

- original request
- rejected candidate text
- draft AI-like percent
- candidate AI-like percent
- diagnosis tags
- quality gate failure reason
- rewrite plan

The repair strategy must not be a blind retry. It must explicitly target the observed failure pattern.

- [ ] **Step 4: Run tests**

```bash
npm run test -- tests/unit/rewrite-repair.test.ts tests/unit/openai-output.test.ts
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add lib/openai.ts lib/rewrite-diagnosis.ts tests/unit/rewrite-repair.test.ts
git commit -m "feat: add targeted rewrite repair strategy"
```

### Task 3: Wire Quality Gate Into Rewrite Selection

**Files:**
- Modify: `lib/rewrite.ts`
- Modify: `lib/usage.ts` or the current usage-charging module if named differently
- Modify: `tests/unit/rewrite-quality.test.ts`

- [ ] **Step 1: Write failing selection tests**

Tests must cover:

- if first candidate is worse, repair is attempted
- if repaired candidate passes, final response uses repaired candidate
- if all candidates fail quality gates, API returns a safe failure result
- usage is not charged when all candidates fail quality gates
- unavailable Sapling score still returns a rewrite with `Signal unavailable` but does not count as evaluation target-met

- [ ] **Step 2: Run tests to verify failure**

```bash
npm run test -- tests/unit/rewrite-quality.test.ts
```

Expected: fails under current selection behavior because worse candidates can still be returned.

- [ ] **Step 3: Implement gated selection**

Change `rewriteWithOptimization` so it:

1. measures draft
2. generates up to 2 initial candidates
3. rejects candidates that are worse or insufficient
4. invokes up to 2 targeted repair candidates when needed
5. selects only a passing candidate
6. fails safely when no candidate passes
7. does not charge usage for safe-quality failure

- [ ] **Step 4: Run focused tests**

```bash
npm run test -- tests/unit/rewrite-quality.test.ts tests/unit/rewrite-quality-gate.test.ts tests/unit/rewrite-repair.test.ts
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add lib/rewrite.ts tests/unit/rewrite-quality.test.ts
git commit -m "feat: gate rewrite selection by measured signal"
```

### Task 4: Update API And UI Failure State

**Files:**
- Modify: `app/api/rewrite/route.ts`
- Modify: `components/app/rewrite-workspace.tsx`
- Modify: `tests/unit/workspace-copy.test.ts`
- Modify: `tests/e2e/app.spec.ts` or existing app e2e test file

- [ ] **Step 1: Write failing UI/API tests**

Tests must assert:

- API can return a safe quality failure without charging usage
- UI does not show `Lower AI-like signal` when rewrite score is higher
- UI shows a clear retry-oriented state for `Still high AI-like signal`
- UI still allows copying only when a successful rewritten text exists

- [ ] **Step 2: Run tests to verify failure**

```bash
npm run test -- tests/unit/workspace-copy.test.ts
npm run test:e2e
```

Expected: current UI does not handle the safe-failure state correctly.

- [ ] **Step 3: Implement safe failure rendering**

Use neutral wording:

- `Still high AI-like signal`
- `We could not produce a better version yet. Try again or adjust the draft.`

Do not imply success when the signal gets worse.

- [ ] **Step 4: Run tests**

```bash
npm run test -- tests/unit/workspace-copy.test.ts
npm run test:e2e
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add app/api/rewrite/route.ts components/app/rewrite-workspace.tsx tests/unit/workspace-copy.test.ts tests/e2e
git commit -m "feat: show safe failure when rewrite signal does not improve"
```

### Task 5: Expand Long-Case Evaluation

**Files:**
- Modify: `scripts/eval-scenarios.ts`
- Modify: `docs/scenario-evaluation-results.md`
- Modify: `docs/optimization-notes.md`

- [ ] **Step 1: Add evaluation cases**

Add at least:

- 25 total measured cases
- 10 long cases between 300 and 900 words
- 5 long customer-support cases
- Priya billing/proration regression case
- 3 cases where first candidate fails and repair improves the result

- [ ] **Step 2: Run evaluation**

Run with Node 22:

```bash
. "$HOME/.nvm/nvm.sh" && nvm use 22.13.1 >/dev/null && npm run eval:scenarios
```

Expected:

- `docs/scenario-evaluation-results.md` updates with all required fields.
- Priya regression final candidate is not worse than the draft.
- Unavailable Sapling scores are not counted as target-met.

- [ ] **Step 3: Update optimization notes**

Append the new round to `docs/optimization-notes.md` with:

- sample count
- long-case count
- average reduction
- percent below 50
- rejected-candidate count
- repair-success count
- remaining risks

- [ ] **Step 4: Commit**

```bash
git add scripts/eval-scenarios.ts docs/scenario-evaluation-results.md docs/optimization-notes.md
git commit -m "test: expand long-form rewrite evaluation"
```

### Task 6: Final Verification And Deployment

**Files:**
- Modify only if verification reveals a targeted issue.

- [ ] **Step 1: Run full verification**

```bash
npm run typecheck
npm run lint
npm run test
npm run build
npm run test:e2e
rg -n "humanizer|bypass|undetect|detector|evade" app components public lib
```

Expected:

- typecheck passes
- lint passes
- unit tests pass
- build passes
- e2e passes
- banned-term grep returns no matches

- [ ] **Step 2: Run Cloudflare build**

```bash
. "$HOME/.nvm/nvm.sh" && nvm use 22.13.1 >/dev/null && npm run cf:build
```

Expected: OpenNext build passes.

- [ ] **Step 3: Deploy only if gates pass**

```bash
. "$HOME/.nvm/nvm.sh" && nvm use 22.13.1 >/dev/null && npm run cf:deploy
```

Expected: deploys to `replyinmyvoice-app` and keeps existing Cloudflare vars.

- [ ] **Step 4: Smoke test production**

```bash
curl -sS https://replyinmyvoice.com/api/health/db
curl -I https://replyinmyvoice.com/app
```

Expected:

- DB health returns `{"ok":true}`
- `/app` redirects signed-out users to sign-in

- [ ] **Step 5: Commit and push final docs**

```bash
git status --short
git push origin main
```

Expected: all committed changes are pushed to GitHub.
