import fs from "node:fs";
import path from "node:path";

import { neon, type NeonQueryFunction } from "@neondatabase/serverless";

import {
  analyzeLearningSamples,
  type LearningAnalysisResult,
  type LearningFindingDraft,
  type LearningSampleRow,
  type StrategyCandidateDraft,
} from "../lib/learningops";
import { buildStrategyCandidatePromotionTask } from "../lib/learningops/promotion-brief";

type SqlClient = NeonQueryFunction<false, false>;

const PROMOTION_TASK_PATH = "plans/learningops-promotion-task.md";
const PROMOTION_ISSUE_ID = "M2.5-005";

function loadLocalEnv() {
  const envPath = path.join(process.cwd(), ".env.local");
  if (!fs.existsSync(envPath)) {
    return;
  }

  for (const line of fs.readFileSync(envPath, "utf8").split(/\r?\n/)) {
    if (!line || line.trim().startsWith("#")) {
      continue;
    }

    const index = line.indexOf("=");
    if (index === -1) {
      continue;
    }

    const key = line.slice(0, index).trim();
    let value = line.slice(index + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    process.env[key] = process.env[key] ?? value;
  }
}

function round(value: number | null) {
  return value === null ? "n/a" : String(Math.round(value));
}

function markdownTable(rows: string[][]) {
  if (rows.length === 0) {
    return "_No rows._";
  }

  const [header, ...body] = rows;
  const separator = header.map(() => "---");
  return [header, separator, ...body]
    .map((row) => `| ${row.join(" | ")} |`)
    .join("\n");
}

function findingRows(findings: LearningFindingDraft[]) {
  return findings.map((finding) => [
    finding.failureType,
    finding.scenario ?? "all",
    finding.commonTone ?? "mixed",
    finding.primaryDiagnosisTag ?? "n/a",
    finding.severity,
    String(finding.evidenceCount),
    finding.promotionRecommendation,
    finding.diagnosisTags.join(", ") || "n/a",
  ]);
}

function candidateRows(candidates: StrategyCandidateDraft[]) {
  return candidates.map((candidate) => [
    candidate.title,
    candidate.scenario ?? "all",
    candidate.patchTarget,
    candidate.patchAction,
    candidate.riskLevel,
    String(candidate.evidenceCount),
    candidate.status,
  ]);
}

function buildDigest(analysis: LearningAnalysisResult, runId: string) {
  const generatedAt = new Date().toISOString();

  return `# Rewrite Memory Digest

Generated: ${generatedAt}

Learning run: ${runId}

This digest is generated from internally stored rewrite learning samples. It summarizes patterns and does not print user-submitted text.

## Summary

- Samples scanned: ${analysis.summary.sampleCount}
- Measured samples: ${analysis.summary.measuredCount}
- Successful rewrites: ${analysis.summary.successCount}
- Quality-gate failures: ${analysis.summary.qualityFailureCount}
- Average signal drop: ${round(analysis.summary.averageDropPoints)} pts
- Rewrites below 50% AI-like signal: ${analysis.summary.below50Count}/${analysis.summary.measuredCount}
- Measured rewrites worse than draft: ${analysis.summary.worseThanDraftCount}/${analysis.summary.measuredCount}
- Promotion decision: ${analysis.promotionDecision}

## Findings

${markdownTable([
  [
    "Failure type",
    "Scenario",
    "Common tone",
    "Primary tag",
    "Severity",
    "Evidence",
    "Recommendation",
    "Diagnosis tags",
  ],
  ...findingRows(analysis.findings),
])}

## Strategy Candidates

${markdownTable([
  ["Title", "Scenario", "Patch target", "Patch action", "Risk", "Evidence", "Status"],
  ...candidateRows(analysis.strategyCandidates),
])}

## Promotion Policy

- Run every 24 hours automatically.
- Push/deploy automatically only when a qualified strategy promotion passes all gates.
- Never deploy just because the scheduled job ran.
- Production strategy is promoted through code, tests, GitHub push, and Cloudflare deploy. It is not hot-loaded from database rows.
`;
}

type InsertedStrategyCandidateRef = {
  candidate: StrategyCandidateDraft;
  candidateId: string;
  findingId: string;
};

function buildNoPromotionTask(analysis: LearningAnalysisResult, runId: string) {
  return [
    "# LearningOps Strategy Promotion Task",
    "",
    `Learning run: ${runId}`,
    "",
    "No promotable StrategyCandidate was generated for this run.",
    "",
    "## Summary",
    "",
    `- Samples scanned: ${analysis.summary.sampleCount}`,
    `- Findings: ${analysis.findings.length}`,
    `- Strategy candidates: ${analysis.strategyCandidates.length}`,
    `- Promotion decision: ${analysis.promotionDecision}`,
    "",
    "No Codex MCP promotion session should be started for this run.",
    "",
  ].join("\n");
}

function buildPromotionTask({
  analysis,
  candidates,
  runId,
}: {
  analysis: LearningAnalysisResult;
  candidates: InsertedStrategyCandidateRef[];
  runId: string;
}) {
  const selected = candidates[0];
  if (!selected) {
    return buildNoPromotionTask(analysis, runId);
  }

  return buildStrategyCandidatePromotionTask(selected.candidate, {
    candidateId: selected.candidateId,
    findingId: selected.findingId,
    issueId: PROMOTION_ISSUE_ID,
    runId,
  });
}

function writePromotionTask({
  analysis,
  candidates,
  runId,
}: {
  analysis: LearningAnalysisResult;
  candidates: InsertedStrategyCandidateRef[];
  runId: string;
}) {
  const content = buildPromotionTask({ analysis, candidates, runId });
  fs.writeFileSync(path.join(process.cwd(), PROMOTION_TASK_PATH), content);
}

async function insertLearningRun({
  sql,
  runId,
  analysis,
  digestPath,
}: {
  sql: SqlClient;
  runId: string;
  analysis: LearningAnalysisResult;
  digestPath: string;
}) {
  await sql`
    INSERT INTO "LearningRun" (
      "id",
      "startedAt",
      "finishedAt",
      "status",
      "sampleCount",
      "measuredCount",
      "findingCount",
      "candidateCount",
      "promotionDecision",
      "validationStatus",
      "digestPath"
    )
    VALUES (
      ${runId},
      NOW(),
      NOW(),
      'completed',
      ${analysis.summary.sampleCount},
      ${analysis.summary.measuredCount},
      ${analysis.findings.length},
      ${analysis.strategyCandidates.length},
      ${analysis.promotionDecision},
      'not_run',
      ${digestPath}
    )
  `;
}

async function insertFindingsAndCandidates({
  sql,
  runId,
  findings,
  candidates,
}: {
  sql: SqlClient;
  runId: string;
  findings: LearningFindingDraft[];
  candidates: StrategyCandidateDraft[];
}): Promise<InsertedStrategyCandidateRef[]> {
  const findingIds: string[] = [];
  const insertedCandidates: InsertedStrategyCandidateRef[] = [];

  for (const finding of findings) {
    const findingId = crypto.randomUUID();
    findingIds.push(findingId);
    await sql`
      INSERT INTO "LearningFinding" (
        "id",
        "runId",
        "scenario",
        "commonTone",
        "primaryDiagnosisTag",
        "failureType",
        "diagnosisTags",
        "evidenceCount",
        "severity",
        "recommendation",
        "promotionRecommendation",
        "sampleRefs"
      )
      VALUES (
        ${findingId},
        ${runId},
        ${finding.scenario},
        ${finding.commonTone ?? null},
        ${finding.primaryDiagnosisTag ?? null},
        ${finding.failureType},
        ${JSON.stringify(finding.diagnosisTags)},
        ${finding.evidenceCount},
        ${finding.severity},
        ${finding.recommendation},
        ${finding.promotionRecommendation},
        ${JSON.stringify(finding.sampleRefs)}
      )
    `;
  }

  for (const candidate of candidates) {
    const findingId = findingIds[candidate.findingIndex];
    if (!findingId) {
      continue;
    }
    const candidateId = crypto.randomUUID();

    await sql`
      INSERT INTO "StrategyCandidate" (
        "id",
        "findingId",
        "title",
        "scenario",
        "patchTarget",
        "patchAction",
        "patchText",
        "proposedChangeSummary",
        "riskLevel",
        "status",
        "requiredRegressionTest",
        "requiredEval",
        "evidenceCount"
      )
      VALUES (
        ${candidateId},
        ${findingId},
        ${candidate.title},
        ${candidate.scenario},
        ${candidate.patchTarget},
        ${candidate.patchAction},
        ${candidate.patchText},
        ${candidate.proposedChangeSummary},
        ${candidate.riskLevel},
        ${candidate.status},
        ${candidate.requiredRegressionTest},
        ${candidate.requiredEval},
        ${candidate.evidenceCount}
      )
    `;
    insertedCandidates.push({ candidate, candidateId, findingId });
  }

  return insertedCandidates;
}

async function main() {
  loadLocalEnv();
  const databaseUrl = process.env.DATABASE_URL;
  if (!databaseUrl) {
    throw new Error("DATABASE_URL is required to run LearningOps.");
  }

  const sql = neon(databaseUrl);
  const rows = (await sql`
    SELECT
      "id",
      "scenario",
      "tonePreset",
      "status",
      "draftAiLikePercent",
      "rewriteAiLikePercent",
      "changePoints",
      "diagnosisTags",
      "repairCandidates",
      "rejectedCandidates",
      "createdAt"
    FROM "RewriteLearningSample"
    ORDER BY "createdAt" DESC
    LIMIT 500
  `) as LearningSampleRow[];

  const analysis = analyzeLearningSamples(
    rows.map((row) => ({
      ...row,
      createdAt: String(row.createdAt),
    })),
  );
  const runId = crypto.randomUUID();
  const digestPath = "docs/rewrite-memory-digest.md";
  const digest = buildDigest(analysis, runId);
  fs.writeFileSync(path.join(process.cwd(), digestPath), digest);

  await insertLearningRun({ sql, runId, analysis, digestPath });
  const insertedCandidates = await insertFindingsAndCandidates({
    sql,
    runId,
    findings: analysis.findings,
    candidates: analysis.strategyCandidates,
  });
  writePromotionTask({ analysis, candidates: insertedCandidates, runId });

  console.log(
    [
      `LearningOps run ${runId}`,
      `samples=${analysis.summary.sampleCount}`,
      `findings=${analysis.findings.length}`,
      `candidates=${analysis.strategyCandidates.length}`,
      `decision=${analysis.promotionDecision}`,
      `promotion_task=${PROMOTION_TASK_PATH}`,
    ].join(" "),
  );
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : "LearningOps failed");
  process.exit(1);
});
