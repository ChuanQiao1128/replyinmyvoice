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
  GET as usageExportGet,
  dynamic as usageExportDynamic,
} from "../../app/api/me/api-usage/export/route";
import {
  GET as billingExportGet,
  dynamic as billingExportDynamic,
} from "../../app/api/me/billing/export/route";
import { serializeCsv } from "../../lib/csv-export";
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

describe("CSV export serialization", () => {
  it("quotes fields with quotes, commas, and line breaks", () => {
    const csv = serializeCsv(
      ["createdAt", "endpoint", "statusCode", "latencyMs", "keyLast4"],
      [
        {
          createdAt: "2026-06-05T10:00:00Z",
          endpoint: "/api/v1/rewrite,bulk",
          keyLast4: "ab\"d",
          latencyMs: null,
          statusCode: 200,
        },
        {
          createdAt: "2026-06-05T10:01:00Z",
          endpoint: "/api/v1/rewrite\nretry",
          keyLast4: "xy12",
          latencyMs: 123,
          statusCode: 429,
        },
      ],
    );

    expect(csv).toBe(
      [
        "createdAt,endpoint,statusCode,latencyMs,keyLast4",
        "2026-06-05T10:00:00Z,\"/api/v1/rewrite,bulk\",200,,\"ab\"\"d\"",
        "2026-06-05T10:01:00Z,\"/api/v1/rewrite\nretry\",429,123,xy12",
      ].join("\n"),
    );
  });
});

describe("/api/me CSV export routes", () => {
  it("keeps both export routes dynamic", () => {
    expect(usageExportDynamic).toBe("force-dynamic");
    expect(billingExportDynamic).toBe("force-dynamic");
  });

  it("exports recent usage as csv and caps the forwarded limit", async () => {
    fetchMock().mockResolvedValueOnce(
      Response.json(
        [
          {
            createdAt: "2026-06-05T10:00:00Z",
            endpoint: "/api/v1/rewrite,bulk",
            keyLast4: "a1b2",
            latencyMs: 98,
            statusCode: 200,
          },
        ],
        { status: 200 },
      ),
    );

    const response = await usageExportGet(
      request("/api/me/api-usage/export?limit=2500&userId=other"),
    );

    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toContain("text/csv");
    expect(response.headers.get("content-disposition")).toBe(
      'attachment; filename="api-usage.csv"',
    );
    await expect(response.text()).resolves.toBe(
      [
        "createdAt,endpoint,statusCode,latencyMs,keyLast4",
        "2026-06-05T10:00:00Z,\"/api/v1/rewrite,bulk\",200,98,a1b2",
      ].join("\n"),
    );
    expect(fetchMock()).toHaveBeenCalledWith(
      `${azureUrl}/api/me/api-usage/recent?limit=1000`,
      {
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${accessValue}`,
        },
      },
    );
  });

  it("exports billing history with the first available receipt or invoice url", async () => {
    fetchMock().mockResolvedValueOnce(
      Response.json(
        [
          {
            amount: 900,
            currency: "nzd",
            date: "2026-06-05T10:00:00Z",
            description: "Subscription invoice, June",
            hostedInvoiceUrl: "https://payments.example.test/invoice/1",
            receiptUrl: null,
            status: "paid",
            type: "subscription",
          },
        ],
        { status: 200 },
      ),
    );

    const response = await billingExportGet(request("/api/me/billing/export"));

    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toContain("text/csv");
    expect(response.headers.get("content-disposition")).toBe(
      'attachment; filename="billing-history.csv"',
    );
    await expect(response.text()).resolves.toBe(
      [
        "date,type,description,amount,currency,status,url",
        "2026-06-05T10:00:00Z,subscription,\"Subscription invoice, June\",900,nzd,paid,https://payments.example.test/invoice/1",
      ].join("\n"),
    );
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/me/billing/history`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessValue}`,
      },
    });
  });

  it("rejects cross-origin export requests before auth or forwarding", async () => {
    const response = await usageExportGet(
      request("/api/me/api-usage/export", "https://attacker.example.test"),
    );

    expect(response.status).toBe(403);
    expect(getCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("rejects cross-origin billing export requests before auth or forwarding", async () => {
    const response = await billingExportGet(
      request("/api/me/billing/export", "https://attacker.example.test"),
    );

    expect(response.status).toBe(403);
    expect(getCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("returns 401 when usage export has no access token", async () => {
    vi.mocked(getCurrentAccessToken).mockResolvedValueOnce(null);

    const response = await usageExportGet(request("/api/me/api-usage/export"));

    await expect(response.json()).resolves.toEqual({
      error: "Authentication required.",
    });
    expect(response.status).toBe(401);
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("returns 401 when billing export has no access token", async () => {
    vi.mocked(getCurrentAccessToken).mockResolvedValueOnce(null);

    const response = await billingExportGet(request("/api/me/billing/export"));

    await expect(response.json()).resolves.toEqual({
      error: "Authentication required.",
    });
    expect(response.status).toBe(401);
    expect(fetchMock()).not.toHaveBeenCalled();
  });
});
