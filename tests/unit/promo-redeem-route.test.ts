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

import { POST, dynamic } from "../../app/api/promo/redeem/route";
import { getAzureApiBaseUrl } from "../../lib/azure-api";
import { getCurrentAccessToken } from "../../lib/entra-auth";

const appUrl = "https://replyinmyvoice.com";
const azureUrl = "https://rimv-api.example.test";
const accessValue = "unit-auth-value";
const authScheme = "Bearer";
const clientIp = "203.0.113.7";
const turnstileUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

let proxySecret: string;
let turnstileSecret: string;

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function request(
  body: unknown,
  headers: Record<string, string> = {},
) {
  return new Request(`${appUrl}/api/promo/redeem`, {
    body: JSON.stringify(body),
    headers: {
      "cf-connecting-ip": clientIp,
      "content-type": "application/json",
      origin: appUrl,
      ...headers,
    },
    method: "POST",
  });
}

beforeEach(() => {
  proxySecret = crypto.randomUUID();
  turnstileSecret = crypto.randomUUID();
  vi.stubEnv("NODE_ENV", "development");
  process.env.NEXT_PUBLIC_APP_URL = appUrl;
  process.env.PROMO_PROXY_SHARED_SECRET = proxySecret;
  process.env.TURNSTILE_SECRET_KEY = turnstileSecret;
  vi.stubGlobal("fetch", vi.fn());
  vi.mocked(getAzureApiBaseUrl).mockReset().mockReturnValue(azureUrl);
  vi.mocked(getCurrentAccessToken).mockReset().mockResolvedValue(accessValue);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  vi.clearAllMocks();
  delete process.env.NEXT_PUBLIC_APP_URL;
  delete process.env.PROMO_PROXY_SHARED_SECRET;
  delete process.env.TURNSTILE_SECRET_KEY;
});

describe("/api/promo/redeem route handler", () => {
  it("is dynamic", () => {
    expect(dynamic).toBe("force-dynamic");
  });

  it("rejects non-same-origin POSTs before auth or provider calls", async () => {
    const response = await POST(request({
      code: "TRIAL2026",
      turnstileToken: "captcha-token",
    }, {
      origin: "https://attacker.example.test",
    }));

    expect(response.status).toBe(403);
    expect(getCurrentAccessToken).not.toHaveBeenCalled();
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("returns invalid_captcha when the token is missing", async () => {
    const response = await POST(request({
      code: "TRIAL2026",
    }));

    await expect(response.json()).resolves.toEqual({ error: "invalid_captcha" });
    expect(response.status).toBe(403);
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("returns invalid_captcha when Turnstile rejects the token", async () => {
    fetchMock().mockResolvedValueOnce(Response.json({ success: false }, { status: 200 }));

    const response = await POST(request({
      code: "TRIAL2026",
      turnstileToken: "blocked-token",
    }));

    await expect(response.json()).resolves.toEqual({ error: "invalid_captcha" });
    expect(response.status).toBe(403);
    expect(fetchMock()).toHaveBeenCalledTimes(1);
  });

  it("verifies Turnstile and forwards auth, trusted IP, and proxy secret", async () => {
    const azureBody = {
      alreadyRedeemed: false,
      creditsGranted: 3,
      expiresAt: "2026-09-01T00:00:00Z",
      totalRemaining: 3,
    };
    fetchMock()
      .mockResolvedValueOnce(Response.json({ success: true }, { status: 200 }))
      .mockResolvedValueOnce(Response.json(azureBody, { status: 200 }));

    const response = await POST(request({
      code: "TRIAL2026",
      turnstileToken: "captcha-token",
    }, {
      "x-forwarded-for": "198.51.100.99",
    }));

    await expect(response.json()).resolves.toEqual(azureBody);
    expect(response.status).toBe(200);

    expect(fetchMock()).toHaveBeenCalledTimes(2);
    const [siteverifyUrl, siteverifyInit] = fetchMock().mock.calls[0];
    expect(siteverifyUrl).toBe(turnstileUrl);
    expect(siteverifyInit).toMatchObject({
      method: "POST",
    });
    const siteverifyBody = siteverifyInit?.body;
    expect(siteverifyBody).toBeInstanceOf(URLSearchParams);
    expect((siteverifyBody as URLSearchParams).get("response")).toBe("captcha-token");
    expect((siteverifyBody as URLSearchParams).get("secret")).toBe(turnstileSecret);
    expect((siteverifyBody as URLSearchParams).get("remoteip")).toBe(clientIp);

    expect(fetchMock()).toHaveBeenNthCalledWith(2, `${azureUrl}/api/promo/redeem`, {
      body: JSON.stringify({
        code: "TRIAL2026",
        turnstileToken: "captcha-token",
      }),
      cache: "no-store",
      headers: {
        Authorization: `${authScheme} ${accessValue}`,
        "Content-Type": "application/json",
        "X-Client-IP": clientIp,
        "X-RIMV-Proxy-Secret": proxySecret,
      },
      method: "POST",
    });
  });

  it("returns server_config in production when the Turnstile secret is missing", async () => {
    vi.stubEnv("NODE_ENV", "production");
    delete process.env.TURNSTILE_SECRET_KEY;

    const response = await POST(request({
      code: "TRIAL2026",
      turnstileToken: "captcha-token",
    }));

    await expect(response.json()).resolves.toEqual({ error: "server_config" });
    expect(response.status).toBe(500);
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("returns server_config in production when the proxy secret is missing", async () => {
    vi.stubEnv("NODE_ENV", "production");
    delete process.env.PROMO_PROXY_SHARED_SECRET;

    const response = await POST(request({
      code: "TRIAL2026",
      turnstileToken: "captcha-token",
    }));

    await expect(response.json()).resolves.toEqual({ error: "server_config" });
    expect(response.status).toBe(500);
    expect(fetchMock()).not.toHaveBeenCalled();
  });
});
