-- Make Stripe webhook processing retry-safe.
ALTER TABLE "StripeEvent"
  ADD COLUMN IF NOT EXISTS "status" TEXT NOT NULL DEFAULT 'processed',
  ADD COLUMN IF NOT EXISTS "processedAt" TIMESTAMP(3),
  ADD COLUMN IF NOT EXISTS "failedAt" TIMESTAMP(3),
  ADD COLUMN IF NOT EXISTS "lastError" TEXT,
  ADD COLUMN IF NOT EXISTS "attemptCount" INTEGER NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS "stripeMode" TEXT NOT NULL DEFAULT 'unknown',
  ADD COLUMN IF NOT EXISTS "updatedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP;

UPDATE "StripeEvent"
SET
  "status" = COALESCE(NULLIF("status", ''), 'processed'),
  "processedAt" = COALESCE("processedAt", "createdAt"),
  "attemptCount" = GREATEST("attemptCount", 1),
  "updatedAt" = CURRENT_TIMESTAMP;

CREATE INDEX IF NOT EXISTS "StripeEvent_status_idx" ON "StripeEvent"("status");
CREATE INDEX IF NOT EXISTS "StripeEvent_type_idx" ON "StripeEvent"("type");
CREATE INDEX IF NOT EXISTS "StripeEvent_createdAt_idx" ON "StripeEvent"("createdAt");

-- Request-level rewrite cost telemetry for admin observability.
CREATE TABLE IF NOT EXISTS "RewriteCostLog" (
  "id" TEXT NOT NULL,
  "userId" TEXT,
  "learningSampleId" TEXT,
  "requestId" TEXT NOT NULL,
  "strategyVersion" TEXT NOT NULL,
  "scenario" TEXT NOT NULL,
  "tonePreset" TEXT NOT NULL,
  "status" TEXT NOT NULL,
  "errorCode" TEXT,
  "startedAt" TIMESTAMP(3) NOT NULL,
  "finishedAt" TIMESTAMP(3),
  "durationMs" INTEGER,
  "inputCharCount" INTEGER NOT NULL DEFAULT 0,
  "draftWordCount" INTEGER NOT NULL DEFAULT 0,
  "rewriteWordCount" INTEGER,
  "draftAiLikePercent" INTEGER,
  "rewriteAiLikePercent" INTEGER,
  "changePoints" INTEGER,
  "internalStrategies" INTEGER NOT NULL DEFAULT 0,
  "repairCandidates" INTEGER NOT NULL DEFAULT 0,
  "rejectedCandidates" INTEGER NOT NULL DEFAULT 0,
  "usedEscalation" BOOLEAN NOT NULL DEFAULT false,
  "openAiInputTokens" INTEGER NOT NULL DEFAULT 0,
  "openAiOutputTokens" INTEGER NOT NULL DEFAULT 0,
  "openAiCostUsd" DECIMAL(65,30) NOT NULL DEFAULT 0,
  "saplingCallCount" INTEGER NOT NULL DEFAULT 0,
  "saplingCharacters" INTEGER NOT NULL DEFAULT 0,
  "saplingCostUsd" DECIMAL(65,30) NOT NULL DEFAULT 0,
  "totalEstimatedCostUsd" DECIMAL(65,30) NOT NULL DEFAULT 0,
  "modelsUsedJson" TEXT NOT NULL DEFAULT '[]',
  "providerCallsJson" TEXT NOT NULL DEFAULT '[]',
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "updatedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "RewriteCostLog_pkey" PRIMARY KEY ("id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "RewriteCostLog_requestId_key" ON "RewriteCostLog"("requestId");
CREATE INDEX IF NOT EXISTS "RewriteCostLog_createdAt_idx" ON "RewriteCostLog"("createdAt");
CREATE INDEX IF NOT EXISTS "RewriteCostLog_userId_idx" ON "RewriteCostLog"("userId");
CREATE INDEX IF NOT EXISTS "RewriteCostLog_status_idx" ON "RewriteCostLog"("status");
CREATE INDEX IF NOT EXISTS "RewriteCostLog_scenario_idx" ON "RewriteCostLog"("scenario");
CREATE INDEX IF NOT EXISTS "RewriteCostLog_totalEstimatedCostUsd_idx" ON "RewriteCostLog"("totalEstimatedCostUsd");

ALTER TABLE "RewriteCostLog"
  ADD CONSTRAINT "RewriteCostLog_userId_fkey"
  FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

CREATE TABLE IF NOT EXISTS "RewriteProviderCall" (
  "id" TEXT NOT NULL,
  "costLogId" TEXT NOT NULL,
  "provider" TEXT NOT NULL,
  "role" TEXT NOT NULL,
  "model" TEXT,
  "inputTokens" INTEGER,
  "outputTokens" INTEGER,
  "characters" INTEGER,
  "estimatedCostUsd" DECIMAL(65,30) NOT NULL DEFAULT 0,
  "latencyMs" INTEGER,
  "success" BOOLEAN NOT NULL DEFAULT true,
  "errorCode" TEXT,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "RewriteProviderCall_pkey" PRIMARY KEY ("id")
);

CREATE INDEX IF NOT EXISTS "RewriteProviderCall_costLogId_idx" ON "RewriteProviderCall"("costLogId");
CREATE INDEX IF NOT EXISTS "RewriteProviderCall_provider_idx" ON "RewriteProviderCall"("provider");
CREATE INDEX IF NOT EXISTS "RewriteProviderCall_role_idx" ON "RewriteProviderCall"("role");
CREATE INDEX IF NOT EXISTS "RewriteProviderCall_createdAt_idx" ON "RewriteProviderCall"("createdAt");

ALTER TABLE "RewriteProviderCall"
  ADD CONSTRAINT "RewriteProviderCall_costLogId_fkey"
  FOREIGN KEY ("costLogId") REFERENCES "RewriteCostLog"("id") ON DELETE CASCADE ON UPDATE CASCADE;
