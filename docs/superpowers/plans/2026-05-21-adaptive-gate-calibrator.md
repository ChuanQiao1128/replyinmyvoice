# Adaptive Gate Calibrator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce false `Facts need another pass` failures by separating hard fact failures from soft gate false positives before returning quality failure.

**Architecture:** Add an adaptive gate layer on top of the existing deterministic fact/structure checks. The deterministic checker still finds issues, but the adaptive gate classifies each issue as hard-blocking or soft/adjudicated, then the rewrite pipeline uses that result for pass/fail and diagnostics.

**Tech Stack:** TypeScript, Vitest, existing Next.js rewrite pipeline, Sapling/OpenAI telemetry unchanged.

---

### Task 1: Add Adaptive Gate Tests

**Files:**
- Test: `/Users/qc/Desktop/CloudFlare/tests/unit/rewrite-pipeline-checks.test.ts`

- [x] Write failing tests for:
  - equivalent facts such as `no action required` vs `no action is required`;
  - generic support signoff and greeting variants not blocking otherwise valid output;
  - real hard facts still blocking, including amounts, dates, deadlines, names, policy conditions, and unsupported amounts.

- [x] Run focused tests and confirm the new tests fail before implementation.

### Task 2: Implement Adaptive Gate

**Files:**
- Modify: `/Users/qc/Desktop/CloudFlare/lib/rewrite-pipeline/checks.ts`
- Modify: `/Users/qc/Desktop/CloudFlare/lib/rewrite-pipeline/pipeline.ts`

- [x] Add `AdaptiveGateIssue` and `AdaptiveGateResult`.
- [x] Add hard/soft classification for deterministic issues.
- [x] Keep structure, meta-language, malformed closing, deadlines, dates, amounts, counts, names, policies, and unsupported concrete facts as hard-blocking.
- [x] Treat generic support-team signoffs, greeting variants, apology phrases, and known equivalent phrasing as soft or adjudicated.
- [x] Replace direct `deterministic.safe` pipeline decisions with `adaptive.safe`.

### Task 3: Add Live Failure Regression Coverage

**Files:**
- Modify: `/Users/qc/Desktop/CloudFlare/tests/unit/rewrite-pipeline-checks.test.ts`
- Modify: `/Users/qc/Desktop/CloudFlare/tests/unit/openai-output.test.ts`
- Modify: `/Users/qc/Desktop/CloudFlare/docs/rewrite-strategy-memory.md`

- [x] Add the package-delay support failure as a replay-style regression.
- [x] Add a second customer-support soft-gate regression that preserves facts with different wording.
- [x] Record the Adaptive Gate rule in strategy memory.

### Task 4: Verify, Deploy, Push

**Files:**
- All touched files above.

- [ ] Run focused unit tests.
- [ ] Run rewrite pipeline tests.
- [ ] Run typecheck.
- [ ] Run production build/deploy to Cloudflare.
- [ ] Smoke test online login redirect and DB health.
- [ ] Commit only relevant files and push `main`.
