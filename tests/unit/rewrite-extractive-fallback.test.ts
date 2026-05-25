import { describe, expect, it } from "vitest";

import { generateExtractiveFallbackCandidate } from "../../lib/rewrite-extractive-fallback";
import type { RewriteRequestInput } from "../../lib/validation";

function input(roughDraftReply: string): RewriteRequestInput {
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

describe("generateExtractiveFallbackCandidate", () => {
  it("preserves concrete sales proposal facts while removing no facts", () => {
    const result = generateExtractiveFallbackCandidate(
      input(
        "Hi Mateo, I attached the revised proposal with the implementation timeline from our May 12 call. Section three has the pricing language, and section five has the rollout notes. Please send comments by Friday if your legal team wants changes.",
      ),
    );

    expect(result.rewrittenText).toContain("Hi Mateo");
    expect(result.rewrittenText).toContain("May 12");
    expect(result.rewrittenText).toContain("Section three");
    expect(result.rewrittenText).toContain("pricing language");
    expect(result.rewrittenText).toContain("section five");
    expect(result.rewrittenText).toContain("rollout notes");
    expect(result.rewrittenText).toContain("Friday");
    expect(result.rewrittenText).toContain("legal team");
  });

  it("keeps negative constraints in short teacher replies", () => {
    const result = generateExtractiveFallbackCandidate(
      input(
        "Hi Lena, Kai's grade changed because two participation activities and one exit ticket are still missing. The course policy says late work may not receive full credit, but he can submit the exit ticket before class tomorrow and I will review it. I am not promising a grade change yet.",
      ),
    );

    expect(result.rewrittenText).toContain("Hi Lena");
    expect(result.rewrittenText).toContain("Kai");
    expect(result.rewrittenText).toContain("two participation activities");
    expect(result.rewrittenText).toContain("one exit ticket");
    expect(result.rewrittenText).toContain("course policy");
    expect(result.rewrittenText).toContain("before class tomorrow");
    expect(result.rewrittenText).toContain("I'm not promising");
  });

  it("uses a natural teacher make-up quiz pattern while preserving exact times", () => {
    const result = generateExtractiveFallbackCandidate(
      input(
        "Hi Priya, Ravi was absent for the quiz on Monday and still needs to schedule the make-up. The available times are Wednesday at 8:15am or Friday after school. I cannot enter the quiz score until he completes it.",
      ),
    );

    expect(result.rewrittenText).toContain("Hi Priya");
    expect(result.rewrittenText).toContain("Ravi missed Monday's quiz");
    expect(result.rewrittenText).toContain("Wednesday at 8:15am");
    expect(result.rewrittenText).toContain("Friday after school");
    expect(result.rewrittenText).toContain("can't enter the quiz score");
  });

  it("does not split sentences at a.m. or p.m. abbreviations", () => {
    const result = generateExtractiveFallbackCandidate(
      input(
        "Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m. because the room is unavailable. I can offer Thursday at 2:30 p.m. or Friday at 9 a.m. Please choose one by Wednesday at noon.",
      ),
    );

    expect(result.rewrittenText).toContain(
      "from 11 a.m. because the room is unavailable",
    );
    expect(result.rewrittenText).toContain(
      "Thursday at 2:30 p.m. or Friday at 9 a.m.",
    );
    expect(result.rewrittenText).not.toContain("Because the room is unavailable.");
    expect(result.rewrittenText).not.toContain("Or Friday at 9 a.m.");
  });

  it("removes generic parent-reply filler while keeping Kai's missing items", () => {
    const result = generateExtractiveFallbackCandidate(
      input(
        "Thank you for reaching out regarding Kai's grade. I understand your concern. The grade change is due to two missing participation activities and one missing exit ticket. I can provide additional details if needed and would be happy to discuss this matter further at your earliest convenience.",
      ),
    );

    expect(result.rewrittenText).toContain("Kai");
    expect(result.rewrittenText).toContain("two missing participation activities");
    expect(result.rewrittenText).toContain("one missing exit ticket");
    expect(result.rewrittenText).not.toContain("earliest convenience");
  });

  it("turns teacher interview updates into a concise work update without losing counts", () => {
    const result = generateExtractiveFallbackCandidate(
      input(
        "I am writing to inform you that the teacher interview notes are now ready for review. We completed six interviews this week. Four teachers mentioned that the onboarding copy felt too technical, and two asked for a sample response before signing up. I recommend updating the first screen and adding one short example before the next test on Wednesday.",
      ),
    );

    expect(result.rewrittenText).toContain("six interviews");
    expect(result.rewrittenText).toContain("ready for review");
    expect(result.rewrittenText).toContain("Four");
    expect(result.rewrittenText).toContain("Two");
    expect(result.rewrittenText).toContain("sample response");
    expect(result.rewrittenText).toContain("first screen");
    expect(result.rewrittenText).toContain("Wednesday");
  });
});
