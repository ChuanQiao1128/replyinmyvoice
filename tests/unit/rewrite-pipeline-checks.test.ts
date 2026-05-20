import { describe, expect, it } from "vitest";

import {
  detectStructureIssues,
  deterministicCheck,
  llmFactCheckPasses,
} from "../../lib/rewrite-pipeline/checks";
import type { ExtractedFacts, StyleCard } from "../../lib/rewrite-pipeline/types";
import type { RewriteRequestInput } from "../../lib/validation";

const input: RewriteRequestInput = {
  scenario: "General reply",
  messageToReplyTo: "",
  roughDraftReply:
    "Hi Monica, Jordan is missing three assignments. If he submits them by this Friday at 5 p.m., I will accept them for partial credit.",
  audience: "",
  purpose: "",
  whatHappened: "",
  factsToPreserve: "",
  tone: "warm",
  tonePreset: "Warm",
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

describe("deterministicCheck", () => {
  it("does not reject a natural rewrite only because a long locked fact was reworded", () => {
    const facts: ExtractedFacts = {
      recipient_name: "Monica",
      sender_name_or_role: "",
      people_mentioned: ["Monica", "Jordan"],
      main_purpose: "Explain missing work.",
      key_facts: ["Jordan is missing three assignments."],
      required_actions: [],
      deadlines: ["this Friday at 5 p.m."],
      dates_times: ["this Friday at 5 p.m."],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: ["partial credit"],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "There are exactly three missing assignments.",
        "this Friday at 5 p.m.",
        "partial credit",
      ],
      sensitive_points: [],
      original_tone: "",
    };
    const rewritten =
      "Hi Monica,\n\nJordan has three assignments missing. If he submits them by this Friday at 5 p.m., I can still accept them for partial credit.";

    const result = deterministicCheck(input, facts, rewritten, styleCard);

    expect(result.safe).toBe(true);
    expect(result.issues).toEqual([]);
  });

  it("rejects a dangling closing without a sender name", () => {
    const facts: ExtractedFacts = {
      recipient_name: "Ava",
      sender_name_or_role: "",
      people_mentioned: ["Ava"],
      main_purpose: "Send design status.",
      key_facts: ["The homepage mockup is ready."],
      required_actions: [],
      deadlines: ["desktop today"],
      dates_times: ["today", "Wednesday morning"],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: ["desktop today"],
      sensitive_points: [],
      original_tone: "",
    };

    const result = deterministicCheck(
      input,
      facts,
      "Ava,\n\nThe homepage mockup is ready.\n\nBest regards,",
      styleCard,
    );

    expect(result.safe).toBe(false);
    expect(result.issues).toContain("malformed:dangling_closing");
  });

  it("rejects fact-reference meta language in final email text", () => {
    const result = deterministicCheck(
      input,
      {
        recipient_name: "",
        sender_name_or_role: "",
        people_mentioned: [],
        main_purpose: "",
        key_facts: [],
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
      },
      "The May 8 client handover is referenced.",
      styleCard,
    );

    expect(result.safe).toBe(false);
    expect(result.issues).toContain("meta_language:fact_reference");
  });

  it("rejects provided-context meta language in final email text", () => {
    const result = deterministicCheck(
      input,
      {
        recipient_name: "",
        sender_name_or_role: "",
        people_mentioned: [],
        main_purpose: "",
        key_facts: [],
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
      },
      "Based on the provided context, the issue appears to be billing.",
      styleCard,
    );

    expect(result.safe).toBe(false);
    expect(result.issues).toContain("meta_language:provided_context");
  });

  it("rejects source-reference meta language in final email text", () => {
    const result = deterministicCheck(
      input,
      {
        recipient_name: "",
        sender_name_or_role: "",
        people_mentioned: [],
        main_purpose: "",
        key_facts: [],
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
      },
      "The source says there are 18 active seats.",
      styleCard,
    );

    expect(result.safe).toBe(false);
    expect(result.issues).toContain("meta_language:source_reference");
  });

  it("rejects detached numbered-list markers", () => {
    const result = deterministicCheck(
      input,
      {
        recipient_name: "",
        sender_name_or_role: "",
        people_mentioned: [],
        main_purpose: "",
        key_facts: [],
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
      },
      "In plain terms, you have two possible options: 1.\n\nYou can transfer your enrollment.\n\n2.\n\nYou can request a refund review.",
      styleCard,
    );

    expect(result.safe).toBe(false);
    expect(result.issues).toContain("structure:broken_numbered_list");
  });

  it("detects sentence-per-paragraph support replies", () => {
    const rewritten = [
      "Hi Daniel,",
      "Thank you for contacting us and explaining your situation.",
      "Your current enrollment is for the June weekend cohort.",
      "The cohort is scheduled to begin on Saturday, 6 June.",
      "You contacted us before the course start date.",
      "You may still be eligible to move to a later cohort.",
      "The next available cohort starts on Saturday, 20 July.",
      "The transfer depends on seat availability.",
      "Refund requests must be submitted at least seven days before the course begins.",
      "We need to review the exact registration timestamp before confirming a full refund.",
      "If a full refund is not available, course credit may still be possible.",
      "Please confirm whether you prefer a transfer or a refund review.",
      "We will not update your registration unless you confirm.",
    ].join("\n\n");

    expect(detectStructureIssues(input, rewritten)).toContain(
      "structure:sentence_per_paragraph",
    );
  });
});

describe("llmFactCheckPasses", () => {
  it("does not fail a fact-safe reply when the model sets safe_to_send false without any concrete issue", () => {
    expect(
      llmFactCheckPasses({
        safe_to_send: false,
        has_added_new_facts: false,
        has_removed_important_facts: false,
        has_changed_names: false,
        has_changed_dates_or_deadlines: false,
        has_changed_conditions_or_policies: false,
        tone_problem: false,
        issues: [],
        required_repairs: [],
      }),
    ).toBe(true);
  });
});
