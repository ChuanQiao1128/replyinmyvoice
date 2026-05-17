import { describe, expect, it } from "vitest";

import { normalizeRewriteOutput } from "../../lib/openai";

describe("normalizeRewriteOutput", () => {
  it("accepts string summaries from the model and normalizes them to arrays", () => {
    const result = normalizeRewriteOutput({
      rewrittenText: "Thanks for letting me know. I can review this tomorrow.",
      changeSummary: "Softened the opening and made the reply more specific.",
      riskNotes: "Review the late-work policy before sending.",
    });

    expect(result.changeSummary).toEqual([
      "Softened the opening and made the reply more specific.",
    ]);
    expect(result.riskNotes).toEqual([
      "Review the late-work policy before sending.",
    ]);
  });

  it("replaces empty risk notes with a review reminder", () => {
    const result = normalizeRewriteOutput({
      rewrittenText: "I can send the numbers by 10am tomorrow.",
      changeSummary: ["Made the timing clear."],
      riskNotes: [""],
    });

    expect(result.riskNotes).toEqual(["Review the reply before sending."]);
  });
});
