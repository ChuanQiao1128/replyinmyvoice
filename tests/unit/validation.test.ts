import { describe, expect, it } from "vitest";

import { rewriteRequestSchema } from "../../lib/validation";

describe("rewriteRequestSchema", () => {
  it("accepts a draft-only custom rewrite request", () => {
    const parsed = rewriteRequestSchema.parse({
      scenario: "Blank / custom",
      roughDraftReply:
        "Thank you for your consideration. I am writing to confirm that the attached document has been completed and is available for review.",
      tone: "direct",
      tonePreset: "Concise",
    });

    expect(parsed.scenario).toBe("Blank / custom");
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
});
