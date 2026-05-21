import { afterEach, describe, expect, it } from "vitest";

import {
  approveRewriteAttempt,
  createInitialBudgetState,
  createRewriteBudget,
  recordRewriteAttempt,
} from "../../lib/rewrite-pipeline/budget-manager";
import type { InputAnalysis, RewriteStrategyDecision } from "../../lib/rewrite-pipeline/types";

const lowRiskAnalysis: InputAnalysis = {
  inputKind: "draft_only",
  scenarioHint: "general",
  riskLevel: "low",
  factualDensity: "low",
  structureRisk: "low",
  rewriteFreedom: "high",
  requiresStructurePreservation: false,
  requiresPolicyCare: false,
  containsForwardedThread: false,
  containsListsOrQuotes: false,
  wordCount: 32,
  paragraphCount: 1,
  recommendedInitialStrategy: "minimal_polish",
  reasons: [],
};

const highRiskAnalysis: InputAnalysis = {
  ...lowRiskAnalysis,
  scenarioHint: "support_policy",
  riskLevel: "high",
  factualDensity: "high",
  structureRisk: "medium",
  rewriteFreedom: "minimal",
  requiresPolicyCare: true,
  wordCount: 310,
  recommendedInitialStrategy: "support_policy_options_rewrite",
};

const strongDecision: RewriteStrategyDecision = {
  strategy: "strong_model_restructure",
  reason: "Escalate once.",
  failureKinds: ["naturalness_not_improved"],
  modelTier: "strong",
  maxCandidates: 1,
  preserveFacts: true,
  preserveStructure: true,
};

describe("rewrite budget manager", () => {
  afterEach(() => {
    delete process.env.REWRITE_TEST_WINDOW_MAX_ATTEMPTS;
  });

  it("keeps low-risk drafts on a small bounded budget", () => {
    const budget = createRewriteBudget(lowRiskAnalysis);

    expect(budget.maxAttempts).toBe(2);
    expect(budget.allowStrongModel).toBe(false);
  });

  it("allows one strong attempt for high-risk policy-heavy input", () => {
    const budget = createRewriteBudget(highRiskAnalysis);
    const state = createInitialBudgetState();

    expect(budget.maxAttempts).toBe(5);
    expect(approveRewriteAttempt({ budget, decision: strongDecision, state })).toEqual({
      approved: true,
      reason: "approved",
    });
  });

  it("blocks a second strong attempt", () => {
    const budget = createRewriteBudget(highRiskAnalysis);
    const state = recordRewriteAttempt({
      decision: strongDecision,
      estimatedUsd: 0.02,
      state: createInitialBudgetState(),
    });

    expect(approveRewriteAttempt({ budget, decision: strongDecision, state })).toEqual({
      approved: false,
      reason: "budget_exceeded:strong_model",
    });
  });

  it("allows the DeepSeek test window to raise the attempt cap to 10", () => {
    process.env.REWRITE_TEST_WINDOW_MAX_ATTEMPTS = "10";

    const budget = createRewriteBudget(highRiskAnalysis);

    expect(budget.maxAttempts).toBe(10);
  });

  it("never allows the test-window attempt cap above 10", () => {
    process.env.REWRITE_TEST_WINDOW_MAX_ATTEMPTS = "25";

    const budget = createRewriteBudget(highRiskAnalysis);

    expect(budget.maxAttempts).toBe(10);
  });
});
