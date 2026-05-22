import { getSql } from "../db";

export type AdminLearningSqlClient = ReturnType<typeof getSql>;

export type StrategyCandidateReviewStatus =
  | "approved"
  | "needs_revision"
  | "rejected";

export type AdminStrategyCandidate = {
  id: string;
  title: string;
  scenario: string | null;
  patchTarget: string | null;
  patchAction: string | null;
  patchText: string | null;
  proposedChangeSummary: string;
  riskLevel: string;
  status: string;
  requiredRegressionTest: string | null;
  requiredEval: string;
  evidenceCount: number;
  linkedCommitHash: string | null;
  linkedWorkHref: string | null;
  createdAt: Date;
};

export type AdminLearningFinding = {
  id: string;
  scenario: string | null;
  commonTone: string | null;
  primaryDiagnosisTag: string | null;
  failureType: string;
  diagnosisTags: string[];
  evidenceCount: number;
  severity: string;
  recommendation: string;
  promotionRecommendation: string;
  sampleRefs: string[];
  createdAt: Date;
  candidates: AdminStrategyCandidate[];
};

export type AdminLearningRun = {
  id: string;
  startedAt: Date;
  finishedAt: Date | null;
  status: string;
  sampleCount: number;
  measuredCount: number;
  findingCount: number;
  candidateCount: number;
  promotionDecision: string;
  validationStatus: string | null;
  digestPath: string | null;
  errorMessage: string | null;
  findings: AdminLearningFinding[];
};

const REVIEW_STATUSES: StrategyCandidateReviewStatus[] = [
  "approved",
  "needs_revision",
  "rejected",
];

function numberValue(value: unknown) {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : 0;
}

function stringValue(value: unknown) {
  return value === null || value === undefined ? "" : String(value);
}

function nullableString(value: unknown) {
  const text = stringValue(value).trim();
  return text.length > 0 ? text : null;
}

function dateValue(value: unknown) {
  return value instanceof Date ? value : new Date(String(value));
}

function nullableDate(value: unknown) {
  return value ? dateValue(value) : null;
}

function parseStringArray(value: unknown, { sort = false } = {}) {
  try {
    const parsed = JSON.parse(String(value ?? "[]")) as unknown;
    const items = Array.isArray(parsed)
      ? parsed.filter((item): item is string => typeof item === "string")
      : [];
    return sort ? [...items].sort() : items;
  } catch {
    return [];
  }
}

function linkedWorkHref(value: string | null) {
  if (!value) {
    return null;
  }

  if (/^https?:\/\//i.test(value)) {
    return value;
  }

  if (/^[a-f0-9]{7,40}$/i.test(value)) {
    return `https://github.com/ChuanQiao1128/replyinmyvoice/commit/${value}`;
  }

  return null;
}

export function parseStrategyCandidateReviewStatus(
  value: FormDataEntryValue | string | null,
): StrategyCandidateReviewStatus | null {
  const status = typeof value === "string" ? value : null;
  return REVIEW_STATUSES.includes(status as StrategyCandidateReviewStatus)
    ? (status as StrategyCandidateReviewStatus)
    : null;
}

