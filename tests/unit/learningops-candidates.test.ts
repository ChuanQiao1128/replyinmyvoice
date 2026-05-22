import { describe, expect, it } from "vitest";

import { proposeStrategyCandidate } from "../../lib/learningops/candidates";
import type { LearningFindingDraft } from "../../lib/learningops";

function finding(overrides: Partial<LearningFindingDraft>): LearningFindingDraft {
  return {
    scenario: "customer_support",
    commonTone: "Warm",
    primaryDiagnosisTag: "support_template_voice",
    failureType: "diagnosis_tag_cluster",
    diagnosisTags: ["support_template_voice", "uniform_rhythm"],
    evidenceCount: 4,
    severity: "medium",
    recommendation:
      "Review repeated failed samples with support template voice.",
    promotionRecommendation: "test-needed",
    sampleRefs: ["case_1", "case_2", "case_3", "case_4"],
    ...overrides,
  };
}

describe("proposeStrategyCandidate", () => {
  it("turns repeated diagnosis clusters into structured prompt patches", () => {
    const candidate = proposeStrategyCandidate(finding({}), 2);

    expect(candidate).toMatchObject({
      findingIndex: 2,
      scenario: "customer_support",
      patchTarget: "repair_prompt",
      patchAction: "add_guardrail",
      riskLevel: "medium",
      status: "proposed",
      evidenceCount: 4,
    });
    expect(candidate?.patchText).toContain(
      "avoid balanced 4-paragraph structure",
    );
    expect(candidate?.patchText).toContain("support_template_voice");
    expect(candidate?.requiredRegressionTest).toContain("customer_support");
    expect(candidate?.requiredRegressionTest).toContain(
      "support_template_voice",
    );
  });

  it("routes quote or list risk clusters to a safer strategy patch", () => {
    const candidate = proposeStrategyCandidate(
      finding({
        scenario: "workplace",
        commonTone: "Direct",
        primaryDiagnosisTag: "quote_or_list_risk",
        diagnosisTags: ["quote_or_list_risk", "broken_numbered_list"],
        evidenceCount: 3,
        severity: "medium",
      }),
      0,
    );

    expect(candidate).toMatchObject({
      patchTarget: "strategy_router",
      patchAction: "route",
      scenario: "workplace",
      evidenceCount: 3,
    });
    expect(candidate?.patchText).toContain("quote/list-safe rewrite");
    expect(candidate?.requiredRegressionTest).toContain(
      "broken_numbered_list",
    );
  });

  it("keeps worse-than-draft candidates structured with a regression test", () => {
    const candidate = proposeStrategyCandidate(
      finding({
        failureType: "worse_than_draft",
        primaryDiagnosisTag: undefined,
        evidenceCount: 1,
        severity: "high",
        promotionRecommendation: "code-change",
      }),
      1,
    );

    expect(candidate).toMatchObject({
      findingIndex: 1,
      patchTarget: "quality_gate",
      patchAction: "tighten",
      riskLevel: "high",
      requiredRegressionTest:
        "Add a regression case using the failing sample pattern, assert final selected rewrite is not worse than the draft, and verify facts/next steps are preserved.",
    });
  });

  it("does not propose patches for low-evidence no-op findings", () => {
    const candidate = proposeStrategyCandidate(
      finding({
        evidenceCount: 1,
        severity: "low",
        promotionRecommendation: "no-op",
      }),
      0,
    );

    expect(candidate).toBeNull();
  });
});
