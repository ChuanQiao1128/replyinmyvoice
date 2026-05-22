import {
  analyzeLearningSamples,
  type LearningAnalysisResult,
  type LearningFindingDraft,
  type LearningPromotionDecision,
  type LearningSampleRow,
  type StrategyCandidateDraft,
} from "../learningops";
import { buildStrategyCandidatePromotionTask } from "./promotion-brief";

export type LearningOpsSqlClient = (
  strings: TemplateStringsArray,
  ...values: unknown[]
) => Promise<unknown>;

export type InsertedStrategyCandidateRef = {
  candidate: StrategyCandidateDraft;
  candidateId: string;
  findingId: string;
};

export type LearningOpsPipelineResult = {
  runId: string;
  status: LearningPromotionDecision;
  analysis: LearningAnalysisResult;
  insertedCandidates: InsertedStrategyCandidateRef[];
  errorMessage?: string;
};

type LearningSampleDbRow = Omit<LearningSampleRow, "createdAt"> & {
  createdAt: Date | string;
};

const DEFAULT_SAMPLE_WINDOW_DAYS = 7;
const DEFAULT_SAMPLE_LIMIT = 500;
const DEFAULT_DIGEST_PATH = "docs/rewrite-memory-digest.md";
const DEFAULT_PROMOTION_ISSUE_ID = "M2.5-005";
const PROMOTION_TASK_PATH = "plans/learningops-promotion-task.md";
const MS_PER_DAY = 24 * 60 * 60 * 1000;

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

function emptyAnalysis(status: LearningPromotionDecision): LearningAnalysisResult {
  return {
    summary: {
      sampleCount: 0,
      measuredCount: 0,
      successCount: 0,
      qualityFailureCount: 0,
      averageDropPoints: null,
      below50Count: 0,
      worseThanDraftCount: 0,
    },
    findings: [],
    strategyCandidates: [],
    promotionDecision: status,
  };
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : "LearningOps failed";
}

export function learningWindowStart(
  now: Date,
  windowDays = DEFAULT_SAMPLE_WINDOW_DAYS,
) {
  return new Date(now.getTime() - windowDays * MS_PER_DAY);
}

export function buildLearningDigest(
  analysis: LearningAnalysisResult,
  runId: string,
  generatedAt = new Date(),
) {
  return `# Rewrite Memory Digest

Generated: ${generatedAt.toISOString()}

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
- Open draft pull requests only when a qualified strategy promotion passes validation.
- Never deploy just because the scheduled job ran.
- Production strategy is promoted through code, tests, GitHub pull request review, and Cloudflare deploy. It is not hot-loaded from database rows.
`;
}

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

export function buildPromotionTask({
  analysis,
  candidates,
  runId,
  promotionIssueId = DEFAULT_PROMOTION_ISSUE_ID,
}: {
  analysis: LearningAnalysisResult;
  candidates: InsertedStrategyCandidateRef[];
  runId: string;
  promotionIssueId?: string;
}) {
  const selected = candidates[0];
  if (!selected) {
    return buildNoPromotionTask(analysis, runId);
  }

  return buildStrategyCandidatePromotionTask(selected.candidate, {
    candidateId: selected.candidateId,
    findingId: selected.findingId,
    issueId: promotionIssueId,
    runId,
  });
}

async function insertLearningRunStart({
  sql,
  runId,
  startedAt,
}: {
  sql: LearningOpsSqlClient;
  runId: string;
  startedAt: Date;
}) {
  await sql`
    INSERT INTO "LearningRun" (
      "id",
      "startedAt",
      "status",
      "promotionDecision",
      "validationStatus"
    )
    VALUES (
      ${runId},
      ${startedAt},
      'blocked',
      'blocked',
      'not_run'
    )
  `;
}

