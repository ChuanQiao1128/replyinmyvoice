# Admin Cost Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add request-level AI cost telemetry and an internal admin dashboard so Reply In My Voice can measure real OpenAI/Sapling cost, rewrite quality, user usage, and pricing risk.

**Architecture:** Store one aggregate `RewriteCostLog` row per user rewrite request and optional `RewriteProviderCall` rows per OpenAI/Sapling call. Instrument the adaptive rewrite pipeline through a small telemetry collector, then expose read-only admin pages protected by Clerk plus an explicit env allowlist.

**Tech Stack:** Next.js App Router, Clerk, Prisma/Postgres via Neon, Cloudflare Workers/OpenNext, OpenAI chat completions, Sapling aidetect, TypeScript, Tailwind.

---

## Context

Current system:

- `/api/rewrite` calls `rewriteWithFactReconstruct(input)`.
- Successful rewrites are charged through `chargeSuccessfulRewrite(user)`.
- Successful and quality-failed requests are logged to `RewriteLearningSample`.
- `RewriteLearningSample` already stores scenario, tone, text, signal scores, diagnosis tags, strategy counts, repair counts, and status.
- Current pipeline config already contains model role pricing defaults in `lib/rewrite-pipeline/config.ts`.
- OpenAI calls currently happen inside `lib/rewrite-pipeline/model.ts:createJsonCompletion`.
- Sapling calls currently happen inside `lib/writing-signal.ts:measureWritingSignal`.

Gap:

- The app does not currently store actual OpenAI token usage.
- The app does not store Sapling character/call usage per request.
- The operator cannot see average cost per successful rewrite or cost by scenario/user.
- There is no internal admin dashboard.

## Non-Goals

- Do not change the public rewrite UX.
- Do not change the public pricing plan in this task.
- Do not implement live Stripe mode here; that is covered by `docs/superpowers/plans/2026-05-20-stripe-live-mode-cutover.md`.
- Do not hot-load rewrite strategy changes from admin data.
- Do not expose admin pages in public navigation.
- Do not show an admin entry to non-admin users.

## Files

Create:

- `lib/admin-auth.ts` - checks whether the current Clerk user is allowed to access admin pages.
- `lib/observability/rewrite-cost.ts` - cost math and provider-call types.
- `lib/observability/rewrite-telemetry.ts` - per-request telemetry collector and DB persistence.
- `lib/admin/metrics.ts` - aggregate admin queries.
- `lib/admin-visible.ts` - pure helper for deciding whether the signed-in user should see an admin entry.
- `app/admin/page.tsx` - internal overview dashboard.
- `app/admin/rewrites/page.tsx` - recent rewrite request table.
- `app/admin/rewrites/[id]/page.tsx` - rewrite telemetry detail view.
- `components/admin/admin-shell.tsx` - simple internal layout.
- `components/admin/metric-card.tsx` - reusable metric card.
- `components/admin/rewrite-cost-table.tsx` - recent rewrite table.
- `components/app/admin-entry.tsx` - compact `/app` header entry that renders only for admin users.
- `tests/unit/rewrite-cost.test.ts` - cost math tests.
- `tests/unit/admin-auth.test.ts` - admin allowlist tests.
- `tests/unit/rewrite-telemetry.test.ts` - collector aggregation tests.

Modify:

- `prisma/schema.prisma` - add `RewriteCostLog` and `RewriteProviderCall`.
- `app/app/page.tsx` or the app workspace header component - render `AdminEntry` near account controls for admin users only.
- `lib/rewrite-pipeline/types.ts` - add optional telemetry observer types if needed.
- `lib/rewrite-pipeline/model.ts` - record OpenAI usage, model role, latency, success/failure.
- `lib/writing-signal.ts` - record Sapling characters, latency, success/failure.
- `lib/rewrite-pipeline/pipeline.ts` - accept and pass a telemetry collector.
- `app/api/rewrite/route.ts` - create telemetry collector, persist it on success and quality failure.
- `.env.example` - document admin and pricing env vars.
- `docs/manual-setup.md` - document admin setup and privacy boundary.
- `docs/next-development-brief.md` - keep aligned with this plan.

## Data Model

Add to `prisma/schema.prisma`:

