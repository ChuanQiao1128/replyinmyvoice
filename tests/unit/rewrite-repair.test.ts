import { describe, expect, it } from "vitest";

import { buildRepairUserPrompt } from "../../lib/openai";
import { evaluateSignalQuality } from "../../lib/rewrite-quality-gate";
import { createRewritePlan } from "../../lib/rewrite-diagnosis";
import type { RewriteRequestInput } from "../../lib/validation";

const priyaInput: RewriteRequestInput = {
  scenario: "Customer support",
  messageToReplyTo:
    "Hi Reply In My Voice team, our usage report shows 18 active seats for May, but we only approved 15 regular seats. The invoice preview is NZD $126 higher than last month. Three temporary contractors were supposed to be removed after the client handover on May 8. Can you confirm what changed and what we should do before the invoice is finalized? Thanks, Priya",
  roughDraftReply:
    "Hi Priya, Thank you for contacting us regarding the usage report and invoice preview in your account. We understand that there appears to be a discrepancy between the number of active seats shown in the dashboard and the number of seats approved during your renewal. The most likely explanation is that the three temporary contractors were counted as active seats during May. Even if a user only joins for part of the month, prorated charges may apply. Please check whether the contractors are still active and send us their names if you would like assistance.",
  audience: "",
  purpose: "",
  whatHappened: "",
  factsToPreserve: "",
  tone: "warm",
  tonePreset: "Warm",
};

describe("buildRepairUserPrompt", () => {
  it("passes score evidence, rejected text, failure reason, tags, and facts into repair", () => {
    const plan = createRewritePlan(priyaInput);
    const quality = evaluateSignalQuality({
      draftPercent: 89,
      rewritePercent: 99,
    });

    const prompt = buildRepairUserPrompt({
      input: priyaInput,
      plan,
      rejectedText:
        "I see how this can be confusing. From what you described, it seems the invoice preview is higher.",
      draftPercent: 89,
      candidatePercent: 99,
      quality,
    });

    expect(prompt).toContain("Draft score: 89%");
    expect(prompt).toContain("Rejected candidate score: 99%");
    expect(prompt).toContain(quality.reason);
    expect(prompt).toContain("support_template_voice");
    expect(prompt).toContain("18 active seats");
    expect(prompt).toContain("15 regular seats");
    expect(prompt).toContain("NZD $126");
    expect(prompt).toContain("May 8");
    expect(prompt).toContain("Rejected candidate:");
  });

  it("tells customer-support repair to remove macro-like phrasing while preserving detail", () => {
    const plan = createRewritePlan(priyaInput);
    const quality = evaluateSignalQuality({
      draftPercent: 89,
      rewritePercent: 100,
    });

    const prompt = buildRepairUserPrompt({
      input: priyaInput,
      plan,
      rejectedText:
        "From what you described, it seems the base plan is not the issue. For next steps, please check whether the accounts are active.",
      draftPercent: 89,
      candidatePercent: 100,
      quality,
    });

    expect(prompt).toContain("Remove these macro-like patterns");
    expect(prompt).toContain("I see how this can be confusing");
    expect(prompt).toContain("From what you described");
    expect(prompt).toContain("For next steps");
    expect(prompt).toContain("keep the billing explanation");
    expect(prompt).toContain("preserve the requested next step");
  });
});
