import { beforeEach, describe, expect, it, vi } from "vitest";

import type {
  CandidateSet,
  ExtractedFacts,
  LlmFactCheckResult,
  ReviewResult,
  ScenarioClassification,
} from "../../lib/rewrite-pipeline/types";
import type { RewriteRequestInput } from "../../lib/validation";

const mocks = vi.hoisted(() => ({
  classifyScenario: vi.fn(),
  diagnoseHighRiskSentences: vi.fn(),
  escalateCandidate: vi.fn(),
  extractFacts: vi.fn(),
  finalizeCandidate: vi.fn(),
  generateCandidates: vi.fn(),
  generateGuaranteedRewriteCandidate: vi.fn(),
  llmFactCheck: vi.fn(),
  repairHighRiskSentences: vi.fn(),
  reviewCandidates: vi.fn(),
  measureWritingSignal: vi.fn(),
}));

vi.mock("../../lib/rewrite-pipeline/model", () => ({
  JsonCompletionQualityError: class JsonCompletionQualityError extends Error {},
  classifyScenario: mocks.classifyScenario,
  diagnoseHighRiskSentences: mocks.diagnoseHighRiskSentences,
  escalateCandidate: mocks.escalateCandidate,
  extractFacts: mocks.extractFacts,
  finalizeCandidate: mocks.finalizeCandidate,
  generateCandidates: mocks.generateCandidates,
  llmFactCheck: mocks.llmFactCheck,
  repairHighRiskSentences: mocks.repairHighRiskSentences,
  reviewCandidates: mocks.reviewCandidates,
}));

vi.mock("../../lib/writing-signal", async () => {
  const actual =
    await vi.importActual<typeof import("../../lib/writing-signal")>(
      "../../lib/writing-signal",
    );

  return {
    ...actual,
    measureWritingSignal: mocks.measureWritingSignal,
  };
});

vi.mock("../../lib/openai", () => ({
  generateGuaranteedRewriteCandidate: mocks.generateGuaranteedRewriteCandidate,
}));

const input: RewriteRequestInput = {
  scenario: "General reply",
  messageToReplyTo: "",
  roughDraftReply: [
    "Hi Monica,",
    "Thank you for reaching out and sharing your concerns about Jordan's grade.",
    "Jordan is missing three assignments from the past two weeks: the reading response, the vocabulary practice, and the short reflection paragraph from last Friday.",
    "If he submits all three assignments by this Friday at 5 p.m., I will still accept them for partial credit.",
    "He can see me during lunch on Tuesday or Thursday.",
    "Best regards,",
    "Ms. Carter",
  ].join("\n\n"),
  audience: "",
  purpose: "",
  whatHappened: "",
  factsToPreserve: "",
  tone: "warm",
  tonePreset: "Warm",
};

const facts: ExtractedFacts = {
  recipient_name: "Monica",
  sender_name_or_role: "Ms. Carter",
  people_mentioned: ["Monica", "Jordan", "Ms. Carter"],
  main_purpose: "Explain Jordan's missing assignments and next steps.",
  key_facts: [
    "Jordan is missing three assignments from the past two weeks.",
    "The missing assignments are the reading response, vocabulary practice, and the short reflection paragraph from last Friday.",
  ],
  required_actions: [
    "Jordan should complete the reading response, vocabulary practice, and short reflection paragraph.",
  ],
  deadlines: ["this Friday at 5 p.m."],
  dates_times: ["last Friday", "Tuesday", "Thursday", "this Friday at 5 p.m."],
  positive_notes: [],
  concerns: ["Missing written work is affecting the grade."],
  policies_or_conditions: ["The assignments can receive partial credit."],
  available_support: ["Lunch on Tuesday or Thursday."],
  clarifications: [],
  facts_that_must_not_change: [
    "Jordan",
    "three assignments",
    "this Friday at 5 p.m.",
    "partial credit",
    "Tuesday or Thursday",
  ],
  sensitive_points: ["Do not promise full credit."],
  original_tone: "polished teacher email",
};

const scenario: ScenarioClassification = {
  domain: "education",
  intent: "explain",
  format: "email",
  relationship: "teacher_to_parent",
  risk: "medium",
  confidence: 0.92,
  style_card_id: "teacher_parent_email_default",
  needs_action_steps: true,
  needs_empathy: true,
  needs_deadline_preservation: true,
  notes: [],
};