```prisma
model RewriteCostLog {
  id                     String                @id @default(cuid())
  userId                 String?
  learningSampleId       String?
  requestId              String                @unique
  strategyVersion        String
  scenario               String
  tonePreset             String
  status                 String
  errorCode              String?
  startedAt              DateTime
  finishedAt             DateTime?
  durationMs             Int?
  inputCharCount         Int                   @default(0)
  draftWordCount         Int                   @default(0)
  rewriteWordCount       Int?
  draftAiLikePercent     Int?
  rewriteAiLikePercent   Int?
  changePoints           Int?
  internalStrategies     Int                   @default(0)
  repairCandidates       Int                   @default(0)
  rejectedCandidates     Int                   @default(0)
  usedEscalation         Boolean               @default(false)
  openAiInputTokens      Int                   @default(0)
  openAiOutputTokens     Int                   @default(0)
  openAiCostUsd          Decimal               @default(0)
  saplingCallCount       Int                   @default(0)
  saplingCharacters      Int                   @default(0)
  saplingCostUsd         Decimal               @default(0)
  totalEstimatedCostUsd  Decimal               @default(0)
  modelsUsedJson         String                @default("[]")
  providerCallsJson      String                @default("[]")
  createdAt              DateTime              @default(now())
  updatedAt              DateTime              @updatedAt
  providerCalls          RewriteProviderCall[]
  user                   User?                 @relation(fields: [userId], references: [id], onDelete: SetNull)

  @@index([createdAt])
  @@index([userId])
  @@index([status])
  @@index([scenario])
  @@index([totalEstimatedCostUsd])
}

model RewriteProviderCall {
  id               String          @id @default(cuid())
  costLogId        String
  provider         String
  role             String
  model            String?
  inputTokens      Int?
  outputTokens     Int?
  characters       Int?
  estimatedCostUsd Decimal         @default(0)
  latencyMs        Int?
  success          Boolean         @default(true)
  errorCode        String?
  createdAt        DateTime        @default(now())
  costLog          RewriteCostLog  @relation(fields: [costLogId], references: [id], onDelete: Cascade)

  @@index([costLogId])
  @@index([provider])
  @@index([role])
  @@index([createdAt])
}
```

Also add this relation to `User`:

```prisma
costLogs RewriteCostLog[]
```

Migration:

```bash
npm run prisma:generate
npx prisma migrate dev --name add_rewrite_cost_observability
```

For Cloudflare/prod deploy:

```bash
npx prisma migrate deploy
```

## Task 1: Cost Math Unit

**Files:**

- Create: `lib/observability/rewrite-cost.ts`
- Test: `tests/unit/rewrite-cost.test.ts`

- [ ] **Step 1: Write failing tests**

```ts
import { describe, expect, it } from "vitest";
import {
  estimateOpenAiCostUsd,
  estimateSaplingCostUsd,
  summarizeProviderCalls,
} from "../../lib/observability/rewrite-cost";

describe("rewrite cost estimation", () => {
  it("estimates OpenAI cost from input and output tokens", () => {
    expect(
      estimateOpenAiCostUsd({
        inputTokens: 2000,
        outputTokens: 500,
        inputPer1M: 0.75,
        outputPer1M: 4.5,
      }),
    ).toBeCloseTo(0.00375, 6);
  });

  it("estimates Sapling cost from characters", () => {
    expect(
      estimateSaplingCostUsd({
        characters: 3000,
        pricePer1000Chars: 0.005,
      }),
    ).toBeCloseTo(0.015, 6);
  });

  it("summarizes provider calls into aggregate totals", () => {
    const summary = summarizeProviderCalls([
      {
        provider: "openai",
        role: "mid_writer",
        model: "gpt-5.4-mini",
        inputTokens: 1000,
        outputTokens: 300,
        estimatedCostUsd: 0.0021,
        success: true,
      },
      {
        provider: "sapling",
        role: "draft_signal",
        characters: 2500,
        estimatedCostUsd: 0.0125,
        success: true,
      },
    ]);

    expect(summary.openAiInputTokens).toBe(1000);
    expect(summary.openAiOutputTokens).toBe(300);
    expect(summary.saplingCallCount).toBe(1);
    expect(summary.saplingCharacters).toBe(2500);
    expect(summary.totalEstimatedCostUsd).toBeCloseTo(0.0146, 6);
  });
});
```

