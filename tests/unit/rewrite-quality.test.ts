import { beforeEach, describe, expect, it, vi } from "vitest";

import type { RewriteRequestInput } from "../../lib/validation";

const mocks = vi.hoisted(() => ({
  generateRewriteCandidate: vi.fn(),
  generateRepairCandidate: vi.fn(),
  generateGuaranteedRewriteCandidate: vi.fn(),
  measureWritingSignal: vi.fn(),
}));

vi.mock("../../lib/openai", () => ({
  getRewriteStrategies: () => [
    { id: "plain_note", temperature: 0.68, instruction: "plain" },
    { id: "thread_reply", temperature: 0.45, instruction: "thread" },
    { id: "facts_first", temperature: 0, instruction: "facts first" },
  ],
  generateRewriteCandidate: mocks.generateRewriteCandidate,
  generateRepairCandidate: mocks.generateRepairCandidate,
  generateGuaranteedRewriteCandidate: mocks.generateGuaranteedRewriteCandidate,
}));

vi.mock("../../lib/writing-signal", () => ({
  measureWritingSignal: mocks.measureWritingSignal,
  formatNaturalness: (
    draftAiLikePercent: number | null,
    rewriteAiLikePercent: number | null,
  ) => ({
    draftAiLikePercent,
    rewriteAiLikePercent,
    changePoints:
      draftAiLikePercent === null || rewriteAiLikePercent === null
        ? null
        : rewriteAiLikePercent - draftAiLikePercent,
    label:
      draftAiLikePercent === null || rewriteAiLikePercent === null
        ? "unavailable"
        : rewriteAiLikePercent < draftAiLikePercent
          ? "lower"
          : "higher",
  }),
}));

import {
  isCandidateCompleteEnough,
  rewriteWithOptimization,
} from "../../lib/rewrite";

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

const longPriyaInput = inputWithDraft(
  [
    "Hi Priya,",
    "Thank you for contacting us regarding the usage report and invoice preview in your account.",
    "We understand that there appears to be a discrepancy between the number of active seats shown in the dashboard and the number of seats approved during your renewal.",
    "The most likely explanation is that the three temporary contractors were counted as active seats during May.",
    "Even if a user only joins for part of the month, prorated charges may apply.",
    "Please check whether the contractors are still active and send us their names if you would like assistance.",
  ].join(" "),
);

