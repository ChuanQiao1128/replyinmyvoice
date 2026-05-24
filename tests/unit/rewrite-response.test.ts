import { describe, expect, it } from "vitest";

import {
  getRewriteAttemptId,
  isRewritePendingPayload,
  normalizeRewriteResponse,
} from "../../lib/rewrite-response";

describe("rewrite response payload normalization", () => {
  it("does not treat a pending rewrite as a successful result", () => {
    const payload = {
      code: "rewrite_pending",
      error: "Rewrite is still processing. Try again in a moment.",
    };

    expect(isRewritePendingPayload(payload)).toBe(true);
    expect(getRewriteAttemptId({ ...payload, attemptId: "attempt-123" })).toBe(
      "attempt-123",
    );
    expect(normalizeRewriteResponse(payload)).toBeNull();
  });

  it("normalizes older successful payloads that omit optional UI fields", () => {
    expect(normalizeRewriteResponse({ rewrittenText: "Done" })).toEqual({
      rewrittenText: "Done",
      changeSummary: [],
      riskNotes: [],
      naturalness: {
        draftAiLikePercent: null,
        rewriteAiLikePercent: null,
        changePoints: null,
        label: "unavailable",
      },
      optimization: {
        internalStrategiesTried: 1,
        userUsageCharged: 1,
      },
    });
  });
});