- [ ] **Step 2: Implement cost helpers**

Export:

```ts
export type ProviderCallTelemetry = {
  provider: "openai" | "sapling";
  role: string;
  model?: string;
  inputTokens?: number;
  outputTokens?: number;
  characters?: number;
  estimatedCostUsd: number;
  latencyMs?: number;
  success: boolean;
  errorCode?: string;
};
```

Implement:

```ts
export function estimateOpenAiCostUsd(args: {
  inputTokens: number;
  outputTokens: number;
  inputPer1M: number;
  outputPer1M: number;
}) {
  return (
    (args.inputTokens / 1_000_000) * args.inputPer1M +
    (args.outputTokens / 1_000_000) * args.outputPer1M
  );
}

export function estimateSaplingCostUsd(args: {
  characters: number;
  pricePer1000Chars: number;
}) {
  return (args.characters / 1000) * args.pricePer1000Chars;
}
```

- [ ] **Step 3: Run tests**

Run:

```bash
npm test -- tests/unit/rewrite-cost.test.ts
```

Expected: the new tests pass.

## Task 2: Admin Authorization

**Files:**

- Create: `lib/admin-auth.ts`
- Test: `tests/unit/admin-auth.test.ts`

- [ ] **Step 1: Write tests**

Test pure helper behavior by exporting:

```ts
export function isAdminIdentityAllowed(args: {
  clerkUserId?: string | null;
  email?: string | null;
  adminEmails?: string;
  adminClerkUserIds?: string;
}): boolean
```

Cover:

- matching email grants access.
- matching Clerk user id grants access.
- blank env denies access.
- case-insensitive email match works.

- [ ] **Step 2: Implement `lib/admin-auth.ts`**

Use `currentUser()` from Clerk in a server-only function:

```ts
export async function requireAdminUser() {
  const user = await currentUser();
  if (!user) {
    notFound();
  }

  const email =
    user.primaryEmailAddress?.emailAddress ??
    user.emailAddresses.at(0)?.emailAddress ??
    null;

  if (
    !isAdminIdentityAllowed({
      clerkUserId: user.id,
      email,
      adminEmails: optionalEnv("ADMIN_EMAILS", ""),
      adminClerkUserIds: optionalEnv("ADMIN_CLERK_USER_IDS", ""),
    })
  ) {
    notFound();
  }

  return { clerkUserId: user.id, email };
}
```

Use `notFound()` instead of a visible "forbidden" page for non-admins.

## Task 3: Telemetry Collector

**Files:**

- Create: `lib/observability/rewrite-telemetry.ts`
- Modify: `lib/rewrite-pipeline/types.ts`
- Test: `tests/unit/rewrite-telemetry.test.ts`

- [ ] **Step 1: Define collector API**

Add:

```ts
export type RewriteTelemetryCollector = {
  requestId: string;
  startedAt: Date;
  recordProviderCall(call: ProviderCallTelemetry): void;
  finish(args: RewriteTelemetryFinishArgs): RewriteCostLogInsert;
};
```

`RewriteTelemetryFinishArgs` must include:

- `userId`
- `input`
- `status`
- `errorCode`
- `response`
- `qualityError`
- `strategyVersion`

- [ ] **Step 2: Implement DB persistence**

Export:

```ts
export async function persistRewriteCostLog(log: RewriteCostLogInsert) {
  // insert aggregate RewriteCostLog first
  // insert RewriteProviderCall rows second
}
```

Use raw SQL through `getSql()` to match current project style.

- [ ] **Step 3: Make persistence non-blocking for user experience**

Export:

```ts
export async function tryPersistRewriteCostLog(log: RewriteCostLogInsert) {
  try {
    await persistRewriteCostLog(log);
  } catch (error) {
    console.warn("rewrite_cost_log_failed", {
      message: error instanceof Error ? error.message.slice(0, 180) : "Unknown",
    });
  }
}
```

Do not let cost logging failure break a successful rewrite response.

## Task 4: Instrument OpenAI Calls

**Files:**

- Modify: `lib/rewrite-pipeline/model.ts`
- Modify: `lib/rewrite-pipeline/pipeline.ts`
- Test: existing pipeline/model unit tests plus new collector assertions where practical.

- [ ] **Step 1: Add optional telemetry argument to model calls**

Change `createJsonCompletion` to accept:

