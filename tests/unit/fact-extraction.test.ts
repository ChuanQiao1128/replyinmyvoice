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

describe("extractRequiredFacts", () => {
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
