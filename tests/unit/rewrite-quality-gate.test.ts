import { describe, expect, it } from "vitest";

import {
  evaluateSignalQuality,
  shouldRejectCandidate,
  shouldRepairCandidate,
} from "../../lib/rewrite-quality-gate";

describe("evaluateSignalQuality", () => {
  it("passes a candidate below the AI-like signal threshold", () => {
    const result = evaluateSignalQuality({
      draftPercent: 82,
      rewritePercent: 42,
    });

    expect(result.status).toBe("pass_below_threshold");
    expect(shouldRejectCandidate(result)).toBe(false);
    expect(shouldRepairCandidate(result)).toBe(false);
  });

  it("passes an already-low candidate when it does not worsen the draft", () => {
    const result = evaluateSignalQuality({
      draftPercent: 0,
      rewritePercent: 0,
    });

    expect(result.status).toBe("pass_below_threshold");
    expect(result.changePoints).toBe(0);
    expect(shouldRejectCandidate(result)).toBe(false);
  });

  it("passes a candidate with at least a 30 point reduction", () => {
    const result = evaluateSignalQuality({
      draftPercent: 95,
      rewritePercent: 60,
    });

    expect(result.status).toBe("pass_reduction");
    expect(shouldRejectCandidate(result)).toBe(false);
  });

  it("rejects a candidate that is worse than the draft", () => {
    const result = evaluateSignalQuality({
      draftPercent: 89,
      rewritePercent: 99,
    });

    expect(result.status).toBe("fail_worse");
    expect(result.changePoints).toBe(10);
    expect(shouldRejectCandidate(result)).toBe(true);
    expect(shouldRepairCandidate(result)).toBe(true);
  });

  it("rejects a high candidate with insufficient reduction", () => {
    const result = evaluateSignalQuality({
      draftPercent: 89,
      rewritePercent: 65,
    });

    expect(result.status).toBe("fail_insufficient_reduction");
    expect(result.changePoints).toBe(-24);
    expect(shouldRejectCandidate(result)).toBe(true);
    expect(shouldRepairCandidate(result)).toBe(true);
  });

  it("keeps unavailable signal neutral", () => {
    const result = evaluateSignalQuality({
      draftPercent: null,
      rewritePercent: null,
    });

    expect(result.status).toBe("signal_unavailable");
    expect(result.changePoints).toBe(null);
    expect(shouldRejectCandidate(result)).toBe(false);
    expect(shouldRepairCandidate(result)).toBe(false);
  });
});
