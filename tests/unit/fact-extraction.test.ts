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

  it("extracts compact count facts that are easy to paraphrase away", () => {
    const facts = extractRequiredFacts(
      draftOnly(
        "Four teachers said the onboarding copy felt too technical, and two asked for a sample response before signing up.",
      ),
    );

    expect(facts.map((fact) => fact.normalizedText)).toContain("two asked");
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
