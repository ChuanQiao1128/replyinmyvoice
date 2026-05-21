import { describe, expect, it } from "vitest";

import { rewriteRequestSchema } from "../../lib/validation";

describe("rewriteRequestSchema", () => {
  it("accepts a draft-only custom rewrite request", () => {
    const parsed = rewriteRequestSchema.parse({
      roughDraftReply:
        "Thank you for your consideration. I am writing to confirm that the attached document has been completed and is available for review.",
      tone: "direct",
      tonePreset: "Direct",
    });

    expect(parsed.scenario).toBe("General reply");
    expect(parsed.messageToReplyTo).toBe("");
    expect(parsed.roughDraftReply).toContain("attached document");
  });

  it("rejects unknown scenarios before provider calls", () => {
    const result = rewriteRequestSchema.safeParse({
      scenario: "Random social post",
      roughDraftReply:
        "Please rewrite this in a clearer and more natural way while preserving the details.",
      tone: "warm",
      tonePreset: "Warm",
    });

    expect(result.success).toBe(false);
  });

  it("enforces the confirmed per-field rewrite input limits", () => {
    expect(
      rewriteRequestSchema.safeParse({
        messageToReplyTo: "m".repeat(3001),
        roughDraftReply: "This draft is long enough.",
        tone: "warm",
        tonePreset: "Warm",
      }).success,
    ).toBe(false);

    expect(
      rewriteRequestSchema.safeParse({
        roughDraftReply: "d".repeat(3001),
        tone: "warm",
        tonePreset: "Warm",
      }).success,
    ).toBe(false);

    expect(
      rewriteRequestSchema.safeParse({
        roughDraftReply: "This draft is long enough.",
        audience: "a".repeat(301),
        tone: "warm",
        tonePreset: "Warm",
      }).success,
    ).toBe(false);

    expect(
      rewriteRequestSchema.safeParse({
        roughDraftReply: "This draft is long enough.",
        purpose: "p".repeat(501),
        tone: "warm",
        tonePreset: "Warm",
      }).success,
    ).toBe(false);

    expect(
      rewriteRequestSchema.safeParse({
        roughDraftReply: "This draft is long enough.",
        whatHappened: "w".repeat(1001),
        tone: "warm",
        tonePreset: "Warm",
      }).success,
    ).toBe(false);

    expect(
      rewriteRequestSchema.safeParse({
        roughDraftReply: "This draft is long enough.",
        factsToPreserve: "f".repeat(1001),
        tone: "warm",
        tonePreset: "Warm",
      }).success,
    ).toBe(false);
  });

  it("rejects requests over the confirmed combined cap", () => {
    const result = rewriteRequestSchema.safeParse({
      messageToReplyTo: "m".repeat(2500),
      roughDraftReply: "d".repeat(2501),
      tone: "direct",
      tonePreset: "Direct",
    });

    expect(result.success).toBe(false);
  });
});
