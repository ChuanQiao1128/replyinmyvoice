import { describe, expect, it } from "vitest";

import {
  clusterFailedCasesByDiagnosisTag,
  type LearningClusterSampleRow,
} from "../../lib/learningops/cluster";

function sample(overrides: Partial<LearningClusterSampleRow>): LearningClusterSampleRow {
  return {
    id: "sample_1",
    scenario: "Customer support",
    tonePreset: "Warm",
    status: "success",
    draftAiLikePercent: 90,
    rewriteAiLikePercent: 25,
    diagnosisTags: JSON.stringify(["stock_opening"]),
    createdAt: "2026-05-22T00:00:00.000Z",
    ...overrides,
  };
}

describe("clusterFailedCasesByDiagnosisTag", () => {
  it("groups failed cases by primary diagnosis tag", () => {
    const findings = clusterFailedCasesByDiagnosisTag([
      sample({
        id: "case_stock_1",
        status: "quality_failed",
        diagnosisTags: JSON.stringify(["stock_opening", "corporate_polish"]),
      }),
      sample({
        id: "case_stock_2",
        rewriteAiLikePercent: 75,
        diagnosisTags: JSON.stringify(["stock_opening", "uniform_rhythm"]),
      }),
      sample({
        id: "case_corporate_1",
        scenario: "Work update",
        tonePreset: "Direct",
        draftAiLikePercent: 70,
        rewriteAiLikePercent: 82,
        diagnosisTags: JSON.stringify(["corporate_polish"]),
      }),
      sample({
        id: "case_success_1",
        diagnosisTags: JSON.stringify(["stock_opening"]),
      }),
    ]);

    expect(findings).toHaveLength(2);
    expect(findings[0]).toMatchObject({
      failureType: "diagnosis_tag_cluster",
      primaryDiagnosisTag: "stock_opening",
      evidenceCount: 2,
      scenario: "Customer support",
      commonTone: "Warm",
      sampleRefs: ["case_stock_1", "case_stock_2"],
      diagnosisTags: ["stock_opening", "corporate_polish", "uniform_rhythm"],
      promotionRecommendation: "test-needed",
    });
    expect(findings[1]).toMatchObject({
      primaryDiagnosisTag: "corporate_polish",
      evidenceCount: 1,
      scenario: "Work update",
      commonTone: "Direct",
      sampleRefs: ["case_corporate_1"],
      promotionRecommendation: "no-op",
    });
  });

  it("uses untagged clusters when failed cases do not carry valid tags", () => {
    const findings = clusterFailedCasesByDiagnosisTag([
      sample({
        id: undefined,
        scenario: "Email or message reply",
        status: "quality_failed",
        diagnosisTags: "not-json",
        createdAt: "2026-05-22T00:01:00.000Z",
      }),
    ]);

    expect(findings).toHaveLength(1);
    expect(findings[0]).toMatchObject({
      primaryDiagnosisTag: "untagged",
      evidenceCount: 1,
      scenario: "Email or message reply",
      sampleRefs: ["Email or message reply:2026-05-22T00:01:00.000Z:0"],
    });
  });
});
