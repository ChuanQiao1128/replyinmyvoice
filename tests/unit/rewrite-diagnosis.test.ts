import { describe, expect, it } from "vitest";

import {
  analyzeDraftForRewrite,
  createRewritePlan,
} from "../../lib/rewrite-diagnosis";
import type { RewriteRequestInput } from "../../lib/validation";

function input(overrides: Partial<RewriteRequestInput>): RewriteRequestInput {
  return {
    scenario: "Email or message reply",
    messageToReplyTo: "",
    roughDraftReply:
      "Thank you for reaching out. I understand your concern and will respond accordingly at your earliest convenience.",
    audience: "",
    purpose: "",
    whatHappened: "",
    factsToPreserve: "",
    tone: "warm",
    tonePreset: "Warm",
    ...overrides,
  };
}

describe("rewrite diagnosis", () => {
  it("tags stock openings, corporate polish, and generic transitions", () => {
    const tags = analyzeDraftForRewrite(
      input({
        roughDraftReply:
          "Thank you for reaching out. I understand your concern regarding the matter. Furthermore, we will proceed accordingly at your earliest convenience.",
      }),
    );

    expect(tags).toContain("stock_opening");
    expect(tags).toContain("corporate_polish");
    expect(tags).toContain("generic_transitions");
  });

  it("recognizes cover letter cliches", () => {
    const tags = analyzeDraftForRewrite(
      input({
        scenario: "Cover letter",
        roughDraftReply:
          "I am writing to express my interest in the role. I am a passionate and results-driven professional with a proven track record and I believe I would be a perfect fit.",
      }),
    );

    expect(tags).toContain("application_cliche");
  });

  it("builds scenario-specific guardrails into the rewrite plan", () => {
    const request = input({
      scenario: "Customer support",
      messageToReplyTo:
        "Why is my invoice NZD $126 higher, and are the three temporary users still active?",
      roughDraftReply:
        "Thank you for contacting us regarding your invoice concern. We understand the discrepancy and will look into the matter.",
    });
    const plan = createRewritePlan(request, analyzeDraftForRewrite(request));

    expect(plan.scenario).toBe("Customer support");
    expect(plan.guardrails.join(" ")).toContain("preserve amounts");
    expect(plan.steps.join(" ")).toContain("answer the specific question");
  });
});
