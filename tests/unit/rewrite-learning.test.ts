import { describe, expect, it } from "vitest";

import { RewriteQualityError, type RewriteResponsePayload } from "../../lib/rewrite";
import { buildRewriteLearningSample } from "../../lib/rewrite-learning";
import type { RewriteRequestInput } from "../../lib/validation";

const input: RewriteRequestInput = {
  scenario: "Customer support",
  messageToReplyTo: "Hi team, the May invoice is higher than expected.",
  roughDraftReply:
    "Dear customer, thank you for contacting us regarding the billing discrepancy.",
  audience: "",
  purpose: "",
  whatHappened: "",
  factsToPreserve: "",
  tone: "warm",
  tonePreset: "Professional",
};

const response: RewriteResponsePayload = {
  rewrittenText: "Hi Priya, I checked the May invoice preview.",
  changeSummary: ["Removed template wording."],
  riskNotes: ["Review billing facts before sending."],
  naturalness: {
    draftAiLikePercent: 89,
    rewriteAiLikePercent: 32,
    changePoints: -57,
    label: "lower",
  },
  optimization: {
    internalStrategiesTried: 2,
    repairCandidatesTried: 1,
    rejectedCandidates: 1,
    userUsageCharged: 1,
    diagnosisTags: ["support_template_voice", "corporate_polish"],
    rewritePlanSummary: "Keep billing facts and remove macro phrasing.",
    candidateSignals: [
      {
        stage: "repair",
        aiLikePercent: 32,
        status: "pass_below_threshold",
        rejected: false,
        reason: "Candidate passed signal target.",
      },
    ],
  },
};

describe("rewrite learning samples", () => {
  it("builds a success sample with content, scores, diagnosis, and repair metadata", () => {
    const sample = buildRewriteLearningSample({
      user: { id: "user_123" },
      input,
      status: "success",
      response,
    });

    expect(sample.userId).toBe("user_123");
    expect(sample.scenario).toBe("Customer support");
    expect(sample.tonePreset).toBe("Professional");
    expect(sample.messageToReplyTo).toContain("May invoice");
    expect(sample.roughDraftReply).toContain("billing discrepancy");
    expect(sample.rewrittenText).toContain("May invoice preview");
    expect(sample.draftAiLikePercent).toBe(89);
    expect(sample.rewriteAiLikePercent).toBe(32);
    expect(sample.changePoints).toBe(-57);
    expect(JSON.parse(sample.diagnosisTags)).toEqual([
      "support_template_voice",
      "corporate_polish",
    ]);
    expect(sample.internalStrategies).toBe(2);
    expect(sample.repairCandidates).toBe(1);
    expect(sample.status).toBe("success");
    expect(sample.errorCode).toBeNull();
  });

  it("builds a quality-failure sample without a rewritten result", () => {
    const qualityError = new RewriteQualityError({
      naturalness: {
        draftAiLikePercent: 89,
        rewriteAiLikePercent: 99,
        changePoints: 10,
        label: "still_high",
      },
      rejectedCandidates: 3,
      repairCandidatesTried: 2,
      candidateSignals: [
        {
          stage: "repair",
          aiLikePercent: 99,
          status: "fail_worse",
          rejected: true,
          reason: "Rewrite signal was not lower than draft.",
        },
      ],
      diagnosisTags: ["uniform_rhythm"],
      rewritePlanSummary: "Break template rhythm.",
    });

    const sample = buildRewriteLearningSample({
      user: { id: "user_123" },
      input,
      status: "quality_failed",
      qualityError,
    });

    expect(sample.rewrittenText).toBeNull();
    expect(sample.draftAiLikePercent).toBe(89);
    expect(sample.rewriteAiLikePercent).toBe(99);
    expect(JSON.parse(sample.diagnosisTags)).toEqual(["uniform_rhythm"]);
    expect(sample.repairCandidates).toBe(2);
    expect(sample.rejectedCandidates).toBe(3);
    expect(sample.status).toBe("quality_failed");
    expect(sample.errorCode).toBe("quality_gate_failed");
  });
});