const candidates: CandidateSet = {
  candidate_a_concise:
    "Hi Monica,\n\nJordan has three assignments missing from the past two weeks: the reading response, vocabulary practice, and the short reflection paragraph from last Friday.\n\nIf he submits all three by this Friday at 5 p.m., I can still give partial credit. He can come by during lunch Tuesday or Thursday.\n\nBest regards,\nMs. Carter",
  candidate_b_warm:
    "Hi Monica,\n\nThank you for checking in. Jordan is missing three assignments from the past two weeks: the reading response, vocabulary practice, and the short reflection paragraph from last Friday.\n\nIf he submits all three by this Friday at 5 p.m., I can still give partial credit. He can also come by during lunch on Tuesday or Thursday.\n\nBest regards,\nMs. Carter",
  candidate_c_natural:
    "Hi Monica,\n\nFor Jordan, the missing work is still the main issue. He has three assignments missing from the past two weeks: the reading response, vocabulary practice, and the short reflection paragraph from last Friday.\n\nIf he submits all three by this Friday at 5 p.m., I can still give partial credit. He can stop by during lunch on Tuesday or Thursday if he needs help.\n\nBest regards,\nMs. Carter",
};

const review: ReviewResult = {
  scores: {
    candidate_a_concise: {
      factual_accuracy: 10,
      naturalness: 8,
      tone_appropriateness: 8,
      concision: 9,
      low_template_feel: 8,
      clarity_of_action_steps: 9,
      total: 52,
      issues: [],
    },
    candidate_b_warm: {
      factual_accuracy: 10,
      naturalness: 8,
      tone_appropriateness: 9,
      concision: 8,
      low_template_feel: 8,
      clarity_of_action_steps: 9,
      total: 52,
      issues: [],
    },
    candidate_c_natural: {
      factual_accuracy: 10,
      naturalness: 9,
      tone_appropriateness: 9,
      concision: 8,
      low_template_feel: 9,
      clarity_of_action_steps: 9,
      total: 54,
      issues: [],
    },
  },
  best_candidate_key: "candidate_c_natural",
  required_edits: [],
  risk_notes: ["Review before sending."],
};

const safeFactCheck: LlmFactCheckResult = {
  safe_to_send: true,
  has_added_new_facts: false,
  has_removed_important_facts: false,
  has_changed_names: false,
  has_changed_dates_or_deadlines: false,
  has_changed_conditions_or_policies: false,
  tone_problem: false,
  issues: [],
  required_repairs: [],
};

beforeEach(() => {
  for (const mock of Object.values(mocks)) {
    mock.mockReset();
  }

  mocks.extractFacts.mockResolvedValue(facts);
  mocks.classifyScenario.mockResolvedValue(scenario);
  mocks.generateCandidates.mockResolvedValue(candidates);
  mocks.reviewCandidates.mockResolvedValue(review);
  mocks.finalizeCandidate.mockResolvedValue(candidates.candidate_c_natural);
  mocks.escalateCandidate.mockResolvedValue(candidates.candidate_a_concise);
  mocks.diagnoseHighRiskSentences.mockResolvedValue({
    sentence_diagnostics: [
      {
        sentence:
          "For Jordan, the missing work is still the main issue.",
        issue_tags: ["low_specificity"],
        repair_instruction:
          "Make this sentence more concrete without changing the facts.",
      },
    ],
    overall_notes: [],
  });
  mocks.repairHighRiskSentences.mockResolvedValue(
    candidates.candidate_a_concise,
  );
  mocks.llmFactCheck.mockResolvedValue(safeFactCheck);
  mocks.generateGuaranteedRewriteCandidate.mockReturnValue({
    rewrittenText: candidates.candidate_a_concise,
    changeSummary: ["Used deterministic facts-first fallback."],
    riskNotes: ["Review before sending."],
  });
});

