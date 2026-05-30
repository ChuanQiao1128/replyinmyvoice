import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const {
  mockGetAzureApiBaseUrl,
  mockGetCurrentAccessToken,
  mockGetCurrentSession,
} = vi.hoisted(() => ({
  mockGetAzureApiBaseUrl: vi.fn(),
  mockGetCurrentAccessToken: vi.fn(),
  mockGetCurrentSession: vi.fn(),
}));

vi.mock("./azure-api", () => ({
  getAzureApiBaseUrl: mockGetAzureApiBaseUrl,
}));

vi.mock("./entra-auth", () => ({
  getCurrentAccessToken: mockGetCurrentAccessToken,
  getCurrentSession: mockGetCurrentSession,
}));

import { GET as accessTokenGet } from "../app/api/auth/access-token/route";
import { POST as portalPost } from "../app/api/stripe/portal/route";

const appUrl = "https://replyinmyvoice.com";

function apiRequest(path: string, init: RequestInit = {}) {
  return new Request(`${appUrl}${path}`, init);
}

describe("same-origin route guards", () => {
  beforeEach(() => {
    process.env.NEXT_PUBLIC_APP_URL = appUrl;
    mockGetAzureApiBaseUrl.mockReturnValue("https://azure.example.test");
    mockGetCurrentAccessToken.mockResolvedValue("session-access-token");
    mockGetCurrentSession.mockResolvedValue({
      email: "casey@example.test",
      exp: Math.floor(Date.now() / 1000) + 60,
      name: "Casey",
      sub: "user-1",
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
    delete process.env.NEXT_PUBLIC_APP_URL;
  });

  it("same-origin rejects cross-origin portal POSTs and allows same-origin POSTs", async () => {
    const fetchMock = vi.fn(async () =>
      Response.json({ url: "https://billing.example.test/session" }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const rejected = await portalPost(
      apiRequest("/api/stripe/portal", {
        headers: { origin: "https://attacker.example.test" },
        method: "POST",
      }),
    );

    expect(rejected.status).toBe(403);
    expect(mockGetCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();

    const allowed = await portalPost(
      apiRequest("/api/stripe/portal", {
        headers: { origin: appUrl },
        method: "POST",
      }),
    );

    expect(allowed.status).toBe(200);
    await expect(allowed.json()).resolves.toEqual({
      url: "https://billing.example.test/session",
    });
    expect(fetchMock).toHaveBeenCalledWith(
      "https://azure.example.test/api/stripe/portal",
      {
        headers: {
          Authorization: "Bearer session-access-token",
        },
        method: "POST",
      },
    );
  });

  it("same-origin rejects cross-origin access-token reads", async () => {
    const response = await accessTokenGet(
      apiRequest("/api/auth/access-token", {
        headers: { origin: "https://attacker.example.test" },
        method: "GET",
      }),
    );

    expect(response.status).toBe(403);
    expect(mockGetCurrentSession).not.toHaveBeenCalled();
    expect(mockGetCurrentAccessToken).not.toHaveBeenCalled();
  });

  it("same-origin keeps access-token reads gated by the current session", async () => {
    mockGetCurrentSession.mockResolvedValueOnce(null);
    mockGetCurrentAccessToken.mockResolvedValueOnce("token-without-session");

    const withoutSession = await accessTokenGet(
      apiRequest("/api/auth/access-token", {
        headers: { origin: appUrl },
        method: "GET",
      }),
    );

    expect(withoutSession.status).toBe(401);
    expect(mockGetCurrentAccessToken).not.toHaveBeenCalled();

    mockGetCurrentAccessToken.mockReset();
    mockGetCurrentSession.mockResolvedValueOnce({
      email: "casey@example.test",
      exp: Math.floor(Date.now() / 1000) + 60,
      name: "Casey",
      sub: "user-1",
    });
    mockGetCurrentAccessToken.mockResolvedValueOnce("session-access-token");

    const withSession = await accessTokenGet(
      apiRequest("/api/auth/access-token", {
        headers: { origin: appUrl },
        method: "GET",
      }),
    );

    expect(withSession.status).toBe(200);
    await expect(withSession.json()).resolves.toEqual({
      accessToken: "session-access-token",
    });
  });
});
