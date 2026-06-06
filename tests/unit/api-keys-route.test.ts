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

import { GET, POST, dynamic } from "../../app/api/keys/route";
import { DELETE } from "../../app/api/keys/[id]/route";
import { POST as ROTATE } from "../../app/api/keys/[id]/rotate/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";
import { getCurrentAccessToken } from "../../lib/entra-auth";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const accessValue = "unit-auth-value";
const keyId = "11111111-1111-4111-8111-111111111111";

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

function context(id = keyId) {
  return {
    params: Promise.resolve({ id }),
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

describe("/api/keys proxy routes", () => {
  it("is dynamic", () => {
    expect(dynamic).toBe("force-dynamic");
  });

  it("forwards key list requests with the current bearer value", async () => {
    const payload = [
      {
        createdAt: "2026-06-04T00:00:00Z",
        id: keyId,
        lastUsedAt: null,
        last30dUsage: {
          calls: 2,
          failed: 1,
          succeeded: 1,
        },
        maskedKey: "rmv_live_****abcd",
        name: "Server key",
        revokedAt: null,
      },
    ];
    fetchMock().mockResolvedValueOnce(Response.json(payload, { status: 200 }));

    const response = await GET();

    await expect(response.json()).resolves.toEqual(payload);
    expect(response.status).toBe(200);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/keys`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessValue}`,
      },
    });
  });

  it("forwards same-origin key creation with the current bearer value", async () => {
    const createPayload = { name: "Sandbox client", test: true };
    const azureBody = {
      createdAt: "2026-06-04T00:00:00Z",
      id: keyId,
      isTest: true,
      key: "rmv_test_plaintext_once",
      name: "Sandbox client",
    };
    fetchMock().mockResolvedValueOnce(Response.json(azureBody, { status: 201 }));

    const response = await POST(
      request("/api/keys", "POST", createPayload),
    );

    await expect(response.json()).resolves.toEqual(azureBody);
    expect(response.status).toBe(201);
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/keys`, {
      body: JSON.stringify(createPayload),
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessValue}`,
        "Content-Type": "application/json",
      },
      method: "POST",
    });
  });

  it("rejects cross-origin key creation before auth or forwarding", async () => {
    const response = await POST(
      request(
        "/api/keys",
        "POST",
        { name: "Server key" },
        "https://attacker.example.test",
      ),
    );

    expect(response.status).toBe(403);
    expect(getCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("forwards key deletion and preserves empty 204 responses", async () => {
    fetchMock().mockResolvedValueOnce(new Response(null, { status: 204 }));

    const response = await DELETE(
      request(`/api/keys/${keyId}`, "DELETE"),
      context(),
    );

    expect(response.status).toBe(204);
    expect(await response.text()).toBe("");
    expect(fetchMock()).toHaveBeenCalledWith(`${azureUrl}/api/keys/${keyId}`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessValue}`,
      },
      method: "DELETE",
    });
  });

  it("forwards same-origin key rotation with the current bearer value", async () => {
    const azureBody = {
      createdAt: "2026-06-04T00:00:00Z",
      id: "22222222-2222-4222-8222-222222222222",
      key: "rmv_live_rotated_plaintext_once",
      name: "Server key",
    };
    fetchMock().mockResolvedValueOnce(Response.json(azureBody, { status: 201 }));

    const response = await ROTATE(
      request(`/api/keys/${keyId}/rotate`, "POST"),
      context(),
    );

    await expect(response.json()).resolves.toEqual(azureBody);
    expect(response.status).toBe(201);
    expect(fetchMock()).toHaveBeenCalledWith(
      `${azureUrl}/api/keys/${keyId}/rotate`,
      {
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${accessValue}`,
        },
        method: "POST",
      },
    );
  });

  it("returns 401 when no access token is available", async () => {
    vi.mocked(getCurrentAccessToken).mockResolvedValueOnce(null);

    const response = await GET();

    await expect(response.json()).resolves.toEqual({
      error: "Authentication required.",
    });
    expect(response.status).toBe(401);
    expect(fetchMock()).not.toHaveBeenCalled();
  });
});
