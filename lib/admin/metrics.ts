import { getSql } from "../db";

export type AdminOverviewMetrics = {
  requests24h: number;
  requests7d: number;
  requests30d: number;
  successCount30d: number;
  qualityFailureCount30d: number;
  serverFailureCount30d: number;
  averageSignalDrop30d: number | null;
  below50Rate30d: number | null;
  averageSuccessCostUsd30d: number | null;
  p95CostUsd30d: number | null;
  openAiCostUsd30d: number;
  saplingCostUsd30d: number;
  totalCostUsd30d: number;
  averageStrategies30d: number | null;
  escalationRate30d: number | null;
  topScenarios: Array<{
    scenario: string;
    count: number;
    totalCostUsd: number;
  }>;
};

export type RecentRewriteCostLog = {
  id: string;
  requestId: string;
  createdAt: Date;
  userEmail: string | null;
  userId: string | null;
  scenario: string;
  tonePreset: string;
  status: string;
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  internalStrategies: number;
  repairCandidates: number;
  rejectedCandidates: number;
  usedEscalation: boolean;
  totalEstimatedCostUsd: number;
  durationMs: number | null;
};

export type RewriteProviderCallRow = {
  id: string;
  provider: string;
  role: string;
  model: string | null;
  inputTokens: number | null;
  outputTokens: number | null;
  characters: number | null;
  estimatedCostUsd: number;
  latencyMs: number | null;
  success: boolean;
  errorCode: string | null;
  createdAt: Date;
};

export type RewriteCostLogDetail = RecentRewriteCostLog & {
  learningSampleId: string | null;
  strategyVersion: string;
  startedAt: Date;
  finishedAt: Date | null;
  inputCharCount: number;
  draftWordCount: number;
  rewriteWordCount: number | null;
  openAiInputTokens: number;
  openAiOutputTokens: number;
  openAiCostUsd: number;
  saplingCallCount: number;
  saplingCharacters: number;
  saplingCostUsd: number;
  modelsUsed: string[];
  providerCalls: RewriteProviderCallRow[];
  learningSample?: {
    messageToReplyTo: string | null;
    roughDraftReply: string;
    rewrittenText: string | null;
    diagnosisTags: string;
    rewritePlanSummary: string | null;
    candidateSignals: string;
  } | null;
};

function numberValue(value: unknown) {
  if (value === null || value === undefined) {
    return null;
  }
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : null;
}

function requiredNumber(value: unknown) {
  return numberValue(value) ?? 0;
}

function dateValue(value: unknown) {
  return value instanceof Date ? value : new Date(String(value));
}

function parseModels(value: unknown) {
  try {
    const parsed = JSON.parse(String(value ?? "[]")) as unknown;
    return Array.isArray(parsed)
      ? parsed.filter((item): item is string => typeof item === "string")
      : [];
  } catch {
    return [];
  }
}

function mapRecentRow(row: Record<string, unknown>): RecentRewriteCostLog {
  return {
    id: String(row.id),
    requestId: String(row.requestId),
    createdAt: dateValue(row.createdAt),
    userEmail: row.userEmail ? String(row.userEmail) : null,
    userId: row.userId ? String(row.userId) : null,
    scenario: String(row.scenario),
    tonePreset: String(row.tonePreset),
    status: String(row.status),
    draftAiLikePercent: numberValue(row.draftAiLikePercent),
    rewriteAiLikePercent: numberValue(row.rewriteAiLikePercent),
    changePoints: numberValue(row.changePoints),
    internalStrategies: requiredNumber(row.internalStrategies),
    repairCandidates: requiredNumber(row.repairCandidates),
    rejectedCandidates: requiredNumber(row.rejectedCandidates),
    usedEscalation: Boolean(row.usedEscalation),
    totalEstimatedCostUsd: requiredNumber(row.totalEstimatedCostUsd),
    durationMs: numberValue(row.durationMs),
  };
}

