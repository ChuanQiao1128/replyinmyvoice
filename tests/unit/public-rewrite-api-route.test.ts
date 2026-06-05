import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { azureApiMock } = vi.hoisted(() => ({
  azureApiMock: {
    getAzureApiBaseUrl: vi.fn(),
  },
}));

vi.mock("../../lib/azure-api", () => azureApiMock);

import { POST, dynamic as submitDynamic } from "../../app/api/v1/rewrite/route";
import {
  GET,
  dynamic as resultDynamic,
} from "../../app/api/v1/rewrite/[id]/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const rewriteId = "11111111-1111-4111-8111-111111111111";
const authorization = "Bearer rmv_live_unit_token";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function fetchCall() {
  expect(fetchMock()).toHaveBeenCalledTimes(1);
  const [url, init] = fetchMock().mock.calls[0];

  return {
    init: init as RequestInit,
    url: String(url),
  };
}

function request(path: string, init: RequestInit) {
  return new Request(`${appUrl}${path}`, init);
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  vi.mocked(getAzureApiBaseUrl).mockReset().mockReturnValue(azureUrl);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("/api/v1/rewrite Next proxy routes", () => {
  it("keeps both route handlers dynamic", () => {
    expect(submitDynamic).toBe("force-dynamic");
    expect(resultDynamic).toBe("force-dynamic");
  });

  it("surfaces backend auth rejection for submit requests without credentials", async () => {
    const backendBody = {
      error: {
        code: "invalid_key",
        message: "A valid API key is required.",
      },
    };
    const body = JSON.stringify({ draft: "Please send the update tomorrow." });
    fetchMock().mockResolvedValueOnce(Response.json(backendBody, { status: 401 }));

    const response = await POST(
      request("/api/v1/rewrite", {
        body,
        headers: {
          "content-type": "application/json",
          origin: "https://client.example.test",
        },
        method: "POST",
      }),
    );

    await expect(response.json()).resolves.toEqual(backendBody);
    expect(response.status).toBe(401);

    const { init, url } = fetchCall();
    const headers = new Headers(init.headers);
    expect(url).toBe(`${azureUrl}/api/v1/rewrite`);
    expect(init).toMatchObject({
      body,
      cache: "no-store",
      method: "POST",
    });
    expect(headers.get("Authorization")).toBeNull();
    expect(headers.get("Content-Type")).toBe("application/json");
  });

  it("forwards submit requests with the caller bearer value", async () => {
    const backendBody = { id: rewriteId, status: "processing" };
    const body = JSON.stringify({ draft: "Please send the update tomorrow." });
    fetchMock().mockResolvedValueOnce(Response.json(backendBody, { status: 202 }));

    const response = await POST(
      request("/api/v1/rewrite", {
        body,
        headers: {
          authorization,
          "content-type": "application/json",
          origin: "https://client.example.test",
        },
        method: "POST",
      }),
    );

    await expect(response.json()).resolves.toEqual(backendBody);
    expect(response.status).toBe(202);

    const { init, url } = fetchCall();
    const headers = new Headers(init.headers);
    expect(url).toBe(`${azureUrl}/api/v1/rewrite`);
    expect(init).toMatchObject({
      body,
      cache: "no-store",
      method: "POST",
    });
    expect(headers.get("Authorization")).toBe(authorization);
    expect(headers.get("Content-Type")).toBe("application/json");
  });

  it("surfaces backend auth rejection for result requests without credentials", async () => {
    const backendBody = {
      error: {
        code: "invalid_key",
        message: "A valid API key is required.",
      },
    };
    fetchMock().mockResolvedValueOnce(Response.json(backendBody, { status: 401 }));

    const response = await GET(
      request(`/api/v1/rewrite/${rewriteId}`, {
        headers: {
          origin: "https://client.example.test",
        },
        method: "GET",
      }),
      { params: Promise.resolve({ id: rewriteId }) },
    );

    await expect(response.json()).resolves.toEqual(backendBody);
    expect(response.status).toBe(401);

    const { init, url } = fetchCall();
    const headers = new Headers(init.headers);
    expect(url).toBe(`${azureUrl}/api/v1/rewrite/${rewriteId}`);
    expect(init.cache).toBe("no-store");
    expect(headers.get("Authorization")).toBeNull();
  });

  it("forwards result requests with the caller bearer value", async () => {
    const backendBody = {
      id: rewriteId,
      rewrittenText: "I can send the update tomorrow.",
      status: "succeeded",
    };
    fetchMock().mockResolvedValueOnce(Response.json(backendBody, { status: 200 }));

    const response = await GET(
      request(`/api/v1/rewrite/${rewriteId}`, {
        headers: {
          authorization,
          origin: "https://client.example.test",
        },
        method: "GET",
      }),
      { params: Promise.resolve({ id: rewriteId }) },
    );

    await expect(response.json()).resolves.toEqual(backendBody);
    expect(response.status).toBe(200);

    const { init, url } = fetchCall();
    const headers = new Headers(init.headers);
    expect(url).toBe(`${azureUrl}/api/v1/rewrite/${rewriteId}`);
    expect(init.cache).toBe("no-store");
    expect(headers.get("Authorization")).toBe(authorization);
  });
});
