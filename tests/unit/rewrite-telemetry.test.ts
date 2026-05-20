import { describe, expect, it } from "vitest";

import {
  createRewriteTelemetryCollector,
  serializeRewriteCostLog,
} from "../../lib/observability/rewrite-telemetry";
import type { RewriteResponsePayload } from "../../lib/rewrite-types";
import type { RewriteRequestInput } from "../../lib/validation";

const input: RewriteRequestInput = {
  scenario: "Customer support",
  messageToReplyTo: "Hi team, the May invoice looks too high.",
  roughDraftReply: "Thank you for reaching out about the invoice.",
  audience: "",
  purpose: "",
  whatHappened: "",
  factsToPreserve: "",
  tone: "warm",
  tonePreset: "Warm",
};

const response: RewriteResponsePayload = {
  rewrittenText: "Hi Priya, I checked the May invoice preview.",
  changeSummary: ["Removed stock support wording."],
  riskNotes: ["Review billing facts before sending."],
  naturalness: {
    draftAiLikePercent: 88,
    rewriteAiLikePercent: 22,
    changePoints: -66,
    label: "lower",
  },
  optimization: {
    internalStrategiesTried: 3,
    repairCandidatesTried: 1,
    rejectedCandidates: 2,
    userUsageCharged: 1,
    selectionStatus: "passed",
    diagnosisTags: ["support_template_voice"],
    rewritePlanSummary: "Use support-specific structure.",
    candidateSignals: [
      {
        stage: "targeted_repair",
        aiLikePercent: 22,
        status: "pass_below_threshold",
        rejected: false,
        reason: "Passed.",
      },
    ],
  },
};

describe("rewrite telemetry collector", () => {
  it("builds a request-level cost log from provider calls and rewrite result", () => {
    const collector = createRewriteTelemetryCollector({
      requestId: "req_test",
      startedAt: new Date("2026-05-20T01:00:00.000Z"),
    });

    collector.recordProviderCall({
      provider: "openai",
      role: "mid_writer",
      model: "gpt-5.4-mini",
      inputTokens: 1200,
      outputTokens: 400,
      estimatedCostUsd: 0.0027,
      success: true,
      latencyMs: 1200,
    });
    collector.recordProviderCall({
      provider: "sapling",
      role: "final_signal",
      characters: 2500,
      estimatedCostUsd: 0.0125,
      success: true,
      latencyMs: 300,
    });

    const log = collector.finish({
      userId: "user_123",
      input,
      status: "success",
      response,
      strategyVersion: "adaptive_rewrite_orchestrator",
      finishedAt: new Date("2026-05-20T01:00:02.000Z"),
    });

    expect(log.userId).toBe("user_123");
    expect(log.requestId).toBe("req_test");
    expect(log.status).toBe("success");
    expect(log.scenario).toBe("Customer support");
    expect(log.draftAiLikePercent).toBe(88);
    expect(log.rewriteAiLikePercent).toBe(22);
    expect(log.changePoints).toBe(-66);
    expect(log.openAiInputTokens).toBe(1200);
    expect(log.openAiOutputTokens).toBe(400);
    expect(log.saplingCallCount).toBe(1);
    expect(log.saplingCharacters).toBe(2500);
    expect(log.totalEstimatedCostUsd).toBeCloseTo(0.0152, 6);
    expect(log.usedEscalation).toBe(false);
    expect(log.durationMs).toBe(2000);
    expect(serializeRewriteCostLog(log).providerCallsJson).toContain(
      "final_signal",
    );
  });
});
