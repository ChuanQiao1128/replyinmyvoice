import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { azureApiMock } = vi.hoisted(() => ({
  azureApiMock: {
    getAzureApiBaseUrl: vi.fn(),
  },
}));

vi.mock("../../lib/azure-api", () => azureApiMock);

import { GET, dynamic } from "../../app/api/v1/usage/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const apiAuthorization = "Bearer rmv_live_unit_test_key";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function request(origin = "https://client.example.test") {
  return new Request(`${appUrl}/api/v1/usage`, {
    headers: {
      authorization: apiAuthorization,
      origin,
    },
    method: "GET",
  });
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  vi.mocked(getAzureApiBaseUrl).mockReset().mockReturnValue(azureUrl);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("/api/v1/usage proxy route", () => {
  it("is dynamic", () => {
    expect(dynamic).toBe("force-dynamic");
  });

  it("forwards caller authorization to the Azure usage endpoint", async () => {
    const payload = {
      periodEnd: "2026-07-01T00:00:00Z",
      periodKey: "paid:sub_unit:2026-07-01T00:00:00.0000000+00:00",
      quota: 90,
      remaining: 78,
      scope: "paid",
      used: 12,
    };
    fetchMock().mockResolvedValueOnce(
      Response.json(payload, {
        headers: {
          "X-RateLimit-Limit": "60",
          "X-RateLimit-Remaining": "57",
          "X-RateLimit-Reset": "1812345678",
        },
        status: 200,
      }),
    );

    const response = await GET(request());

    await expect(response.json()).resolves.toEqual(payload);
    expect(response.status).toBe(200);
    expect(response.headers.get("X-RateLimit-Limit")).toBe("60");
    expect(response.headers.get("X-RateLimit-Remaining")).toBe("57");
    expect(response.headers.get("X-RateLimit-Reset")).toBe("1812345678");
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/v1/usage`, {
      cache: "no-store",
      headers: {
        Authorization: apiAuthorization,
      },
    });
  });

  it("does not require the caller to be same-origin", async () => {
    fetchMock().mockResolvedValueOnce(Response.json({ error: "invalid_key" }, { status: 401 }));

    const response = await GET(request("https://external-client.example.test"));

    expect(response.status).toBe(401);
    expect(fetchMock()).toHaveBeenCalledTimes(1);
  });
});