async function updateLearningRunComplete({
  sql,
  runId,
  finishedAt,
  status,
  analysis,
  digestPath,
}: {
  sql: LearningOpsSqlClient;
  runId: string;
  finishedAt: Date;
  status: LearningPromotionDecision;
  analysis: LearningAnalysisResult;
  digestPath: string | null;
}) {
  await sql`
    UPDATE "LearningRun"
    SET
      "finishedAt" = ${finishedAt},
      "status" = ${status},
      "sampleCount" = ${analysis.summary.sampleCount},
      "measuredCount" = ${analysis.summary.measuredCount},
      "findingCount" = ${analysis.findings.length},
      "candidateCount" = ${analysis.strategyCandidates.length},
      "promotionDecision" = ${analysis.promotionDecision},
      "validationStatus" = 'not_run',
      "digestPath" = ${digestPath},
      "errorMessage" = NULL
    WHERE "id" = ${runId}
  `;
}

async function updateLearningRunBlocked({
  sql,
  runId,
  finishedAt,
  message,
}: {
  sql: LearningOpsSqlClient;
  runId: string;
  finishedAt: Date;
  message: string;
}) {
  await sql`
    UPDATE "LearningRun"
    SET
      "finishedAt" = ${finishedAt},
      "status" = ${"blocked"},
      "promotionDecision" = ${"blocked"},
      "validationStatus" = 'failed',
      "errorMessage" = ${message}
    WHERE "id" = ${runId}
  `;
}

async function readRecentLearningSamples({
  sql,
  since,
  limit,
}: {
  sql: LearningOpsSqlClient;
  since: Date;
  limit: number;
}) {
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
    WHERE "createdAt" >= ${since}
    ORDER BY "createdAt" DESC
    LIMIT ${limit}
  `) as LearningSampleDbRow[];

  return rows.map((row) => ({
    ...row,
    createdAt: String(row.createdAt),
  }));
}

async function insertFindingsAndCandidates({
  sql,
  runId,
  findings,
  candidates,
}: {
  sql: LearningOpsSqlClient;
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

export async function runLearningOpsPipeline({
  sql,
  now = new Date(),
  sampleWindowDays = DEFAULT_SAMPLE_WINDOW_DAYS,
  sampleLimit = DEFAULT_SAMPLE_LIMIT,
  digestPath = DEFAULT_DIGEST_PATH,
  writeDigest,
  writePromotionTask,
  promotionIssueId = DEFAULT_PROMOTION_ISSUE_ID,
}: {
  sql: LearningOpsSqlClient;
  now?: Date;
  sampleWindowDays?: number;
  sampleLimit?: number;
  digestPath?: string | null;
  writeDigest?: (digestPath: string, content: string) => Promise<void>;
  writePromotionTask?: (content: string, taskPath: string) => Promise<void>;
  promotionIssueId?: string;
}): Promise<LearningOpsPipelineResult> {
  const runId = crypto.randomUUID();
  await insertLearningRunStart({ sql, runId, startedAt: now });

  try {
    const samples = await readRecentLearningSamples({
      sql,
      since: learningWindowStart(now, sampleWindowDays),
      limit: sampleLimit,
    });
    const analysis = analyzeLearningSamples(samples);
    const status = analysis.promotionDecision;
    const resolvedDigestPath = writeDigest && digestPath ? digestPath : null;

    if (writeDigest && resolvedDigestPath) {
      await writeDigest(resolvedDigestPath, buildLearningDigest(analysis, runId, now));
    }

    await updateLearningRunComplete({
      sql,
      runId,
      finishedAt: now,
      status,
      analysis,
      digestPath: resolvedDigestPath,
    });

    const insertedCandidates = await insertFindingsAndCandidates({
      sql,
      runId,
      findings: analysis.findings,
      candidates: analysis.strategyCandidates,
    });

    if (writePromotionTask) {
      await writePromotionTask(
        buildPromotionTask({
          analysis,
          candidates: insertedCandidates,
          runId,
          promotionIssueId,
        }),
        PROMOTION_TASK_PATH,
      );
    }

    return {
      runId,
      status,
      analysis,
      insertedCandidates,
    };
  } catch (error) {
    const message = errorMessage(error);
    await updateLearningRunBlocked({
      sql,
      runId,
      finishedAt: new Date(),
      message,
    });

    return {
      runId,
      status: "blocked",
      analysis: emptyAnalysis("blocked"),
      insertedCandidates: [],
      errorMessage: message,
    };
  }
}
