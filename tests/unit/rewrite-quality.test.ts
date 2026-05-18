import { beforeEach, describe, expect, it, vi } from "vitest";

import type { RewriteRequestInput } from "../../lib/validation";

const mocks = vi.hoisted(() => ({
  generateRewriteCandidate: vi.fn(),
  generateRepairCandidate: vi.fn(),
  measureWritingSignal: vi.fn(),
}));

vi.mock("../../lib/openai", () => ({
  getRewriteStrategies: () => [
    { id: "plain_note", temperature: 0.68, instruction: "plain" },
    { id: "thread_reply", temperature: 0.45, instruction: "thread" },
  ],
  generateRewriteCandidate: mocks.generateRewriteCandidate,
  generateRepairCandidate: mocks.generateRepairCandidate,
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
  RewriteQualityError,
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

  it("throws a quality error instead of returning a worse candidate", async () => {
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
      .mockResolvedValueOnce({ aiLikePercent: 97 });

    await expect(rewriteWithOptimization(longPriyaInput)).rejects.toBeInstanceOf(
      RewriteQualityError,
    );
    expect(mocks.generateRepairCandidate).toHaveBeenCalledTimes(2);
  });
});
