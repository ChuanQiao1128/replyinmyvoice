-- Add diagnosis-tag cluster metadata for LearningFinding rows.
ALTER TABLE "LearningFinding" ADD COLUMN IF NOT EXISTS "commonTone" TEXT;
ALTER TABLE "LearningFinding" ADD COLUMN IF NOT EXISTS "primaryDiagnosisTag" TEXT;

CREATE INDEX IF NOT EXISTS "LearningFinding_commonTone_idx" ON "LearningFinding"("commonTone");
CREATE INDEX IF NOT EXISTS "LearningFinding_primaryDiagnosisTag_idx" ON "LearningFinding"("primaryDiagnosisTag");
