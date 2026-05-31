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

import { DELETE, GET as getAccount, dynamic } from "../../app/api/me/route";
import {
  GET as getPayments,
  dynamic as paymentsDynamic,
} from "../../app/api/me/payments/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";
import { getCurrentAccessToken } from "../../lib/entra-auth";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const accessValue = "unit-auth-value";
const authScheme = "Bearer";

function request(method: "DELETE" | "GET") {
  return new Request(`${appUrl}/api/me`, {
    headers: {
      origin: appUrl,
    },
    method,
  });
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

describe("/api/me route handler", () => {
  it("is dynamic", () => {
    expect(dynamic).toBe("force-dynamic");
  });

  it("forwards GET to Azure with the current auth value", async () => {
    const summary = {
      email: "casey@example.com",
      subscriptionStatus: "active",
      usage: {
        exhausted: false,
        periodEnd: "2026-06-30T00:00:00Z",
        periodKey: "2026-06",
        quota: 40,
        remaining: 31,
        reserved: 0,
        scope: "paid",
        used: 9,
      },
      userId: "user_1",
    };
    fetchMock().mockResolvedValueOnce(Response.json(summary, { status: 200 }));

    const response = await getAccount();

    await expect(response.json()).resolves.toEqual(summary);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/me`, {
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
      },
    });
  });

  it("forwards DELETE to Azure with the current auth value", async () => {
    const payload = { ok: true };
    fetchMock().mockResolvedValueOnce(Response.json(payload, { status: 200 }));

    const response = await DELETE(request("DELETE"));

    await expect(response.json()).resolves.toEqual(payload);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/me`, {
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
      },
      method: "DELETE",
    });
  });

  it("returns 401 when no access token is available", async () => {
    vi.mocked(getCurrentAccessToken).mockResolvedValueOnce(null);

    const response = await getAccount();

    await expect(response.json()).resolves.toEqual({
      error: "Authentication required.",
    });
    expect(response.status).toBe(401);
    expect(fetchMock()).not.toHaveBeenCalled();
  });
});

describe("/api/me/payments route handler", () => {
  it("is dynamic", () => {
    expect(paymentsDynamic).toBe("force-dynamic");
  });

  it("forwards GET to Azure payments with the current auth value", async () => {
    const payments = [
      {
        amount: 250,
        currency: "nzd",
        date: "2026-05-30T10:00:00Z",
        expiry: "2026-08-30T10:00:00Z",
        receiptUrl: "https://pay.stripe.test/receipts/quick-pack",
        remaining: 9,
        sku: "quick_pack",
      },
    ];
    fetchMock().mockResolvedValueOnce(Response.json(payments, { status: 200 }));

    const response = await getPayments();

    await expect(response.json()).resolves.toEqual(payments);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/me/payments`, {
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
      },
    });
  });
});
