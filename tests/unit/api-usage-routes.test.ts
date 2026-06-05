import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { azureApiMock, entraAuthMock } = vi.hoisted(() => ({
  azureApiMock: {
    getAzureApiBaseUrl: vi.fn(),
  },
  entraAuthMock: {
    getCurrentAccessToken: vi.fn(),
  },
}));

vi.mock("../../lib/azure-api", () => azureApiMock);
vi.mock("../../lib/entra-auth", () => entraAuthMock);

import {
  GET as recentGet,
  dynamic as recentDynamic,
} from "../../app/api/me/api-usage/recent/route";
import {
  GET as seriesGet,
  dynamic as seriesDynamic,
} from "../../app/api/me/api-usage/series/route";
import {
  GET as summaryGet,
  dynamic as summaryDynamic,
} from "../../app/api/me/api-usage/summary/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";
import { getCurrentAccessToken } from "../../lib/entra-auth";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const accessValue = "unit-auth-value";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function request(path: string, origin = appUrl) {
  return new Request(`${appUrl}${path}`, {
    headers: {
      origin,
    },
    method: "GET",
  });
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  vi.mocked(getAzureApiBaseUrl).mockReset().mockReturnValue(azureUrl);
  vi.mocked(getCurrentAccessToken).mockReset().mockResolvedValue(accessValue);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("/api/me/api-usage proxy routes", () => {
  it("keeps every usage route dynamic", () => {
    expect(summaryDynamic).toBe("force-dynamic");
    expect(seriesDynamic).toBe("force-dynamic");
    expect(recentDynamic).toBe("force-dynamic");
  });

  it("forwards summary requests with the current bearer value", async () => {
    const payload = {
      last30dCalls: 9,
      monthToDate: { calls: 5, failed: 1, succeeded: 4 },
      quota: 90,
      remaining: 81,
      today: { calls: 2, failed: 0, succeeded: 2 },
      used: 9,
      yesterday: { calls: 1, failed: 1, succeeded: 0 },
    };
    fetchMock().mockResolvedValueOnce(Response.json(payload, { status: 200 }));

    const response = await summaryGet(request("/api/me/api-usage/summary"));

    await expect(response.json()).resolves.toEqual(payload);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/me/api-usage/summary`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessValue}`,
      },
    });
  });

  it("forwards series and recent query strings", async () => {
    fetchMock()
      .mockResolvedValueOnce(Response.json([{ date: "2026-06-05", calls: 1 }], { status: 200 }))
      .mockResolvedValueOnce(Response.json([{ endpoint: "/api/v1/rewrite" }], { status: 200 }));

    const seriesResponse = await seriesGet(request("/api/me/api-usage/series?days=7"));
    const recentResponse = await recentGet(request("/api/me/api-usage/recent?limit=25"));

    expect(seriesResponse.status).toBe(200);
    expect(recentResponse.status).toBe(200);
    expect(fetchMock()).toHaveBeenNthCalledWith(1, `${azureUrl}/api/me/api-usage/series?days=7`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessValue}`,
      },
    });
    expect(fetchMock()).toHaveBeenNthCalledWith(2, `${azureUrl}/api/me/api-usage/recent?limit=25`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessValue}`,
      },
    });
  });

  it("rejects cross-origin requests before auth or forwarding", async () => {
    const response = await summaryGet(
      request("/api/me/api-usage/summary", "https://attacker.example.test"),
    );

    expect(response.status).toBe(403);
    expect(getCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("returns 401 when no access token is available", async () => {
    vi.mocked(getCurrentAccessToken).mockResolvedValueOnce(null);

    const response = await recentGet(request("/api/me/api-usage/recent"));

    await expect(response.json()).resolves.toEqual({
      error: "Authentication required.",
    });
    expect(response.status).toBe(401);
    expect(fetchMock()).not.toHaveBeenCalled();
  });
});