export async function getAdminOverviewMetrics(): Promise<AdminOverviewMetrics> {
  const sql = getSql();
  const rows = (await sql`
    SELECT
      COUNT(*) FILTER (WHERE "createdAt" >= now() - interval '24 hours')::int AS "requests24h",
      COUNT(*) FILTER (WHERE "createdAt" >= now() - interval '7 days')::int AS "requests7d",
      COUNT(*) FILTER (WHERE "createdAt" >= now() - interval '30 days')::int AS "requests30d",
      COUNT(*) FILTER (WHERE "createdAt" >= now() - interval '30 days' AND "status" = 'success')::int AS "successCount30d",
      COUNT(*) FILTER (WHERE "createdAt" >= now() - interval '30 days' AND "status" = 'quality_failed')::int AS "qualityFailureCount30d",
      COUNT(*) FILTER (WHERE "createdAt" >= now() - interval '30 days' AND "status" = 'server_failed')::int AS "serverFailureCount30d",
      AVG(-"changePoints") FILTER (WHERE "createdAt" >= now() - interval '30 days' AND "changePoints" IS NOT NULL)::float AS "averageSignalDrop30d",
      AVG(CASE WHEN "rewriteAiLikePercent" < 50 THEN 1 ELSE 0 END) FILTER (WHERE "createdAt" >= now() - interval '30 days' AND "rewriteAiLikePercent" IS NOT NULL)::float AS "below50Rate30d",
      AVG("totalEstimatedCostUsd") FILTER (WHERE "createdAt" >= now() - interval '30 days' AND "status" = 'success')::float AS "averageSuccessCostUsd30d",
      percentile_cont(0.95) WITHIN GROUP (ORDER BY "totalEstimatedCostUsd") FILTER (WHERE "createdAt" >= now() - interval '30 days')::float AS "p95CostUsd30d",
      COALESCE(SUM("openAiCostUsd") FILTER (WHERE "createdAt" >= now() - interval '30 days'), 0)::float AS "openAiCostUsd30d",
      COALESCE(SUM("saplingCostUsd") FILTER (WHERE "createdAt" >= now() - interval '30 days'), 0)::float AS "saplingCostUsd30d",
      COALESCE(SUM("totalEstimatedCostUsd") FILTER (WHERE "createdAt" >= now() - interval '30 days'), 0)::float AS "totalCostUsd30d",
      AVG("internalStrategies") FILTER (WHERE "createdAt" >= now() - interval '30 days')::float AS "averageStrategies30d",
      AVG(CASE WHEN "usedEscalation" THEN 1 ELSE 0 END) FILTER (WHERE "createdAt" >= now() - interval '30 days')::float AS "escalationRate30d"
    FROM "RewriteCostLog"
  `) as Array<Record<string, unknown>>;

  const scenarioRows = (await sql`
    SELECT
      "scenario",
      COUNT(*)::int AS "count",
      COALESCE(SUM("totalEstimatedCostUsd"), 0)::float AS "totalCostUsd"
    FROM "RewriteCostLog"
    WHERE "createdAt" >= now() - interval '30 days'
    GROUP BY "scenario"
    ORDER BY COALESCE(SUM("totalEstimatedCostUsd"), 0) DESC
    LIMIT 5
  `) as Array<Record<string, unknown>>;

  const row = rows[0] ?? {};

  return {
    requests24h: requiredNumber(row.requests24h),
    requests7d: requiredNumber(row.requests7d),
    requests30d: requiredNumber(row.requests30d),
    successCount30d: requiredNumber(row.successCount30d),
    qualityFailureCount30d: requiredNumber(row.qualityFailureCount30d),
    serverFailureCount30d: requiredNumber(row.serverFailureCount30d),
    averageSignalDrop30d: numberValue(row.averageSignalDrop30d),
    below50Rate30d: numberValue(row.below50Rate30d),
    averageSuccessCostUsd30d: numberValue(row.averageSuccessCostUsd30d),
    p95CostUsd30d: numberValue(row.p95CostUsd30d),
    openAiCostUsd30d: requiredNumber(row.openAiCostUsd30d),
    saplingCostUsd30d: requiredNumber(row.saplingCostUsd30d),
    totalCostUsd30d: requiredNumber(row.totalCostUsd30d),
    averageStrategies30d: numberValue(row.averageStrategies30d),
    escalationRate30d: numberValue(row.escalationRate30d),
    topScenarios: scenarioRows.map((scenario) => ({
      scenario: String(scenario.scenario),
      count: requiredNumber(scenario.count),
      totalCostUsd: requiredNumber(scenario.totalCostUsd),
    })),
  };
}

