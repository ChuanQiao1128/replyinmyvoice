import { describe, expect, it } from "vitest";

import { selectHighRiskSentences } from "../../lib/rewrite-pipeline/targeted-repair";

describe("selectHighRiskSentences", () => {
  it("selects the highest sentence scores above the naturalness threshold", () => {
    const selected = selectHighRiskSentences({
      threshold: 40,
      signal: {
        aiLikePercent: 72,
        sentenceScores: [
          { sentence: "OK.", aiLikePercent: 99 },
          {
            sentence:
              "I completely understand your concern and appreciate your partnership moving forward.",
            aiLikePercent: 93,
          },
          {
            sentence:
              "Jordan can come by during lunch on Tuesday or Thursday.",
            aiLikePercent: 12,
          },
          {
            sentence:
              "Thank you again for your support in helping Jordan take responsibility.",
            aiLikePercent: 75,
          },
          {
            sentence:
              "With consistent effort, I believe he can get back on track.",
            aiLikePercent: 67,
          },
          {
            sentence:
              "If he submits all three by this Friday at 5 p.m., I can still give partial credit.",
            aiLikePercent: 44,
          },
        ],
      },
    });

    expect(selected).toEqual([
      {
        sentence:
          "I completely understand your concern and appreciate your partnership moving forward.",
        aiLikePercent: 93,
      },
      {
        sentence:
          "Thank you again for your support in helping Jordan take responsibility.",
        aiLikePercent: 75,
      },
      {
        sentence:
          "With consistent effort, I believe he can get back on track.",
        aiLikePercent: 67,
      },
    ]);
  });
});
