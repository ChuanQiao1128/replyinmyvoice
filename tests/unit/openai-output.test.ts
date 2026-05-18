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

  it("fills safe defaults when the model omits metadata but returns rewritten text", () => {
    const result = normalizeRewriteOutput({
      rewrittenText: "I can send the corrected file before Monday at 10am.",
    });

    expect(result.changeSummary).toEqual([
      "Rewrote the draft into a more natural reply.",
    ]);
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
        scenario: "Customer support",
        messageToReplyTo: "Why is this invoice higher than last month?",
        roughDraftReply:
          "Thank you for contacting us regarding your invoice. The amount may vary based on service usage.",
        audience: "A customer asking about invoice amount",
        purpose: "Explain without sounding generic",
        whatHappened: "They added three seats on June 2.",
        factsToPreserve:
          "Three seats added June 2. Includes prorated seat charges.",
        tone: "direct",
        tonePreset: "Professional",
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
        scenario: "Work update",
        messageToReplyTo: "Can you send the numbers today?",
        roughDraftReply:
          "Unfortunately, due to unforeseen circumstances, the requested numbers cannot be provided today.",
        audience: "A teammate waiting for numbers",
        purpose: "Explain a short delay",
        whatHappened: "The source file arrived late.",
        factsToPreserve: "Source file arrived late. Send by 4pm Friday.",
        tone: "warm",
        tonePreset: "Warm",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("4pm Friday");
    expect(result.rewrittenText).not.toContain("10am tomorrow");
  });

  it("keeps export support facts in the deterministic support fallback", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Customer support",
        messageToReplyTo:
          "The April and May CSV exports are missing the custom tags column for the Northeast region, and we need a corrected file before Monday at 10am.",
        roughDraftReply:
          "Thank you for contacting us regarding the missing custom tags column. Our team is investigating and will provide an update.",
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "direct",
        tonePreset: "Professional",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("custom tags column");
    expect(result.rewrittenText).toContain("April");
    expect(result.rewrittenText).toContain("May");
    expect(result.rewrittenText).toContain("Northeast region");
    expect(result.rewrittenText).toContain("Monday");
    expect(result.rewrittenText).toContain("10am");
  });

  it("answers sales follow-up context instead of repeating generic proposal language", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Email or message reply",
        messageToReplyTo:
          "We like the reporting feature, but the team is comparing two other vendors and probably will not decide until next month.",
        roughDraftReply:
          "Hello Jordan, I am following up on our previous communication regarding the proposal. Please advise whether you would like to proceed with the proposal as discussed.",
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "warm",
        tonePreset: "Friendly",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("Jordan");
    expect(result.rewrittenText).toContain("reporting feature");
    expect(result.rewrittenText).toContain("next month");
    expect(result.rewrittenText).not.toContain("Please advise");
  });

  it("builds a facts-first support rewrite from extracted billing facts", async () => {
    const factsFirst = getRewriteStrategies().find(
      (strategy) => strategy.id === "facts_first",
    );
    expect(factsFirst).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Customer support",
        messageToReplyTo: "",
        roughDraftReply: [
          "Hi Priya,",
          "Thanks for explaining the situation clearly.",
          "Based on what you've described, the increase is most likely related to the three contractor accounts that were added during the first week of May.",
          "That would explain why the dashboard is showing 18 active seats instead of the 15 regular seats your team approved.",
          "In plain English, this looks like a prorated seat charge rather than a change to your base plan.",
          "So the extra NZD $126 is likely coming from the three temporary users being active for part of the billing period.",
          "You could explain it to your finance manager like this.",
          "For the next step, please check whether those three contractor accounts are still active.",
          "If you send over the names or email addresses of the three contractors, we can help confirm whether they are still active.",
        ].join("\n\n"),
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "warm",
        tonePreset: "Warm",
      },
      factsFirst!,
    );

    expect(result.rewrittenText).toContain("Priya");
    expect(result.rewrittenText).toContain("18 active seats");
    expect(result.rewrittenText).toContain("15 regular seats");
    expect(result.rewrittenText).toContain("NZD $126");
    expect(result.rewrittenText).toContain("finance manager");
    expect(result.rewrittenText).toContain("names or email addresses");
    expect(result.rewrittenText).not.toContain("Based on what");
    expect(result.rewrittenText).not.toContain("For the next step");
  });
});
