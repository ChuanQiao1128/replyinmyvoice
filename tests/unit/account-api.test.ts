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
  GET as paymentsGet,
  GET as getPayments,
  dynamic as paymentsDynamic,
} from "../../app/api/me/payments/route";
import {
  GET as billingHistoryGet,
  dynamic as billingHistoryDynamic,
} from "../../app/api/me/billing/history/route";
import {
  GET as billingSupportGet,
  POST as billingSupportPost,
} from "../../app/api/billing-support-requests/route";
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
      paymentGraceEndsAt: null,
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
        "X-Correlation-Id": expect.stringMatching(/^account_/),
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
        "X-Correlation-Id": expect.stringMatching(/^account_/),
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

  it("forwards caller payment reads with the current auth value", async () => {
    const payments = [
      {
        amount: 900,
        currency: "nzd",
        date: "2026-05-31T00:00:00Z",
        expiry: "2026-08-29T00:00:00Z",
        paymentIntentId: "pi_customer_pack",
        remaining: 10,
        sku: "quick_pack",
      },
    ];
    fetchMock().mockResolvedValueOnce(Response.json(payments, { status: 200 }));

    const response = await paymentsGet();

    await expect(response.json()).resolves.toEqual(payments);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/me/payments`, {
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
      },
    });
  });

  it("forwards caller billing support reads and same-origin creates", async () => {
    const created = {
      id: "request-1",
      message: "I was charged twice.",
      relatedPaymentIntentId: "pi_customer_pack",
      status: "open",
      type: "refund",
    };
    fetchMock()
      .mockResolvedValueOnce(Response.json([created], { status: 200 }))
      .mockResolvedValueOnce(Response.json(created, { status: 201 }));

    const readResponse = await billingSupportGet();
    await expect(readResponse.json()).resolves.toEqual([created]);

    const body = JSON.stringify({
      message: "I was charged twice.",
      relatedPaymentIntentId: "pi_customer_pack",
      type: "refund",
    });
    const createResponse = await billingSupportPost(
      new Request(`${appUrl}/api/billing-support-requests`, {
        body,
        headers: {
          "content-type": "application/json",
          origin: appUrl,
        },
        method: "POST",
      }),
    );

    await expect(createResponse.json()).resolves.toEqual(created);
    expect(createResponse.status).toBe(201);
    expect(fetchMock()).toHaveBeenNthCalledWith(1, `${azureUrl}/api/billing-support-requests`, {
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
      },
    });
    expect(fetchMock()).toHaveBeenNthCalledWith(2, `${azureUrl}/api/billing-support-requests`, {
      body,
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
        "Content-Type": "application/json",
      },
      method: "POST",
    });
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

describe("/api/me/billing/history route handler", () => {
  it("is dynamic", () => {
    expect(billingHistoryDynamic).toBe("force-dynamic");
  });

  it("forwards GET to Azure billing history with the current auth value", async () => {
    const history = [
      {
        amount: 900,
        currency: "nzd",
        date: "2026-06-05T10:00:00Z",
        description: "Subscription invoice 2026-06-01 - 2026-07-01",
        hostedInvoiceUrl: "https://invoice.stripe.test/history",
        status: "paid",
        type: "subscription",
      },
      {
        amount: -600,
        currency: "nzd",
        date: "2026-06-04T10:00:00Z",
        description: "Refund for quick_pack",
        status: "refunded",
        type: "refund",
      },
    ];
    fetchMock().mockResolvedValueOnce(Response.json(history, { status: 200 }));

    const response = await billingHistoryGet(request("GET"));

    await expect(response.json()).resolves.toEqual(history);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/me/billing/history`, {
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
      },
    });
  });

  it("returns 401 when no access token is available", async () => {
    vi.mocked(getCurrentAccessToken).mockResolvedValueOnce(null);

    const response = await billingHistoryGet(request("GET"));

    await expect(response.json()).resolves.toEqual({
      error: "Authentication required.",
    });
    expect(response.status).toBe(401);
    expect(fetchMock()).not.toHaveBeenCalled();
  });
});