```ts
telemetry?: RewriteTelemetryCollector;
role: "cheap_structured" | "mid_writer" | "strong_escalation";
```

- [ ] **Step 2: Read OpenAI usage fields**

When parsing the OpenAI response, read:

```ts
usage?: {
  prompt_tokens?: number;
  completion_tokens?: number;
}
```

Record one provider call with:

- provider: `openai`
- role
- model
- inputTokens
- outputTokens
- estimatedCostUsd from role pricing
- latencyMs
- success

- [ ] **Step 3: Record failed OpenAI calls**

If OpenAI returns a non-2xx response or throws, record a failed provider call with:

- provider: `openai`
- role
- model
- estimatedCostUsd: `0`
- success: `false`
- errorCode: safe error type/status

Do not record request body or secret headers.

## Task 5: Instrument Sapling Calls

**Files:**

- Modify: `lib/writing-signal.ts`
- Modify: `lib/rewrite-pipeline/pipeline.ts`

- [ ] **Step 1: Add optional telemetry argument**

Change `measureWritingSignal` options to:

```ts
type MeasureWritingSignalOptions = {
  calibrateSentenceScores?: boolean;
  telemetry?: RewriteTelemetryCollector;
  role?: "draft_signal" | "candidate_signal" | "repair_signal" | "final_signal";
};
```

- [ ] **Step 2: Record Sapling usage**

For every Sapling attempt, record:

- provider: `sapling`
- role
- characters: `text.length`
- estimatedCostUsd using `SAPLING_PRICE_PER_1000_CHARS_USD`, default `0.005`
- latencyMs
- success
- errorCode on provider error, schema change, timeout, or network error

If retry happens, record each provider call separately.

## Task 6: Persist Telemetry From `/api/rewrite`

**Files:**

- Modify: `app/api/rewrite/route.ts`
- Modify: `lib/rewrite-learning.ts` if linking `learningSampleId` requires returning the inserted sample id.

- [ ] **Step 1: Create collector before quota check**

Create the collector after request validation and after the local user is known:

```ts
const telemetry = createRewriteTelemetryCollector();
```

- [ ] **Step 2: Pass collector into pipeline**

Call:

```ts
const rewrite = await rewriteWithFactReconstruct(input, { telemetry });
```

If changing the function signature is too broad, define an options object with only `telemetry` first.

- [ ] **Step 3: Persist success log**

After successful rewrite and learning sample logging, persist:

```ts
await tryPersistRewriteCostLog(
  telemetry.finish({
    userId: user.id,
    input,
    status: "success",
    response: rewrite,
    strategyVersion: rewrite.optimization.strategyVersion,
  }),
);
```

- [ ] **Step 4: Persist quality failure log**

In `FactReconstructQualityError` catch block, persist status `quality_failed` with no charge.

- [ ] **Step 5: Persist provider/server failure log where safe**

For unexpected provider/server errors after user validation, persist status `server_failed` and `errorCode`, without raw exception content.

## Task 7: Admin Data Queries

**Files:**

- Create: `lib/admin/metrics.ts`

Implement:

```ts
export async function getAdminOverviewMetrics()
export async function getRecentRewriteCostLogs(args: { limit: number; offset: number })
export async function getRewriteCostLogDetail(id: string)
```

Use SQL aggregates for:

- total requests today / 7 days / 30 days
- success count
- quality failure count
- provider failure count
- average signal drop
- below-50 final signal rate
- average successful request cost
- p95 request cost
- OpenAI/Sapling/total cost sums
- escalation rate

Do not select raw `roughDraftReply` or `rewrittenText` in overview/list queries.

## Task 8: Admin UI

**Files:**

- Create: `components/app/admin-entry.tsx`
- Create: `components/admin/admin-shell.tsx`
- Create: `components/admin/metric-card.tsx`
- Create: `components/admin/rewrite-cost-table.tsx`
- Create: `app/admin/page.tsx`
- Create: `app/admin/rewrites/page.tsx`
- Create: `app/admin/rewrites/[id]/page.tsx`
- Modify: `app/app/page.tsx` or the existing app header component

- [ ] **Step 1: Protect every admin route**

At the top of each admin server component:

```ts
await requireAdminUser();
```

- [ ] **Step 2: Add admin-only entry in the signed-in app header**