describe("rewriteWithFactReconstruct", () => {
  it("returns a successful rewrite only after fact and naturalness gates pass", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.rewrittenText).toBe(candidates.candidate_c_natural);
    expect(result.naturalness.draftAiLikePercent).toBe(100);
    expect(result.naturalness.rewriteAiLikePercent).toBe(18);
    expect(result.optimization.selectionStatus).toBe("passed");
    expect(mocks.escalateCandidate).not.toHaveBeenCalled();
  });

  it("keeps the support replacement path before the refund boundary", async () => {
    const { prioritizeSupportRemedyBeforeRefundBoundary } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const negativeFirst =
      "Hi Lena,\n\nThanks for the photo of the cracked replacement mug from order R4821. I checked the order notes this morning and saw the replacement was marked delivered on May 6. The photo shows damage to the mug, but the matching saucer looks fine.\n\nI can't refund the full order from this ticket because the original refund window closed on April 30. What I can do is send one no-cost replacement for the mug after I confirm the delivery address, per our support policy.\n\nPlease reply with your current delivery address by Friday at 3 p.m., and I'll queue the replacement mug.";

    const reordered = prioritizeSupportRemedyBeforeRefundBoundary(negativeFirst);

    expect(reordered.indexOf("send one no-cost replacement")).toBeLessThan(
      reordered.indexOf("can't refund"),
    );
  });

  it("restores short support greeting and photo-damage evidence from the source draft", async () => {
    const { stabilizeSupportReplyFromSource } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const source: RewriteRequestInput = {
      scenario: "Customer support",
      messageToReplyTo: "",
      roughDraftReply:
        "Hi Lena, thanks for sending the photo of the cracked replacement mug from order R4821. The photo shows damage to the replacement mug, but the matching saucer looks fine. Our support policy lets me send one no-cost replacement for the mug after I confirm the delivery address.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    };
    const compressed =
      "Thanks for getting in touch about the damaged replacement mug from order R4821. The matching saucer is undamaged. Our support policy lets me send one no-cost replacement for the mug after I confirm the delivery address.";

    const stabilized = stabilizeSupportReplyFromSource(source, compressed);

    expect(stabilized).toMatch(/^Hi Lena,/);
    expect(stabilized).toContain("The photo shows damage to the replacement mug.");
  });

  it("restores compact source anchors that models commonly compress away", async () => {
    const { stabilizeSupportReplyFromSource } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const baseInput: RewriteRequestInput = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply: "",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    };

    expect(
      stabilizeSupportReplyFromSource(
        {
          ...baseInput,
          roughDraftReply:
            "Hi Alina, thanks for interviewing with the product team for the Senior Support Lead role.",
        },
        "Hi Alina,\n\nThanks again for coming in for the Senior Support Lead role.",
      ),
    ).toContain("product team");

    expect(
      stabilizeSupportReplyFromSource(
        {
          ...baseInput,
          roughDraftReply:
            "Team, quick Beacon handoff update: the API checklist is done.",
        },
        "Team,\n\nQuick update: The API checklist is done. Legal copy is still waiting on Mina.",
      ),
    ).toContain("Beacon handoff");

    expect(
      stabilizeSupportReplyFromSource(
        {
          ...baseInput,
          roughDraftReply:
            "I sent the release request at 10:15 a.m. and most requests are reviewed within two business days.",
        },
        "Most release requests are reviewed within two business days.\n\nToday's note is for 10:15 a.m.",
      ),
    ).toContain("I sent the release request at 10:15 a.m.");
  });

  it("restores product-team anchors when the model summarizes the role and panel", async () => {
    const { stabilizeSupportReplyFromSource } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const source: RewriteRequestInput = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply:
        "Hi Alina, thanks for interviewing with the product team for the Senior Support Lead role on May 10.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    };
    const compressed = [
      "Hi Alina,",
      "Quick update on the Senior Support Lead role. The panel enjoyed learning about your experience with queue operations and onboarding on May 10.",
      "We have not made a final hiring decision yet.",
    ].join("\n\n");

    const stabilized = stabilizeSupportReplyFromSource(source, compressed);

    expect(stabilized).toContain("product team");
    expect(stabilized).toContain("Senior Support Lead role");
  });

  it("removes paragraph breaks inside title and name anchors", async () => {
    const { stabilizeSupportReplyFromSource } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const source: RewriteRequestInput = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply:
        "I sent the release request to Dr. Chen's queue today at 10:15 a.m.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    };
    const broken =
      "I sent the release request to Dr.\n\nChen's queue today at 10:15 a.m.";

    const stabilized = stabilizeSupportReplyFromSource(source, broken);

    expect(stabilized).toContain("Dr. Chen's queue");
    expect(stabilized).not.toContain("Dr.\n\nChen");
  });

  it("restores property access confirmation choices before lockbox access", async () => {
    const { stabilizeSupportReplyFromSource } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const source: RewriteRequestInput = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply: [
        "Hi Cam, I am following up about the leak under the kitchen sink in Unit 4B.",
        "The earliest access window the vendor gave us is Tuesday, May 28 between 9 a.m. and noon.",
        "Please confirm whether someone can let the plumber in during that window or whether we should use the lockbox code already on file.",
        "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number.",
      ].join(" "),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    };
    const softened = [
      "Cam, just following up on the leak under the kitchen sink in Unit 4B.",
      "We have your lockbox code already on file, so that's an option for access on Tuesday, May 28 between 9 a.m. and noon.",
      "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number.",
    ].join("\n\n");

    const stabilized = stabilizeSupportReplyFromSource(source, softened);

    expect(stabilized).toContain(
      "Please confirm whether someone can let the plumber in during that window or whether we should use the lockbox code already on file.",
    );
  });

  it("reviews extracted facts before scenario routing and candidate generation", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const billingInput: RewriteRequestInput = {
      ...input,
      roughDraftReply:
        "Hi Priya, the dashboard shows 18 active seats instead of 15 regular seats. The extra NZD $126 appears to be prorated, and the base plan has not changed.",
    };
    const billingFacts: ExtractedFacts = {
      ...facts,
      recipient_name: "Priya",
      people_mentioned: ["There", "Priya"],
      key_facts: ["The invoice is higher."],
      facts_that_must_not_change: [
        "full refund available",
        "base plan has not changed",
      ],
    };
    const billingCandidates: CandidateSet = {
      candidate_a_concise:
        "Hi Priya,\n\nThe dashboard shows 18 active seats instead of the 15 regular seats. The extra NZD $126 appears to be prorated, and the base plan has not changed.",
      candidate_b_warm:
        "Hi Priya,\n\nThe dashboard shows 18 active seats instead of the 15 regular seats. The extra NZD $126 appears to be prorated, and the base plan has not changed.",
      candidate_c_natural:
        "Hi Priya,\n\nThe dashboard shows 18 active seats instead of the 15 regular seats. The extra NZD $126 appears to be prorated, and the base plan has not changed.",
    };
    const billingReview: ReviewResult = {
      ...review,
      best_candidate_key: "candidate_c_natural",
    };
    mocks.extractFacts.mockResolvedValue(billingFacts);
    mocks.generateCandidates.mockResolvedValue(billingCandidates);
    mocks.reviewCandidates.mockResolvedValue(billingReview);
    mocks.finalizeCandidate.mockResolvedValue(
      billingCandidates.candidate_c_natural,
    );
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 89 })
      .mockResolvedValueOnce({ aiLikePercent: 5 });

    const result = await rewriteWithFactReconstruct(billingInput);
    const generatedFacts = mocks.generateCandidates.mock.calls[0][0]
      .facts as ExtractedFacts;

    expect(result.optimization.selectionStatus).toBe("passed");
    expect(generatedFacts.people_mentioned).toContain("Priya");
    expect(generatedFacts.people_mentioned).not.toContain("There");
    expect(generatedFacts.facts_that_must_not_change).toEqual(
      expect.arrayContaining(["18 active seats", "15 regular seats", "NZD $126"]),
    );
    expect(generatedFacts.facts_that_must_not_change).not.toContain(
      "full refund available",
    );
    expect(result.optimization.rewritePlanSummary).toContain("Fact ledger added");
    expect(result.optimization.factDiagnostics).toMatchObject({
      extractedFacts: expect.objectContaining({
        recipient_name: "Priya",
      }),
      reviewedFacts: expect.objectContaining({
        recipient_name: "Priya",
      }),
      addedAnchors: expect.arrayContaining([
        expect.objectContaining({ text: "NZD $126" }),
      ]),
      rejectedFacts: expect.arrayContaining([
        expect.objectContaining({ text: "There" }),
      ]),
    });
  });

  it("uses strong escalation once when the first final stays above the threshold", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 72 })
      .mockResolvedValueOnce({ aiLikePercent: 24 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.rewrittenText).toBe(candidates.candidate_a_concise);
    expect(result.naturalness.rewriteAiLikePercent).toBe(24);
    expect(result.optimization.selectionStatus).toBe("passed");
    expect(result.optimization.repairCandidatesTried).toBe(1);
    expect(mocks.escalateCandidate).toHaveBeenCalledTimes(1);
  });

  it("repairs high-risk sentences before using strong escalation when the final signal stays high", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    const targetedRepair =
      "Hi Monica,\n\nJordan is missing three assignments from the past two weeks: the reading response, vocabulary practice, and the short reflection paragraph from last Friday.\n\nIf he submits all three by this Friday at 5 p.m., I can still give partial credit. He can stop by during lunch on Tuesday or Thursday if he needs help.\n\nBest regards,\nMs. Carter";
    mocks.repairHighRiskSentences.mockResolvedValue(targetedRepair);
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({
        aiLikePercent: 72,
        sentenceScores: [
          {
            sentence:
              "For Jordan, the missing work is still the main issue.",
            aiLikePercent: 91,
          },
          {
            sentence:
              "If he submits all three by this Friday at 5 p.m., I can still give partial credit.",
            aiLikePercent: 12,
          },
        ],
      })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.rewrittenText).toBe(targetedRepair);
    expect(result.naturalness.rewriteAiLikePercent).toBe(18);
    expect(mocks.diagnoseHighRiskSentences).toHaveBeenCalledTimes(1);
    expect(mocks.repairHighRiskSentences).toHaveBeenCalledTimes(1);
    expect(mocks.escalateCandidate).not.toHaveBeenCalled();
    expect(result.optimization.candidateSignals.at(-1)?.stage).toBe(
      "targeted_repair",
    );
  });

  it("uses strong escalation once when the first final fails fact gates", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    mocks.finalizeCandidate.mockResolvedValue(
      "Hi Monica,\n\nJordan has three assignments missing.",
    );
    mocks.escalateCandidate.mockResolvedValue(candidates.candidate_a_concise);
    mocks.llmFactCheck
      .mockResolvedValueOnce({
        ...safeFactCheck,
        safe_to_send: false,
        has_removed_important_facts: true,
      })
      .mockResolvedValueOnce(safeFactCheck);
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.rewrittenText).toBe(candidates.candidate_a_concise);
    expect(result.naturalness.rewriteAiLikePercent).toBe(18);
    expect(result.optimization.repairCandidatesTried).toBe(1);
    expect(mocks.escalateCandidate).toHaveBeenCalledTimes(1);
  });

  it("does not escalate when the only deterministic miss is a generic support footer", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    const deliveryInput: RewriteRequestInput = {
      ...input,
      roughDraftReply:
        "Hi Emma, your package is still in transit. The carrier should provide a new update within the next one to two business days. Best regards, Customer Support Team",
    };
    const deliveryFacts: ExtractedFacts = {
      recipient_name: "Emma",
      sender_name_or_role: "Customer Support Team",
      people_mentioned: ["Emma"],
      main_purpose: "Explain package delay.",
      key_facts: ["The package is still in transit."],
      required_actions: [],
      deadlines: ["one to two business days"],
      dates_times: [],
      positive_notes: [],
      concerns: ["Package delay"],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "Customer Support Team",
        "one to two business days",
      ],
      sensitive_points: [],
      original_tone: "",
    };
	    const final =
	      "Hi Emma,\n\nYour package is still in transit. The carrier should post another delivery update within the next one to two business days.\n\nSorry for the delay.";
	    const deliveryCandidates: CandidateSet = {
	      candidate_a_concise:
	        "Hi Emma,\n\nYour package is still in transit. The carrier should provide a new update within the next one to two business days.\n\nBest regards,\nCustomer Support Team",
	      candidate_b_warm:
	        "Hi Emma,\n\nYour package is still in transit, and the carrier should provide a new update within the next one to two business days.\n\nBest regards,\nCustomer Support Team",
	      candidate_c_natural:
	        "Hi Emma,\n\nYour package is still in transit. The carrier should provide a new update within the next one to two business days.\n\nBest regards,\nCustomer Support Team",
	    };
	    mocks.extractFacts.mockResolvedValue(deliveryFacts);
	    mocks.generateCandidates.mockResolvedValue(deliveryCandidates);
	    mocks.finalizeCandidate.mockResolvedValue(final);
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 77 })
      .mockResolvedValueOnce({ aiLikePercent: 5 });

    const result = await rewriteWithFactReconstruct(deliveryInput);

    expect(result.rewrittenText).toBe(final);
    expect(result.optimization.selectionStatus).toBe("passed");
    expect(mocks.escalateCandidate).not.toHaveBeenCalled();
  });

  it("does not return a Sapling-passing final email that leaks internal fact-reference language", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const billingInput: RewriteRequestInput = {
      ...input,
      roughDraftReply: "Hi Priya,\n\nThe May 8 client handover is referenced.",
    };
    const billingFacts: ExtractedFacts = {
      recipient_name: "Priya",
      sender_name_or_role: "",
      people_mentioned: ["Priya"],
      main_purpose: "Mention the May 8 client handover.",
      key_facts: ["May 8 client handover"],
      required_actions: [],
      deadlines: [],
      dates_times: ["May 8"],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: ["May 8 client handover"],
      sensitive_points: [],
      original_tone: "",
    };
	    mocks.extractFacts.mockResolvedValue(billingFacts);
	    mocks.generateCandidates.mockResolvedValue({
	      candidate_a_concise:
	        "Hi Priya,\n\nThe May 8 client handover is the next item to review.",
	      candidate_b_warm:
	        "Hi Priya,\n\nThe May 8 client handover is the next item to review.",
	      candidate_c_natural:
	        "Hi Priya,\n\nThe May 8 client handover is the next item to review.",
	    });
	    mocks.finalizeCandidate.mockResolvedValue(
	      "Hi Priya,\n\nThe May 8 client handover is referenced.",
    );
    mocks.escalateCandidate.mockResolvedValue(
      "Hi Priya,\n\nThe May 8 client handover may be the next place to check.",
    );
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithFactReconstruct(billingInput);

    expect(result.rewrittenText).not.toContain("is referenced");
    expect(result.rewrittenText).toContain("May 8 client handover");
    expect(mocks.escalateCandidate).toHaveBeenCalledTimes(1);
    expect(
      result.optimization.candidateSignals.some((signal) =>
        signal.reason.includes("meta_language:fact_reference"),
      ),
    ).toBe(true);
  });

  it("does not return a Sapling-passing final email with detached numbered-list markers", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    mocks.extractFacts.mockResolvedValue({
      recipient_name: "Daniel",
      sender_name_or_role: "",
      people_mentioned: ["Daniel"],
      main_purpose: "Explain enrollment transfer and refund review options.",
      key_facts: ["Daniel has transfer and refund review options."],
      required_actions: ["Daniel should confirm which option he prefers."],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [
        "Transfer is subject to availability.",
        "Registration will not be updated unless Daniel confirms.",
      ],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "Daniel",
        "subject to availability",
        "will not update your registration unless you confirm",
      ],
      sensitive_points: [],
      original_tone: "",
    });
	    const fixed =
	      "Hi Daniel,\n\nYou may be eligible to transfer to a later cohort, subject to availability.\n\nYou can also request a refund review.\n\nWe will not update your registration unless you confirm which option you prefer.";
	    mocks.generateCandidates.mockResolvedValue({
	      candidate_a_concise: fixed,
	      candidate_b_warm: fixed,
	      candidate_c_natural: fixed,
	    });
	    mocks.finalizeCandidate.mockResolvedValue(
      "Hi Daniel,\n\nIn plain terms, you currently have two possible options: 1.\n\nYou can transfer your enrollment.\n\n2.\n\nYou can request a refund review.",
    );
    mocks.escalateCandidate.mockResolvedValue(fixed);
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithFactReconstruct({
      ...input,
      roughDraftReply:
        "Hi Daniel, you may be eligible to transfer to a later cohort, subject to availability. You can also request a refund review. We will not update your registration unless you confirm.",
    });

    expect(result.rewrittenText).toBe(fixed);
    expect(result.rewrittenText).not.toContain("options: 1.");
    expect(mocks.escalateCandidate).toHaveBeenCalledTimes(1);
    expect(
      result.optimization.candidateSignals.some((signal) =>
        signal.reason.includes("structure:broken_numbered_list"),
      ),
    ).toBe(true);
  });

  it("does not return a policy-drifting refund guarantee", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    mocks.extractFacts.mockResolvedValue({
      recipient_name: "Daniel",
      sender_name_or_role: "",
      people_mentioned: ["Daniel"],
      main_purpose: "Explain that refund eligibility needs review.",
      key_facts: [
        "A refund may be possible depending on the registration timestamp and seven-day policy.",
      ],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [
        "A refund may be possible, but it is not confirmed.",
        "Transfer may be available depending on seat availability.",
      ],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "refund may be possible",
        "registration timestamp",
        "seven-day policy",
        "depending on seat availability",
      ],
      sensitive_points: ["Do not promise a refund."],
      original_tone: "",
    });
	    const fixed =
	      "Hi Daniel,\n\nA refund may be possible, but we need to review the exact registration timestamp against the seven-day policy before confirming that.\n\nWe can also look at a transfer to a later cohort, depending on seat availability.";
	    mocks.generateCandidates.mockResolvedValue({
	      candidate_a_concise: fixed,
	      candidate_b_warm: fixed,
	      candidate_c_natural: fixed,
	    });
	    mocks.finalizeCandidate.mockResolvedValue(
      "Hi Daniel,\n\nA full refund is available under the seven-day policy.",
    );
    mocks.escalateCandidate.mockResolvedValue(fixed);
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 18 });

    const result = await rewriteWithFactReconstruct({
      ...input,
      roughDraftReply:
        "Hi Daniel, a refund may be possible, but it depends on the exact registration timestamp and the seven-day policy. A transfer may also be available depending on seat availability.",
    });

    expect(result.rewrittenText).toBe(fixed);
    expect(result.rewrittenText).toContain("may be possible");
    expect(result.rewrittenText).not.toContain("full refund is available");
    expect(mocks.escalateCandidate).toHaveBeenCalledTimes(1);
  });

  it("uses the deterministic facts-first fallback only after model escalation misses the gate", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    const fallbackText =
      "Hi Monica,\n\nJordan is missing three assignments from the past two weeks: the reading response, vocabulary practice, and the short reflection paragraph from last Friday.\n\nIf he submits all three by this Friday at 5 p.m., I can still accept them for partial credit. He can come by during lunch Tuesday or Thursday if he needs help.\n\nBest regards,\nMs. Carter";

    mocks.generateGuaranteedRewriteCandidate.mockReturnValue({
      rewrittenText: fallbackText,
      changeSummary: ["Used deterministic facts-first fallback."],
      riskNotes: ["Review before sending."],
    });
    mocks.escalateCandidate.mockResolvedValue(candidates.candidate_a_concise);
    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 100 })
      .mockResolvedValueOnce({ aiLikePercent: 95 })
      .mockResolvedValueOnce({ aiLikePercent: 90 })
      .mockResolvedValueOnce({ aiLikePercent: 12 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.rewrittenText).toContain("Hi Monica");
    expect(result.rewrittenText).toContain("three assignments");
    expect(result.rewrittenText).toContain("this Friday at 5 p.m.");
    expect(result.naturalness.rewriteAiLikePercent).toBe(12);
    expect(result.optimization.repairCandidatesTried).toBe(2);
    expect(result.optimization.candidateSignals.at(-1)?.stage).toBe("fallback");
  });

  it("tries the deterministic fallback when the internal reviewer rejects all model candidates", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    const fallbackText =
      "Hi Monica,\n\nJordan is missing three assignments from the past two weeks: the reading response, vocabulary practice, and the short reflection paragraph from last Friday.\n\nIf he submits all three by this Friday at 5 p.m., I can still give partial credit. He can come by during lunch Tuesday or Thursday.\n\nBest regards,\nMs. Carter";

    mocks.reviewCandidates.mockResolvedValue({
      ...review,
      scores: {
        candidate_a_concise: {
          ...review.scores.candidate_a_concise,
          factual_accuracy: 8,
        },
        candidate_b_warm: {
          ...review.scores.candidate_b_warm,
          low_template_feel: 7,
        },
        candidate_c_natural: {
          ...review.scores.candidate_c_natural,
          tone_appropriateness: 7,
        },
      },
    });
    mocks.generateGuaranteedRewriteCandidate.mockReturnValue({
      rewrittenText: fallbackText,
      changeSummary: ["Used deterministic facts-first fallback."],
      riskNotes: ["Review before sending."],
    });
    mocks.measureWritingSignal
      .mockResolvedValue({ aiLikePercent: 0 })
      .mockResolvedValueOnce({ aiLikePercent: 89 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.rewrittenText).toContain("Hi Monica");
    expect(result.rewrittenText).toContain("three assignments");
    expect(result.rewrittenText).toContain("this Friday at 5 p.m.");
    expect(result.optimization.repairCandidatesTried).toBe(1);
    expect(result.optimization.candidateSignals.at(-1)?.stage).toBe("fallback");
    expect(mocks.finalizeCandidate).not.toHaveBeenCalled();
  });

  it("stabilizes deterministic fallback output before returning it", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );
    const medicalInput: RewriteRequestInput = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply:
        "Hi Nora, I checked the portal message about Eli's lab report from the May 3 visit. I can see that the report is marked received by our office, but it has not been released to the patient portal yet. I am not a clinician, so I cannot interpret the results in this message. I sent the release request to Dr. Chen's queue today at 10:15 a.m. Most release requests are reviewed within two business days. If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    };

    mocks.extractFacts.mockResolvedValue({
      recipient_name: "Nora",
      sender_name_or_role: "",
      people_mentioned: ["Nora", "Eli", "Dr. Chen"],
      main_purpose: "Explain lab report release status.",
      key_facts: [
        "The report is marked received by the office.",
        "The report has not been released to the patient portal yet.",
        "The release request was sent to Dr. Chen's queue today at 10:15 a.m.",
      ],
      required_actions: [],
      deadlines: [],
      dates_times: ["May 3", "10:15 a.m."],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [
        "The sender is not a clinician.",
        "The sender cannot interpret the results.",
        "Most release requests are reviewed within two business days.",
        "New or worsening symptoms should go through the clinic line.",
      ],
      available_support: ["clinic line"],
      clarifications: [],
      facts_that_must_not_change: [
        "Nora",
        "Eli",
        "Dr. Chen's queue",
        "10:15 a.m.",
        "not been released to the patient portal",
        "not a clinician",
        "cannot interpret",
        "clinic line",
      ],
      sensitive_points: [],
      original_tone: "",
    });
    mocks.reviewCandidates.mockResolvedValue({
      ...review,
      scores: {
        candidate_a_concise: {
          ...review.scores.candidate_a_concise,
          factual_accuracy: 8,
        },
        candidate_b_warm: {
          ...review.scores.candidate_b_warm,
          factual_accuracy: 8,
        },
        candidate_c_natural: {
          ...review.scores.candidate_c_natural,
          factual_accuracy: 8,
        },
      },
    });
    mocks.measureWritingSignal
      .mockResolvedValue({ aiLikePercent: 0 })
      .mockResolvedValueOnce({ aiLikePercent: 0 });

    const result = await rewriteWithFactReconstruct(medicalInput);

    expect(result.optimization.candidateSignals.at(-1)?.stage).toBe("fallback");
    expect(result.rewrittenText).toContain("Dr. Chen's queue");
    expect(result.rewrittenText).not.toContain("Dr.\n\nChen");
  });

  it("reports reviewer_threshold_failed when rejected model candidates cannot be rescued", async () => {
    const { FactReconstructQualityError, rewriteWithFactReconstruct } =
      await import("../../lib/rewrite-pipeline/pipeline");

    mocks.reviewCandidates.mockResolvedValue({
      ...review,
      scores: {
        candidate_a_concise: {
          ...review.scores.candidate_a_concise,
          factual_accuracy: 8,
        },
        candidate_b_warm: {
          ...review.scores.candidate_b_warm,
          low_template_feel: 7,
        },
        candidate_c_natural: {
          ...review.scores.candidate_c_natural,
          tone_appropriateness: 7,
        },
      },
    });
    mocks.measureWritingSignal
      .mockResolvedValue({ aiLikePercent: 91 })
      .mockResolvedValueOnce({ aiLikePercent: 89 });

    try {
      await rewriteWithFactReconstruct(input);
      throw new Error("expected rewrite to fail");
    } catch (error) {
      expect(error).toBeInstanceOf(FactReconstructQualityError);
      expect(error).toMatchObject({
        name: "FactReconstructQualityError",
        reason: "reviewer_threshold_failed",
      });
    }
  });

  it("does not reject a fallback only because an LLM locked fact uses noisy labels", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    mocks.extractFacts.mockResolvedValue({
      ...facts,
      facts_that_must_not_change: [
        ...facts.facts_that_must_not_change,
        "support note label",
      ],
    });
    mocks.reviewCandidates.mockResolvedValue({
      ...review,
      scores: {
        candidate_a_concise: {
          ...review.scores.candidate_a_concise,
          factual_accuracy: 8,
        },
        candidate_b_warm: {
          ...review.scores.candidate_b_warm,
          low_template_feel: 7,
        },
        candidate_c_natural: {
          ...review.scores.candidate_c_natural,
          tone_appropriateness: 7,
        },
      },
    });
    mocks.generateGuaranteedRewriteCandidate.mockReturnValue({
      rewrittenText:
        "Hi Monica,\n\nJordan is missing three assignments from the past two weeks: the reading response, vocabulary practice, and the short reflection paragraph from last Friday.\n\nIf he submits all three by this Friday at 5 p.m., I can still give partial credit. He can come by during lunch Tuesday or Thursday.\n\nBest regards,\nMs. Carter",
      changeSummary: ["Used deterministic facts-first fallback."],
      riskNotes: ["Review before sending."],
    });
    mocks.measureWritingSignal
      .mockResolvedValue({ aiLikePercent: 0 })
      .mockResolvedValueOnce({ aiLikePercent: 89 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.optimization.candidateSignals.at(-1)?.stage).toBe("fallback");
    expect(result.optimization.selectionStatus).toBe("passed");
  });

  it("accepts a rewrite under the threshold even when it raised an already-low draft signal", async () => {
    const { rewriteWithFactReconstruct } = await import(
      "../../lib/rewrite-pipeline/pipeline"
    );

    // draft 22 -> final 28: the rewrite rose above the draft but stays well
    // under the 40 threshold. The old draft-relative rule rejected this; the
    // robust gate accepts it, with no needless escalation.
    mocks.measureWritingSignal
      .mockResolvedValue({ aiLikePercent: 28 })
      .mockResolvedValueOnce({ aiLikePercent: 22 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.optimization.selectionStatus).toBe("passed");
    expect(result.naturalness.rewriteAiLikePercent).toBe(28);
    expect(mocks.escalateCandidate).not.toHaveBeenCalled();
  });

  it("fails without a successful rewrite when the signal provider is unavailable", async () => {
    const { FactReconstructQualityError, rewriteWithFactReconstruct } =
      await import("../../lib/rewrite-pipeline/pipeline");

    mocks.measureWritingSignal.mockResolvedValueOnce({
      aiLikePercent: null,
      unavailableReason: "provider_error",
    });

    await expect(rewriteWithFactReconstruct(input)).rejects.toBeInstanceOf(
      FactReconstructQualityError,
    );
    expect(mocks.extractFacts).not.toHaveBeenCalled();
  });
});
