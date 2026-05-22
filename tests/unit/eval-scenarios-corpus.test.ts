import { readFileSync } from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { parseLearningBaselineCorpus } from "../../scripts/eval-scenarios";

const fixturePath = path.join(
  process.cwd(),
  "tests",
  "fixtures",
  "learning-corpus-mini.md",
);

describe("learning baseline corpus parser", () => {
  const markdown = readFileSync(fixturePath, "utf8");

  it("parses learning baseline markdown tables into eval cases", () => {
    const cases = parseLearningBaselineCorpus(markdown);

    expect(cases).toHaveLength(5);
    expect(cases[0]).toMatchObject({
      id: "lbc-blank-001",
      scenario: "Blank / custom",
      tonePreset: "Warm",
      messageToReplyTo: "",
      roughDraftReply: "Please note that the room changed to B12 for Thursday.",
      expectedFacts: ["Room changed to B12", "day is Thursday."],
    });
    expect(cases[3]).toMatchObject({
      id: "lbc-email-001",
      scenario: "Email or message reply",
      tonePreset: "Warm",
      expectedFacts: ["Lina", "make-up quiz Thursday after school."],
    });
  });

  it("trims table cell values and fact fragments", () => {
    const cases = parseLearningBaselineCorpus(markdown);

    expect(cases[1]).toMatchObject({
      id: "lbc-blank-002",
      roughDraftReply:
        "Need a note that the survey link was fixed at 2:15 PM and replies are due Friday.",
      expectedFacts: ["Survey link fixed at 2:15 PM", "replies due Friday."],
    });
  });

  it("rejects unknown tones with the row ID", () => {
    const invalid = markdown.replace(
      "| lbc-email-002 | hand_crafted_edge_case | Email | Direct |",
      "| lbc-email-002 | hand_crafted_edge_case | Email | Friendly |",
    );

    expect(() => parseLearningBaselineCorpus(invalid)).toThrow(
      /lbc-email-002.*tone.*Friendly/i,
    );
  });

  it("rejects rows with missing columns and cites the row ID", () => {
    const invalid = markdown.replace(
      "| lbc-blank-003 | hand_crafted_edge_case | Blank | Warm | Thank Aria for covering pickup yesterday and offer to take Friday. | Thank Aria; pickup was yesterday; offer Friday. | 40-60% |",
      "| lbc-blank-003 | hand_crafted_edge_case | Blank | Warm | Thank Aria for covering pickup yesterday and offer to take Friday. | Thank Aria; pickup was yesterday; offer Friday. |",
    );

    expect(() => parseLearningBaselineCorpus(invalid)).toThrow(
      /lbc-blank-003.*7 columns/i,
    );
  });
});
