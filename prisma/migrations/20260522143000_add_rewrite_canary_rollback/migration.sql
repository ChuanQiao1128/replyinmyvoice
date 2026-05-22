CREATE TABLE IF NOT EXISTS "RewriteCanaryRollback" (
  "id" TEXT NOT NULL PRIMARY KEY,
  "canaryStrategyVersion" TEXT NOT NULL,
  "controlStrategyVersion" TEXT NOT NULL,
  "scenario" TEXT NOT NULL,
  "state" TEXT NOT NULL DEFAULT 'open',
  "windowRewrites" INTEGER NOT NULL,
  "rollingAverageSignalDrop" DOUBLE PRECISION NOT NULL,
  "baselineAverageSignalDrop" DOUBLE PRECISION NOT NULL,
  "regressionPoints" DOUBLE PRECISION NOT NULL,
  "windowStartedAt" TIMESTAMP(3),
  "windowEndedAt" TIMESTAMP(3),
  "adminEmailStatus" TEXT NOT NULL DEFAULT 'pending',
  "adminEmailSentAt" TIMESTAMP(3),
  "githubIssueStatus" TEXT NOT NULL DEFAULT 'pending',
  "githubIssueUrl" TEXT,
  "githubIssueOpenedAt" TIMESTAMP(3),
  "lastError" TEXT,
  "lastCheckedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "resolvedAt" TIMESTAMP(3)
);

CREATE UNIQUE INDEX IF NOT EXISTS "RewriteCanaryRollback_active_strategy_scenario_key"
  ON "RewriteCanaryRollback"("canaryStrategyVersion", "scenario")
  WHERE "resolvedAt" IS NULL;

CREATE INDEX IF NOT EXISTS "RewriteCanaryRollback_canaryStrategyVersion_createdAt_idx"
  ON "RewriteCanaryRollback"("canaryStrategyVersion", "createdAt");

CREATE INDEX IF NOT EXISTS "RewriteCanaryRollback_scenario_idx"
  ON "RewriteCanaryRollback"("scenario");

CREATE INDEX IF NOT EXISTS "RewriteCanaryRollback_state_idx"
  ON "RewriteCanaryRollback"("state");

CREATE INDEX IF NOT EXISTS "RewriteCanaryRollback_resolvedAt_idx"
  ON "RewriteCanaryRollback"("resolvedAt");
