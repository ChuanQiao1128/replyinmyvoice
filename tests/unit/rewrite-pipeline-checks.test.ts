import { describe, expect, it } from "vitest";

import {
  adaptiveGateCheck,
  detectMetaLanguage,
  detectStructureIssues,
  deterministicCheck,
  llmFactCheckPasses,
  preservesMustNotChange,
} from "../../lib/rewrite-pipeline/checks";
import { reviewExtractedFacts } from "../../lib/rewrite-pipeline/fact-ledger";
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

function lockedFacts(factsThatMustNotChange: string[], overrides: Partial<ExtractedFacts> = {}): ExtractedFacts {
  return {
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
    facts_that_must_not_change: factsThatMustNotChange,
    sensitive_points: [],
    original_tone: "",
    ...overrides,
  };
}

function draftOnlyInput(roughDraftReply: string): RewriteRequestInput {
  return {
    scenario: "General reply",
    messageToReplyTo: "",
    roughDraftReply,
    audience: "",
    purpose: "",
    whatHappened: "",
    factsToPreserve: "",
    tone: "warm",
    tonePreset: "Warm",
  };
}

function emptyExtractedFacts(): ExtractedFacts {
  return lockedFacts([]);
}

function inputDerivedFacts(input: RewriteRequestInput) {
  return reviewExtractedFacts(input, emptyExtractedFacts()).facts;
}

function gateDraftRewrite(roughDraftReply: string, rewrittenText: string) {
  const request = draftOnlyInput(roughDraftReply);

  return adaptiveGateCheck(
    request,
    inputDerivedFacts(request),
    rewrittenText,
    styleCard,
  );
}

