import { describe, expect, it } from "vitest";

import {
  evaluateRewriteCanary,
  getRewriteCanaryConfig,
  selectRewriteCanaryAssignment,
  type RewriteCanarySignalSummary,
} from "../../lib/rewrite-pipeline/canary";

const now = new Date("2026-05-22T12:00:00.000Z");

function summary(
  strategyVersion: string,
  overrides: Partial<RewriteCanarySignalSummary> = {},
): RewriteCanarySignalSummary {
  return {
    strategyVersion,
    requestCount: 250,
    successCount: 250,
    measuredCount: 250,
    averageSignalDrop: 42,
    p10SignalDrop: 28,
    p50SignalDrop: 41,
    p90SignalDrop: 58,
    below50Rate: 0.74,
    firstSeenAt: new Date("2026-05-21T10:00:00.000Z"),
    lastSeenAt: new Date("2026-05-22T11:00:00.000Z"),
    ...overrides,
  };
}

describe("rewrite strategy canary", () => {
  it("parses the env feature flag with a 10 percent default", () => {
    const config = getRewriteCanaryConfig({
      REWRITE_STRATEGY_CANARY_ENABLED: "true",
      REWRITE_STRATEGY_CANARY_VERSION: "support-policy-v2",
    });

    expect(config.enabled).toBe(true);
    expect(config.initialPercent).toBe(10);
    expect(config.canaryStrategyVersion).toBe("support-policy-v2");
    expect(config.rampPercents).toEqual([10, 25, 50, 100]);
  });

  it("assigns a stable percentage of traffic to the canary version", () => {
    const config = getRewriteCanaryConfig({
      REWRITE_STRATEGY_CANARY_ENABLED: "true",
      REWRITE_STRATEGY_CANARY_VERSION: "support-policy-v2",
      REWRITE_STRATEGY_CANARY_PERCENT: "10",
    });
    const decision = evaluateRewriteCanary({
      config,
      control: null,
      canary: null,
      now,
    });

    const assignments = Array.from({ length: 1000 }, (_, index) =>
      selectRewriteCanaryAssignment({
        config,
        decision,
        stickinessKey: `user_${index}`,
      }),
    );
    const canaryCount = assignments.filter(
      (assignment) => assignment.bucket === "canary",
    ).length;

    expect(
      selectRewriteCanaryAssignment({
        config,
        decision,
        stickinessKey: "user_42",
      }),
    ).toEqual(
      selectRewriteCanaryAssignment({
        config,
        decision,
        stickinessKey: "user_42",
      }),
    );
    expect(canaryCount).toBeGreaterThanOrEqual(80);
    expect(canaryCount).toBeLessThanOrEqual(120);
  });

  it("pauses canary traffic when mature signal drop is worse", () => {
    const config = getRewriteCanaryConfig({
      REWRITE_STRATEGY_CANARY_ENABLED: "true",
      REWRITE_STRATEGY_CANARY_VERSION: "support-policy-v2",
    });

    const decision = evaluateRewriteCanary({
      config,
      control: summary("adaptive_rewrite_orchestrator", {
        averageSignalDrop: 44,
      }),
      canary: summary("support-policy-v2", {
        averageSignalDrop: 38,
      }),
      now,
    });

    expect(decision.state).toBe("paused");
    expect(decision.effectivePercent).toBe(0);
    expect(decision.reason).toContain("lower average signal drop");
  });

  it("ramps canary traffic after a mature better result", () => {
    const config = getRewriteCanaryConfig({
      REWRITE_STRATEGY_CANARY_ENABLED: "true",
      REWRITE_STRATEGY_CANARY_VERSION: "support-policy-v2",
    });

    const decision = evaluateRewriteCanary({
      config,
      control: summary("adaptive_rewrite_orchestrator", {
        averageSignalDrop: 41,
      }),
      canary: summary("support-policy-v2", {
        averageSignalDrop: 47,
        measuredCount: 210,
      }),
      now,
    });

    expect(decision.state).toBe("ramping");
    expect(decision.effectivePercent).toBe(25);
  });
});
