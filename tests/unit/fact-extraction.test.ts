import { describe, expect, it } from "vitest";

import {
  detectUnsupportedFacts,
  extractRequiredFacts,
  missingRequiredFacts,
  normalizeFactText,
} from "../../lib/fact-extraction";
import type { RewriteRequestInput } from "../../lib/validation";

function draftOnly(roughDraftReply: string): RewriteRequestInput {
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

function replyInput(overrides: Partial<RewriteRequestInput>): RewriteRequestInput {
  return {
    scenario: "General reply",
    messageToReplyTo: "",
    roughDraftReply: "",
    audience: "",
    purpose: "",
    whatHappened: "",
    factsToPreserve: "",
    tone: "warm",
    tonePreset: "Warm",
    ...overrides,
  };
}

describe("extractRequiredFacts", () => {
  it("uses facts to preserve as the hard fact source when reply context has background dates", () => {
    const input = replyInput({
      messageToReplyTo:
        "Hi Ms Alvarez, I got the reminder that Maya is missing the permission slip for the April 9 science museum trip. I am almost sure I sent it in her blue folder last Thursday.",
      roughDraftReply:
        "Hello, I checked and I do not see it. You should send another form and payment so she can attend.",
      whatHappened:
        "The teacher checked the classroom folder basket and payment envelope after dismissal on March 28. The office has extra forms. The deadline is April 2.",
      factsToPreserve:
        "Maya needs a signed permission slip by April 2. The $12 payment is still missing. Parent can send a new form or ask for one to be sent home. Do not imply the parent is wrong or careless.",
    });

    const facts = extractRequiredFacts(input).map((fact) => fact.normalizedText);
    const missing = missingRequiredFacts(
      input,
      "Hi Ms Alvarez,\n\nI checked after dismissal, and Maya still needs a signed permission slip by April 2. The $12 payment is also still missing.\n\nYou can send a new form, or I can send another one home if that is easier.",
    ).map((fact) => fact.normalizedText);

    expect(facts).toEqual(
      expect.arrayContaining(["maya", "permission slip", "april 2", "$12"]),
    );
    expect(facts).not.toEqual(
      expect.arrayContaining(["april 9", "thursday", "march 28"]),
    );
    expect(missing).not.toEqual(
      expect.arrayContaining(["april 9", "thursday", "wrong", "careless"]),
    );
  });

  it("extracts implementation schedule facts without treating list labels as people", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Morgan,",
          "The training session originally planned for Tuesday, 4 June will need to move.",
          "The best replacement time is Thursday, 6 June at 10:30 a.m. The backup option is Friday, 7 June after 2 p.m.",
          "Please do not change the go-live date yet. We are still aiming for Monday, 17 June, as long as the user-permission issue is resolved by the end of next week.",
          "The warehouse supervisors can see the dashboard, but they cannot approve shift changes.",
          "The export file is missing the approved by column, which finance needs for the weekly reconciliation report.",
          "SMS reminders are not part of this phase, and we are not ready to approve the additional NZD $480 setup fee.",
          "Document SMS reminders as a possible phase-two item because regional managers may ask about it later.",
          "Could you send an updated implementation note with confirmation that the go-live date is still Monday, 17 June unless the permission issue is not resolved?",
          "Best,",
          "Avery",
        ].join("\n\n"),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);
    const personFacts = facts
      .filter((fact) => fact.category === "person")
      .map((fact) => fact.normalizedText);

    expect(factText).toContain("morgan");
    expect(factText).toContain("avery");
    expect(factText).toContain("tuesday, 4 june");
    expect(factText).toContain("thursday, 6 june");
    expect(factText).toContain("10:30 a.m.");
    expect(factText).toContain("friday, 7 june");
    expect(factText).toContain("2 p.m.");
    expect(factText).toContain("monday, 17 june");
    expect(factText).toContain("user-permission issue");
    expect(factText).toContain("warehouse supervisors");
    expect(factText).toContain("cannot approve shift changes");
    expect(factText).toContain("approved by column");
    expect(factText).toContain("weekly reconciliation report");
    expect(factText).toContain("sms reminders are not part of this phase");
    expect(factText).toContain("nzd $480");
    expect(factText).toContain("phase-two item");
    expect(factText).toContain("regional managers");
    expect(personFacts).not.toContain("sms");
    expect(personFacts).not.toContain("could");
    expect(personFacts).not.toContain("confirmation");
  });

  it("does not treat workshop date and room labels as people", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Additional details: the Saturday workshop is in Room 204 at 6:30pm. Families received the original reminder on Tuesday.",
      ),
    );
    const personFacts = facts
      .filter((fact) => fact.category === "person")
      .map((fact) => fact.normalizedText);

    expect(personFacts).not.toContain("additional");
    expect(personFacts).not.toContain("saturday");
    expect(personFacts).not.toContain("room");
  });

  it("accepts still-welcome wording for optional bring facts", () => {
    const input = draftOnly(
      "Participants may still bring printed scholarship drafts if they want feedback during the session.",
    );

    const missing = missingRequiredFacts(
      input,
      "Printed scholarship drafts are still welcome.",
    ).map((fact) => fact.normalizedText);

    expect(missing).not.toContain(
      "participants may still bring printed scholarship drafts if they want feedback during the session",
    );
  });

  it("extracts package-delay facts without treating There or generic support signoffs as hard facts", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Emma,",
          "Your order has already left our fulfillment center and is currently with the delivery carrier.",
          "The delay seems to be related to a temporary processing issue at the local distribution facility.",
          "Your package is still in transit and has not been marked as lost or returned.",
          "We expect a new delivery update within the next one to two business days.",
          "There is no action required from you right now.",
          "If the tracking status does not change within two business days, we can open a follow-up investigation with the carrier.",
          "Best regards,",
          "Customer Support Team",
        ].join("\n\n"),
      ),
    );
    const factText = facts.map((fact) => fact.normalizedText);
    const personFacts = facts
      .filter((fact) => fact.category === "person")
      .map((fact) => fact.normalizedText);

    expect(factText).toContain("emma");
    expect(factText).toContain("fulfillment center");
    expect(factText).toContain("delivery carrier");
    expect(factText).toContain("temporary processing issue");
    expect(factText).toContain("local distribution facility");
    expect(factText).toContain("still in transit");
    expect(factText).toContain("lost or returned");
    expect(factText).toContain("one to two business days");
    expect(factText).toContain("no action required");
    expect(factText).toContain("follow-up investigation with the carrier");
    expect(personFacts).not.toContain("there");
    expect(personFacts).not.toContain("sorry");
    expect(factText).not.toContain("best regards customer support team");
    expect(
      missingRequiredFacts(
        draftOnly(
          "There is no action required from you right now. Sorry for the delay.",
        ),
        "For now, no action is required from you. Sorry for the delay.",
      ),
    ).toEqual([]);
  });

  it("keeps line-wrapped dates and confirmation dependencies as required facts", () => {
    const input = draftOnly(
      [
        "Hi Lena, thanks for sending the photo of the cracked replacement mug from order R4821.",
        "I checked the order notes this morning, and the replacement was marked delivered on May",
        "6. Our support policy lets me send one no-cost replacement for the mug after I confirm the",
        "delivery address. Please reply with the current delivery address by Friday at 3 p.m.",
      ].join("\n"),
    );
    const facts = extractRequiredFacts(input).map((fact) => fact.normalizedText);
    const missing = missingRequiredFacts(
      input,
      [
        "Thanks for sending the photo. The replacement mug from order R4821 arrived cracked.",
        "Our support policy lets me send one no-cost replacement for the mug.",
        "Please reply by Friday at 3 p.m.",
      ].join(" "),
    ).map((fact) => fact.normalizedText);

    expect(facts).toContain("may 6");
    expect(facts).toEqual(
      expect.arrayContaining([
        expect.stringContaining("after i confirm the delivery address"),
      ]),
    );
    expect(missing).toContain("may 6");
    expect(missing).toEqual(
      expect.arrayContaining([expect.stringContaining("delivery address")]),
    );
  });

  it("locks conditional photo requests as next-step dependencies", () => {
    const input = draftOnly(
      [
        "Hi Drew, I am checking on the loose stair rail at 18 Maple, Unit 2.",
        "If the rail has gotten worse since your last message, please send one updated photo today so I can add it to the work order.",
      ].join(" "),
    );
    const facts = extractRequiredFacts(input).map((fact) => fact.normalizedText);
    const missing = missingRequiredFacts(
      input,
      "Drew, the contractor can inspect the loose stair rail at 18 Maple, Unit 2.",
    ).map((fact) => fact.normalizedText);

    expect(facts).toEqual(
      expect.arrayContaining([expect.stringContaining("one updated photo today")]),
    );
    expect(missing).toEqual(
      expect.arrayContaining([expect.stringContaining("one updated photo today")]),
    );
  });

  it("preserves upload evidence and named project handoff phrases", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "I need to move our Thursday appointment because the room is unavailable.",
          "I do see one empty file attempt from Theo at 7:42 p.m. that night.",
          "Team, quick Beacon handoff update: the API checklist is done.",
        ].join(" "),
      ),
    ).map((fact) => fact.normalizedText);

    expect(facts).toContain("room is unavailable");
    expect(facts).toContain("empty file attempt");
    expect(facts).toContain("beacon handoff");
  });

  it("does not lock sentence-initial labels as person anchors while preserving real full names", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Move the onboarding call to Tuesday.",
          "Options are transfer, refund review, or keeping the booking as-is.",
          "Requested change should wait for confirmation.",
          "Manager approval is required before the refund review.",
          "Sender should confirm the preferred next step.",
          "Suggested reply should stay concise.",
          "Priya Shah approved the price change.",
          "Martin Hale owns the updated quote.",
        ].join(" "),
      ),
    );

    const personFacts = facts
      .filter((fact) => fact.category === "person")
      .map((fact) => fact.normalizedText);

    expect(personFacts).not.toEqual(
      expect.arrayContaining([
        "move",
        "options",
        "requested",
        "manager",
        "sender",
        "suggested",
      ]),
    );
    expect(personFacts).toEqual(
      expect.arrayContaining(["priya shah", "martin hale"]),
    );
  });

  it("does not extract identifier prefixes as people while preserving real full names", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Priya Nair,",
          "Proposal P-311 covers 62 users, SSO, CRM sync, and two admin trainings for $18,400 annually.",
          "Quote Q-7442 shows 38 seats at $19 per seat per month.",
        ].join("\n\n"),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);
    const personFacts = facts
      .filter((fact) => fact.category === "person")
      .map((fact) => fact.normalizedText);

    expect(personFacts).toContain("priya nair");
    expect(personFacts).not.toEqual(
      expect.arrayContaining([
        "proposal p-",
        "proposal p-311",
        "quote q-",
        "quote q-7442",
      ]),
    );
    expect(factText).not.toEqual(
      expect.arrayContaining(["proposal p-", "quote q-"]),
    );
  });

  it("extracts greeting and signoff first names as required people", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Maya,",
          "The order can stay as-is until you confirm the next step.",
          "Best,",
          "Priya",
        ].join("\n\n"),
      ),
    );
    const personFacts = facts
      .filter((fact) => fact.category === "person" || fact.category === "signoff")
      .map((fact) => fact.normalizedText);

    expect(personFacts).toEqual(expect.arrayContaining(["maya", "priya"]));
  });

  it("extracts draft-only names, assignments, deadlines, policy limits, and signoff", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Monica,",
          "Jordan is missing the reading response, vocabulary practice, and the short reflection paragraph from Friday.",
          "He should submit them by the end of this week for partial credit.",
          "Best regards,",
          "Ms. Carter",
        ].join("\n\n"),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("monica");
    expect(factText).toContain("jordan");
    expect(factText).toContain("reading response");
    expect(factText).toContain("vocabulary practice");
    expect(factText).toContain("short reflection paragraph from friday");
    expect(factText).toContain("end of this week");
    expect(factText).toContain("partial credit");
    expect(factText).toContain("best regards ms. carter");
  });

  it("extracts ordered steps as facts that must remain ordered", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Nina owns the API fix, Omar owns the QA script, and both are due before the Friday demo.",
      ),
    );

    expect(facts).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          category: "ordered_step",
          normalizedText: expect.stringContaining("nina owns the api fix"),
        }),
        expect.objectContaining({
          category: "ordered_step",
          normalizedText: expect.stringContaining("omar owns the qa script"),
        }),
      ]),
    );
  });

  it("detects missing required facts after rewrite", () => {
    const input = draftOnly(
      "The refund window ended May 10. We can offer account credit, but manager approval is required.",
    );

    const missing = missingRequiredFacts(
      input,
      "We can offer account credit after approval.",
    );

    expect(missing.map((fact) => fact.normalizedText)).toContain("may 10");
    expect(missing.map((fact) => fact.normalizedText)).toContain(
      "manager approval",
    );
  });

  it("allows natural signoff wording changes when the signer name remains", () => {
    const input = draftOnly("Best regards,\nMs. Carter");

    const missing = missingRequiredFacts(input, "Best,\nMs. Carter");

    expect(missing.map((fact) => fact.normalizedText)).not.toContain(
      "best regards ms. carter",
    );
  });

  it("detects unsupported dates, amounts, and names added by a rewrite", () => {
    const input = draftOnly(
      "The tax line increased because the billing address changed to Australia. The base subscription is unchanged.",
    );

    const unsupported = detectUnsupportedFacts(
      input,
      "Hi Sarah, the tax line increased by NZD $126 because the billing address changed to Australia on June 1. The base subscription is unchanged.",
    );

    expect(unsupported).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ text: "Sarah" }),
        expect.objectContaining({ text: "NZD $126" }),
        expect.objectContaining({ text: "June 1" }),
      ]),
    );
  });

  it("does not treat source-backed option verbs as added hard facts", () => {
    const input = draftOnly(
      "Option one is the split shipment. Option two is Model T. Please confirm the choice by noon May 28.",
    );

    const unsupported = detectUnsupportedFacts(
      input,
      "Accept the split shipment. Switch to Model T if that works better. Please confirm by noon May 28.",
    );

    const unsupportedFacts = unsupported.map((fact) => fact.normalizedText);
    expect(unsupportedFacts).not.toContain("accept");
    expect(unsupportedFacts).not.toContain("switch");
  });

  it("does not treat common capitalized workflow words as people or unsupported additions", () => {
    const input = draftOnly(
      "Please keep the Option label on the shipment, use Annual billing, and leave Decision pending until May 28.",
    );
    const facts = extractRequiredFacts(input);
    const personFacts = facts
      .filter((fact) => fact.category === "person")
      .map((fact) => fact.normalizedText);
    const unsupported = detectUnsupportedFacts(
      input,
      "Per our policy, annual billing stays pending until May 28. Remember, no substitute or cancellation is allowed without written approval.",
    ).map((fact) => fact.normalizedText);

    expect(personFacts).not.toEqual(
      expect.arrayContaining(["option", "annual", "decision"]),
    );
    expect(unsupported).not.toEqual(
      expect.arrayContaining(["per", "remember"]),
    );
  });

  it("still flags invented concrete names and amounts", () => {
    const input = draftOnly(
      "You can accept the split shipment or switch to Model T.",
    );

    const unsupported = detectUnsupportedFacts(
      input,
      "Alex approved the switch to Model T with a NZD $450 setup fee.",
    );

    expect(unsupported).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ normalizedText: "alex" }),
        expect.objectContaining({ normalizedText: "nzd $450" }),
      ]),
    );
  });

  it("does not treat sentence starters or instruction words as required people", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "It should keep Jordan's name, but do not invent a company.",
          "Keep the reporting feature and team templates.",
          "Avoid pressure while they compare two other vendors.",
          "This should sound like a real follow-up.",
        ].join(" "),
      ),
    );

    const people = facts
      .filter((fact) => fact.category === "person")
      .map((fact) => fact.normalizedText);

    expect(people).toContain("jordan");
    expect(people).not.toEqual(
      expect.arrayContaining(["it", "keep", "avoid", "this", "jordan's"]),
    );
  });

  it("does not treat capitalized transition words in polished drafts as required facts", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Monica,",
          "Right now, Jordan is missing three assignments from the past two weeks: the reading response, the vocabulary practice, and the short reflection paragraph from last Friday.",
          "However, the missing written work is beginning to have a significant impact on his overall grade.",
          "After that, he can work on the short reflection paragraph.",
          "If he submits all three assignments by this Friday at 5 p.m., I will still accept them for partial credit.",
          "He is welcome to come see me during lunch on Tuesday or Thursday.",
          "Best regards,",
          "Ms. Carter",
        ].join("\n\n"),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("monica");
    expect(factText).toContain("jordan");
    expect(factText).toContain("friday");
    expect(factText).toContain("tuesday");
    expect(factText).not.toEqual(
      expect.arrayContaining(["right", "however", "after", "if"]),
    );
  });

  it("does not treat capitalized ordinary words in rewrites as unsupported names", () => {
    const input = draftOnly(
      "Hi Monica, Jordan is missing three assignments and can get partial credit by Friday.",
    );
    const unsupported = detectUnsupportedFacts(
      input,
      [
        "Hi Monica,",
        "Missing work is the main issue right now.",
        "Just to clarify, Jordan can still get partial credit by Friday.",
        "Looking forward to helping him catch up.",
      ].join("\n\n"),
    );

    expect(unsupported.map((fact) => fact.normalizedText)).not.toEqual(
      expect.arrayContaining(["missing", "just", "looking"]),
    );
  });

  it("does not treat Let as an unsupported person when a support rewrite starts naturally", () => {
    const input = draftOnly(
      "Hi Michael, your subscription is scheduled to renew at the end of the current billing cycle.",
    );

    const unsupported = detectUnsupportedFacts(
      input,
      "Let me check that for you. Your subscription is still scheduled to renew at the end of the current billing cycle.",
    );

    expect(unsupported.map((fact) => fact.normalizedText)).not.toContain("let");
  });

  it("does not treat ticket labels as unsupported people", () => {
    const input = draftOnly("Hi Claire, ticket #4821 is still open.");

    const unsupported = detectUnsupportedFacts(
      input,
      "Hi Claire,\n\nTicket #4821 is still open.",
    );

    expect(unsupported.map((fact) => fact.normalizedText)).not.toContain(
      "ticket",
    );
  });

  it("does not treat sentence-starting number words as unsupported people", () => {
    const input = draftOnly(
      "Four teachers said the onboarding copy felt too technical, and two asked for a sample response.",
    );

    const unsupported = detectUnsupportedFacts(
      input,
      "Four teachers said the copy felt too technical. Two asked if they could see a sample response.",
    );

    expect(unsupported.map((fact) => fact.normalizedText)).not.toEqual(
      expect.arrayContaining(["four", "two"]),
    );
  });

  it("does not treat sentence-starting connectors as unsupported people", () => {
    const input = replyInput({
      messageToReplyTo:
        "Thanks for the demo. Can you send the final numbers again?",
      roughDraftReply:
        "The price is $1,800 per month and onboarding can start May 28.",
      factsToPreserve:
        "Price is $1,800 per month for 25 seats plus $650 onboarding. Earliest kickoff is May 28. Full setup usually takes 5 business days after signature and data access. Do not promise completion before June 1.",
    });

    const unsupported = detectUnsupportedFacts(
      input,
      [
        "Following the demo, here are the final numbers.",
        "Once the contract is signed, setup usually takes 5 business days.",
        "So the earliest kickoff is May 28, which keeps the timing realistic.",
      ].join("\n\n"),
    );

    expect(unsupported.map((fact) => fact.normalizedText)).not.toEqual(
      expect.arrayContaining(["following", "here", "on", "once", "so", "which"]),
    );
  });

  it("does not treat fact labels and time fragments as names", () => {
    const input = replyInput({
      roughDraftReply:
        "Core dashboard is ready. Need Finance confirmation by 9:30 AM May 7.",
      factsToPreserve:
        "Quiz score was 72 percent, not a failure. Offer Tuesday, March 26 at 4:15 PM or Friday, March 29 at 8:05 AM. Explain that it is a prorated difference, not a random fee.",
    });

    const facts = extractRequiredFacts(input).map((fact) => fact.normalizedText);

    expect(facts).not.toEqual(
      expect.arrayContaining([
        "am",
        "am may",
        "core",
        "explain",
        "offer tuesday",
        "quiz",
        "upgrade",
      ]),
    );
  });

  it("normalizes articles inside deadline facts", () => {
    const input = draftOnly(
      "We need a corrected file before the Monday deadline and before Monday at 10am if possible.",
    );

    const missing = missingRequiredFacts(
      input,
      "Send the export settings if you need a corrected file before Monday at 10am.",
    );

    expect(missing.map((fact) => fact.normalizedText)).not.toContain(
      "before the monday",
    );
  });

  it("does not treat a shorter same-day deadline as unsupported when a more specific deadline was provided", () => {
    const input = draftOnly(
      "If Jordan submits the assignments by this Friday at 5 p.m., I can accept them for partial credit.",
    );

    const unsupported = detectUnsupportedFacts(
      input,
      "Jordan can submit the assignments by this Friday for partial credit.",
    );

    expect(unsupported.map((fact) => fact.normalizedText)).not.toContain(
      "by friday",
    );
  });

  it("does not treat before-day wording as unsupported when the original only names the same day", () => {
    const input = draftOnly(
      "I recommend updating the first screen and adding one short example before the next test on Wednesday.",
    );

    const unsupported = detectUnsupportedFacts(
      input,
      "I think the first screen should be updated before Wednesday, with one short example added.",
    );

    expect(unsupported.map((fact) => fact.normalizedText)).not.toContain(
      "before wednesday",
    );
  });

  it("does not turn background and-then workflow prose into a strict ordered step", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "The operations team imports the CSV into a spreadsheet, checks each campaign against its custom tag, and then sends the reconciled numbers to finance and the board assistant.",
      ),
    );

    expect(facts.some((fact) => fact.category === "ordered_step")).toBe(false);
  });

  it("extracts email addresses and repeated-invite facts", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Mina was added to the workspace with mina@northstar.example, and the invite was resent twice.",
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("mina@northstar.example");
    expect(factText).toContain("resent the invite twice");
  });

  it("accepts passive invite wording as preserving the repeated-invite fact", () => {
    const missing = missingRequiredFacts(
      draftOnly(
        "Mina was added to the workspace with mina@northstar.example, and we resent the invite twice.",
      ),
      "Mina's email address is mina@northstar.example. The invite was resent twice, so support should check the workspace link.",
    );

    expect(missing.map((fact) => fact.normalizedText)).not.toContain(
      "resent the invite twice",
    );
  });

  it("extracts short negative constraints that should survive paraphrase pressure", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "The May invoice will not be recalculated, the base plan did not change, and I cannot approve the final language yet.",
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("not be recalculated");
    expect(factText).toContain("base plan did not change");
    expect(factText).toContain("cannot approve");
  });

  it("extracts refund-window facts so support replies cannot shift the timing", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Hi Eli, the refund window ended May 10. We can offer account credit, but manager approval is required before I can apply it. I cannot promise the credit today.",
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("refund window");
  });

  it("extracts compact count facts that are easy to paraphrase away", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Four teachers said the onboarding copy felt too technical, and two asked for a sample response before signing up.",
      ),
    );

    expect(facts.map((fact) => fact.normalizedText)).toContain("two asked");
  });

  it("does not extract sentence-starting question words as names", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Did we get enough feedback from the teacher interviews to update the onboarding copy?",
      ),
    );

    expect(facts.map((fact) => fact.normalizedText)).not.toContain("did");
  });

  it("extracts sales and teacher constraints that should not be softened away", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "I am not promising a grade change yet. The expansion quote includes 12 additional seats, and I can send a second quote after manager approval.",
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("not promising");
    expect(factText).toContain("12 additional seats");
    expect(factText).toContain("second quote");
  });

  it("normalizes quiz scheduling constraints so natural rewrites can preserve them", () => {
    const input = draftOnly(
      "The quiz retake is separate and needs to be scheduled with me.",
    );

    const missing = missingRequiredFacts(
      input,
      "The quiz retake is separate, and he should schedule the quiz retake with me.",
    );

    expect(extractRequiredFacts(input).map((fact) => fact.normalizedText)).toContain(
      "scheduled with me",
    );
    expect(missing.map((fact) => fact.normalizedText)).not.toContain(
      "scheduled with me",
    );
  });

  it("extracts sales renewal facts that should not disappear from concise rewrites", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Hi Jordan, thanks for looking at the renewal proposal. I will send a shorter summary of the two plan options for your finance thread. I know your team is also comparing two other vendors, and the earliest decision point is the first week of June.",
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("renewal proposal");
    expect(factText).toContain("two plan options");
    expect(factText).toContain("finance thread");
    expect(factText).toContain("two other vendors");
    expect(factText).toContain("first week of june");
  });

  it("extracts compact teacher support facts from short draft-only replies", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Hi Mr. Ortiz, Amelia did complete the lab notes, but I have not received the reflection paragraph from Tuesday. She can bring it to lunch study hall on Thursday. If she submits it then, I can mark it late but complete.",
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("mr. ortiz");
    expect(factText).toContain("amelia");
    expect(factText).toContain("lab notes");
    expect(factText).toContain("reflection paragraph");
    expect(factText).toContain("tuesday");
    expect(factText).toContain("thursday");
    expect(factText).toContain("late but complete");
  });

  it("extracts support export and sales proposal details that concise rewrites often drop", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Mina, the April CSV export is missing the custom tags column for the Northeast region. The underlying campaign data is still safe.",
          "Hi Mateo, I attached the revised proposal with the implementation timeline from our May 12 call. Section three has the pricing language, and section five has the rollout notes. Please send comments by Friday if your legal team wants changes.",
        ].join(" "),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("april csv export");
    expect(factText).toContain("northeast region");
    expect(factText).toContain("data is still safe");
    expect(factText).toContain("implementation timeline");
    expect(factText).toContain("section three");
    expect(factText).toContain("section five");
    expect(factText).toContain("rollout notes");
    expect(factText).toContain("legal team");
  });

  it("extracts cover-letter seniority constraints as required style facts", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "I am applying for the Support Specialist role. I answer customer questions by email and chat. Please do not make me sound senior.",
      ),
    );

    expect(facts.map((fact) => fact.normalizedText)).toContain(
      "do not make me sound senior",
    );
  });

  it("extracts cover-letter numeric responsibility facts", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "I coordinate 32 volunteers, prepare monthly partner updates, manage weekend workshop schedules, and track attendance numbers for grant reports.",
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("32 volunteers");
    expect(factText).toContain("attendance numbers");
  });

  it("extracts negative pricing constraints in sales demos", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Hi Leah, we can move the demo to Thursday at 3pm. I will keep the agenda focused on reporting, team templates, and the approval workflow. I will not include pricing unless you ask for it.",
      ),
    );

    expect(facts.map((fact) => fact.normalizedText)).toContain(
      "will not include pricing",
    );
  });

  it("extracts draft-only product topics and negative support constraints from email corpus cases", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "Hi Jordan Park, I checked the support ticket and photo upload on May 9.",
          "The record I found is order number HL-48219.",
          "The relevant date for the cracked FinchLite desk lamp is May 6.",
          "The shipping credit is $9.80.",
          "I can send a replacement shade after address confirmation, but I cannot refund more than the $9.80 shipping credit.",
          "The next step is to confirm the mailing address.",
        ].join(" "),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("cracked finchlite desk lamp");
    expect(factText).toContain("replacement shade");
    expect(factText).toContain("cannot refund more than the $9.80 shipping credit");
  });

  it("extracts draft-only field-trip and scheduling topics from email corpus cases", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "The relevant date for the April 9 science museum field trip is April 9.",
          "I can provide a spare blank form through the front office, but I cannot add Maya after April 2.",
          "The relevant date for the cardiology scheduling request is December 3.",
          "I can hold the December 3 slot for 48 hours, but I cannot book the appointment before the referral arrives.",
        ].join(" "),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("april 9 science museum field trip");
    expect(factText).toContain("cannot add maya after april 2");
    expect(factText).toContain("cardiology scheduling request");
    expect(factText).toContain("hold the december 3 slot for 48 hours");
    expect(factText).toContain("cannot book the appointment before the referral arrives");
  });

  it("extracts draft-only volunteer shift and interview constraints from email corpus cases", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        [
          "The relevant date for the Saturday pantry volunteer shift is October 7.",
          "The shift length is 3 hours.",
          "I can reserve the three-hour packing shift for Samira, but I cannot place Samira at client intake without intake training.",
          "The next step is to arrive at the west entrance by 8:45 a.m.",
          "The relevant date for the product analyst final interview is January 16.",
          "I can hold the January 16 slot until January 12 at 3 p.m., but I cannot promise travel reimbursement for the cancelled train.",
        ].join(" "),
      ),
    );

    const factText = facts.map((fact) => fact.normalizedText);

    expect(factText).toContain("saturday pantry volunteer shift");
    expect(factText).toContain("three-hour packing shift");
    expect(factText).toContain("cannot place samira at client intake without intake training");
    expect(factText).toContain("west entrance");
    expect(factText).toContain("product analyst final interview");
    expect(factText).toContain("not promising travel reimbursement for the cancelled train");
  });

  it("reports missing negative pricing constraints in sales demos", () => {
    const input = draftOnly(
      "Hi Leah, we can move the demo to Thursday at 3pm. I will keep the agenda focused on reporting, team templates, and the approval workflow. I will not include pricing unless you ask for it.",
    );

    const missing = missingRequiredFacts(
      input,
      "Hi Leah,\n\nThe demo is now Thursday at 3pm. The agenda will cover reporting, team templates, and the approval workflow.",
    );

    expect(missing.map((fact) => fact.normalizedText)).toContain(
      "will not include pricing",
    );
  });

  it("extracts same-day desktop delivery facts that should not disappear", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Hi Ava, the homepage mockup is ready, but the mobile version still needs one spacing check. I can send desktop today and mobile by Wednesday morning. The logo color has not changed.",
      ),
    );

    expect(facts.map((fact) => fact.normalizedText)).toContain(
      "desktop today",
    );
  });

  it("reports missing same-day desktop delivery facts", () => {
    const input = draftOnly(
      "Hi Ava, the homepage mockup is ready, but the mobile version still needs one spacing check. I can send desktop today and mobile by Wednesday morning. The logo color has not changed.",
    );

    const missing = missingRequiredFacts(
      input,
      "Ava,\n\nThe homepage mockup and mobile version are ready for your review. They require one final spacing check by Wednesday morning. The logo color has not changed.",
    );

    expect(missing.map((fact) => fact.normalizedText)).toContain(
      "desktop today",
    );
  });

  it("extracts permission slip as a required school form fact", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Hi Alex, Maya can still join the science fair group, but the permission slip is due Thursday morning.",
      ),
    );

    expect(facts.map((fact) => fact.normalizedText)).toContain(
      "permission slip",
    );
  });

  it("normalizes low-risk equivalent wording used in natural rewrites", () => {
    expect(normalizeFactText("The retry worker is on hold.")).toContain(
      "retry worker is paused",
    );
    expect(normalizeFactText("not to cut down the tree")).toBe(
      "not cutting down the tree",
    );
    expect(normalizeFactText("I can't guarantee the credit today.")).toContain(
      "not promising the credit",
    );
    expect(
      normalizeFactText("We haven't pinned down the root cause yet."),
    ).toContain("root cause is not confirmed");
    expect(normalizeFactText("I can't say for sure today.")).toContain(
      "not promising today",
    );
  });
});
