# LearningOps V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a DB-backed daily LearningOps workflow that analyzes rewrite outcomes, records findings/candidates, and supports automatic push/deploy only when a qualified strategy promotion passes validation.

**Architecture:** Keep `RewriteLearningSample` as the raw source of truth. Add `LearningRun`, `LearningFinding`, and `StrategyCandidate` tables for scheduled analysis state, then refactor the memory script into reusable analyzer functions plus a daily CLI command. Production rewrite behavior remains code-based; database findings never hot-update prompts at request time.

**Tech Stack:** Next.js 15, Prisma/Postgres on Neon, TypeScript, Vitest, existing `tsx` scripts, Cloudflare/OpenNext deployment.

---

### Task 1: Data Model And Migration

**Files:**
- Modify: `prisma/schema.prisma`
- Create: `prisma/migrations/<timestamp>_add_learningops_tables/migration.sql`

- [ ] **Step 1: Add Prisma models**

Add `LearningRun`, `LearningFinding`, and `StrategyCandidate` models. Relations should connect findings to runs and strategy candidates to findings. Do not add a live production strategy-rule table.

- [ ] **Step 2: Create migration**

Create SQL tables with indexes on `createdAt`, `status`, `promotionDecision`, `scenario`, `failureType`, and `riskLevel`.

- [ ] **Step 3: Generate Prisma client**

Run: `npm run prisma:generate`

Expected: Prisma client generation succeeds.

### Task 2: LearningOps Analyzer Module

**Files:**
- Create: `lib/learningops.ts`
- Test: `tests/unit/learningops.test.ts`

- [ ] **Step 1: Write unit tests for analysis classification**

Cover these cases:

- no samples -> `digest_only`
- repeated high final signal -> `test-needed` or `code-change`
- worse-than-draft samples -> severe finding
- repaired successes -> reusable repair finding
- single weak failure -> not promotable

- [ ] **Step 2: Implement analyzer types and helpers**

Implement pure functions that accept rows shaped like `RewriteLearningSample` and return:

- summary metrics
- grouped scenario/tag stats
- findings
- strategy candidates
- promotion decision: `digest_only`, `docs_only`, `promoted_candidate`, or `blocked`

The module must not read `.env.local`, call providers, mutate files, or print user content.

- [ ] **Step 3: Verify unit tests**

Run: `npm run test -- tests/unit/learningops.test.ts`

Expected: tests pass.

### Task 3: Daily LearningOps Command

**Files:**
- Create: `scripts/learningops-run.ts`
- Modify: `package.json`
- Modify: `docs/rewrite-memory-digest.md` through command output during verification

- [ ] **Step 1: Add `learningops:run` script**

Add:

```json
"learningops:run": "tsx scripts/learningops-run.ts"
```

- [ ] **Step 2: Implement command**

Command behavior:

1. Load `.env.local` without printing values.
2. Read latest 500 `RewriteLearningSample` rows.
3. Analyze rows with `lib/learningops.ts`.
4. Insert one `LearningRun`.
5. Insert generated `LearningFinding` rows.
6. Insert generated `StrategyCandidate` rows.
7. Write/update `docs/rewrite-memory-digest.md` with summary and recommendations, without dumping raw user content.
8. Print only safe summary counts and the final decision.

- [ ] **Step 3: Verify command locally**

Run: `npm run learningops:run`

Expected: command succeeds or fails with a clear non-secret configuration message.

### Task 4: Documentation And Automation Contract

**Files:**
- Modify: `docs/next-development-brief.md`
- Modify: `docs/rewrite-learning-system.md`
- Create or Modify: `docs/learningops-runbook.md`

- [ ] **Step 1: Document the daily policy**

The docs must state:

```text
Run every 24 hours automatically.
Push/deploy automatically only when a qualified strategy promotion passes all gates.
Never deploy just because the scheduled job ran.
```

- [ ] **Step 2: Document source of learning**

Make clear:

- source is `RewriteLearningSample`
- `LearningRun`, `LearningFinding`, and `StrategyCandidate` are internal pipeline state
- production strategy changes only through code/test/push/deploy
- no DB hot prompt updates

### Task 5: Validation And GitHub/Deploy Gate

**Files:**
- Modify only files from Tasks 1-4 unless tests expose a narrow fix.

- [ ] **Step 1: Run validation**

Run:

```bash
npm run typecheck
npm run lint
npm run test
npm run build
npm run cf:build
```

Expected: all pass.

- [ ] **Step 2: Commit and push if validation passes**

Commit message:

```text
feat: add learningops strategy analysis pipeline
```

Push to `origin main`.

- [ ] **Step 3: Deploy only if validation passes**

Run:

```bash
npm run cf:deploy
```

Expected: deploy succeeds. If validation or deploy fails, do not force deploy; document the blocker.