describe("deterministicCheck", () => {
  it("locks record identifiers and cutoff consequences from case 001-style drafts", () => {
    const result = gateDraftRewrite(
      [
        "Hi Jamie, I checked Maya's folder basket, the payment envelope, and my teacher log on March 28 for the April 9 science museum field trip.",
        "The record is FieldTrip-4A-09.",
        "Right now I do not have Maya's signed permission slip on file, and I also do not have the $12 trip payment recorded.",
        "The deadline I have to work with is April 2, and I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing.",
        "Please send a new signed slip with the $12 trip fee.",
      ].join(" "),
      "Hi Jamie, I checked and still need Maya's signed permission slip and the $12 trip fee by April 2. Please send a new signed slip with the fee so I can update the trip record.",
    );
    const issues = result.blockingIssues.join("\n");

    expect(issues).toContain("fieldtrip-4a-09");
    expect(issues).toMatch(/cannot add maya.*april 2|april 2.*signed slip.*missing/);
  });

  it("locks approval-cycle and June 7 order-form boundaries from case 005-style drafts", () => {
    const result = gateDraftRewrite(
      [
        "Hi Dev, thanks again for the call about the Northstar rollout.",
        "I attached quote Q-7719 again so it is easier to find.",
        "The quote is for 18 seats at $42 per seat per month, and it includes onboarding, the admin workspace, and the standard email support package.",
        "The quote expires on June 7.",
        "I can answer scope questions this week, but I cannot add the advanced SSO setup or a discount to this quote without a new approval cycle.",
        "If the current quote still matches what you need, please reply by June 7 and I can send the order form.",
      ].join(" "),
      "Hi Dev, I attached quote Q-7719 for the Northstar rollout. It covers 18 seats at $42 per seat per month and includes onboarding, the admin workspace, and standard email support. Let me know if it still works and I can help with next steps.",
    );
    const issues = result.blockingIssues.join("\n");

    expect(issues).toContain("advanced sso");
    expect(issues).toContain("discount");
    expect(issues).toMatch(/approval cycle/);
    expect(issues).toMatch(/june 7.*order form|order form.*june 7/);
  });

  it("hard-rejects invented scheduling options from case 006-style drafts", () => {
    const result = gateDraftRewrite(
      [
        "Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m. because the room is unavailable.",
        "I can offer Thursday at 2:30 p.m. or Friday at 9 a.m.",
        "Please choose one by Wednesday at noon.",
        "I cannot hold both times after noon, but I will confirm the selected slot as soon as you reply.",
      ].join(" "),
      "Ren, quick update: the room for your Thursday, May 16 appointment at 11 a.m. is not available. Here are some other options: Wednesday at noon, Thursday at 2:30 p.m., Friday at 9 a.m., or after noon on Friday. Let me know your preference by Wednesday at noon.",
    );
    const issues = result.blockingIssues.join("\n");

    expect(issues).toContain("unsupported:");
    expect(issues).toMatch(/noon on friday|wednesday/);
  });

  it("locks medical admin disclaimers from case 008-style drafts", () => {
    const result = gateDraftRewrite(
      [
        "Hi Nora, I checked the portal message about Eli's lab report from the May 3 visit.",
        "I can see that the report is marked received by our office, but it has not been released to the patient portal yet.",
        "I am not a clinician, so I cannot interpret the results in this message.",
        "I sent the release request to Dr. Chen's queue today at 10:15 a.m.",
        "Most release requests are reviewed within two business days.",
        "If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply.",
      ].join(" "),
      "Hi Nora, Dr. Chen's team is reviewing Eli's May 3 lab report and most release requests are reviewed within two business days. If Eli has new or worsening symptoms, please call the clinic line.",
    );
    const issues = result.blockingIssues.join("\n");

    expect(issues).toContain("report is marked received");
    expect(issues).toContain("not been released");
    expect(issues).toContain("not a clinician");
    expect(issues).toContain("cannot interpret");
    expect(issues).toContain("dr. chen");
  });

  it("rejects invented photo deadlines and locks rent-credit boundaries from case 009-style drafts", () => {
    const result = gateDraftRewrite(
      [
        "Hi Cam, I am following up about the leak under the kitchen sink in Unit 4B.",
        "The plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out.",
        "The earliest access window the vendor gave us is Tuesday, May 28 between 9 a.m. and noon.",
        "Please confirm whether someone can let the plumber in during that window or whether we should use the lockbox code already on file.",
        "Please also send one photo of the cabinet floor today if the water has spread past the towel line.",
        "I cannot approve a rent credit from this maintenance thread.",
        "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number.",
      ].join(" "),
      "Cam, the plumber is scheduled for Tuesday, May 28 between 9 a.m. and noon. Please confirm access before I can send the vendor confirmation number. If water has spread past the towel line, send one photo of the cabinet floor today by 9 a.m.",
    );
    const issues = result.blockingIssues.join("\n");

    expect(issues).toContain("today by 9");
    expect(issues).toContain("lockbox code");
    expect(issues).toContain("cannot approve a rent credit");
  });

  it("locks the count of two cleanup volunteers from case 010-style drafts", () => {
    const result = gateDraftRewrite(
      [
        "Hello everyone, quick update for Saturday's Park Pantry packing day.",
        "We are still meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after 10 a.m.",
        "Volunteer check-in starts at 8:15 a.m., and packing begins at 9 a.m.",
        "If you signed up for delivery routes, bring a charged phone and check in with Mateo before loading your car.",
        "We still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay.",
        "Parking is in the east lot only because the west gate will be locked.",
      ].join(" "),
      "Hello everyone, Saturday's Park Pantry packing day is moving to Hall B because of heavy rain. Check-in starts at 8:15 a.m., packing begins at 9 a.m., and delivery route volunteers should bring a charged phone and check in with Mateo. Reply if you can help with cleanup from noon to 1 p.m. Parking is in the east lot only.",
    );
    const issues = result.blockingIssues.join("\n");

    expect(issues).toMatch(/two people|two cleanup volunteers|two.*cleanup/);
  });

  it("accepts dense locked facts when every concrete atom is present", () => {
    const cases: Array<{
      name: string;
      facts: string[];
      candidate: string;
    }> = [
      {
        name: "044 quote seats and per-seat price",
        facts: ["Quote Q-7442 shows 38 seats at $19 per seat per month."],
        candidate:
          "Your current quote Q-7442 is for 38 seats at $19 per seat per month, and you've mentioned you'd like to move to 27 seats.",
      },
      {
        name: "050 proposal identifier",
        facts: ["Proposal P-311"],
        candidate:
          "Proposal P-311 covers 62 users, SSO, CRM sync, and two admin trainings for $18,400 annually.",
      },
      {
        name: "041 order and price",
        facts: ["Order #WA-1187", "Price was $689"],
        candidate:
          "Thanks for reaching out about order #WA-1187. The washer was delivered May 6 at a price of $689, and the damage was reported May 9. No credit, replacement, or refund will be processed without your confirmation.",
      },
      {
        name: "047 purchase order details",
        facts: ["PO-9012 for 120 Model S scanners at $42 each."],
        candidate:
          "PO-9012 is for 120 Model S badge scanners at $42 each. The supplier can ship 80 units by May 30. Option 1 - Partial Model S shipment. Please let us know your decision by noon May 28. Remember, no substitute or cancellation is allowed without written approval.",
      },
      {
        name: "043 transfer window",
        facts: ["One transfer is allowed up to 10 business days before start."],
        candidate:
          "Per our policy, one cohort transfer is allowed up to 10 business days before the course start, provided seats are available. The August 12 cohort currently has 4 seats open and costs $675. Since you paid $620 for the July 8 course, there is a $55 difference.",
      },
    ];

    for (const testCase of cases) {
      expect(
        preservesMustNotChange(testCase.candidate, lockedFacts(testCase.facts)),
        testCase.name,
      ).toEqual([]);
    }

    expect(
      detectMetaLanguage(cases[1]?.candidate ?? ""),
    ).not.toContain("meta_language:fact_reference");
  });

  it("accepts identifier facts when populated people mention identifier labels", () => {
    const cases: Array<{
      name: string;
      candidate: string;
      facts: ExtractedFacts;
    }> = [
      {
        name: "proposal full identifier mention",
        candidate:
          "Here is what Proposal P-311 covers: 62 users, SSO, CRM sync, and two admin trainings for $18,400 annually.",
        facts: lockedFacts(["Proposal P-311"], {
          people_mentioned: ["Proposal P-311"],
        }),
      },
      {
        name: "proposal identifier-prefix mention",
        candidate:
          "Here is what Proposal P-311 covers: 62 users, SSO, CRM sync, and two admin trainings for $18,400 annually.",
        facts: lockedFacts(["Proposal P-311"], {
          people_mentioned: ["Proposal P-"],
        }),
      },
      {
        name: "quote full identifier mention",
        candidate:
          "Your current quote Q-7442 is set at 38 seats at $19 per seat per month.",
        facts: lockedFacts(["Quote Q-7442 shows 38 seats at $19 per seat per month."], {
          people_mentioned: ["Quote Q-7442"],
        }),
      },
      {
        name: "quote identifier-prefix mention",
        candidate:
          "Your current quote Q-7442 is set at 38 seats at $19 per seat per month.",
        facts: lockedFacts(["Quote Q-7442 shows 38 seats at $19 per seat per month."], {
          people_mentioned: ["Quote Q-"],
        }),
      },
    ];

    for (const testCase of cases) {
      expect(
        preservesMustNotChange(testCase.candidate, testCase.facts),
        testCase.name,
      ).toEqual([]);
    }
  });

  it("still rejects locked facts when concrete atoms are lost or altered", () => {
    const cases: Array<{
      name: string;
      facts: ExtractedFacts;
      candidate: string;
      expectedMissing: string;
    }> = [
      {
        name: "missing amount",
        facts: lockedFacts(["Price was $689"]),
        candidate: "The price is unchanged from the original order.",
        expectedMissing: "Price was $689",
      },
      {
        name: "wrong amount",
        facts: lockedFacts(["Price was $689"]),
        candidate: "The price was $789.",
        expectedMissing: "Price was $689",
      },
      {
        name: "missing order identifier",
        facts: lockedFacts(["Order #WA-1187"]),
        candidate: "Thanks for reaching out about the order.",
        expectedMissing: "Order #WA-1187",
      },
      {
        name: "wrong due date",
        facts: lockedFacts(["Corrected project due April 19 at 3:30 PM"]),
        candidate: "The corrected project is due April 12 at 3:30 PM.",
        expectedMissing: "Corrected project due April 19 at 3:30 PM",
      },
      {
        name: "missing locked person",
        facts: lockedFacts(["Priya"], { people_mentioned: ["Priya"] }),
        candidate: "Thanks for sending this over.",
        expectedMissing: "Priya",
      },
    ];

    for (const testCase of cases) {
      expect(
        preservesMustNotChange(testCase.candidate, testCase.facts),
        testCase.name,
      ).toContain(testCase.expectedMissing);
    }
  });

  it("keeps real two-word name locks strict", () => {
    const facts = lockedFacts(["Priya Nair"], {
      people_mentioned: ["Priya Nair"],
    });

    expect(preservesMustNotChange("Thanks, Priya.", facts)).toEqual([
      "Priya Nair",
    ]);
    expect(preservesMustNotChange("Thanks, Priya Nair.", facts)).toEqual([]);
  });

  it("allows normal business prose that says a charge is included", () => {
    expect(detectMetaLanguage("The setup fee is included in the annual price.")).not.toContain(
      "meta_language:fact_reference",
    );
  });

  it("requires only atomic locked-fact tokens while preserving atom loss failures", () => {
    const cases: Array<{
      name: string;
      roughDraftReply: string;
      lockedFact: string;
      rewrittenText: string;
      safe: boolean;
    }> = [
      {
        name: "seat count paraphrase",
        roughDraftReply: "Requested count is 27 seats.",
        lockedFact: "Requested count is 27 seats",
        rewrittenText: "Please renew with 27 seats and use the 27-seat count.",
        safe: true,
      },
      {
        name: "split delivery schedule paraphrase",
        roughDraftReply: "80 arrive by May 30 and 40 by June 6.",
        lockedFact: "80 arrive by May 30 and 40 by June 6",
        rewrittenText: "We can deliver 80 by May 30, and the final 40 by June 6.",
        safe: true,
      },
      {
        name: "amount missing",
        roughDraftReply: "Price was $689.",
        lockedFact: "Price was $689",
        rewrittenText: "The price is still the same as before.",
        safe: false,
      },
      {
        name: "amount present",
        roughDraftReply: "Price was $689.",
        lockedFact: "Price was $689",
        rewrittenText: "The price is $689.",
        safe: true,
      },
      {
        name: "phased launch paraphrase",
        roughDraftReply: "Possible phased launch for first 25 users.",
        lockedFact: "Possible phased launch for first 25 users",
        rewrittenText: "A phased launch for the first 25 users is possible.",
        safe: true,
      },
    ];

    for (const testCase of cases) {
      const facts: ExtractedFacts = {
        recipient_name: "",
        sender_name_or_role: "",
        people_mentioned: [],
        main_purpose: testCase.name,
        key_facts: [],
        required_actions: [],
        deadlines: [],
        dates_times: [],
        positive_notes: [],
        concerns: [],
        policies_or_conditions: [],
        available_support: [],
        clarifications: [],
        facts_that_must_not_change: [testCase.lockedFact],
        sensitive_points: [],
        original_tone: "",
      };

      const result = deterministicCheck(
        {
          ...input,
          roughDraftReply: testCase.roughDraftReply,
        },
        facts,
        testCase.rewrittenText,
        styleCard,
      );

      expect(result.safe, testCase.name).toBe(testCase.safe);
      if (!testCase.safe) {
        expect(result.issues, testCase.name).toContain(
          `missing_locked:${testCase.lockedFact}`,
        );
      }
    }
  });

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

  it("accepts locked facts when semantic equivalents preserve the concrete details", () => {
    const facts: ExtractedFacts = {
      recipient_name: "",
      sender_name_or_role: "",
      people_mentioned: [],
      main_purpose: "Explain quote details.",
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
        "Price was $689",
        "Credit covers invoice",
      ],
      sensitive_points: [],
      original_tone: "",
    };
    const quoteInput: RewriteRequestInput = {
      ...input,
      roughDraftReply: "Price was $689. Credit covers invoice.",
    };
    const rewritten =
      "The cost is $689. The credit can be used for the invoice.";

    const result = deterministicCheck(quoteInput, facts, rewritten, styleCard);

    expect(result.safe).toBe(true);
    expect(result.issues).toEqual([]);
  });

  it("still rejects locked facts when the amount, name, or date is missing", () => {
    const facts: ExtractedFacts = {
      recipient_name: "Priya Shah",
      sender_name_or_role: "",
      people_mentioned: ["Priya Shah"],
      main_purpose: "Explain quote details.",
      key_facts: [],
      required_actions: [],
      deadlines: [],
      dates_times: ["April 15"],
      positive_notes: [],
      concerns: [],
      policies_or_conditions: [],
      available_support: [],
      clarifications: [],
      facts_that_must_not_change: [
        "Priya Shah",
        "April 15",
        "Price was $689",
      ],
      sensitive_points: [],
      original_tone: "",
    };
    const rewritten = "The cost is $698.";

    const result = deterministicCheck(input, facts, rewritten, styleCard);

    expect(result.safe).toBe(false);
    expect(result.issues).toEqual(
      expect.arrayContaining([
        "missing_locked:Priya Shah",
        "missing_locked:April 15",
        "missing_locked:Price was $689",
      ]),
    );
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

  it("rejects detached sentence fragments in short rewrites", () => {
    const rewritten = [
      "Hi Ren,",
      "I need to move our Thursday, May 16 appointment from 11 a.m. Because the room is unavailable.",
      "I can offer Thursday at 2:30 p.m. Or Friday at 9 a.m.",
      "Please choose one by Wednesday at noon.",
    ].join("\n\n");

    expect(detectStructureIssues(input, rewritten)).toContain(
      "structure:sentence_fragment",
    );
  });

  it("does not treat lowercase continuations after a.m. or p.m. as fragments", () => {
    const rewritten = [
      "Hi Ren,",
      "I need to move our Thursday, May 16 appointment from 11 a.m. because the room is unavailable.",
      "I can offer Thursday at 2:30 p.m. or Friday at 9 a.m. Please choose one by Wednesday at noon.",
    ].join("\n\n");

    expect(detectStructureIssues(input, rewritten)).not.toContain(
      "structure:sentence_fragment",
    );
  });

  it("does not treat valid If action sentences after p.m. as fragments", () => {
    const rewritten = [
      "Hi Drew,",
      "Please reply yes or no by Friday at 5 p.m. If the rail has gotten worse since your last message, please send one updated photo today.",
    ].join("\n\n");

    expect(detectStructureIssues(input, rewritten)).not.toContain(
      "structure:sentence_fragment",
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

    expect(
      detectStructureIssues(
        {
          ...input,
          messageToReplyTo:
            "My order #R8142 arrived today and two of the six mugs were broken.",
        },
        "Hi Order,\n\nPlease send photos of the damaged mugs.",
      ),
    ).toContain("structure:unsupported_greeting");

    expect(
      detectStructureIssues(
        {
          ...input,
          messageToReplyTo:
            "I upgraded from Basic to Team on May 10 and have a billing question.",
        },
        "Hi Basic,\n\nThe invoice is prorated.",
      ),
    ).toContain("structure:unsupported_greeting");

    expect(
      detectStructureIssues(
        {
          ...input,
          messageToReplyTo:
            "I upgraded from Basic to Team on May 10 and have a billing question.",
        },
        "Hi Team,\n\nThe invoice is prorated.",
      ),
    ).toContain("structure:unsupported_greeting");

    expect(
      detectStructureIssues(
        {
          ...input,
          messageToReplyTo:
            "The customer can downgrade to Solo before renewal.",
        },
        "Hi Solo,\n\nPlease confirm the downgrade.",
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

  it("blocks missing protected product, topic, and policy-boundary phrases", () => {
    const protectedInput: RewriteRequestInput = {
      ...input,
      roughDraftReply:
        "The cracked FinchLite desk lamp can receive a replacement shade, but I cannot refund more than the $9.80 shipping credit.",
    };
    const facts = lockedFacts([
      "cracked FinchLite desk lamp",
      "replacement shade",
      "cannot refund more than the $9.80 shipping credit",
    ]);
    const rewritten =
      "I checked your order and can send a replacement after address confirmation. There is a $9.80 shipping credit on file.";

    const result = adaptiveGateCheck(protectedInput, facts, rewritten, styleCard);

    expect(result.safe).toBe(false);
    expect(result.blockingIssues).toEqual(
      expect.arrayContaining([
        "missing:cracked finchlite desk lamp",
        "missing:replacement shade",
        "missing:cannot refund more than the $9.80 shipping credit",
      ]),
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
