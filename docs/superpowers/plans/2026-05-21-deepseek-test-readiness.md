# DeepSeek Test Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the DeepSeek adaptive rewrite test window locally ready, then stop before any live provider or eval run.

**Architecture:** Add OpenAI-compatible provider routing, a parser/selector for the 100-case markdown corpus, and test-window budget/attempt-ledger primitives around the existing fact-reconstruct pipeline. Keep production defaults unchanged unless explicit test-window environment variables are set.

**Tech Stack:** TypeScript, Vitest, existing Next/Cloudflare runtime, OpenAI-compatible chat completions, Sapling writing-signal integration.

---

### Task 1: Provider Routing

**Files:**
- Create: `lib/openai-compatible.ts`
- Modify: `lib/rewrite-pipeline/model.ts`
- Modify: `lib/openai.ts`
- Modify: `.env.example`
- Test: `tests/unit/openai-compatible.test.ts`
- Test: `tests/unit/rewrite-pipeline-model.test.ts`

- [ ] Write failing tests that prove `OPENAI_BASE_URL=https://api.deepseek.com` routes chat completions to `https://api.deepseek.com/v1/chat/completions` and uses `DEEPSEEK_API_KEY` without logging or writing the key.
- [ ] Implement a small URL/key helper and replace hard-coded chat completion URLs in both model call paths.
- [ ] Add empty example environment variables only; do not add real keys.
- [ ] Run focused provider/model tests.

### Task 2: 100-Case Evaluation Loader

**Files:**
- Create: `lib/rewrite-eval-cases.ts`
- Modify: `scripts/eval-scenarios.ts`
- Test: `tests/unit/rewrite-email-eval-cases.test.ts`

- [ ] Write failing tests that parse `docs/rewrite-email-eval-cases-100.md` into 100 structured cases.
- [ ] Implement a strict markdown parser for the current synthetic case format.
- [ ] Add staged selection: smoke = 10, focused = 40, full = 100 when the markdown corpus is selected.
- [ ] Keep the old built-in eval cases available as a fallback.

### Task 3: Attempt Budget And Ledger Primitives

**Files:**
- Modify: `lib/rewrite-pipeline/types.ts`
- Modify: `lib/rewrite-pipeline/budget-manager.ts`
- Modify: `lib/rewrite-types.ts`
- Modify: `lib/rewrite-pipeline/pipeline.ts`
- Test: `tests/unit/rewrite-budget-manager.test.ts`
- Test: `tests/unit/rewrite-attempt-ledger.test.ts`

- [ ] Write failing tests for `REWRITE_TEST_WINDOW_MAX_ATTEMPTS=10`, clamped to a hard maximum of 10.
- [ ] Write failing tests for a typed attempt-ledger entry that stores attempt number, strategy, model role/name, thinking mode, candidate text, failure analysis, failure kinds, gate summaries, signal result, and next strategy decision.
- [ ] Implement the budget override without changing production defaults.
- [ ] Add the attempt-ledger type/helper and attach it to rewrite success/failure payloads as internal optimization metadata.

### Task 4: Offline Verification And Stop

**Files:**
- Modify: `docs/skill-run-log.md`

- [ ] Run focused Vitest files.
- [ ] Run `npm run typecheck`.
- [ ] Do not run `npm run eval:scenarios` against live providers.
- [ ] Do not call DeepSeek, OpenAI, Sapling, deploy, or run remote smoke tests.
- [ ] Append project skill-run log entries with verification evidence and limitations.
