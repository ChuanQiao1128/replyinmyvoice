import { describe, expect, it } from "vitest";

import {
  generateRewriteCandidate,
  getRewriteStrategies,
  normalizeRewriteOutput,
} from "../../lib/openai";

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

describe("thread fallback rewrite pass", () => {
  const threadFallback = getRewriteStrategies().find(
    (strategy) => strategy.id === "thread_reply",
  );

  it("uses invoice facts from the request instead of hardcoded sample details", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        messageToReplyTo: "Why is this invoice higher than last month?",
        roughDraftReply:
          "Thank you for contacting us regarding your invoice. The amount may vary based on service usage.",
        audience: "A customer asking about invoice amount",
        purpose: "Explain without sounding generic",
        whatHappened: "They added three seats on June 2.",
        factsToPreserve:
          "Three seats added June 2. Includes prorated seat charges.",
        tone: "direct",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("Three seats");
    expect(result.rewrittenText).toContain("June 2");
    expect(result.rewrittenText).not.toContain("two seats");
    expect(result.rewrittenText).not.toContain("May 4");
  });

  it("uses delay timing from the request instead of hardcoded sample timing", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        messageToReplyTo: "Can you send the numbers today?",
        roughDraftReply:
          "Unfortunately, due to unforeseen circumstances, the requested numbers cannot be provided today.",
        audience: "A teammate waiting for numbers",
        purpose: "Explain a short delay",
        whatHappened: "The source file arrived late.",
        factsToPreserve: "Source file arrived late. Send by 4pm Friday.",
        tone: "warm",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("4pm Friday");
    expect(result.rewrittenText).not.toContain("10am tomorrow");
  });
});