export async function getRecentLearningRuns({
  sql = getSql(),
  limit = 12,
}: {
  sql?: AdminLearningSqlClient;
  limit?: number;
} = {}): Promise<AdminLearningRun[]> {
  const rows = (await sql`
    SELECT
      r."id",
      r."startedAt",
      r."finishedAt",
      r."status",
      r."sampleCount",
      r."measuredCount",
      r."findingCount",
      r."candidateCount",
      r."promotionDecision",
      r."validationStatus",
      r."digestPath",
      r."errorMessage",
      f."id" AS "findingId",
      f."scenario" AS "findingScenario",
      f."commonTone",
      f."primaryDiagnosisTag",
      f."failureType",
      f."diagnosisTags",
      f."evidenceCount" AS "findingEvidenceCount",
      f."severity",
      f."recommendation",
      f."promotionRecommendation",
      f."sampleRefs",
      f."createdAt" AS "findingCreatedAt",
      c."id" AS "candidateId",
      c."title",
      c."scenario" AS "candidateScenario",
      c."patchTarget",
      c."patchAction",
      c."patchText",
      c."proposedChangeSummary",
      c."riskLevel",
      c."status" AS "candidateStatus",
      c."requiredRegressionTest",
      c."requiredEval",
      c."evidenceCount" AS "candidateEvidenceCount",
      c."linkedCommitHash",
      c."createdAt" AS "candidateCreatedAt"
    FROM (
      SELECT *
      FROM "LearningRun"
      ORDER BY "startedAt" DESC
      LIMIT ${Math.min(Math.max(limit, 1), 50)}
    ) r
    LEFT JOIN "LearningFinding" f ON f."runId" = r."id"
    LEFT JOIN "StrategyCandidate" c ON c."findingId" = f."id"
    ORDER BY r."startedAt" DESC, f."createdAt" DESC, c."createdAt" DESC
  `) as Array<Record<string, unknown>>;

  const runs = new Map<string, AdminLearningRun>();
  const findingsByRun = new Map<string, Map<string, AdminLearningFinding>>();

  for (const row of rows) {
    const runId = stringValue(row.id);
    let run = runs.get(runId);
    if (!run) {
      run = {
        id: runId,
        startedAt: dateValue(row.startedAt),
        finishedAt: nullableDate(row.finishedAt),
        status: stringValue(row.status),
        sampleCount: numberValue(row.sampleCount),
        measuredCount: numberValue(row.measuredCount),
        findingCount: numberValue(row.findingCount),
        candidateCount: numberValue(row.candidateCount),
        promotionDecision: stringValue(row.promotionDecision),
        validationStatus: nullableString(row.validationStatus),
        digestPath: nullableString(row.digestPath),
        errorMessage: nullableString(row.errorMessage),
        findings: [],
      };
      runs.set(runId, run);
      findingsByRun.set(runId, new Map());
    }

    const findingId = nullableString(row.findingId);
    if (!findingId) {
      continue;
    }

    const findingMap = findingsByRun.get(runId);
    let finding = findingMap?.get(findingId);
    if (!finding) {
      finding = {
        id: findingId,
        scenario: nullableString(row.findingScenario),
        commonTone: nullableString(row.commonTone),
        primaryDiagnosisTag: nullableString(row.primaryDiagnosisTag),
        failureType: stringValue(row.failureType),
        diagnosisTags: parseStringArray(row.diagnosisTags, { sort: true }),
        evidenceCount: numberValue(row.findingEvidenceCount),
        severity: stringValue(row.severity),
        recommendation: stringValue(row.recommendation),
        promotionRecommendation: stringValue(row.promotionRecommendation),
        sampleRefs: parseStringArray(row.sampleRefs),
        createdAt: dateValue(row.findingCreatedAt),
        candidates: [],
      };
      findingMap?.set(findingId, finding);
      run.findings.push(finding);
    }

    const candidateId = nullableString(row.candidateId);
    if (!candidateId) {
      continue;
    }

    const linkValue = nullableString(row.linkedCommitHash);
    finding.candidates.push({
      id: candidateId,
      title: stringValue(row.title),
      scenario: nullableString(row.candidateScenario),
      patchTarget: nullableString(row.patchTarget),
      patchAction: nullableString(row.patchAction),
      patchText: nullableString(row.patchText),
      proposedChangeSummary: stringValue(row.proposedChangeSummary),
      riskLevel: stringValue(row.riskLevel),
      status: stringValue(row.candidateStatus),
      requiredRegressionTest: nullableString(row.requiredRegressionTest),
      requiredEval: stringValue(row.requiredEval),
      evidenceCount: numberValue(row.candidateEvidenceCount),
      linkedCommitHash: linkValue,
      linkedWorkHref: linkedWorkHref(linkValue),
      createdAt: dateValue(row.candidateCreatedAt),
    });
  }

  return Array.from(runs.values());
}

export async function updateStrategyCandidateStatus({
  sql = getSql(),
  candidateId,
  status,
}: {
  sql?: AdminLearningSqlClient;
  candidateId: string;
  status: StrategyCandidateReviewStatus;
}) {
  const id = candidateId.trim();
  if (!id) {
    throw new Error("Strategy candidate id is required");
  }

  if (!parseStrategyCandidateReviewStatus(status)) {
    throw new Error("Unsupported strategy candidate status");
  }

  const rows = (await sql`
    UPDATE "StrategyCandidate"
    SET "status" = ${status}
    WHERE "id" = ${id}
    RETURNING "id", "status"
  `) as Array<Record<string, unknown>>;

  return rows[0]
    ? {
        id: stringValue(rows[0].id),
        status: stringValue(rows[0].status),
      }
    : null;
}
