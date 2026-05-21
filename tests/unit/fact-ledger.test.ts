import { describe, expect, it } from "vitest";

import { reviewExtractedFacts } from "../../lib/rewrite-pipeline/fact-ledger";
import type { ExtractedFacts } from "../../lib/rewrite-pipeline/types";
import type { RewriteRequestInput } from "../../lib/validation";

function inputFromDraft(roughDraftReply: string): RewriteRequestInput {
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

function emptyFacts(overrides: Partial<ExtractedFacts> = {}): ExtractedFacts {
  return {
    recipient_name: "",
    sender_name_or_role: "",
    people_mentioned: [],
    main_purpose: "",
    key_facts: [],
    required_actions: [],
    deadlines: [],
    dates_times: [],
    positive_notes: [],
    concerns: [],
    policies_or_conditions: [],
    available_support: [],
    clarifications: [],
    facts_that_must_not_change: [],
    sensitive_points: [],
    original_tone: "",
    ...overrides,
  };
}

describe("reviewExtractedFacts", () => {
  it("adds deterministic anchors and rejects unsupported hard facts before rewrite", () => {
    const input = inputFromDraft(
      [
        "Hi Emma,",
        "Your order has already left our fulfillment center and is currently with the delivery carrier.",
        "The delay seems to be related to a temporary processing issue at the local distribution facility.",
        "Your package is still in transit and has not been marked as lost or returned.",
        "We expect a new delivery update within the next one to two business days.",
        "There is no action required from you right now.",
        "If the tracking status does not change within two business days, we can open a follow-up investigation with the carrier.",
        "Best regards,",
        "Customer Support Team",
      ].join("\n\n"),
    );
    const rawFacts = emptyFacts({
      recipient_name: "Emma",
      people_mentioned: ["There", "Emma"],
      key_facts: ["The package is lost."],
      facts_that_must_not_change: [
        "Customer Support Team",
        "full refund available",
        "one to two business days",
      ],
    });

    const reviewed = reviewExtractedFacts(input, rawFacts);

    expect(reviewed.facts.people_mentioned).toContain("Emma");
    expect(reviewed.facts.people_mentioned).not.toContain("There");
    expect(reviewed.facts.key_facts).not.toContain("The package is lost.");
    expect(reviewed.facts.facts_that_must_not_change).toEqual(
      expect.arrayContaining([
        "fulfillment center",
        "delivery carrier",
        "temporary processing issue",
        "local distribution facility",
        "still in transit",
        "lost or returned",
        "one to two business days",
        "no action required",
        "follow-up investigation with the carrier",
      ]),
    );
    expect(reviewed.facts.facts_that_must_not_change).not.toContain(
      "full refund available",
    );
    expect(reviewed.rejectedFacts.map((fact) => fact.text)).toEqual(
      expect.arrayContaining(["The package is lost.", "full refund available"]),
    );
  });

  it("promotes missed money, counts, dates, and policy constraints into the hard ledger", () => {
    const input = inputFromDraft(
      "Hi Priya, the dashboard shows 18 active seats instead of 15 regular seats. The extra NZD $126 appears to be prorated, and the base plan has not changed.",
    );
    const rawFacts = emptyFacts({
      recipient_name: "Priya",
      key_facts: ["The invoice is higher."],
      facts_that_must_not_change: ["base plan has not changed"],
    });

    const reviewed = reviewExtractedFacts(input, rawFacts);

    expect(reviewed.facts.facts_that_must_not_change).toEqual(
      expect.arrayContaining([
        "18 active seats",
        "15 regular seats",
        "NZD $126",
        "base plan",
      ]),
    );
    expect(reviewed.addedAnchors.map((fact) => fact.normalizedText)).toEqual(
      expect.arrayContaining([
        "18 active seats",
        "15 regular seats",
        "nzd $126",
        "base plan",
      ]),
    );
  });
});
