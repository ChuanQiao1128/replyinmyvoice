import { describe, expect, it } from "vitest";

import {
  adaptiveGateCheck,
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

  it("accepts already-submitted question wording when resent is rewritten as send again", () => {
    const facts: ExtractedFacts = {
      recipient_name: "",
      sender_name_or_role: "",
      people_mentioned: [],
      main_purpose: "Workshop update.",
      key_facts: [],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "already-submitted questions do not need to be resent",
      ],
      sensitive_points: [],
      original_tone: "",
    };
    const workshopInput: RewriteRequestInput = {
      ...input,
      roughDraftReply:
        "Participants who already submitted questions do not need to send them again.",
    };
    const rewritten =
      "If you already submitted questions, you do not need to send them again.";

    const result = deterministicCheck(workshopInput, facts, rewritten, styleCard);

    expect(result.safe).toBe(true);
    expect(result.issues).toEqual([]);
  });

  it("accepts label-style locked facts when their concrete date and time remain", () => {
    const facts: ExtractedFacts = {
      recipient_name: "",
      sender_name_or_role: "",
      people_mentioned: [],
      main_purpose: "Workshop update.",
      key_facts: [],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "workshop date: Saturday",
        "workshop start time: 6:30pm",
      ],
      sensitive_points: [],
      original_tone: "",
    };
    const workshopInput: RewriteRequestInput = {
      ...input,
      roughDraftReply:
        "The Saturday workshop is now in Room 204 and starts at 6:30pm.",
    };
    const rewritten =
      "Saturday workshop update: we are moving to Room 204. We'll still start at 6:30pm.";

    const result = deterministicCheck(workshopInput, facts, rewritten, styleCard);

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

  it("rejects greetings inferred from department or status nouns", () => {
    expect(
      detectStructureIssues(
        {
          ...input,
          messageToReplyTo:
            "Can you send the dashboard update before the exec prep meeting tomorrow? Finance is asking whether the Q2 forecast tab will be ready.",
        },
        "Hi Finance,\n\nThe core dashboard is ready.",
      ),
    ).toContain("structure:unsupported_greeting");

    expect(
      detectStructureIssues(
        {
          ...input,
          messageToReplyTo:
            "I applied for the hardship discount and got a message saying I am not eligible. Can someone review it again?",
        },
        "Hi Reopening,\n\nWe can reopen the application once within 30 days.",
      ),
    ).toContain("structure:unsupported_greeting");
  });

  it("rejects repeated fact dumps and internal user-context echoes", () => {
    const rewritten = [
      "Hi,",
      "Core dashboard is ready.",
      "Q2 forecast tab depends on Finance confirming 7 accounts. Need Finance confirmation by 9:30 AM May 7 to send by 11:00 AM.",
      "The user corrected 41 duplicates but still needs Finance to confirm 7 accounts. The core dashboard is ready.",
    ].join("\n\n");

    const issues = detectStructureIssues(input, rewritten);

    expect(issues).toContain("structure:repeated_fact");
    expect(issues).toContain("meta_language:user_context_reference");
  });
});