export async function getRecentRewriteCostLogs({
  limit = 50,
  offset = 0,
}: {
  limit?: number;
  offset?: number;
} = {}) {
  const sql = getSql();
  const rows = (await sql`
    SELECT
      l.*,
      u."email" AS "userEmail"
    FROM "RewriteCostLog" l
    LEFT JOIN "User" u ON u."id" = l."userId"
    ORDER BY l."createdAt" DESC
    LIMIT ${Math.min(Math.max(limit, 1), 100)}
    OFFSET ${Math.max(offset, 0)}
  `) as Array<Record<string, unknown>>;

  return rows.map(mapRecentRow);
}

export async function getRewriteCostLogDetail(id: string) {
  const sql = getSql();
  const rows = (await sql`
    SELECT
      l.*,
      u."email" AS "userEmail"
    FROM "RewriteCostLog" l
    LEFT JOIN "User" u ON u."id" = l."userId"
    WHERE l."id" = ${id}
    LIMIT 1
  `) as Array<Record<string, unknown>>;

  if (!rows[0]) {
    return null;
  }

  const calls = (await sql`
    SELECT *
    FROM "RewriteProviderCall"
    WHERE "costLogId" = ${id}
    ORDER BY "createdAt" ASC
  `) as Array<Record<string, unknown>>;

  const base = mapRecentRow(rows[0]);
  const learningSampleId = rows[0].learningSampleId
    ? String(rows[0].learningSampleId)
    : null;
  const sampleRows = learningSampleId
    ? (await sql`
      SELECT
        "messageToReplyTo",
        "roughDraftReply",
        "rewrittenText",
        "diagnosisTags",
        "rewritePlanSummary",
        "candidateSignals"
      FROM "RewriteLearningSample"
      WHERE "id" = ${learningSampleId}
      LIMIT 1
    `) as Array<Record<string, unknown>>
    : [];

  return {
    ...base,
    learningSampleId,
    strategyVersion: String(rows[0].strategyVersion),
    startedAt: dateValue(rows[0].startedAt),
    finishedAt: rows[0].finishedAt ? dateValue(rows[0].finishedAt) : null,
    inputCharCount: requiredNumber(rows[0].inputCharCount),
    draftWordCount: requiredNumber(rows[0].draftWordCount),
    rewriteWordCount: numberValue(rows[0].rewriteWordCount),
    openAiInputTokens: requiredNumber(rows[0].openAiInputTokens),
    openAiOutputTokens: requiredNumber(rows[0].openAiOutputTokens),
    openAiCostUsd: requiredNumber(rows[0].openAiCostUsd),
    saplingCallCount: requiredNumber(rows[0].saplingCallCount),
    saplingCharacters: requiredNumber(rows[0].saplingCharacters),
    saplingCostUsd: requiredNumber(rows[0].saplingCostUsd),
    modelsUsed: parseModels(rows[0].modelsUsedJson),
    providerCalls: calls.map((call) => ({
      id: String(call.id),
      provider: String(call.provider),
      role: String(call.role),
      model: call.model ? String(call.model) : null,
      inputTokens: numberValue(call.inputTokens),
      outputTokens: numberValue(call.outputTokens),
      characters: numberValue(call.characters),
      estimatedCostUsd: requiredNumber(call.estimatedCostUsd),
      latencyMs: numberValue(call.latencyMs),
      success: Boolean(call.success),
      errorCode: call.errorCode ? String(call.errorCode) : null,
      createdAt: dateValue(call.createdAt),
    })),
    learningSample: sampleRows[0]
      ? {
          messageToReplyTo: sampleRows[0].messageToReplyTo
            ? String(sampleRows[0].messageToReplyTo)
            : null,
          roughDraftReply: String(sampleRows[0].roughDraftReply),
          rewrittenText: sampleRows[0].rewrittenText
            ? String(sampleRows[0].rewrittenText)
            : null,
          diagnosisTags: String(sampleRows[0].diagnosisTags),
          rewritePlanSummary: sampleRows[0].rewritePlanSummary
            ? String(sampleRows[0].rewritePlanSummary)
            : null,
          candidateSignals: String(sampleRows[0].candidateSignals),
        }
      : null,
  } satisfies RewriteCostLogDetail;
}
