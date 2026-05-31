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

import { POST as grantCredits } from "../../app/api/admin/users/[userId]/credits/route";
import { POST as issueRefund } from "../../app/api/admin/users/[userId]/refund/route";
import { POST as setSuspension } from "../../app/api/admin/users/[userId]/suspension/route";
import { GET as userDetail } from "../../app/api/admin/users/[userId]/route";
import { GET as listUsers } from "../../app/api/admin/users/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";
import { getCurrentAccessToken } from "../../lib/entra-auth";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const accessValue = "admin-access-token";
const userId = "6ee67a08-a662-4c2b-9225-dc4b18e3a73c";

function request(path: string, init: RequestInit = {}) {
  return new Request(`${appUrl}${path}`, init);
}

function context(id = userId) {
  return {
    params: Promise.resolve({ userId: id }),
  };
}

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  vi.mocked(getAzureApiBaseUrl).mockReset().mockReturnValue(azureUrl);
  vi.mocked(getCurrentAccessToken).mockReset().mockResolvedValue(accessValue);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("admin API route handlers", () => {
  it("rejects cross-origin admin list reads before auth or forwarding", async () => {
    const response = await listUsers(
      request("/api/admin/users", {
        headers: { origin: "https://attacker.example.test" },
        method: "GET",
      }),
    );

    expect(response.status).toBe(403);
    expect(getCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("forwards paginated admin user reads with the current bearer token", async () => {
    const payload = {
      page: 2,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      users: [],
    };
    fetchMock().mockResolvedValueOnce(Response.json(payload, { status: 200 }));

    const response = await listUsers(
      request("/api/admin/users?page=2&pageSize=10", {
        headers: { origin: appUrl },
        method: "GET",
      }),
    );

    await expect(response.json()).resolves.toEqual(payload);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(
      `${azureUrl}/api/admin/users?page=2&pageSize=10`,
      {
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${accessValue}`,
        },
      },
    );
  });

  it("forwards detail, credit, suspension, and refund calls to Azure admin endpoints", async () => {
    fetchMock()
      .mockResolvedValueOnce(Response.json({ id: userId }, { status: 200 }))
      .mockResolvedValueOnce(Response.json({ creditId: "credit-1" }, { status: 200 }))
      .mockResolvedValueOnce(Response.json({ suspended: true }, { status: 200 }))
      .mockResolvedValueOnce(Response.json({ refundId: "refund-1" }, { status: 200 }));

    await userDetail(
      request(`/api/admin/users/${userId}`, {
        headers: { origin: appUrl },
        method: "GET",
      }),
      context(),
    );

    await grantCredits(
      request(`/api/admin/users/${userId}/credits`, {
        body: JSON.stringify({ amount: 5, reason: "Launch test credit" }),
        headers: { "content-type": "application/json", origin: appUrl },
        method: "POST",
      }),
      context(),
    );

    await setSuspension(
      request(`/api/admin/users/${userId}/suspension`, {
        body: JSON.stringify({ suspended: true }),
        headers: { "content-type": "application/json", origin: appUrl },
        method: "POST",
      }),
      context(),
    );

    await issueRefund(
      request(`/api/admin/users/${userId}/refund`, {
        body: JSON.stringify({
          amount: 1200,
          currency: "nzd",
          paymentIntentId: "pi_test_admin_refund",
          reason: "Launch test refund",
        }),
        headers: { "content-type": "application/json", origin: appUrl },
        method: "POST",
      }),
      context(),
    );

    expect(fetchMock()).toHaveBeenNthCalledWith(
      1,
      `${azureUrl}/api/admin/users/${userId}`,
      {
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${accessValue}`,
        },
      },
    );
    expect(fetchMock()).toHaveBeenNthCalledWith(
      2,
      `${azureUrl}/api/admin/users/${userId}/credits`,
      {
        body: JSON.stringify({ amount: 5, reason: "Launch test credit" }),
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${accessValue}`,
          "Content-Type": "application/json",
        },
        method: "POST",
      },
    );
    expect(fetchMock()).toHaveBeenNthCalledWith(
      3,
      `${azureUrl}/api/admin/users/${userId}/suspension`,
      {
        body: JSON.stringify({ suspended: true }),
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${accessValue}`,
          "Content-Type": "application/json",
        },
        method: "POST",
      },
    );
    expect(fetchMock()).toHaveBeenNthCalledWith(
      4,
      `${azureUrl}/api/admin/users/${userId}/refund`,
      {
        body: JSON.stringify({
          amount: 1200,
          currency: "nzd",
          paymentIntentId: "pi_test_admin_refund",
          reason: "Launch test refund",
        }),
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${accessValue}`,
          "Content-Type": "application/json",
        },
        method: "POST",
      },
    );
  });
});
