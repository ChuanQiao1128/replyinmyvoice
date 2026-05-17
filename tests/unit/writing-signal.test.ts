import { describe, expect, it } from "vitest";

import {
  getSignalLabel,
  scoreToPercent,
} from "../../lib/writing-signal";

describe("scoreToPercent", () => {
  it("converts a 0 to 1 score to a percentage", () => {
    expect(scoreToPercent(0.327)).toBe(33);
  });

  it("clamps out-of-range values", () => {
    expect(scoreToPercent(-0.1)).toBe(0);
    expect(scoreToPercent(1.2)).toBe(100);
  });
});

describe("getSignalLabel", () => {
  it("returns lower when the rewrite materially improves the signal", () => {
    expect(getSignalLabel(78, 32)).toBe("lower");
  });

  it("returns low_signal when both scores are low but the rewrite is higher", () => {
    expect(getSignalLabel(0, 5)).toBe("low_signal");
  });

  it("returns lower when the rewrite is still high but lower than the draft", () => {
    expect(getSignalLabel(78, 72)).toBe("lower");
  });

  it("returns still_high when the rewrite does not improve and remains high", () => {
    expect(getSignalLabel(45, 72)).toBe("still_high");
  });

  it("returns unavailable when either value is missing", () => {
    expect(getSignalLabel(null, 32)).toBe("unavailable");
  });
});
