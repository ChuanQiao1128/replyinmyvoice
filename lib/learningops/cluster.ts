import type {
  LearningFindingDraft,
  LearningPromotionRecommendation,
  LearningSeverity,
} from "../learningops";

export type LearningClusterSampleRow = {
  id?: string;
  scenario: string;
  tonePreset: string;
  status: string;
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  diagnosisTags: string;
  createdAt: string;
};

export type DiagnosisClusterFinding = LearningFindingDraft & {
  failureType: "diagnosis_tag_cluster";
  primaryDiagnosisTag: string;
  commonTone: string | null;
};

const EXEMPLAR_LIMIT = 5;
const UNTAGGED_CLUSTER = "untagged";

function parseDiagnosisTags(value: string) {
  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .map((tag) => (typeof tag === "string" ? tag.trim() : ""))
      .filter((tag) => tag.length > 0);
  } catch {
    return [];
  }
}

function isMeasured(row: LearningClusterSampleRow) {
  return row.draftAiLikePercent !== null && row.rewriteAiLikePercent !== null;
}

function signalDrop(row: LearningClusterSampleRow) {
  if (!isMeasured(row)) {
    return null;
  }

  return Number(row.draftAiLikePercent) - Number(row.rewriteAiLikePercent);
}

function isFailedLearningCase(row: LearningClusterSampleRow) {
  if (row.status !== "success") {
    return true;
  }

  const drop = signalDrop(row);
  if (drop === null) {
    return false;
  }

  return (
    Number(row.rewriteAiLikePercent) >= Number(row.draftAiLikePercent) ||
    (Number(row.rewriteAiLikePercent) >= 50 && drop < 30)
  );
}

function primaryDiagnosisTag(row: LearningClusterSampleRow) {
  return parseDiagnosisTags(row.diagnosisTags)[0] ?? UNTAGGED_CLUSTER;
}

function sampleRef(row: LearningClusterSampleRow, index: number) {
  return row.id ?? `${row.scenario}:${row.createdAt}:${index}`;
}

function unique(values: string[]) {
  return Array.from(new Set(values));
}

function mostCommon(values: string[]) {
  const counts = new Map<string, { count: number; firstIndex: number }>();
  values.forEach((value, index) => {
    const existing = counts.get(value);
    counts.set(value, {
      count: (existing?.count ?? 0) + 1,
      firstIndex: existing?.firstIndex ?? index,
    });
  });

  return (
    Array.from(counts.entries()).sort((left, right) => {
      const countDiff = right[1].count - left[1].count;
      if (countDiff !== 0) {
        return countDiff;
      }

      const firstIndexDiff = left[1].firstIndex - right[1].firstIndex;
      if (firstIndexDiff !== 0) {
        return firstIndexDiff;
      }

      return left[0].localeCompare(right[0]);
    })[0]?.[0] ?? null
  );
}

function clusterSeverity(count: number): LearningSeverity {
  return count >= 3 ? "medium" : "low";
}

function clusterPromotionRecommendation(
  count: number,
): LearningPromotionRecommendation {
  return count >= 2 ? "test-needed" : "no-op";
}

function clusterDiagnosisTags(rows: LearningClusterSampleRow[], primary: string) {
  const tags = unique(rows.flatMap((row) => parseDiagnosisTags(row.diagnosisTags)));
  const secondaryTags = tags.filter((tag) => tag !== primary).sort();
  return [primary, ...secondaryTags];
}

export function clusterFailedCasesByDiagnosisTag(
  samples: LearningClusterSampleRow[],
): DiagnosisClusterFinding[] {
  const clusters = new Map<string, { row: LearningClusterSampleRow; index: number }[]>();

  samples.forEach((row, index) => {
    if (!isFailedLearningCase(row)) {
      return;
    }

    const primary = primaryDiagnosisTag(row);
    clusters.set(primary, [...(clusters.get(primary) ?? []), { row, index }]);
  });

  return Array.from(clusters.entries())
    .map(([primary, entries]) => {
      const rows = entries.map((entry) => entry.row);
      const commonScenario = mostCommon(rows.map((row) => row.scenario));
      const commonTone = mostCommon(rows.map((row) => row.tonePreset));
      const evidenceCount = rows.length;

      return {
        scenario: commonScenario,
        commonTone,
        primaryDiagnosisTag: primary,
        failureType: "diagnosis_tag_cluster" as const,
        diagnosisTags: clusterDiagnosisTags(rows, primary),
        evidenceCount,
        severity: clusterSeverity(evidenceCount),
        recommendation: `Review ${evidenceCount} failed learning sample(s) with primary diagnosis tag ${primary}; common scenario is ${commonScenario ?? "mixed"} and common tone is ${commonTone ?? "mixed"}.`,
        promotionRecommendation: clusterPromotionRecommendation(evidenceCount),
        sampleRefs: entries
          .map((entry) => sampleRef(entry.row, entry.index))
          .slice(0, EXEMPLAR_LIMIT),
      };
    })
    .sort((left, right) => {
      const countDiff = right.evidenceCount - left.evidenceCount;
      if (countDiff !== 0) {
        return countDiff;
      }

      return left.primaryDiagnosisTag.localeCompare(right.primaryDiagnosisTag);
    });
}
