import { describe, expect, it } from "vitest";

import { createRewriteAttemptLedgerEntry } from "../../lib/rewrite-pipeline/attempt-ledger";

describe("rewrite attempt ledger", () => {
  it("records a failed attempt with evidence needed by the next retry", () => {
    const entry = createRewriteAttemptLedgerEntry({
      attemptNo: 2,
      strategy: "full_structure_rewrite",
      modelRole: "mid_writer",
      modelName: "deepseek-v4-pro",
      thinkingMode: "non_thinking",
      candidateText: "A failed candidate.",
      failureAnalysis: "The candidate split every sentence into a separate paragraph.",
      failureKinds: ["sentence_per_paragraph", "line_split_paraphrase"],
      factGateResult: "pass",
      structureGateResult: "fail:sentence_per_paragraph",
      policyIntentGateResult: "pass",
      saplingResult: "fail:signal_not_improved",
      nextStrategyDecision: "full_structure_rewrite",
    });

    expect(entry).toEqual({
      attemptNo: 2,
      strategy: "full_structure_rewrite",
      modelRole: "mid_writer",
      modelName: "deepseek-v4-pro",
      thinkingMode: "non_thinking",
      candidateText: "A failed candidate.",
      failureAnalysis: "The candidate split every sentence into a separate paragraph.",
      failureKinds: ["sentence_per_paragraph", "line_split_paraphrase"],
      factGateResult: "pass",
      structureGateResult: "fail:sentence_per_paragraph",
      policyIntentGateResult: "pass",
      saplingResult: "fail:signal_not_improved",
      nextStrategyDecision: "full_structure_rewrite",
    });
  });

  it("rejects attempt numbers outside the hard 10-attempt budget", () => {
    expect(() =>
      createRewriteAttemptLedgerEntry({
        attemptNo: 11,
        strategy: "quality_failure",
        modelRole: "strong_escalation",
        modelName: "deepseek-v4-pro",
        thinkingMode: "thinking_high",
        candidateText: "",
        failureAnalysis: "Budget exhausted.",
        failureKinds: ["naturalness_not_improved"],
        factGateResult: "pass",
        structureGateResult: "pass",
        policyIntentGateResult: "pass",
        saplingResult: "fail:still_high",
        nextStrategyDecision: "quality_failure",
      }),
    ).toThrow("attemptNo must be between 1 and 10");
  });
});
