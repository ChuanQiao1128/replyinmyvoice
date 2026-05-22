-- Add structured prompt/strategy patch metadata for LearningOps strategy candidates.
ALTER TABLE "StrategyCandidate" ADD COLUMN IF NOT EXISTS "patchTarget" TEXT;
ALTER TABLE "StrategyCandidate" ADD COLUMN IF NOT EXISTS "patchAction" TEXT;
ALTER TABLE "StrategyCandidate" ADD COLUMN IF NOT EXISTS "patchText" TEXT;
ALTER TABLE "StrategyCandidate" ADD COLUMN IF NOT EXISTS "requiredRegressionTest" TEXT;

UPDATE "StrategyCandidate"
SET "requiredRegressionTest" = "requiredEval"
WHERE "requiredRegressionTest" IS NULL;

CREATE INDEX IF NOT EXISTS "StrategyCandidate_patchTarget_idx" ON "StrategyCandidate"("patchTarget");
