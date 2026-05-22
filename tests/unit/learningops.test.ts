import { describe, expect, it } from "vitest";

import {
  analyzeLearningSamples,
  type LearningSampleRow,
} from "../../lib/learningops";

function sample(overrides: Partial<LearningSampleRow>): LearningSampleRow {
  return {
    id: "sample_1",
    scenario: "customer_support",
    tonePreset: "professional",
    status: "success",
    draftAiLikePercent: 88,
    rewriteAiLikePercent: 22,
    changePoints: -66,
    diagnosisTags: JSON.stringify(["support_template_voice"]),
    repairCandidates: 0,
    rejectedCandidates: 0,
    createdAt: "2026-05-18T00:00:00.000Z",
    ...overrides,
  };
}

describe("analyzeLearningSamples", () => {
  it("returns digest_only when there are no samples", () => {
    const result = analyzeLearningSamples([]);

    expect(result.summary.sampleCount).toBe(0);
    expect(result.promotionDecision).toBe("digest_only");
    expect(result.findings).toEqual([]);
    expect(result.strategyCandidates).toEqual([]);
  });

  it("flags a severe worse-than-draft regression as a code-change candidate", () => {
    const result = analyzeLearningSamples([
      sample({
        id: "worse_1",
        draftAiLikePercent: 89,
        rewriteAiLikePercent: 100,
        changePoints: 11,
        rejectedCandidates: 3,
      }),
    ]);

    expect(result.promotionDecision).toBe("promoted_candidate");
    expect(result.findings[0]).toMatchObject({
      failureType: "worse_than_draft",
      severity: "high",
      promotionRecommendation: "code-change",
    });
    expect(result.strategyCandidates[0]).toMatchObject({
      riskLevel: "high",
      status: "proposed",
      scenario: "customer_support",
    });
  });

  it("creates a strategy candidate for repeated high final signal", () => {
    const rows = ["a", "b", "c"].map((id) =>
      sample({
        id,
        draftAiLikePercent: 92,
        rewriteAiLikePercent: 75,
        changePoints: -17,
        diagnosisTags: JSON.stringify(["corporate_polish", "uniform_rhythm"]),
      }),
    );

    const result = analyzeLearningSamples(rows);

    expect(result.promotionDecision).toBe("promoted_candidate");
    expect(result.findings.some((finding) => finding.failureType === "high_final_signal")).toBe(
      true,
    );
    expect(
      result.strategyCandidates.some((candidate) =>
        candidate.proposedChangeSummary.includes("high final AI-like signal"),
      ),
    ).toBe(true);
  });

  it("records repeated repair success as docs-only learning", () => {
    const rows = ["a", "b"].map((id) =>
      sample({
        id,
        draftAiLikePercent: 91,
        rewriteAiLikePercent: 28,
        changePoints: -63,
        repairCandidates: 1,
        rejectedCandidates: 1,
      }),
    );

    const result = analyzeLearningSamples(rows);

    expect(result.promotionDecision).toBe("docs_only");
    expect(result.findings[0]).toMatchObject({
      failureType: "repair_success",
      promotionRecommendation: "docs-only",
      severity: "low",
    });
    expect(result.strategyCandidates).toEqual([]);
  });

  it("does not promote a single weak high-signal cluster", () => {
    const result = analyzeLearningSamples([
      sample({
        id: "weak_1",
        draftAiLikePercent: 70,
        rewriteAiLikePercent: 58,
        changePoints: -12,
      }),
    ]);

    expect(result.promotionDecision).toBe("digest_only");
    expect(result.findings).toHaveLength(1);
    expect(result.findings[0]).toMatchObject({
      failureType: "diagnosis_tag_cluster",
      primaryDiagnosisTag: "support_template_voice",
      evidenceCount: 1,
      promotionRecommendation: "no-op",
    });
    expect(result.strategyCandidates).toEqual([]);
  });

  it("promotes repeated diagnosis clusters into structured strategy candidates", () => {
    const rows = ["cluster_1", "cluster_2"].map((id) =>
      sample({
        id,
        status: "quality_failed",
        diagnosisTags: JSON.stringify([
          "support_template_voice",
          "uniform_rhythm",
        ]),
      }),
    );

    const result = analyzeLearningSamples(rows);

    expect(result.promotionDecision).toBe("promoted_candidate");
    expect(result.strategyCandidates[0]).toMatchObject({
      scenario: "customer_support",
      patchTarget: "repair_prompt",
      patchAction: "add_guardrail",
      requiredRegressionTest: expect.stringContaining(
        "support_template_voice",
      ),
      evidenceCount: 2,
    });
  });
});
