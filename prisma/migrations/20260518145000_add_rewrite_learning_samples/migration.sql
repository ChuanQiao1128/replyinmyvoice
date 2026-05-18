-- CreateTable
CREATE TABLE "RewriteLearningSample" (
    "id" TEXT NOT NULL,
    "userId" TEXT,
    "scenario" TEXT NOT NULL,
    "tonePreset" TEXT NOT NULL,
    "messageToReplyTo" TEXT,
    "roughDraftReply" TEXT NOT NULL,
    "rewrittenText" TEXT,
    "draftAiLikePercent" INTEGER,
    "rewriteAiLikePercent" INTEGER,
    "changePoints" INTEGER,
    "label" TEXT,
    "diagnosisTags" TEXT NOT NULL,
    "rewritePlanSummary" TEXT,
    "candidateSignals" TEXT NOT NULL,
    "internalStrategies" INTEGER NOT NULL DEFAULT 0,
    "repairCandidates" INTEGER NOT NULL DEFAULT 0,
    "rejectedCandidates" INTEGER NOT NULL DEFAULT 0,
    "status" TEXT NOT NULL,
    "errorCode" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "RewriteLearningSample_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "RewriteLearningSample_createdAt_idx" ON "RewriteLearningSample"("createdAt");

-- CreateIndex
CREATE INDEX "RewriteLearningSample_scenario_idx" ON "RewriteLearningSample"("scenario");

-- CreateIndex
CREATE INDEX "RewriteLearningSample_status_idx" ON "RewriteLearningSample"("status");

-- CreateIndex
CREATE INDEX "RewriteLearningSample_userId_idx" ON "RewriteLearningSample"("userId");

-- AddForeignKey
ALTER TABLE "RewriteLearningSample" ADD CONSTRAINT "RewriteLearningSample_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;