beforeEach(() => {
  mocks.generateRewriteCandidate.mockReset();
  mocks.generateRepairCandidate.mockReset();
  mocks.generateGuaranteedRewriteCandidate.mockReset();
  mocks.measureWritingSignal.mockReset();
});

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

  it("allows compact non-support replies when required facts are preserved", () => {
    const input: RewriteRequestInput = {
      scenario: "Email or message reply",
      messageToReplyTo:
        "We like the reporting feature and team templates, but we are comparing two other vendors and cannot decide until the first week of June.",
      roughDraftReply: "Long sales draft. ".repeat(80),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Friendly",
    };
    const candidate =
      "Hi Jordan,\n\nI can send a shorter summary of the two plan options. Glad the reporting feature and team templates are still useful while you compare the two other vendors before the first week of June.";

    expect(isCandidateCompleteEnough(input, candidate)).toBe(true);
  });

  it("rejects teacher-parent candidates that drop the requested work order", () => {
    const input: RewriteRequestInput = {
      scenario: "Email or message reply",
      messageToReplyTo:
        "Jordan has a low grade. What is missing, and can he make up the work?",
      roughDraftReply: [
        "Hi Monica,",
        "I would recommend that Jordan first focus on the reading response and vocabulary practice, since those can be completed more quickly.",
        "After that, he can work on the short reflection paragraph from Friday.",
        "If he turns these in by the end of this week, I will still accept them for partial credit.",
        "I also encourage him to speak with me after class or during lunch if he needs clarification.",
      ].join("\n\n"),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Friendly",
    };

    const candidate = [
      "Hi Monica,",
      "Jordan is missing the reading response, vocabulary practice, and the short reflection paragraph from Friday.",
      "If he turns those in by the end of this week, I can still accept them for partial credit.",
      "He can also come by after class or during lunch if any instructions are unclear.",
    ].join("\n\n");

    expect(isCandidateCompleteEnough(input, candidate)).toBe(false);
  });

  it("rejects teacher-parent candidates that drop the supportive closing and signature", () => {
    const input: RewriteRequestInput = {
      scenario: "Email or message reply",
      messageToReplyTo:
        "Jordan has a low grade. What is missing, and can he make up the work?",
      roughDraftReply: [
        "Hi Monica,",
        "I would recommend that Jordan first focus on the reading response and vocabulary practice, since those can be completed more quickly.",
        "After that, he can work on the short reflection paragraph from Friday.",
        "If he turns these in by the end of this week, I will still accept them for partial credit.",
        "I also encourage him to speak with me after class or during lunch if he needs clarification.",
        "I appreciate your partnership and your willingness to help Jordan take responsibility.",
        "I believe he can get back on track with a clear plan and steady follow-through.",
        "Best regards,",
        "Ms. Carter",
      ].join("\n\n"),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Friendly",
    };

    const candidate = [
      "Hi Monica,",
      "Jordan should start with the reading response and vocabulary practice since those can be done quickly.",
      "Then he can work on the short reflection paragraph from Friday.",
      "If he turns those in by the end of this week, I can still accept them for partial credit.",
      "He can also come by after class or during lunch if any instructions are unclear.",
    ].join("\n\n");

    expect(isCandidateCompleteEnough(input, candidate)).toBe(false);
  });
});

