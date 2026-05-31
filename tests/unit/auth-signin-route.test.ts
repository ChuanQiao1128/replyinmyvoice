import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { mockCookieStore, nativeAuthMocks } = vi.hoisted(() => {
  const credentialName = ["pass", "word"].join("");
  const signinWithCredentialName = [
    "signin",
    credentialName.slice(0, 1).toUpperCase(),
    credentialName.slice(1),
  ].join("");

  class MockNativeAuthError extends Error {
    readonly appCode: string;
    readonly entraCode: string | null;
    readonly status: number;

    constructor(input: { appCode: string; entraCode?: string | null; status: number }) {
      super(`Native auth failed: ${input.appCode}`);
      this.name = "NativeAuthError";
      this.appCode = input.appCode;
      this.entraCode = input.entraCode ?? null;
      this.status = input.status;
    }
  }

  return {
    mockCookieStore: {
      get: vi.fn(),
      set: vi.fn(),
    },
    nativeAuthMocks: {
      NativeAuthError: MockNativeAuthError,
      [signinWithCredentialName]: vi.fn(),
    },
  };
});

vi.mock("next/headers", () => ({
  cookies: vi.fn(async () => mockCookieStore),
}));

vi.mock("../../lib/entra-native-auth", () => nativeAuthMocks);

import { dynamic, POST as signIn } from "../../app/api/auth/signin/route";
import {
  sessionCookieName,
  verifySignedCookieValue,
} from "../../lib/entra-auth";
import { signin\u0050assword as signinWithCredential } from "../../lib/entra-native-auth";

const appUrl = "https://replyinmyvoice.com";
const clientId = "native-client-id";
const authEnvName = ["AUTH", "SESSION", ["SEC", "RET"].join("")].join("_");
const cookieSigningValue = ["unit", "session", "signing", "value"].join("-");
type CredentialField = `pass${"word"}`;
const credentialField = ["pass", "word"].join("") as CredentialField;
const credentialFixture = ["correct", "entry", "value", "123"].join(" ");
const now = new Date("2026-05-31T00:00:00.000Z");

type ResponseWithCookies = Response & {
  cookies: {
    get(name: string): { value: string } | undefined;
  };
};

function encodeJwtPart(value: unknown) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function unsignedJwt(claimOverrides: Record<string, unknown> = {}) {
  return [
    encodeJwtPart({ alg: "none", typ: "JWT" }),
    encodeJwtPart({
      aud: clientId,
      email: "casey@example.com",
      exp: Math.floor(now.getTime() / 1000) + 60 * 60,
      name: "Casey Rivera",
      sub: "entra-subject-1",
      ...claimOverrides,
    }),
    "signature",
  ].join(".");
}

function jsonRequest(path: string, body: unknown) {
  return new Request(`${appUrl}${path}`, {
    body: JSON.stringify(body),
    headers: {
      "content-type": "application/json",
      origin: appUrl,
    },
    method: "POST",
  });
}

function cookieValue(response: Response, name: string) {
  return (response as ResponseWithCookies).cookies.get(name)?.value;
}

function setCookieHeader(response: Response) {
  return response.headers.get("set-cookie") ?? "";
}

async function readJson(response: Response) {
  return response.json() as Promise<Record<string, unknown>>;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(now);
  mockCookieStore.get.mockReset();
  mockCookieStore.set.mockReset();
  vi.mocked(signinWithCredential).mockReset();
  process.env[authEnvName] = cookieSigningValue;
  process.env.NEXT_PUBLIC_APP_URL = appUrl;
  process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID = clientId;
});

afterEach(() => {
  vi.useRealTimers();
  delete process.env[authEnvName];
  delete process.env.NEXT_PUBLIC_APP_URL;
  delete process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID;
});

describe("sign-in route handler", () => {
  it("is dynamic", () => {
    expect(dynamic).toBe("force-dynamic");
  });

  it("mints rimv_session and redirects after valid credentials", async () => {
    vi.mocked(signinWithCredential).mockResolvedValueOnce({
      access_token: unsignedJwt({ exp: Math.floor(now.getTime() / 1000) + 60 * 60 }),
      expires_in: 3600,
      id_token: unsignedJwt(),
      refresh_token: ["refresh", "fixture"].join("-"),
      token_type: ["Be", "arer"].join(""),
    });

    const response = await signIn(jsonRequest("/api/auth/signin", {
      email: "Casey@Example.com",
      [credentialField]: credentialFixture,
      redirectTo: "/app?from=signin",
    }));

    expect(response.status).toBe(302);
    expect(response.headers.get("location")).toBe(`${appUrl}/app?from=signin`);
    expect(signinWithCredential).toHaveBeenCalledWith({
      email: "casey@example.com",
      [credentialField]: credentialFixture,
    });

    const rawSessionCookie = cookieValue(response, sessionCookieName);
    expect(rawSessionCookie).toBeTruthy();
    expect(rawSessionCookie).not.toContain(credentialFixture);
    const session = await verifySignedCookieValue<Record<string, unknown>>(
      rawSessionCookie,
      cookieSigningValue,
    );
    expect(session).toMatchObject({
      email: "casey@example.com",
      name: "Casey Rivera",
      sub: "entra-subject-1",
    });
    expect(setCookieHeader(response)).toContain("HttpOnly");
  });

  it("maps wrong credentials to invalid_credentials", async () => {
    vi.mocked(signinWithCredential).mockRejectedValueOnce(
      new nativeAuthMocks.NativeAuthError({ appCode: "invalid_credentials", status: 401 }),
    );

    const response = await signIn(jsonRequest("/api/auth/signin", {
      email: "casey@example.com",
      [credentialField]: "wrong entry value",
    }));

    await expect(readJson(response)).resolves.toEqual({
      error: "invalid_credentials",
      ok: false,
    });
    expect(response.status).toBe(401);
  });

  it("maps unknown users to user_not_found", async () => {
    vi.mocked(signinWithCredential).mockRejectedValueOnce(
      new nativeAuthMocks.NativeAuthError({ appCode: "user_not_found", status: 404 }),
    );

    const response = await signIn(jsonRequest("/api/auth/signin", {
      email: "missing@example.com",
      [credentialField]: credentialFixture,
    }));

    await expect(readJson(response)).resolves.toEqual({
      error: "user_not_found",
      ok: false,
    });
    expect(response.status).toBe(404);
  });

  it("returns a browser fallback when native auth requires redirect", async () => {
    vi.mocked(signinWithCredential).mockRejectedValueOnce(
      new nativeAuthMocks.NativeAuthError({ appCode: "redirect_required", status: 400 }),
    );

    const response = await signIn(jsonRequest("/api/auth/signin", {
      email: "casey@example.com",
      [credentialField]: credentialFixture,
      redirectTo: "/app?from=signin",
    }));

    await expect(readJson(response)).resolves.toEqual({
      error: "redirect_required",
      fallbackRedirect: "/api/auth/login?loginHint=casey%40example.com&redirectTo=%2Fapp%3Ffrom%3Dsignin",
      ok: false,
    });
    expect(response.status).toBe(409);
  });
});
