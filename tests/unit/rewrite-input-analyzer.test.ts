import { describe, expect, it } from "vitest";

import { analyzeRewriteInput } from "../../lib/rewrite-pipeline/input-analyzer";
import type { RewriteRequestInput } from "../../lib/validation";

function makeInput(partial: Partial<RewriteRequestInput>): RewriteRequestInput {
  return {
    scenario: "General reply",
    messageToReplyTo: "",
    roughDraftReply: "Thanks for the note. I will review this today.",
    audience: "",
    purpose: "",
    whatHappened: "",
    factsToPreserve: "",
    tone: "warm",
    tonePreset: "Warm",
    ...partial,
  };
}

describe("analyzeRewriteInput", () => {
  it("routes long support policy text to support-policy options rewrite", () => {
    const analysis = analyzeRewriteInput(
      makeInput({
        messageToReplyTo:
          "Daniel wants to transfer his online course enrollment. The June weekend cohort starts Saturday, 6 June. A refund may be possible, but it depends on the exact registration timestamp and the seven-day refund policy.",
        roughDraftReply:
          "Hi Daniel, you may be eligible to move to the Saturday, 20 July cohort depending on seat availability. We will not update your registration or cancel your current seat unless you confirm which option you want.",
      }),
    );

    expect(analysis.scenarioHint).toBe("support_policy");
    expect(analysis.requiresPolicyCare).toBe(true);
    expect(analysis.riskLevel).toBe("high");
    expect(analysis.recommendedInitialStrategy).toBe(
      "support_policy_options_rewrite",
    );
  });

  it("routes numbered or quoted content to quote/list-safe rewrite", () => {
    const analysis = analyzeRewriteInput(
      makeInput({
        roughDraftReply:
          "Please keep these two options:\n\n1. Transfer the enrollment to July.\n2. Request a refund review.\n\n\"We will not change the registration unless Daniel confirms.\"",
      }),
    );

    expect(analysis.inputKind).toBe("quote_or_list_heavy");
    expect(analysis.containsListsOrQuotes).toBe(true);
    expect(analysis.recommendedInitialStrategy).toBe("quote_list_safe_rewrite");
  });

  it("uses a small strategy for short low-risk drafts", () => {
    const analysis = analyzeRewriteInput(
      makeInput({
        roughDraftReply:
          "Hi Chris, I can move our catch-up to Thursday at 2pm if that still works.",
      }),
    );

    expect(analysis.riskLevel).toBe("low");
    expect(analysis.recommendedInitialStrategy).toBe("minimal_polish");
  });
});
