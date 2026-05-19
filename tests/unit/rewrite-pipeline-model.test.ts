import { afterEach, describe, expect, it, vi } from "vitest";

import {
  classifyScenario,
  diagnoseHighRiskSentences,
  extractFacts,
  generateCandidates,
  llmFactCheck,
  repairHighRiskSentences,
  reviewCandidates,
} from "../../lib/rewrite-pipeline/model";
import type {
  ExtractedFacts,
  FactReconstructConfig,
  ScenarioClassification,
  StyleCard,
} from "../../lib/rewrite-pipeline/types";
import type { RewriteRequestInput } from "../../lib/validation";

const config: FactReconstructConfig = {
  strategyVersion: "fact_reconstruct",
  naturalnessThreshold: 40,
  maxEscalations: 1,
  models: {
    cheap_structured: "test-structured-model",
    mid_writer: "test-writer-model",
    strong_escalation: "test-strong-model",
  },
  pricing: {
    cheap_structured: { inputPer1M: 0.2, outputPer1M: 1.25 },
    mid_writer: { inputPer1M: 0.75, outputPer1M: 4.5 },
    strong_escalation: { inputPer1M: 2.5, outputPer1M: 15 },
  },
};

const input: RewriteRequestInput = {
  scenario: "General reply",
  messageToReplyTo: "",
  roughDraftReply: "Hi Monica, Jordan is missing the reading response.",
  audience: "",
  purpose: "",
  whatHappened: "",
  factsToPreserve: "",
  tone: "warm",
  tonePreset: "Warm",
};

afterEach(() => {
  vi.restoreAllMocks();
  delete process.env.OPENAI_API_KEY;
});

