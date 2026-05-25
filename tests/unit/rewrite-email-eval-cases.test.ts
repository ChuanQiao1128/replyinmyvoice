import { readFileSync } from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import {
  parseRewriteEmailEvalCasePlan,
  parseRewriteEmailEvalCases,
  rewriteEmailEvalCaseToRequestInput,
  selectRewriteEmailEvalCases,
  validateRewriteEmailEvalCorpus,
} from "../../lib/rewrite-eval-cases";

const corpusPath = path.join(
  process.cwd(),
  "docs",
  "rewrite-email-eval-cases-100.md",
);

describe("rewrite email eval corpus parser", () => {
  const markdown = readFileSync(corpusPath, "utf8");

  it("parses the 100-row single-input draft case plan", () => {
    const plan = parseRewriteEmailEvalCasePlan(markdown);

    expect(plan).toHaveLength(100);
    expect(plan[0]).toMatchObject({
      caseNumber: 1,
      id: "rewrite-draft-001",
      sourceType: "email",
      tonePreset: "Warm",
    });
    expect(plan.at(-1)).toMatchObject({
      caseNumber: 100,
      id: "rewrite-draft-100",
    });
  });

  it("parses all 100 materialized single-input draft cases", () => {
    const cases = parseRewriteEmailEvalCases(markdown);

    expect(cases).toHaveLength(100);
    expect(cases[0]).toMatchObject({
      caseNumber: 1,
      id: "rewrite-draft-001",
      category: "teacher_parent",
      tonePreset: "Warm",
      sourceType: "email",
      inputWordCountBand: "160-280",
    });
    expect(cases[0].inputDraft.length).toBeGreaterThan(50);
    expect(cases[0].inputDraft.split(/\s+/).length).toBeLessThanOrEqual(400);
    expect(cases[0].mustKeep.length).toBeGreaterThanOrEqual(6);
    expect(cases[0].mustNotClaim.length).toBeGreaterThanOrEqual(2);
    expect(cases[0].rewriteQualityTargets.length).toBeGreaterThan(20);

    expect(cases.at(-1)).toMatchObject({
      caseNumber: 100,
      id: "rewrite-draft-100",
    });
    expect(cases.at(-1)!.category).toBeTruthy();
    expect(cases.at(-1)!.mustKeep.length).toBeGreaterThanOrEqual(6);
    expect(cases.at(-1)!.mustNotClaim.length).toBeGreaterThanOrEqual(2);

    for (const sample of cases) {
      expect(sample.tonePreset).toBe("Warm");
      for (const forbiddenClaim of sample.mustNotClaim) {
        expect(forbiddenClaim).toMatch(/^Do not /i);
      }
    }
  });

  it("selects materialized corpus sizes per mode", () => {
    const cases = parseRewriteEmailEvalCases(markdown);

    expect(selectRewriteEmailEvalCases(cases, "smoke")).toHaveLength(10);
    expect(selectRewriteEmailEvalCases(cases, "focused")).toHaveLength(40);
    expect(selectRewriteEmailEvalCases(cases, "full")).toHaveLength(100);
  });

  it("maps draft eval cases into the product single-input warm rewrite request", () => {
    const cases = parseRewriteEmailEvalCases(markdown);

    for (const sample of cases) {
      const input = rewriteEmailEvalCaseToRequestInput(sample);

      expect(input.messageToReplyTo).toBe("");
      expect(input.roughDraftReply).toBe(sample.inputDraft);
      expect(input.audience).toBe("");
      expect(input.purpose).toBe("");
      expect(input.whatHappened).toBe("");
      expect(input.factsToPreserve).toBe("");
      expect(input.tonePreset).toBe("Warm");
    }
  });

  it("treats Beacon handoff as a context anchor, not an invented work-state claim", () => {
    const cases = parseRewriteEmailEvalCases(markdown);
    const beaconCase = cases.find((sample) => sample.id === "rewrite-draft-013");

    expect(beaconCase?.mustKeep).toContain(
      "The update is about the Beacon handoff.",
    );
    expect(beaconCase?.mustKeep).not.toContain(
      "The team is working on the Beacon handoff.",
    );
  });

  it("validates corpus plan and materialized draft cases", () => {
    expect(validateRewriteEmailEvalCorpus(markdown)).toEqual({
      planRows: 100,
      materializedCases: 100,
    });
  });
});
