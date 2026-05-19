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
  escalateCandidate: vi.fn(),
  extractFacts: vi.fn(),
  finalizeCandidate: vi.fn(),
  generateCandidates: vi.fn(),
  generateGuaranteedRewriteCandidate: vi.fn(),
  llmFactCheck: vi.fn(),
  reviewCandidates: vi.fn(),
  measureWritingSignal: vi.fn(),
}));

vi.mock("../../lib/rewrite-pipeline/model", () => ({
  classifyScenario: mocks.classifyScenario,
  escalateCandidate: mocks.escalateCandidate,
  extractFacts: mocks.extractFacts,
  finalizeCandidate: mocks.finalizeCandidate,
  generateCandidates: mocks.generateCandidates,
  llmFactCheck: mocks.llmFactCheck,
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
      .mockResolvedValueOnce({ aiLikePercent: 89 })
      .mockResolvedValueOnce({ aiLikePercent: 0 });

    const result = await rewriteWithFactReconstruct(input);

    expect(result.rewrittenText).toContain("Hi Monica");
    expect(result.rewrittenText).toContain("three assignments");
    expect(result.rewrittenText).toContain("this Friday at 5 p.m.");
    expect(result.optimization.repairCandidatesTried).toBe(1);
    expect(result.optimization.candidateSignals.at(-1)?.stage).toBe("fallback");
    expect(mocks.finalizeCandidate).not.toHaveBeenCalled();
  });

  it("rejects a rewrite that raises an already-low draft signal", async () => {
    const { FactReconstructQualityError, rewriteWithFactReconstruct } =
      await import("../../lib/rewrite-pipeline/pipeline");

    mocks.measureWritingSignal
      .mockResolvedValueOnce({ aiLikePercent: 22 })
      .mockResolvedValueOnce({ aiLikePercent: 28 })
      .mockResolvedValueOnce({ aiLikePercent: 24 })
      .mockResolvedValueOnce({ aiLikePercent: 23 })
      .mockResolvedValueOnce({ aiLikePercent: 23 });

    await expect(rewriteWithFactReconstruct(input)).rejects.toBeInstanceOf(
      FactReconstructQualityError,
    );
    expect(mocks.escalateCandidate).toHaveBeenCalledTimes(1);
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