Render a compact `Admin` icon/button in the `/app` top header/account area only when the signed-in user is allowed by `ADMIN_EMAILS` or `ADMIN_CLERK_USER_IDS`.

Rules:

- Owner account `ADMIN_EMAILS=chuanqiao1128@gmail.com` should see the entry after sign-in.
- Non-admin signed-in users must not see the entry.
- Public pages must not show the entry.
- The entry links to `/admin`.
- Server-side authorization still protects `/admin`; hiding the button is only a UI convenience.

Suggested component:

```tsx
import Link from "next/link";
import { Settings } from "lucide-react";

type AdminEntryProps = {
  visible: boolean;
};

export function AdminEntry({ visible }: AdminEntryProps) {
  if (!visible) {
    return null;
  }

  return (
    <Link
      href="/admin"
      className="inline-flex h-10 items-center gap-2 rounded-md border border-line bg-paper px-3 text-sm font-semibold text-ink transition hover:border-clay hover:text-clay"
      title="Admin dashboard"
    >
      <Settings className="h-4 w-4" aria-hidden="true" />
      <span>Admin</span>
    </Link>
  );
}
```

- [ ] **Step 3: Build `/admin` overview**

Show cards:

- Requests 24h / 7d / 30d
- Success rate
- Quality failure rate
- Average AI-like signal drop
- Average cost per success
- P95 cost
- OpenAI cost
- Sapling cost
- Escalation rate

- [ ] **Step 4: Build `/admin/rewrites` table**

Columns:

- Date
- User
- Scenario
- Tone
- Status
- Draft %
- Rewrite %
- Change
- Strategies
- Repairs
- Escalation
- Cost
- Duration

- [ ] **Step 5: Build detail view**

Show provider-call breakdown. Only show raw input/output when:

```ts
optionalEnv("ADMIN_ALLOW_RAW_REWRITE_TEXT", "false") === "true"
```

If raw text is disabled, show:

```text
Raw rewrite text is hidden in this environment.
Set ADMIN_ALLOW_RAW_REWRITE_TEXT=true only for approved internal debugging.
```

## Task 9: Tests And Verification

Run:

```bash
npm test
npm run typecheck
npm run lint
npm run build
npm run cf:build
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

Expected:

- Unit tests pass.
- Typecheck passes.
- Build passes.
- Cloudflare build passes.
- Banned-term scan has no user-facing violations.

Manual local smoke:

```text
1. Sign in as admin.
2. Open /admin.
3. Confirm dashboard cards render.
4. Open /admin/rewrites.
5. Confirm recent rows render without raw text.
6. Open a detail page.
7. Confirm provider-call cost breakdown renders.
8. Sign in as non-admin or clear admin env.
9. Confirm /admin is not accessible.
```

Production smoke after deploy:

```text
1. Run one successful rewrite.
2. Confirm a RewriteCostLog row is created.
3. Confirm cost values are non-negative.
4. Confirm Sapling character count is greater than zero when Sapling is configured.
5. Confirm admin dashboard shows the new request.
```

## Task 10: Docs And Environment

**Files:**

- Modify: `.env.example`
- Modify: `docs/manual-setup.md`
- Modify: `docs/next-development-brief.md`

Add env docs:

```env
ADMIN_EMAILS=
ADMIN_CLERK_USER_IDS=
ADMIN_ALLOW_RAW_REWRITE_TEXT=false
SAPLING_PRICE_PER_1000_CHARS_USD=0.005
ADMIN_NZD_PER_USD=
```

Document:

- Admin pages are internal and not linked publicly.
- Raw user text is hidden by default.
- Costs are estimates, not accounting-grade invoices.
- The pricing panel is for internal plan decisions.

## Acceptance Criteria

- Every successful and quality-failed rewrite writes a request-level `RewriteCostLog`.
- OpenAI token usage is captured when returned by the API.
- Sapling character count and call count are captured.
- Admin pages require explicit allowlist access.
- `/admin` shows aggregate cost and quality metrics.
- `/admin/rewrites` shows recent request rows.
- `/admin/rewrites/[id]` shows provider-call breakdown.
- Raw text is hidden unless explicitly enabled.
- Tests, typecheck, lint, Next build, and Cloudflare build pass.
- GitHub push and Cloudflare deploy happen only after verification passes.
