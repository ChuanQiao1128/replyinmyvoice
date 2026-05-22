import { afterEach, describe, expect, it, vi } from "vitest";

import { getSql } from "../../lib/db";
import {
  createRewriteTelemetryCollector,
  persistRewriteCostLog,
  serializeRewriteCostLog,
} from "../../lib/observability/rewrite-telemetry";
import { FactReconstructQualityError } from "../../lib/rewrite-pipeline/pipeline";
import type { RewriteResponsePayload } from "../../lib/rewrite-types";
import type { RewriteRequestInput } from "../../lib/validation";

vi.mock("../../lib/db", async () => {
  const actual = await vi.importActual<typeof import("../../lib/db")>(
    "../../lib/db",
  );

  return {
    ...actual,
    getSql: vi.fn(),
  };
});

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

function qualityError(
  reason: ConstructorParameters<typeof FactReconstructQualityError>[0]["reason"],
) {
  return new FactReconstructQualityError({
    naturalness: {
      draftAiLikePercent: 88,
      rewriteAiLikePercent: 91,
      changePoints: 3,
      label: "still_high",
    },
    rejectedCandidates: 2,
    repairCandidatesTried: 1,
    candidateSignals: [
      {
        stage: "repair",
        aiLikePercent: 91,
        status: "fail_worse",
        rejected: true,
        reason: "Rejected during internal quality review.",
      },
    ],
    diagnosisTags: ["support_template_voice"],
    rewritePlanSummary: "Repair support reply structure.",
    reason,
  });
}

afterEach(() => {
  vi.restoreAllMocks();
});

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

  it("stores the specific quality failure reason on request-level telemetry", () => {
    const collector = createRewriteTelemetryCollector({
      requestId: "req_quality_reason",
      startedAt: new Date("2026-05-20T01:00:00.000Z"),
    });

    const log = collector.finish({
      input,
      status: "quality_failed",
      qualityError: qualityError("fact_check_failed"),
      strategyVersion: "adaptive_rewrite_orchestrator",
      finishedAt: new Date("2026-05-20T01:00:02.000Z"),
    });

    expect(log.status).toBe("quality_failed");
    expect(log.errorCode).toBe("fact_check_failed");
  });

  it("keeps provider-unavailable details on provider calls while request reason stays signal_unavailable", () => {
    const collector = createRewriteTelemetryCollector({
      requestId: "req_signal_unavailable",
      startedAt: new Date("2026-05-20T01:00:00.000Z"),
    });
    collector.recordProviderCall({
      provider: "sapling",
      role: "draft_signal",
      characters: 1200,
      estimatedCostUsd: 0.006,
      success: false,
      errorCode: "timeout_or_network",
    });

    const log = collector.finish({
      input,
      status: "quality_failed",
      qualityError: qualityError("signal_unavailable"),
      strategyVersion: "adaptive_rewrite_orchestrator",
      finishedAt: new Date("2026-05-20T01:00:02.000Z"),
    });

    expect(log.errorCode).toBe("signal_unavailable");
    expect(log.providerCalls[0]).toMatchObject({
      provider: "sapling",
      role: "draft_signal",
      success: false,
      errorCode: "timeout_or_network",
    });
  });

  it("normalizes generic server exception names to server_failed", () => {
    const collector = createRewriteTelemetryCollector({
      requestId: "req_server_failed",
      startedAt: new Date("2026-05-20T01:00:00.000Z"),
    });

    const log = collector.finish({
      input,
      status: "server_failed",
      errorCode: "TypeError",
      strategyVersion: "adaptive_rewrite_orchestrator",
      finishedAt: new Date("2026-05-20T01:00:02.000Z"),
    });

    expect(log.status).toBe("server_failed");
    expect(log.errorCode).toBe("server_failed");
  });

  it("persists the request log and provider calls in one transaction", async () => {
    const collector = createRewriteTelemetryCollector({
      requestId: "req_transaction",
      startedAt: new Date("2026-05-20T01:00:00.000Z"),
    });
    collector.recordProviderCall({
      provider: "openai",
      role: "mid_writer",
      model: "gpt-5.4-mini",
      inputTokens: 800,
      outputTokens: 200,
      estimatedCostUsd: 0.0015,
      success: true,
    });
    collector.recordProviderCall({
      provider: "sapling",
      role: "final_signal",
      characters: 1200,
      estimatedCostUsd: 0.006,
      success: true,
    });
    const log = collector.finish({
      input,
      status: "success",
      response,
      strategyVersion: "adaptive_rewrite_orchestrator",
    });
    const statements: string[] = [];
    const directSql = Object.assign(
      vi.fn(() => {
        throw new Error("cost log persistence must use a transaction");
      }),
      {
        transaction: vi.fn((callback: (txn: typeof directSql) => unknown[]) => {
          const tx = vi.fn((strings: TemplateStringsArray) => {
            statements.push(strings.join("?"));
            return Promise.resolve([]);
          });
          return Promise.all(callback(tx as typeof directSql));
        }),
      },
    );
    vi.mocked(getSql).mockReturnValue(
      directSql as unknown as ReturnType<typeof getSql>,
    );

    await persistRewriteCostLog(log);

    expect(directSql.transaction).toHaveBeenCalledTimes(1);
    expect(statements.some((sql) => sql.includes('INSERT INTO "RewriteCostLog"')))
      .toBe(true);
    expect(
      statements.some((sql) => sql.includes('"id" = EXCLUDED."id"')),
    ).toBe(true);
    expect(statements.some((sql) => sql.includes('DELETE FROM "RewriteProviderCall"')))
      .toBe(true);
    expect(
      statements.filter((sql) => sql.includes('INSERT INTO "RewriteProviderCall"')),
    ).toHaveLength(2);
  });
});
