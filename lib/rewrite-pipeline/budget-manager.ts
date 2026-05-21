import type {
  InputAnalysis,
  RewriteBudget,
  RewriteBudgetState,
  RewriteStrategyDecision,
} from "./types";
import { optionalEnv } from "../env";

function withTestWindowAttemptCap(budget: RewriteBudget): RewriteBudget {
  const rawValue = optionalEnv("REWRITE_TEST_WINDOW_MAX_ATTEMPTS");
  if (!rawValue) {
    return budget;
  }

  const parsedValue = Math.trunc(Number(rawValue));
  if (!Number.isFinite(parsedValue) || parsedValue <= 0) {
    return budget;
  }

  return {
    ...budget,
    maxAttempts: Math.max(budget.maxAttempts, Math.min(parsedValue, 10)),
  };
}

export function createRewriteBudget(analysis: InputAnalysis): RewriteBudget {
  if (
    analysis.scenarioHint === "support_policy" ||
    analysis.riskLevel === "high" ||
    analysis.wordCount >= 260
  ) {
    return withTestWindowAttemptCap({
      maxAttempts: 5,
      maxCandidatesPerAttempt: 3,
      allowStrongModel: true,
      maxStrongAttempts: 1,
      maxEstimatedUsd: 0.12,
      reason: "High-risk or policy-heavy input gets more bounded attempts.",
    });
  }

  if (analysis.structureRisk === "medium" || analysis.factualDensity === "medium") {
    return withTestWindowAttemptCap({
      maxAttempts: 3,
      maxCandidatesPerAttempt: 3,
      allowStrongModel: false,
      maxStrongAttempts: 0,
      maxEstimatedUsd: 0.06,
      reason: "Medium-risk input gets normal retry budget.",
    });
  }

  return withTestWindowAttemptCap({
    maxAttempts: 2,
    maxCandidatesPerAttempt: 2,
    allowStrongModel: false,
    maxStrongAttempts: 0,
    maxEstimatedUsd: 0.03,
    reason: "Low-risk short input gets a small budget.",
  });
}

export function createInitialBudgetState(): RewriteBudgetState {
  return {
    attemptsUsed: 0,
    strongAttemptsUsed: 0,
    estimatedUsdUsed: 0,
  };
}

export function approveRewriteAttempt({
  budget,
  decision,
  state,
}: {
  budget: RewriteBudget;
  decision: RewriteStrategyDecision;
  state: RewriteBudgetState;
}) {
  if (state.attemptsUsed >= budget.maxAttempts) {
    return {
      approved: false,
      reason: "budget_exceeded:max_attempts",
    };
  }

  if (
    decision.modelTier === "strong" &&
    (!budget.allowStrongModel || state.strongAttemptsUsed >= budget.maxStrongAttempts)
  ) {
    return {
      approved: false,
      reason: "budget_exceeded:strong_model",
    };
  }

  if (state.estimatedUsdUsed >= budget.maxEstimatedUsd) {
    return {
      approved: false,
      reason: "budget_exceeded:estimated_cost",
    };
  }

  return {
    approved: true,
    reason: "approved",
  };
}

export function recordRewriteAttempt({
  decision,
  estimatedUsd,
  state,
}: {
  decision: RewriteStrategyDecision;
  estimatedUsd: number;
  state: RewriteBudgetState;
}): RewriteBudgetState {
  return {
    attemptsUsed: state.attemptsUsed + 1,
    strongAttemptsUsed:
      decision.modelTier === "strong"
        ? state.strongAttemptsUsed + 1
        : state.strongAttemptsUsed,
    estimatedUsdUsed: state.estimatedUsdUsed + Math.max(0, estimatedUsd),
  };
}
