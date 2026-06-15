import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createClient, RimvApiError, type UsageResponse } from "replyinmyvoice-api";

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
    const rewriteId = "8f14e45f-ea4d-4f62-9d22-4f134b7f4c40";
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: rewriteId, status: "processing" }, 202))
      .mockResolvedValueOnce(jsonResponse({ id: rewriteId, status: "processing" }))
      .mockResolvedValueOnce(
        jsonResponse({
          id: rewriteId,
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
    expect(fetchMock()).toHaveBeenNthCalledWith(2, `${baseUrl}/api/v1/rewrite/${rewriteId}`, {
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
      method: "GET",
    });
    expect(fetchMock()).toHaveBeenNthCalledWith(3, `${baseUrl}/api/v1/rewrite/${rewriteId}`, {
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
      method: "GET",
    });
  });

  it("sends an idempotency key on submit when provided", async () => {
    const idempotencyKey = "request-2026-06-06-001";
    fetchMock().mockResolvedValueOnce(
      jsonResponse({
        id: "8f14e45f-ea4d-4f62-9d22-4f134b7f4c40",
        status: "processing",
      }),
    );

    const client = createClient({ apiKey, baseUrl });

    await expect(client.submitRewrite(draft, { idempotencyKey })).resolves.toEqual({
      id: "8f14e45f-ea4d-4f62-9d22-4f134b7f4c40",
      status: "processing",
    });
    expect(fetchMock()).toHaveBeenCalledWith(`${baseUrl}/api/v1/rewrite`, {
      body: JSON.stringify({ draft }),
      headers: {
        Authorization: `Bearer ${apiKey}`,
        "Content-Type": "application/json",
        "Idempotency-Key": idempotencyKey,
      },
      method: "POST",
    });
  });

  it("throws RimvApiError when submit response is missing an id", async () => {
    fetchMock().mockResolvedValueOnce(jsonResponse({ status: "processing" }, 202));

    const client = createClient({ apiKey, baseUrl });

    await expectRimvApiError(client.rewrite(draft), {
      code: "invalid_response",
      message: "Rewrite submit response was missing required fields.",
      status: 200,
    });
    expect(fetchMock()).toHaveBeenCalledTimes(1);
  });

  it("waits for the poll interval before the first status request", async () => {
    vi.useFakeTimers();
    const rewriteId = "4d7707a4-9f0f-42d2-8f1f-f6d8d9a1b234";
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: rewriteId, status: "processing" }, 202))
      .mockResolvedValueOnce(
        jsonResponse({
          id: rewriteId,
          rewrittenText: "I can send a warmer update about the delayed shipment.",
          signal: { draft: 72, rewrite: 24 },
          status: "succeeded",
        }),
      );

    const client = createClient({ apiKey, baseUrl });
    const resultPromise = client.rewrite(draft, {
      pollIntervalMs: 250,
      timeoutMs: 2_000,
    });

    await vi.advanceTimersByTimeAsync(249);
    expect(fetchMock()).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1);

    await expect(resultPromise).resolves.toEqual({
      rewrittenText: "I can send a warmer update about the delayed shipment.",
      signal: { draft: 72, rewrite: 24 },
    });
    expect(fetchMock()).toHaveBeenCalledTimes(2);
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
    const rewriteId = "b6acbb70-0585-4a31-8c19-5875a71bb295";
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: rewriteId, status: "processing" }, 202))
      .mockResolvedValueOnce(
        jsonResponse({
          error: {
            code: "quality_unavailable",
            message: "The rewrite could not be completed.",
          },
          id: rewriteId,
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
    const rewriteId = "55f6778a-b430-4863-9841-9ec07a4d9ebe";
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: rewriteId, status: "processing" }, 202))
      .mockImplementation(() =>
        Promise.resolve(jsonResponse({ id: rewriteId, status: "processing" })),
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
    const usage: UsageResponse = {
      periodEnd: null,
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