describe("fact-reconstruct model parsing", () => {
  it("normalizes string and object fact fields into string arrays", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  recipient_name: "Monica",
                  sender_name_or_role: null,
                  people_mentioned: ["Monica", "Jordan"],
                  main_purpose: "Not specified",
                  key_facts: {
                    assignments: [
                      "reading response",
                      "vocabulary practice",
                      "short reflection paragraph",
                    ],
                  },
                  required_actions: ["complete missing work"],
                  deadlines: { final: "this Friday at 5 p.m." },
                  dates_times: { support: ["Tuesday", "Thursday"] },
                  positive_notes: "Jordan participates in class discussions.",
                  concerns: "Missing written work affects the grade.",
                  policies_or_conditions: "Partial credit is available.",
                  available_support: "Lunch on Tuesday or Thursday.",
                  clarifications:
                    "Jordan does not need to redo work already completed.",
                  facts_that_must_not_change: {
                    count: "three missing assignments",
                    credit: "partial credit",
                    unchanged: false,
                  },
                  sensitive_points: "Do not promise full credit.",
                  original_tone: "polished",
                }),
              },
            },
          ],
        }),
      ),
    );

    const facts = await extractFacts(input, config);

    expect(facts.sender_name_or_role).toBe("");
    expect(facts.main_purpose).toBe("");
    expect(facts.key_facts).toEqual([
      "reading response",
      "vocabulary practice",
      "short reflection paragraph",
    ]);
    expect(facts.deadlines).toEqual(["this Friday at 5 p.m."]);
    expect(facts.dates_times).toEqual(["Tuesday", "Thursday"]);
    expect(facts.positive_notes).toEqual([
      "Jordan participates in class discussions.",
    ]);
    expect(facts.facts_that_must_not_change).toEqual([
      "three missing assignments",
      "partial credit",
    ]);
  });

  it("falls back to a safe scenario intent when the model returns a free-text label", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  domain: "education",
                  intent: "address_concerns",
                  format: "email",
                  relationship: "teacher_to_parent",
                  risk: "medium",
                  confidence: 0.86,
                  style_card_id: "teacher_parent_email_default",
                  needs_action_steps: true,
                  needs_empathy: true,
                  needs_deadline_preservation: true,
                  notes: "Teacher parent grade reply.",
                }),
              },
            },
          ],
        }),
      ),
    );
    const facts: ExtractedFacts = {
      recipient_name: "Monica",
      sender_name_or_role: "Ms. Carter",
      people_mentioned: ["Monica", "Jordan"],
      main_purpose: "Reply to a parent about missing work.",
      key_facts: ["Jordan is missing work."],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [],
      sensitive_points: [],
      original_tone: "",
    };

    const scenario = await classifyScenario(facts, config);

    expect(scenario.domain).toBe("education");
    expect(scenario.intent).toBe("general_reply");
    expect(scenario.style_card_id).toBe("teacher_parent_email_default");
    expect(scenario.notes).toEqual(["Teacher parent grade reply."]);
  });

  it("normalizes object-shaped candidate values into candidate text", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  candidate_a_concise: {
                    subject: "Reply",
                    body: "Hi Monica,\n\nJordan is missing the reading response.",
                  },
                  candidate_b_warm: {
                    email: "Hi Monica,\n\nThanks for checking in about Jordan.",
                  },
                  candidate_c_natural: [
                    "Hi Monica,",
                    "Jordan still needs to turn in the reading response.",
                  ],
                }),
              },
            },
          ],
        }),
      ),
    );
    const facts: ExtractedFacts = {
      recipient_name: "Monica",
      sender_name_or_role: "Ms. Carter",
      people_mentioned: ["Monica", "Jordan"],
      main_purpose: "Reply to a parent.",
      key_facts: ["Jordan is missing the reading response."],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [],
      sensitive_points: [],
      original_tone: "",
    };
    const scenario: ScenarioClassification = {
      domain: "education",
      intent: "general_reply",
      format: "email",
      relationship: "teacher_to_parent",
      risk: "medium",
      confidence: 0.86,
      style_card_id: "teacher_parent_email_default",
      needs_action_steps: true,
      needs_empathy: true,
      needs_deadline_preservation: true,
      notes: [],
    };
    const styleCard: StyleCard = {
      style_card_id: "teacher_parent_email_default",
      voice: "clear",
      paragraph_style: "short",
      sentence_style: "varied",
      opening_style: "specific",
      body_style: "facts first",
      closing_style: "brief",
      good_phrases: [],
      phrases_to_avoid_or_limit: [],
      rules: [],
    };

    const candidates = await generateCandidates({
      facts,
      scenario,
      styleCard,
      config,
    });

    expect(candidates.candidate_a_concise).toContain(
      "Jordan is missing the reading response.",
    );
    expect(candidates.candidate_b_warm).toContain("Thanks for checking in");
    expect(candidates.candidate_c_natural).toContain(
      "Jordan still needs to turn in the reading response.",
    );
  });

  it("normalizes reviewer responses that put candidate scores at the top level", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  candidate_a_concise: {
                    factual_accuracy: 9,
                    naturalness: 8,
                    tone_appropriateness: 8,
                    concision: 10,
                    low_template_feel: 8,
                    clarity_of_action_steps: 9,
                    total: 52,
                    issues: "",
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
                  best_version: "candidate_c_natural",
                  required_edits: "Keep the deadline exact.",
                  risk_notes: "Review before sending.",
                }),
              },
            },
          ],
        }),
      ),
    );
    const facts: ExtractedFacts = {
      recipient_name: "Monica",
      sender_name_or_role: "Ms. Carter",
      people_mentioned: ["Monica", "Jordan"],
      main_purpose: "Reply to a parent.",
      key_facts: ["Jordan is missing the reading response."],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [],
      sensitive_points: [],
      original_tone: "",
    };
    const scenario: ScenarioClassification = {
      domain: "education",
      intent: "general_reply",
      format: "email",
      relationship: "teacher_to_parent",
      risk: "medium",
      confidence: 0.86,
      style_card_id: "teacher_parent_email_default",
      needs_action_steps: true,
      needs_empathy: true,
      needs_deadline_preservation: true,
      notes: [],
    };
    const styleCard: StyleCard = {
      style_card_id: "teacher_parent_email_default",
      voice: "clear",
      paragraph_style: "short",
      sentence_style: "varied",
      opening_style: "specific",
      body_style: "facts first",
      closing_style: "brief",
      good_phrases: [],
      phrases_to_avoid_or_limit: [],
      rules: [],
    };
    const candidates = {
      candidate_a_concise: "A",
      candidate_b_warm: "B",
      candidate_c_natural: "C",
    };

    const review = await reviewCandidates({
      facts,
      scenario,
      styleCard,
      candidates,
      config,
    });

    expect(review.best_candidate_key).toBe("candidate_c_natural");
    expect(review.scores.candidate_c_natural.factual_accuracy).toBe(10);
    expect(review.required_edits).toEqual(["Keep the deadline exact."]);
    expect(review.risk_notes).toEqual(["Review before sending."]);
  });

  it("accepts compact fact-check responses with missing detail booleans", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  safe_to_send: true,
                  issues: "",
                  required_repairs: "",
                }),
              },
            },
          ],
        }),
      ),
    );
    const facts: ExtractedFacts = {
      recipient_name: "Monica",
      sender_name_or_role: "Ms. Carter",
      people_mentioned: ["Monica", "Jordan"],
      main_purpose: "Reply to a parent.",
      key_facts: ["Jordan is missing the reading response."],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [],
      sensitive_points: [],
      original_tone: "",
    };

    const result = await llmFactCheck({
      facts,
      finalEmail: "Hi Monica,\n\nJordan is missing the reading response.",
      config,
    });

    expect(result).toMatchObject({
      safe_to_send: true,
      has_added_new_facts: false,
      has_removed_important_facts: false,
      has_changed_names: false,
      has_changed_dates_or_deadlines: false,
      has_changed_conditions_or_policies: false,
      tone_problem: false,
      issues: [],
      required_repairs: [],
    });
  });

  it("diagnoses and repairs only high-risk sentences with the configured model roles", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  sentence_diagnostics: [
                    {
                      sentence:
                        "I completely understand your concern and appreciate your partnership moving forward.",
                      issue_tags: ["generic_empathy", "corporate_template"],
                      repair_instruction:
                        "Use a simpler concrete opening without changing facts.",
                    },
                  ],
                  overall_notes: ["Repair the template opener only."],
                }),
              },
            },
          ],
        }),
      )
      .mockResolvedValueOnce(
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  final_email:
                    "Hi Monica,\n\nThanks for checking in about Jordan's grade.\n\nJordan is missing the reading response.",
                }),
              },
            },
          ],
        }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const facts: ExtractedFacts = {
      recipient_name: "Monica",
      sender_name_or_role: "Ms. Carter",
      people_mentioned: ["Monica", "Jordan"],
      main_purpose: "Reply to a parent.",
      key_facts: ["Jordan is missing the reading response."],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [],
      sensitive_points: [],
      original_tone: "",
    };
    const scenario: ScenarioClassification = {
      domain: "education",
      intent: "general_reply",
      format: "email",
      relationship: "teacher_to_parent",
      risk: "medium",
      confidence: 0.86,
      style_card_id: "teacher_parent_email_default",
      needs_action_steps: true,
      needs_empathy: true,
      needs_deadline_preservation: true,
      notes: [],
    };
    const styleCard: StyleCard = {
      style_card_id: "teacher_parent_email_default",
      voice: "clear",
      paragraph_style: "short",
      sentence_style: "varied",
      opening_style: "specific",
      body_style: "facts first",
      closing_style: "brief",
      good_phrases: [],
      phrases_to_avoid_or_limit: [],
      rules: [],
    };

    const diagnostics = await diagnoseHighRiskSentences({
      facts,
      scenario,
      styleCard,
      highRiskSentences: [
        {
          sentence:
            "I completely understand your concern and appreciate your partnership moving forward.",
          aiLikePercent: 93,
        },
      ],
      config,
    });
    const repaired = await repairHighRiskSentences({
      facts,
      scenario,
      styleCard,
      finalEmail:
        "Hi Monica,\n\nI completely understand your concern and appreciate your partnership moving forward.\n\nJordan is missing the reading response.",
      diagnostics,
      config,
    });

    expect(diagnostics.sentence_diagnostics[0].issue_tags).toEqual([
      "generic_empathy",
      "corporate_template",
    ]);
    expect(repaired).toContain("Thanks for checking in");
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body)).model).toBe(
      "test-structured-model",
    );
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body)).model).toBe(
      "test-writer-model",
    );
  });

  it("normalizes alternate sentence-diagnosis response shapes", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  diagnostics: [
                    {
                      text: "Thank you for your partnership moving forward.",
                      tags: "corporate_template",
                      suggestion: "Use simpler wording.",
                    },
                  ],
                  notes: "Repair only this sentence.",
                }),
              },
            },
          ],
        }),
      ),
    );
    const facts: ExtractedFacts = {
      recipient_name: "Monica",
      sender_name_or_role: "Ms. Carter",
      people_mentioned: ["Monica", "Jordan"],
      main_purpose: "Reply to a parent.",
      key_facts: ["Jordan is missing the reading response."],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [],
      sensitive_points: [],
      original_tone: "",
    };
    const scenario: ScenarioClassification = {
      domain: "education",
      intent: "general_reply",
      format: "email",
      relationship: "teacher_to_parent",
      risk: "medium",
      confidence: 0.86,
      style_card_id: "teacher_parent_email_default",
      needs_action_steps: true,
      needs_empathy: true,
      needs_deadline_preservation: true,
      notes: [],
    };
    const styleCard: StyleCard = {
      style_card_id: "teacher_parent_email_default",
      voice: "clear",
      paragraph_style: "short",
      sentence_style: "varied",
      opening_style: "specific",
      body_style: "facts first",
      closing_style: "brief",
      good_phrases: [],
      phrases_to_avoid_or_limit: [],
      rules: [],
    };

    const diagnostics = await diagnoseHighRiskSentences({
      facts,
      scenario,
      styleCard,
      highRiskSentences: [
        {
          sentence: "Thank you for your partnership moving forward.",
          aiLikePercent: 88,
        },
      ],
      config,
    });

    expect(diagnostics).toEqual({
      sentence_diagnostics: [
        {
          sentence: "Thank you for your partnership moving forward.",
          issue_tags: ["corporate_template"],
          repair_instruction: "Use simpler wording.",
        },
      ],
      overall_notes: ["Repair only this sentence."],
    });
  });
});
