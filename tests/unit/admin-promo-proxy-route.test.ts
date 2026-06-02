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

import { GET, POST, dynamic } from "../../app/api/admin/promo-codes/route";
import {
  GET as GET_DETAIL,
  PATCH,
} from "../../app/api/admin/promo-codes/[promoCodeId]/route";
import { POST as DISABLE } from "../../app/api/admin/promo-codes/[promoCodeId]/disable/route";
import { POST as ENABLE } from "../../app/api/admin/promo-codes/[promoCodeId]/enable/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";
import { getCurrentAccessToken } from "../../lib/entra-auth";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const accessValue = "unit-auth-value";
const authScheme = "Bearer";
const promoCodeId = "11111111-1111-4111-8111-111111111111";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function request(path: string, method: string, body?: unknown, origin = appUrl) {
  return new Request(`${appUrl}${path}`, {
    body: body === undefined ? undefined : JSON.stringify(body),
    headers: {
      "content-type": "application/json",
      origin,
    },
    method,
  });
}

function params() {
  return {
    params: Promise.resolve({ promoCodeId }),
  };
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  vi.mocked(getAzureApiBaseUrl).mockReset().mockReturnValue(azureUrl);
  vi.mocked(getCurrentAccessToken).mockReset().mockResolvedValue(accessValue);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("/api/admin/promo-codes proxy routes", () => {
  it("is dynamic", () => {
    expect(dynamic).toBe("force-dynamic");
  });

  it("forwards list requests with the current bearer value", async () => {
    const payload = { promoCodes: [] };
    fetchMock().mockResolvedValueOnce(Response.json(payload, { status: 200 }));

    const response = await GET();

    await expect(response.json()).resolves.toEqual(payload);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/admin/promo-codes`, {
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
      },
    });
  });

  it("requires auth before forwarding list requests", async () => {
    vi.mocked(getCurrentAccessToken).mockResolvedValueOnce(null);

    const response = await GET();

    await expect(response.json()).resolves.toEqual({
      error: "Authentication required.",
    });
    expect(response.status).toBe(401);
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("forwards create requests to the admin endpoint", async () => {
    const createPayload = {
      code: "SPRING-2026",
      creditsGranted: 3,
      grantTtlDays: 90,
      maxRedemptionsGlobal: 100,
      maxRedemptionsPerUser: 1,
      validFrom: "2026-06-01T00:00:00.000Z",
      validUntil: "2026-06-30T00:00:00.000Z",
    };
    const azureBody = { code: "SPRING2026", id: promoCodeId };
    fetchMock().mockResolvedValueOnce(Response.json(azureBody, { status: 200 }));

    const response = await POST(
      request("/api/admin/promo-codes", "POST", createPayload),
    );

    await expect(response.json()).resolves.toEqual(azureBody);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/admin/promo-codes`, {
      body: JSON.stringify(createPayload),
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
        "Content-Type": "application/json",
      },
      method: "POST",
    });
  });

  it("rejects cross-origin create before auth or backend calls", async () => {
    const response = await POST(
      request(
        "/api/admin/promo-codes",
        "POST",
        { code: "SPRING-2026" },
        "https://attacker.example.test",
      ),
    );

    expect(response.status).toBe(403);
    expect(getCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("sanitizes successful detail responses before sending them to the browser", async () => {
    fetchMock().mockResolvedValueOnce(Response.json({
      promoCode: {
        code: "SPRING2026",
        createdAt: "2026-06-01T00:00:00.000Z",
        creditsGranted: 3,
        displayCode: "SPRING-2026",
        grantTtlDays: 90,
        id: promoCodeId,
        isActive: true,
        kind: "TrialCredits",
        maxRedemptionsGlobal: 100,
        maxRedemptionsPerUser: 1,
        redemptionCount: 2,
        status: "active",
        updatedAt: "2026-06-01T00:00:00.000Z",
        validFrom: "2026-06-01T00:00:00.000Z",
        validUntil: "2026-06-30T00:00:00.000Z",
      },
      stats: {
        activationRate: 0.5,
        dailyCurve: [{ date: "2026-06-02", redemptions: 2 }],
        distinctUsers: 2,
        ipHashClusters: [
          {
            distinctUsers: 2,
            firstRedeemedAt: "2026-06-02T00:00:00.000Z",
            ipHash: "hash_cluster_1",
            lastRedeemedAt: "2026-06-03T00:00:00.000Z",
            rawIp: "203.0.113.55",
            redemptions: 2,
          },
        ],
        rawIp: "203.0.113.55",
        totalRedemptions: 2,
      },
    }, { status: 200 }));

    const response = await GET_DETAIL(
      request(`/api/admin/promo-codes/${promoCodeId}`, "GET"),
      params(),
    );
    const payload = await response.json();

    expect(response.status).toBe(200);
    expect(JSON.stringify(payload)).not.toContain("203.0.113.55");
    expect(payload.stats.ipHashClusters).toEqual([
      {
        distinctUsers: 2,
        firstRedeemedAt: "2026-06-02T00:00:00.000Z",
        ipHash: "hash_cluster_1",
        lastRedeemedAt: "2026-06-03T00:00:00.000Z",
        redemptions: 2,
      },
    ]);
  });

  it("forwards patch, disable, and enable mutations to the matching admin endpoints", async () => {
    const updatedBody = { id: promoCodeId, isActive: true };
    fetchMock()
      .mockResolvedValueOnce(Response.json(updatedBody, { status: 200 }))
      .mockResolvedValueOnce(Response.json({ ...updatedBody, isActive: false }, { status: 200 }))
      .mockResolvedValueOnce(Response.json(updatedBody, { status: 200 }));

    await PATCH(
      request(`/api/admin/promo-codes/${promoCodeId}`, "PATCH", {
        maxRedemptionsGlobal: 250,
      }),
      params(),
    );
    await DISABLE(
      request(`/api/admin/promo-codes/${promoCodeId}/disable`, "POST"),
      params(),
    );
    await ENABLE(
      request(`/api/admin/promo-codes/${promoCodeId}/enable`, "POST"),
      params(),
    );

    expect(fetchMock()).toHaveBeenNthCalledWith(
      1,
      `${azureUrl}/api/admin/promo-codes/${promoCodeId}`,
      expect.objectContaining({
        method: "PATCH",
      }),
    );
    expect(fetchMock()).toHaveBeenNthCalledWith(
      2,
      `${azureUrl}/api/admin/promo-codes/${promoCodeId}/disable`,
      expect.objectContaining({
        method: "POST",
      }),
    );
    expect(fetchMock()).toHaveBeenNthCalledWith(
      3,
      `${azureUrl}/api/admin/promo-codes/${promoCodeId}/enable`,
      expect.objectContaining({
        method: "POST",
      }),
    );
  });
});