describe("adaptiveGateCheck", () => {
  it("does not block a good support rewrite only because the greeting recipient is omitted", () => {
    const renewalInput: RewriteRequestInput = {
      ...input,
      roughDraftReply: [
        "Hi Michael,",
        "Thank you for reaching out and for providing the details regarding your subscription renewal request.",
        "Your subscription is currently scheduled to renew at the end of the current billing cycle.",
        "There does not appear to be any immediate change to your existing plan, feature access, or renewal pricing.",
        "Please review the renewal summary in your account dashboard before the renewal is processed.",
        "Best regards,",
        "Customer Support Team",
      ].join("\n\n"),
    };
    const facts: ExtractedFacts = {
      recipient_name: "Michael",
      sender_name_or_role: "Customer Support Team",
      people_mentioned: ["Michael"],
      main_purpose: "Confirm subscription renewal details.",
      key_facts: [
        "The subscription is scheduled to renew at the end of the current billing cycle.",
      ],
      required_actions: [
        "Review the renewal summary in the account dashboard before renewal is processed.",
      ],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [
        "There is no immediate change to the existing plan, feature access, or renewal pricing.",
      ],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "Michael is the recipient.",
        "The subscription is scheduled to renew at the end of the current billing cycle.",
        "No immediate change to existing plan, feature access, or renewal pricing.",
        "Customer Support Team",
      ],
      sensitive_points: [],
      original_tone: "",
    };
    const rewritten = [
      "Thanks for sending over the renewal details.",
      "Your subscription is still set to renew at the end of the current billing cycle. From what you shared, there is no immediate change to your current plan, feature access, or renewal pricing.",
      "It would still be worth checking the renewal summary in your account dashboard before the renewal is processed, just to make sure the billing details look right.",
      "Best regards,",
      "Customer Support Team",
    ].join("\n\n");

    const result = adaptiveGateCheck(renewalInput, facts, rewritten, styleCard);

    expect(result.safe).toBe(true);
    expect(result.blockingIssues).toEqual([]);
    expect(result.softIssues).toEqual(
      expect.arrayContaining([
        "missing:michael",
        "missing_locked:Michael is the recipient.",
      ]),
    );
  });

  it("passes package-delay support rewrites when soft wording changes preserve critical facts", () => {
    const packageInput: RewriteRequestInput = {
      ...input,
      scenario: "General reply",
      roughDraftReply: [
        "Hi Emma,",
        "Your order has already left our fulfillment center and is currently with the delivery carrier.",
        "The delay is related to a temporary processing issue at the local distribution facility.",
        "Your package is still in transit and has not been marked as lost or returned.",
        "We expect a new delivery update within the next one to two business days.",
        "There is no action required from you right now.",
        "If the tracking status does not change within two business days, we can open a follow-up investigation with the carrier.",
        "Best regards,",
        "Customer Support Team",
      ].join("\n\n"),
    };
    const facts: ExtractedFacts = {
      recipient_name: "Emma",
      sender_name_or_role: "Customer Support Team",
      people_mentioned: ["Emma"],
      main_purpose: "Explain a delayed package.",
      key_facts: [
        "The order left the fulfillment center.",
        "The package is with the delivery carrier.",
        "The package is still in transit.",
      ],
      required_actions: ["No action is required from Emma right now."],
      deadlines: ["one to two business days"],
      dates_times: [],
      positive_notes: [],
      concerns: ["Package delay"],
      policies_or_conditions: [
        "The package has not been marked as lost or returned.",
      ],
      available_support: ["follow-up investigation with the carrier"],
      clarifications: [],
      facts_that_must_not_change: [
        "Customer Support Team",
        "no action required",
        "one to two business days",
        "not marked as lost or returned",
      ],
      sensitive_points: [],
      original_tone: "",
    };
    const rewritten = [
      "Hi Emma,",
      "I checked the order details. Your package has left our fulfillment center and is with the delivery carrier now.",
      "The delay appears to be from a temporary processing issue at the local distribution facility. It is still in transit and has not been marked lost or returned.",
      "For now, no action is required from you. The carrier should post another delivery update within the next one to two business days. If the tracking status still has not changed after two business days, reply and we can open a follow-up investigation with the carrier.",
      "Sorry for the delay.",
    ].join("\n\n");

    const result = adaptiveGateCheck(packageInput, facts, rewritten, styleCard);

    expect(result.safe).toBe(true);
    expect(result.blockingIssues).toEqual([]);
    expect(result.softIssues).toContain("missing_locked:Customer Support Team");
  });

  it("blocks missing or changed hard facts even when the wording is natural", () => {
    const billingInput: RewriteRequestInput = {
      ...input,
      roughDraftReply:
        "Hi Priya, the May invoice preview is NZD $126 higher because three temporary contractor accounts were active. The base plan has not changed.",
    };
    const facts: ExtractedFacts = {
      recipient_name: "Priya",
      sender_name_or_role: "",
      people_mentioned: ["Priya"],
      main_purpose: "Explain invoice preview.",
      key_facts: ["The invoice preview is NZD $126 higher."],
      required_actions: [],
      deadlines: [],
      dates_times: [],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: ["The base plan has not changed."],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: ["NZD $126", "base plan has not changed"],
      sensitive_points: [],
      original_tone: "",
    };
    const rewritten =
      "Hi Priya,\n\nThe higher invoice preview appears to be from temporary contractor accounts. The base plan is still the same, and the extra charge is NZD $200.";

    const result = adaptiveGateCheck(billingInput, facts, rewritten, styleCard);

    expect(result.safe).toBe(false);
    expect(result.blockingIssues).toContain("missing:nzd $126");
    expect(result.blockingIssues.some((issue) => issue.includes("$200"))).toBe(
      true,
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
