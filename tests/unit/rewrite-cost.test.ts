import { describe, expect, it } from "vitest";

import {
  estimateOpenAiCostUsd,
  estimateSaplingCostUsd,
  summarizeProviderCalls,
} from "../../lib/observability/rewrite-cost";

describe("rewrite cost estimation", () => {
  it("estimates OpenAI cost from input and output tokens", () => {
    expect(
      estimateOpenAiCostUsd({
        inputTokens: 2000,
        outputTokens: 500,
        inputPer1M: 0.75,
        outputPer1M: 4.5,
      }),
    ).toBeCloseTo(0.00375, 6);
  });

  it("estimates Sapling cost from characters", () => {
    expect(
      estimateSaplingCostUsd({
        characters: 3000,
        pricePer1000Chars: 0.005,
      }),
    ).toBeCloseTo(0.015, 6);
  });

  it("summarizes provider calls into aggregate totals", () => {
    const summary = summarizeProviderCalls([
      {
        provider: "openai",
        role: "mid_writer",
        model: "gpt-5.4-mini",
        inputTokens: 1000,
        outputTokens: 300,
        estimatedCostUsd: 0.0021,
        success: true,
      },
      {
        provider: "sapling",
        role: "draft_signal",
        characters: 2500,
        estimatedCostUsd: 0.0125,
        success: true,
      },
    ]);

    expect(summary.openAiInputTokens).toBe(1000);
    expect(summary.openAiOutputTokens).toBe(300);
    expect(summary.saplingCallCount).toBe(1);
    expect(summary.saplingCharacters).toBe(2500);
    expect(summary.totalEstimatedCostUsd).toBeCloseTo(0.0146, 6);
  });
});
