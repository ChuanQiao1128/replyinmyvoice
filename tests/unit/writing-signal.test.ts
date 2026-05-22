import { afterEach, describe, expect, it, vi } from "vitest";

import {
  getSignalLabel,
  measureWritingSignal,
  scoreToPercent,
} from "../../lib/writing-signal";

afterEach(() => {
  vi.restoreAllMocks();
  delete process.env.WRITING_SIGNAL_PROVIDER;
  delete process.env.SAPLING_API_KEY;
  delete process.env.WRITING_SIGNAL_TIMEOUT_SEC;
  delete process.env.WRITING_SIGNAL_RETRY_COUNT;
});

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

describe("measureWritingSignal", () => {
  it("requests and parses Sapling sentence scores without enabling score_string", async () => {
    process.env.WRITING_SIGNAL_PROVIDER = "sapling";
    process.env.SAPLING_API_KEY = "test-sapling-key";

    const fetchMock = vi.fn(async () =>
      Response.json({
        score: 0.73,
        sentence_scores: [
          { sentence: "This sounds polished.", score: 0.91 },
          { sentence: "Jordan can come by Tuesday.", score: 0.12 },
        ],
        tokens: ["This", " sounds"],
        token_probs: [0.9, 0.88],
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const result = await measureWritingSignal("This sounds polished.");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const requestInit = (fetchMock.mock.calls[0] as unknown[])[1] as RequestInit;
    expect(JSON.parse(String(requestInit.body))).toMatchObject({
      key: "test-sapling-key",
      text: "This sounds polished.",
      sent_scores: true,
      score_string: false,
    });
    expect(result.aiLikePercent).toBe(73);
    expect(result.sentenceScores).toEqual([
      { sentence: "This sounds polished.", aiLikePercent: 91 },
      { sentence: "Jordan can come by Tuesday.", aiLikePercent: 12 },
    ]);
  });

  it("returns Sapling call telemetry metadata with the measured signal", async () => {
    process.env.WRITING_SIGNAL_PROVIDER = "sapling";
    process.env.SAPLING_API_KEY = "test-sapling-key";

    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          score: 0.28,
          sentence_scores: [{ sentence: "Plain reply.", score: 0.2 }],
        }),
      ),
    );

    const result = await measureWritingSignal("Plain reply.", {
      role: "final_signal",
    });

    expect(result.aiLikePercent).toBe(28);
    expect(result.chars).toBe("Plain reply.".length);
    expect(result.callCount).toBe(1);
    expect(result.latencyMs).toEqual(expect.any(Number));
  });

  it("can calibrate high overall scores when all sentence scores are low", async () => {
    process.env.WRITING_SIGNAL_PROVIDER = "sapling";
    process.env.SAPLING_API_KEY = "test-sapling-key";

    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          score: 1,
          sentence_scores: [
            { sentence: "First sentence.", score: 0 },
            { sentence: "Second sentence.", score: 0.08 },
            { sentence: "Third sentence.", score: 0 },
          ],
        }),
      ),
    );

    const raw = await measureWritingSignal("First sentence. Second sentence. Third sentence.");
    const calibrated = await measureWritingSignal(
      "First sentence. Second sentence. Third sentence.",
      { calibrateSentenceScores: true },
    );

    expect(raw.aiLikePercent).toBe(100);
    expect(calibrated.aiLikePercent).toBe(3);
    expect(calibrated.rawAiLikePercent).toBe(100);
  });

  it("calibrates short rewrites when provider sentence scores are consistently low", async () => {
    process.env.WRITING_SIGNAL_PROVIDER = "sapling";
    process.env.SAPLING_API_KEY = "test-sapling-key";

    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          score: 1,
          sentence_scores: [{ sentence: "Short factual rewrite.", score: 0 }],
        }),
      ),
    );

    const result = await measureWritingSignal("Short factual rewrite.", {
      calibrateSentenceScores: true,
    });

    expect(result.aiLikePercent).toBe(0);
    expect(result.rawAiLikePercent).toBe(100);
  });
});
