import { describe, expect, it } from "vitest";

import {
  chooseInitialStrategy,
  chooseNextStrategy,
} from "../../lib/rewrite-pipeline/strategy-router";
import type { InputAnalysis } from "../../lib/rewrite-pipeline/types";

const analysis: InputAnalysis = {
  inputKind: "reply_with_context",
  scenarioHint: "support_policy",
  riskLevel: "high",
  factualDensity: "high",
  structureRisk: "medium",
  rewriteFreedom: "minimal",
  requiresStructurePreservation: false,
  requiresPolicyCare: true,
  containsForwardedThread: false,
  containsListsOrQuotes: false,
  wordCount: 260,
  paragraphCount: 5,
  recommendedInitialStrategy: "support_policy_options_rewrite",
  reasons: ["support_policy", "risk:high"],
};

describe("strategy router", () => {
  it("uses the input analysis for initial routing", () => {
    const decision = chooseInitialStrategy(analysis);

    expect(decision.strategy).toBe("support_policy_options_rewrite");
    expect(decision.preserveFacts).toBe(true);
  });

  it("routes fact and policy failures back through a policy-safe strategy", () => {
    const decision = chooseNextStrategy({
      analysis,
      failures: ["changed_policy_or_condition", "policy_intent_drift"],
      previousStrategies: ["support_policy_options_rewrite"],
    });

    expect(decision.strategy).toBe("support_policy_options_rewrite");
    expect(decision.modelTier).toBe("standard");
  });

  it("uses a structure-level strategy for sentence-per-paragraph failures", () => {
    const decision = chooseNextStrategy({
      analysis: {
        ...analysis,
        containsListsOrQuotes: true,
        requiresStructurePreservation: true,
      },
      failures: ["sentence_per_paragraph", "broken_numbered_list"],
      previousStrategies: ["facts_first_reconstruct"],
    });

    expect(decision.strategy).toBe("quote_list_safe_rewrite");
    expect(decision.preserveStructure).toBe(true);
  });

  it("escalates after targeted repair has already missed", () => {
    const decision = chooseNextStrategy({
      analysis,
      failures: ["naturalness_not_improved"],
      previousStrategies: [
        "support_policy_options_rewrite",
        "targeted_sentence_repair",
      ],
    });

    expect(decision.strategy).toBe("strong_model_restructure");
    expect(decision.modelTier).toBe("strong");
  });
});
