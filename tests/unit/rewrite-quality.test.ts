import { describe, expect, it } from "vitest";

import { isCandidateCompleteEnough } from "../../lib/rewrite";
import type { RewriteRequestInput } from "../../lib/validation";

function inputWithDraft(roughDraftReply: string): RewriteRequestInput {
  return {
    scenario: "Customer support",
    messageToReplyTo: "",
    roughDraftReply,
    audience: "Customer or client",
    purpose: "Clarify a misunderstanding",
    whatHappened: "",
    factsToPreserve: "",
    tone: "warm",
    tonePreset: "Professional",
  };
}

describe("isCandidateCompleteEnough", () => {
  it("rejects very short candidates for long customer-support drafts", () => {
    const input = inputWithDraft("A long support draft. ".repeat(80));

    expect(isCandidateCompleteEnough(input, "Hi Priya.\n\nThanks.")).toBe(false);
  });

  it("allows substantial candidates for long customer-support drafts", () => {
    const input = inputWithDraft("A long support draft. ".repeat(80));
    const candidate = [
      "Hi Priya,",
      "",
      "Thanks for laying this out. The higher invoice preview is most likely tied to the three temporary contractor seats rather than a base plan change.",
      "",
      "Even if someone only had access for part of the month, that access can still create a prorated charge for the days they were active. That would explain why the dashboard shows 18 active seats instead of 15 regular seats, and why the preview is NZD $126 higher.",
      "",
      "For the next step, check whether those contractor accounts are still active. If you want us to help confirm, send over their names or email addresses and we can check their status.",
    ].join("\n");

    expect(isCandidateCompleteEnough(input, candidate)).toBe(true);
  });
});
