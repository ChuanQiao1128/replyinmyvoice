import type {
  InputAnalysis,
  RewriteFailureKind,
  RewriteStrategyDecision,
} from "./types";

function hasAny(
  failures: RewriteFailureKind[],
  expected: RewriteFailureKind[],
) {
  return expected.some((failure) => failures.includes(failure));
}

export function chooseInitialStrategy(
  analysis: InputAnalysis,
): RewriteStrategyDecision {
  const strategy = analysis.recommendedInitialStrategy;

  return {
    strategy,
    reason: `Initial route from ${analysis.reasons.join(", ")}.`,
    failureKinds: [],
    modelTier:
      strategy === "strong_model_restructure" ? "strong" : "standard",
    maxCandidates: strategy === "minimal_polish" ? 2 : 3,
    preserveFacts: true,
    preserveStructure: analysis.requiresStructurePreservation,
  };
}

export function chooseNextStrategy({
  analysis,
  failures,
  previousStrategies,
}: {
  analysis: InputAnalysis;
  failures: RewriteFailureKind[];
  previousStrategies: string[];
}): RewriteStrategyDecision {
  if (failures.includes("provider_unavailable")) {
    return {
      strategy: "quality_failure",
      reason: "Provider unavailable; stop without a successful rewrite.",
      failureKinds: failures,
      modelTier: "standard",
      maxCandidates: 0,
      preserveFacts: true,
      preserveStructure: true,
      stopReason: "provider_unavailable",
    };
  }

  if (
    hasAny(failures, [
      "fact_loss",
      "unsupported_fact",
      "changed_policy_or_condition",
      "policy_intent_drift",
    ])
  ) {
    return {
      strategy: analysis.requiresPolicyCare
        ? "support_policy_options_rewrite"
        : "facts_first_reconstruct",
      reason: "Fact or policy failure requires rebuilding from locked facts.",
      failureKinds: failures,
      modelTier: "standard",
      maxCandidates: 3,
      preserveFacts: true,
      preserveStructure: analysis.requiresStructurePreservation,
    };
  }

  if (
    hasAny(failures, [
      "broken_numbered_list",
      "broken_quote_boundary",
      "sentence_per_paragraph",
      "line_split_paraphrase",
      "repeated_structure_failure",
    ])
  ) {
    return {
      strategy: analysis.containsListsOrQuotes
        ? "quote_list_safe_rewrite"
        : "full_structure_rewrite",
      reason: "Structure failure requires a structure-level rewrite, not local phrasing repair.",
      failureKinds: failures,
      modelTier: "standard",
      maxCandidates: 3,
      preserveFacts: true,
      preserveStructure: true,
    };
  }

  if (
    hasAny(failures, [
      "support_macro_voice",
      "naturalness_not_improved",
      "too_generic",
      "over_rewritten",
    ])
  ) {
    if (previousStrategies.includes("targeted_sentence_repair")) {
      return {
        strategy: "strong_model_restructure",
        reason: "A local repair already missed; escalate once to a stronger structure rewrite.",
        failureKinds: failures,
        modelTier: "strong",
        maxCandidates: 1,
        preserveFacts: true,
        preserveStructure: analysis.requiresStructurePreservation,
      };
    }

    return {
      strategy: "targeted_sentence_repair",
      reason: "Naturalness failure without fact drift can be repaired locally first.",
      failureKinds: failures,
      modelTier: "standard",
      maxCandidates: 1,
      preserveFacts: true,
      preserveStructure: true,
    };
  }

  return {
    strategy: analysis.recommendedInitialStrategy,
    reason: "No specific failure matched; reuse the initial route.",
    failureKinds: failures,
    modelTier: "standard",
    maxCandidates: analysis.recommendedInitialStrategy === "minimal_polish" ? 2 : 3,
    preserveFacts: true,
    preserveStructure: analysis.requiresStructurePreservation,
  };
}
