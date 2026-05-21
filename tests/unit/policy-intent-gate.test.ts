import { describe, expect, it } from "vitest";

import { runPolicyIntentGate } from "../../lib/rewrite-pipeline/policy-intent-gate";
import type { RewriteRequestInput } from "../../lib/validation";

function makeInput(roughDraftReply: string): RewriteRequestInput {
  return {
    scenario: "General reply",
    messageToReplyTo: "",
    roughDraftReply,
    audience: "",
    purpose: "",
    whatHappened: "",
    factsToPreserve: "",
    tone: "direct",
    tonePreset: "Direct",
  };
}

describe("runPolicyIntentGate", () => {
  it("rejects turning refund eligibility uncertainty into a guarantee", () => {
    const input = makeInput(
      "A refund may be possible, but it depends on the exact registration timestamp and the seven-day policy.",
    );
    const result = runPolicyIntentGate(
      input,
      "A full refund is available under the seven-day policy.",
    );

    expect(result.safe).toBe(false);
    expect(result.issues.map((issue) => issue.kind)).toContain(
      "changed_policy_or_condition",
    );
    expect(result.issues.map((issue) => issue.ruleId)).toContain(
      "refund_eligibility_uncertainty",
    );
  });

  it("rejects dropping a no-change-without-confirmation constraint", () => {
    const input = makeInput(
      "We will not update your registration or cancel your current seat unless you clearly confirm which option you want.",
    );
    const result = runPolicyIntentGate(
      input,
      "We can move your enrollment to July and update the registration.",
    );

    expect(result.safe).toBe(false);
    expect(result.issues.map((issue) => issue.message)).toContain(
      "Dropped the no-action-without-confirmation constraint.",
    );
    expect(result.issues.map((issue) => issue.ruleId)).toContain(
      "no_action_without_confirmation",
    );
  });

  it("accepts a careful support-policy rewrite that preserves constraints", () => {
    const input = makeInput(
      "You may be eligible to transfer to a later cohort, depending on seat availability. We will not update your registration unless you confirm.",
    );
    const result = runPolicyIntentGate(
      input,
      "You may still be eligible to transfer to a later cohort, depending on availability. We will not update the registration unless you confirm that you want the transfer.",
    );

    expect(result.safe).toBe(true);
    expect(result.issues).toEqual([]);
  });

  it("does not treat a refund review question as a refund guarantee", () => {
    const input = makeInput(
      "A refund may be possible, but we need to review the exact registration timestamp before confirming whether a full refund is available.",
    );
    const result = runPolicyIntentGate(
      input,
      "We need to check the exact registration timestamp before confirming whether a full refund is available.",
    );

    expect(result.safe).toBe(true);
  });

  it("accepts keeping a go-live date while asking for an updated implementation note", () => {
    const input = makeInput(
      "Please do not change the go-live date yet. We are still aiming for Monday, 17 June.",
    );
    const result = runPolicyIntentGate(
      input,
      "Please keep Monday, 17 June as the go-live date for now. Could you send an updated implementation note covering what changed?",
    );

    expect(result.safe).toBe(true);
    expect(result.issues).toEqual([]);
  });
});
