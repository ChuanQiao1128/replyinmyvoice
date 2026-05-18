-- CreateTable
CREATE TABLE "LearningRun" (
    "id" TEXT NOT NULL,
    "startedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "finishedAt" TIMESTAMP(3),
    "status" TEXT NOT NULL,
    "sampleCount" INTEGER NOT NULL DEFAULT 0,
    "measuredCount" INTEGER NOT NULL DEFAULT 0,
    "findingCount" INTEGER NOT NULL DEFAULT 0,
    "candidateCount" INTEGER NOT NULL DEFAULT 0,
    "promotionDecision" TEXT NOT NULL,
    "validationStatus" TEXT,
    "digestPath" TEXT,
    "errorMessage" TEXT,

    CONSTRAINT "LearningRun_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LearningFinding" (
    "id" TEXT NOT NULL,
    "runId" TEXT NOT NULL,
    "scenario" TEXT,
    "failureType" TEXT NOT NULL,
    "diagnosisTags" TEXT NOT NULL,
    "evidenceCount" INTEGER NOT NULL DEFAULT 0,
    "severity" TEXT NOT NULL,
    "recommendation" TEXT NOT NULL,
    "promotionRecommendation" TEXT NOT NULL,
    "sampleRefs" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "LearningFinding_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "StrategyCandidate" (
    "id" TEXT NOT NULL,
    "findingId" TEXT NOT NULL,
    "title" TEXT NOT NULL,
    "scenario" TEXT,
    "proposedChangeSummary" TEXT NOT NULL,
    "riskLevel" TEXT NOT NULL,
    "status" TEXT NOT NULL,
    "requiredEval" TEXT NOT NULL,
    "evidenceCount" INTEGER NOT NULL DEFAULT 0,
    "linkedCommitHash" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "StrategyCandidate_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "LearningRun_startedAt_idx" ON "LearningRun"("startedAt");

-- CreateIndex
CREATE INDEX "LearningRun_status_idx" ON "LearningRun"("status");

-- CreateIndex
CREATE INDEX "LearningRun_promotionDecision_idx" ON "LearningRun"("promotionDecision");

-- CreateIndex
CREATE INDEX "LearningFinding_createdAt_idx" ON "LearningFinding"("createdAt");

-- CreateIndex
CREATE INDEX "LearningFinding_scenario_idx" ON "LearningFinding"("scenario");

-- CreateIndex
CREATE INDEX "LearningFinding_failureType_idx" ON "LearningFinding"("failureType");

-- CreateIndex
CREATE INDEX "LearningFinding_severity_idx" ON "LearningFinding"("severity");

-- CreateIndex
CREATE INDEX "LearningFinding_promotionRecommendation_idx" ON "LearningFinding"("promotionRecommendation");

-- CreateIndex
CREATE INDEX "LearningFinding_runId_idx" ON "LearningFinding"("runId");

-- CreateIndex
CREATE INDEX "StrategyCandidate_createdAt_idx" ON "StrategyCandidate"("createdAt");

-- CreateIndex
CREATE INDEX "StrategyCandidate_scenario_idx" ON "StrategyCandidate"("scenario");

-- CreateIndex
CREATE INDEX "StrategyCandidate_riskLevel_idx" ON "StrategyCandidate"("riskLevel");

-- CreateIndex
CREATE INDEX "StrategyCandidate_status_idx" ON "StrategyCandidate"("status");

-- CreateIndex
CREATE INDEX "StrategyCandidate_findingId_idx" ON "StrategyCandidate"("findingId");

-- AddForeignKey
ALTER TABLE "LearningFinding" ADD CONSTRAINT "LearningFinding_runId_fkey" FOREIGN KEY ("runId") REFERENCES "LearningRun"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "StrategyCandidate" ADD CONSTRAINT "StrategyCandidate_findingId_fkey" FOREIGN KEY ("findingId") REFERENCES "LearningFinding"("id") ON DELETE CASCADE ON UPDATE CASCADE;
