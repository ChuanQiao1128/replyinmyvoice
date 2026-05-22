import { describe, expect, it } from "vitest";

import {
  compareScenarioEvaluationResults,
  parseScenarioEvaluationResults,
} from "../../lib/scenario-evaluation-regression";

const baseMarkdown = `# Scenario Evaluation Results

Average AI-like signal drop: 62 pts
Rewrite below 50% AI-like signal: 10/10
Customer-usable pass count: 10/10
Strict signal pass count: 10/10

## rimv-email-001

Customer-usable pass: yes
Strict signal pass: yes

## rimv-email-002

Customer-usable pass: yes
Strict signal pass: yes
`;

describe("scenario evaluation regression guard", () => {
  it("parses summary metrics and per-case pass flags", () => {
    const parsed = parseScenarioEvaluationResults(baseMarkdown);

    expect(parsed.averageSignalReductionPoints).toBe(62);
    expect(parsed.below50Percent).toBe(100);
    expect(parsed.cases.get("rimv-email-001")).toEqual({
      customerUsablePass: true,
      strictSignalPass: true,
    });
  });

  it("fails when the average signal reduction drops by at least five points", () => {
    const candidateMarkdown = baseMarkdown.replace(
      "Average AI-like signal drop: 62 pts",
      "Average AI-like signal drop: 57 pts",
    );

    const result = compareScenarioEvaluationResults({
      baseMarkdown,
      candidateMarkdown,
    });

    expect(result.passed).toBe(false);
    expect(result.failures).toContain(
      "Average signal reduction dropped by 5.00 points: main 62.00, candidate 57.00.",
    );
  });

  it("fails when the below-50 percentage drops by at least five points", () => {
    const candidateMarkdown = baseMarkdown.replace(
      "Rewrite below 50% AI-like signal: 10/10",
      "Rewrite below 50% AI-like signal: 9/10",
    );

    const result = compareScenarioEvaluationResults({
      baseMarkdown,
      candidateMarkdown,
    });

    expect(result.passed).toBe(false);
    expect(result.failures).toContain(
      "Below-50 rate dropped by 10.00 points: main 100.00%, candidate 90.00%.",
    );
  });

  it("fails when a previously passing case fails in the candidate", () => {
    const candidateMarkdown = baseMarkdown.replace(
      "## rimv-email-002\n\nCustomer-usable pass: yes\nStrict signal pass: yes",
      "## rimv-email-002\n\nCustomer-usable pass: no\nStrict signal pass: no",
    );

    const result = compareScenarioEvaluationResults({
      baseMarkdown,
      candidateMarkdown,
    });

    expect(result.passed).toBe(false);
    expect(result.failures).toEqual([
      "Case rimv-email-002 regressed: Customer-usable pass changed from yes to no.",
      "Case rimv-email-002 regressed: Strict signal pass changed from yes to no.",
    ]);
  });

  it("passes when candidate metrics stay within the allowed regression budget", () => {
    const candidateMarkdown = baseMarkdown
      .replace("Average AI-like signal drop: 62 pts", "Average AI-like signal drop: 58 pts")
      .replace("Rewrite below 50% AI-like signal: 10/10", "Rewrite below 50% AI-like signal: 96/100");

    const result = compareScenarioEvaluationResults({
      baseMarkdown,
      candidateMarkdown,
    });

    expect(result).toEqual({
      passed: true,
      failures: [],
      summary: {
        averageSignalReductionDrop: 4,
        below50Drop: 4,
      },
    });
  });
});
