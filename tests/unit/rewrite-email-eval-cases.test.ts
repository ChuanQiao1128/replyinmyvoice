import { readFileSync } from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import {
  factsToPreserveExpectations,
  parseRewriteEmailEvalCases,
  rewriteEmailEvalCaseToRequestInput,
  selectRewriteEmailEvalCases,
} from "../../lib/rewrite-eval-cases";

const corpusPath = path.join(
  process.cwd(),
  "docs",
  "rewrite-email-eval-cases-100.md",
);

describe("rewrite email eval corpus parser", () => {
  const markdown = readFileSync(corpusPath, "utf8");

  it("parses the synthetic 100-case markdown corpus", () => {
    const cases = parseRewriteEmailEvalCases(markdown);

    expect(cases).toHaveLength(100);
    expect(cases[0]).toMatchObject({
      caseNumber: 1,
      id: "rimv-email-001",
      category: "teacher_parent",
      tonePreset: "Warm",
      messageToReplyTo: expect.stringContaining("Maya"),
      roughDraftReply: expect.stringContaining("I checked"),
      factsToPreserve: expect.stringContaining("April 2"),
    });
    expect(cases.at(-1)).toMatchObject({
      caseNumber: 100,
      id: "rimv-email-100",
      category: "professional_services",
      factsToPreserve: expect.stringContaining("Do not send revised quote"),
    });
  });

  it("selects staged corpus sizes for smoke, focused, and full runs", () => {
    const cases = parseRewriteEmailEvalCases(markdown);

    expect(selectRewriteEmailEvalCases(cases, "smoke")).toHaveLength(10);
    expect(selectRewriteEmailEvalCases(cases, "focused")).toHaveLength(40);
    expect(selectRewriteEmailEvalCases(cases, "full")).toHaveLength(100);
  });

  it("maps email eval cases into rewrite inputs with all context fields", () => {
    const [sample] = parseRewriteEmailEvalCases(markdown);
    const input = rewriteEmailEvalCaseToRequestInput(sample);

    expect(input.messageToReplyTo).toContain("Maya");
    expect(input.roughDraftReply).toContain("another form");
    expect(input.audience).toBe("Parent of a fourth grade student.");
    expect(input.purpose).toContain("help the parent");
    expect(input.whatHappened).toContain("March 28");
    expect(input.factsToPreserve).toContain("April 2");
  });

  it("separates must-include facts from forbidden-claim constraints", () => {
    const expectations = factsToPreserveExpectations(
      "Price is $1,800 per month for 25 seats plus $650 onboarding. Earliest kickoff is May 28. Do not promise completion before June 1. Do not offer a refund.",
    );

    expect(expectations.expectedFacts).toEqual([
      "Price is $1,800 per month for 25 seats plus $650 onboarding.",
      "Earliest kickoff is May 28.",
    ]);
    expect(expectations.forbiddenClaims).toEqual([
      "Do not promise completion before June 1.",
      "Do not offer a refund.",
    ]);
  });
});
