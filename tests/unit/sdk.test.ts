import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createClient, RimvApiError } from "../../packages/sdk/src/index";

const apiKey = "rmv_live_unit_test_key";
const baseUrl = "https://api.example.test";
const draft = "Please send a warmer update about the delayed shipment.";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function jsonResponse(body: unknown, status = 200) {
  return Response.json(body, { status });
}

async function expectRimvApiError(
  promise: Promise<unknown>,
  expected: { code: string; message: string; status: number },
) {
  await expect(promise).rejects.toBeInstanceOf(RimvApiError);
  await expect(promise).rejects.toMatchObject(expected);
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Reply In My Voice TypeScript SDK", () => {
  it("submits a rewrite and polls until the request succeeds", async () => {
    vi.useFakeTimers();
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: "rewrite_123", status: "processing" }, 202))
      .mockResolvedValueOnce(jsonResponse({ id: "rewrite_123", status: "processing" }))
      .mockResolvedValueOnce(
        jsonResponse({
          id: "rewrite_123",
          rewrittenText: "I can send a warmer update about the delayed shipment.",
          signal: { draft: 72, rewrite: 24 },
          status: "succeeded",
        }),
      );

    const client = createClient({ apiKey, baseUrl: `${baseUrl}/` });
    const resultPromise = client.rewrite(draft, {
      pollIntervalMs: 250,
      timeoutMs: 2_000,
    });

    await vi.advanceTimersByTimeAsync(250);

    await expect(resultPromise).resolves.toEqual({
      rewrittenText: "I can send a warmer update about the delayed shipment.",
      signal: { draft: 72, rewrite: 24 },
    });
    expect(fetchMock()).toHaveBeenCalledTimes(3);
    expect(fetchMock()).toHaveBeenNthCalledWith(1, `${baseUrl}/api/v1/rewrite`, {
      body: JSON.stringify({ draft }),
      headers: {
        Authorization: `Bearer ${apiKey}`,
        "Content-Type": "application/json",
      },
      method: "POST",
    });
    expect(fetchMock()).toHaveBeenNthCalledWith(2, `${baseUrl}/api/v1/rewrite/rewrite_123`, {
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
      method: "GET",
    });
    expect(fetchMock()).toHaveBeenNthCalledWith(3, `${baseUrl}/api/v1/rewrite/rewrite_123`, {
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
      method: "GET",
    });
  });

  it.each([
    [401, "invalid_key", "A valid API key is required."],
    [402, "quota_exhausted", "Your account has no remaining rewrites."],
    [429, "rate_limited", "Please wait before sending another request."],
  ])("maps %i API errors to RimvApiError", async (status, code, message) => {
    fetchMock().mockResolvedValueOnce(jsonResponse({ error: { code, message } }, status));

    const client = createClient({ apiKey, baseUrl });

    await expectRimvApiError(client.submitRewrite(draft), {
      code,
      message,
      status,
    });
  });

  it("throws RimvApiError when the rewrite job reports failure", async () => {
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: "rewrite_failed", status: "processing" }, 202))
      .mockResolvedValueOnce(
        jsonResponse({
          error: {
            code: "quality_unavailable",
            message: "The rewrite could not be completed.",
          },
          id: "rewrite_failed",
          status: "failed",
        }),
      );

    const client = createClient({ apiKey, baseUrl });

    await expectRimvApiError(client.rewrite(draft, { pollIntervalMs: 1, timeoutMs: 100 }), {
      code: "quality_unavailable",
      message: "The rewrite could not be completed.",
      status: 200,
    });
  });

  it("throws RimvApiError when polling times out", async () => {
    vi.useFakeTimers();
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: "rewrite_timeout", status: "processing" }, 202))
      .mockImplementation(() =>
        Promise.resolve(jsonResponse({ id: "rewrite_timeout", status: "processing" })),
      );

    const client = createClient({ apiKey, baseUrl });
    const resultPromise = client.rewrite(draft, {
      pollIntervalMs: 100,
      timeoutMs: 250,
    });
    const errorAssertion = expectRimvApiError(resultPromise, {
      code: "timeout",
      message: "Rewrite request timed out.",
      status: 0,
    });

    await vi.advanceTimersByTimeAsync(500);

    await errorAssertion;
  });

  it("reads account usage with bearer authorization", async () => {
    const usage = {
      periodEnd: "2026-07-01T00:00:00Z",
      quota: 90,
      remaining: 78,
      scope: "paid",
      used: 12,
    };
    fetchMock().mockResolvedValueOnce(jsonResponse(usage));

    const client = createClient({ apiKey, baseUrl });

    await expect(client.getUsage()).resolves.toEqual(usage);
    expect(fetchMock()).toHaveBeenCalledWith(`${baseUrl}/api/v1/usage`, {
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
      method: "GET",
    });
  });
});