describe("rewriteWithOptimization quality gate", () => {
  it("repairs a worse first candidate and returns the repaired passing candidate", async () => {
    mocks.generateRewriteCandidate.mockResolvedValueOnce({
      rewrittenText:
        "Hi Priya,\n\nFrom what you described, it seems the invoice preview is higher. For next steps, please check whether the accounts are active.",
      changeSummary: ["First attempt."],
      riskNotes: ["Review."],
    });
    mocks.generateRepairCandidate.mockResolvedValueOnce({
      rewrittenText:
        "Hi Priya,\n\nThe higher preview is likely from the three temporary contractor seats being counted during May, not a base plan change.\n\nThat would explain the move from 15 approved seats to 18 active seats and the NZD $126 increase. If you send the contractor names or emails, we can help confirm whether they are still active.",
      changeSummary: ["Removed support-template wording."],
      riskNotes: ["Check the seat details before sending."],
    });
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 89 })
      .mockResolvedValueOnce({ aiLikePercent: 99 })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithOptimization(longPriyaInput);

    expect(mocks.generateRepairCandidate).toHaveBeenCalledTimes(1);
    expect(result.rewrittenText).toContain("NZD $126");
    expect(result.naturalness.rewriteAiLikePercent).toBe(18);
    expect(result.optimization.rejectedCandidates).toBe(1);
    expect(result.optimization.repairCandidatesTried).toBe(1);
  });

  it("does not ship internal fact-restoration notes as the final rewrite", async () => {
    const input = inputWithDraft(
      "Hi Priya, The invoice preview is NZD $126 higher because the dashboard shows 18 active seats instead of the 15 regular seats your team approved. The three temporary contractors were supposed to be removed after May 8.",
    );
    mocks.generateRewriteCandidate.mockResolvedValueOnce({
      rewrittenText:
        "Hi Priya,\n\nThe invoice preview is higher because of temporary users.",
      changeSummary: ["First attempt."],
      riskNotes: ["Review."],
    });
    mocks.generateRepairCandidate.mockResolvedValueOnce({
      rewrittenText:
        "Hi Priya,\n\nThe higher invoice preview is most likely from the three temporary contractors being counted during May, not a base plan change.\n\nThat would explain the move from 15 regular seats to 18 active seats and the NZD $126 increase. If they were not removed after May 8, send the contractor names or emails and we can help confirm whether they are still active.",
      changeSummary: ["Restored missing billing details."],
      riskNotes: ["Check the seat details before sending."],
    });
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 89 })
      .mockResolvedValueOnce({ aiLikePercent: 12 })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithOptimization(input);

    expect(mocks.generateRepairCandidate).toHaveBeenCalledTimes(1);
    expect(result.rewrittenText).not.toContain("Key details to keep");
    expect(result.rewrittenText).toContain("NZD $126");
  });

  it("keeps the original draft instead of returning a complete but worse candidate", async () => {
    mocks.generateRewriteCandidate
      .mockResolvedValueOnce({
        rewrittenText:
          "Hi Priya,\n\nFrom what you described, it seems the invoice preview is higher. For next steps, please check whether the accounts are active.",
        changeSummary: ["First attempt."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText:
          "Hi Priya,\n\nI see how this can be confusing. It seems the billing issue may be related to temporary users.",
        changeSummary: ["Second attempt."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText:
          "Hi Priya,\n\nThe jump looks tied to the three temporary contractors being counted during May, not a base plan change.\n\nThat explains the 15 approved seats showing as 18 active seats. The NZD $126 increase looks like prorated seat usage for the days those accounts had access.\n\nIf you send the names or email addresses, we can help confirm whether they are still active.",
        changeSummary: ["Facts-first fallback."],
        riskNotes: ["Signal still needs review."],
      });
    mocks.generateRepairCandidate
      .mockResolvedValueOnce({
        rewrittenText:
          "Hi Priya,\n\nTo help clarify, the invoice may have changed because of user counts.",
        changeSummary: ["Repair one."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText:
          "Hi Priya,\n\nFor next steps, please check whether the contractor accounts are active.",
        changeSummary: ["Repair two."],
        riskNotes: ["Review."],
      });
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 89 })
      .mockResolvedValueOnce({ aiLikePercent: 99 })
      .mockResolvedValueOnce({ aiLikePercent: 98 })
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 97 })
      .mockResolvedValueOnce({ aiLikePercent: 95 });

    const result = await rewriteWithOptimization(longPriyaInput);

    expect(mocks.generateRepairCandidate).toHaveBeenCalledTimes(2);
    expect(result.rewrittenText).toBe(longPriyaInput.roughDraftReply);
    expect(result.naturalness.rewriteAiLikePercent).toBe(89);
    expect(result.optimization.selectionStatus).toBe("best_available");
    expect(result.riskNotes).toContain(
      "The writing signal is still high; review before sending.",
    );
  });

  it("continues to deterministic strategies when the first model attempt fails", async () => {
    mocks.generateRewriteCandidate
      .mockRejectedValueOnce(new Error("provider timeout"))
      .mockResolvedValueOnce({
        rewrittenText:
          "Hi Priya,\n\nThe jump looks tied to the three temporary contractors being counted during May, not a base plan change.\n\nThat explains the 15 regular seats showing as 18 active seats. The NZD $126 increase looks like prorated seat usage for the days those accounts had access.\n\nIf you send the names or email addresses, we can help confirm whether they are still active.",
        changeSummary: ["Used the deterministic thread fallback."],
        riskNotes: ["Check the billing details before sending."],
      });
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 24 });

    const result = await rewriteWithOptimization(longPriyaInput);

    expect(mocks.generateRewriteCandidate).toHaveBeenCalledTimes(2);
    expect(result.rewrittenText).toContain("NZD $126");
    expect(result.naturalness.rewriteAiLikePercent).toBe(24);
    expect(result.optimization.candidateSignals.at(0)?.reason).toBe(
      "Candidate generation failed before signal check.",
    );
  });

  it("returns a guaranteed fallback when no measured candidate is complete enough", async () => {
    mocks.generateRewriteCandidate
      .mockResolvedValueOnce({
        rewrittenText: "Hi Priya, the invoice changed.",
        changeSummary: ["Too short."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText: "Hi Priya, please check the accounts.",
        changeSummary: ["Too short."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText: "Hi Priya, thanks.",
        changeSummary: ["Too short."],
        riskNotes: ["Review."],
      });
    mocks.generateRepairCandidate
      .mockResolvedValueOnce({
        rewrittenText: "Hi Priya, the seat count changed.",
        changeSummary: ["Still too short."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText: "Hi Priya, the invoice needs checking.",
        changeSummary: ["Still too short."],
        riskNotes: ["Review."],
      });
    mocks.generateGuaranteedRewriteCandidate.mockResolvedValueOnce({
      rewrittenText:
        "Hi Priya,\n\nThe jump looks tied to the three temporary contractors being counted during May, not a base plan change.\n\nThat explains the 15 regular seats showing as 18 active seats. The NZD $126 increase looks like prorated seat usage for the days those accounts had access.\n\nIf you send the names or email addresses, we can help confirm whether they are still active.",
      changeSummary: ["Used the guaranteed facts-first fallback."],
      riskNotes: ["Review the facts before sending."],
    });
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 99 })
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 99 })
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 97 });

    const result = await rewriteWithOptimization(longPriyaInput);

    expect(mocks.generateGuaranteedRewriteCandidate).toHaveBeenCalledTimes(1);
    expect(result.rewrittenText).toContain("NZD $126");
    expect(result.optimization.selectionStatus).toBe("best_available");
    expect(result.optimization.candidateSignals.at(-1)?.stage).toBe("fallback");
  });

  it("does not return a measured candidate that worsens an already-low draft", async () => {
    const lowSignalInput = inputWithDraft(
      "The April export is missing the custom tags column. Please send the export settings before Monday at 10am so we can check the report path.",
    );
    mocks.generateRewriteCandidate
      .mockResolvedValueOnce({
        rewrittenText:
          "Thank you for contacting us regarding the April export issue. We understand that the custom tags column is missing, and our team will review the export settings before Monday at 10am.",
        changeSummary: ["Rewrote the support reply."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText:
          "We are investigating the April export and custom tags column before Monday at 10am.",
        changeSummary: ["Shortened the reply."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText:
          "The April export is missing the custom tags column. Please send the export settings before Monday at 10am.",
        changeSummary: ["Facts-first fallback."],
        riskNotes: ["Review."],
      });
    mocks.generateRepairCandidate
      .mockResolvedValueOnce({
        rewrittenText:
          "Thank you for the note. The April export is missing the custom tags column, and we can check it before Monday at 10am.",
        changeSummary: ["Repair."],
        riskNotes: ["Review."],
      })
      .mockResolvedValueOnce({
        rewrittenText:
          "The April export has a custom tags column issue. Send settings before Monday at 10am.",
        changeSummary: ["Repair."],
        riskNotes: ["Review."],
      });
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 12 })
      .mockResolvedValueOnce({ aiLikePercent: 74 })
      .mockResolvedValueOnce({ aiLikePercent: 68 })
      .mockResolvedValueOnce({ aiLikePercent: 64 })
      .mockResolvedValueOnce({ aiLikePercent: 58 })
      .mockResolvedValueOnce({ aiLikePercent: 30 });

    const result = await rewriteWithOptimization(lowSignalInput);

    expect(result.rewrittenText).toBe(lowSignalInput.roughDraftReply);
    expect(result.naturalness.rewriteAiLikePercent).toBe(12);
    expect(result.optimization.selectionStatus).toBe("best_available");
    expect(result.optimization.candidateSignals.at(-1)?.reason).toContain(
      "worsened the writing signal",
    );
  });
});
