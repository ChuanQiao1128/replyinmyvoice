import { describe, expect, it } from "vitest";

import {
  buildStrategyCandidatePromotionBrief,
  buildStrategyCandidatePromotionTask,
  isPromotableStrategyCandidate,
} from "../../lib/learningops/promotion-brief";
import type { StrategyCandidateDraft } from "../../lib/learningops";

type StrategyCandidateFixture = Omit<StrategyCandidateDraft, "status"> & {
  status: string;
};

function candidate(
  overrides: Partial<StrategyCandidateFixture> = {},
): StrategyCandidateFixture {
  return {
    findingIndex: 0,
    title: "Patch customer_support strategy for support_template_voice",
    scenario: "customer_support",
    patchTarget: "repair_prompt",
    patchAction: "add_guardrail",
    patchText:
      "Add to the customer_support repair prompt for support_template_voice: answer the concrete account issue before using generic support language.",
    proposedChangeSummary:
      "add_guardrail repair_prompt for customer_support: answer the concrete account issue before using generic support language.",
    riskLevel: "medium",
    status: "proposed",
    requiredRegressionTest:
      "Add a customer_support regression case for support_template_voice and assert the selected rewrite answers the concrete issue.",
    requiredEval:
      "Run the focused scenario evaluation for the customer_support support_template_voice pattern.",
    evidenceCount: 4,
    ...overrides,
  };
}

describe("LearningOps promotion brief", () => {
  it("builds a safe Codex task from a promotable StrategyCandidate", () => {
    const brief = buildStrategyCandidatePromotionBrief(candidate(), {
      candidateId: "cand_123",
      issueId: "M2.5-005",
      runId: "run_456",
    });

    expect(brief.summary).toBe(
      "Promote StrategyCandidate cand_123: Patch customer_support strategy for support_template_voice",
    );
    expect(brief.policy).toEqual({
      pullRequestMode: "draft_only",
      autoMergeAllowed: false,
      productionHotLoadAllowed: false,
      includeRawLearningSamples: false,
    });
    expect(brief.candidate).toMatchObject({
      id: "cand_123",
      runId: "run_456",
      scenario: "customer_support",
      patchTarget: "repair_prompt",
      patchAction: "add_guardrail",
      evidenceCount: 4,
    });
    expect(brief.filesToInspect).toEqual([
      "lib/rewrite-pipeline/model.ts",
      "lib/rewrite-pipeline/targeted-repair.ts",
      "lib/rewrite-pipeline/strategy-catalog.ts",
      "tests/unit/rewrite-targeted-repair.test.ts",
      "tests/unit/rewrite-pipeline.test.ts",
      "docs/rewrite-strategy-memory.md",
    ]);
    expect(brief.requiredChanges).toContain(
      "Update the narrowest repair prompt, strategy guidance, or scenario guardrail that implements the candidate patch.",
    );
    expect(brief.requiredChanges).toContain(
      "Add or update a regression test that proves the candidate pattern is fixed.",
    );
    expect(brief.validationCommands).toEqual([
      "npm run lint",
      "npm run typecheck",
      "npm run test",
    ]);
  });

  it("renders a self-contained task prompt without raw sample text", () => {
    const task = buildStrategyCandidatePromotionTask(candidate(), {
      candidateId: "cand_123",
      issueId: "M2.5-005",
    });

    expect(task).toContain("# LearningOps Strategy Promotion Task");
    expect(task).toContain("Candidate id: cand_123");
    expect(task).toContain("PR mode: draft only; do not merge automatically.");
    expect(task).toContain("Do not print or copy raw learning sample content.");
    expect(task).toContain("npm run test");
    expect(task).not.toContain("messageToReplyTo");
    expect(task).not.toContain("roughDraftReply");
  });

  it("routes patch targets to the most relevant source and test files", () => {
    expect(
      buildStrategyCandidatePromotionBrief(
        candidate({ patchTarget: "generation_prompt" }),
      ).filesToInspect,
    ).toContain("lib/rewrite-pipeline/model.ts");

    expect(
      buildStrategyCandidatePromotionBrief(
        candidate({ patchTarget: "strategy_router" }),
      ).filesToInspect,
    ).toContain("lib/rewrite-pipeline/strategy-router.ts");

    expect(
      buildStrategyCandidatePromotionBrief(
        candidate({ patchTarget: "quality_gate" }),
      ).filesToInspect,
    ).toContain("lib/rewrite-quality-gate.ts");
  });

  it("only treats proposed code-change candidates with enough evidence as promotable", () => {
    expect(isPromotableStrategyCandidate(candidate())).toBe(true);
    expect(
      isPromotableStrategyCandidate(candidate({ status: "needs_revision" })),
    ).toBe(false);
    expect(isPromotableStrategyCandidate(candidate({ evidenceCount: 0 }))).toBe(
      false,
    );
  });
});
