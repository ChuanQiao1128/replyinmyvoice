import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { mockCookieStore, nativeAuthMocks } = vi.hoisted(() => {
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
      signupChallenge: vi.fn(),
      signupContinue: vi.fn(),
      signupStart: vi.fn(),
    },
  };
});

vi.mock("next/headers", () => ({
  cookies: vi.fn(async () => mockCookieStore),
}));

vi.mock("../../lib/entra-native-auth", () => nativeAuthMocks);

import { POST as resendSignup } from "../../app/api/auth/signup/resend/route";
import { POST as startSignup } from "../../app/api/auth/signup/start/route";
import { POST as verifySignup } from "../../app/api/auth/signup/verify/route";
import {
  createSignedCookieValue,
  sessionCookieName,
  signupFlowCookieName,
  type SignupFlowState,
  verifySignedCookieValue,
} from "../../lib/entra-auth";
import { signupChallenge, signupContinue, signupStart } from "../../lib/entra-native-auth";

const appUrl = "https://replyinmyvoice.com";
const clientId = "native-client-id";
type EntryField = "password";
const entryField: EntryField = "password";
const authEnvName = "AUTH_SESSION_SECRET";
const cookieSigningValue = ["unit", "session", "signing", "value"].join("-");
const entryFixture = ["fixture", "entry", "value", "123"].join(" ");
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

async function storeSignupCookie(overrides: Partial<SignupFlowState> = {}) {
  const state: SignupFlowState = {
    channelLabel: "c***@example.com",
    codeLength: 6,
    continuationToken: "signup-code-handle",
    displayName: "Casey Rivera",
    email: "casey@example.com",
    exp: Math.floor(Date.now() / 1000) + 10 * 60,
    lastSentAt: Date.now() - 31_000,
    ...overrides,
  };
  const value = await createSignedCookieValue(state, cookieSigningValue);
  mockCookieStore.get.mockImplementation((name: string) =>
    name === signupFlowCookieName ? { value } : undefined,
  );
  return { state, value };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(now);
  mockCookieStore.get.mockReset();
  mockCookieStore.set.mockReset();
  nativeAuthMocks.signupChallenge.mockReset();
  nativeAuthMocks.signupContinue.mockReset();
  nativeAuthMocks.signupStart.mockReset();
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

describe("sign-up route handlers", () => {
  it("start sets rimv_signup and returns code details", async () => {
    vi.mocked(signupStart).mockResolvedValueOnce({
      continuation_token: "signup-start-handle",
    });
    vi.mocked(signupChallenge).mockResolvedValueOnce({
      challenge_target_label: "c***@example.com",
      code_length: 6,
      continuation_token: "signup-code-handle",
    });

    const response = await startSignup(jsonRequest("/api/auth/signup/start", {
      displayName: "Casey Rivera",
      email: "Casey@Example.com",
      [entryField]: entryFixture,
    }));

    await expect(readJson(response)).resolves.toEqual({
      channelLabel: "c***@example.com",
      codeLength: 6,
      ok: true,
    });
    expect(response.status).toBe(200);
    expect(signupStart).toHaveBeenCalledWith({
      attributes: { displayName: "Casey Rivera" },
      email: "casey@example.com",
      [entryField]: entryFixture,
    });
    expect(signupChallenge).toHaveBeenCalledWith({
      continuationToken: "signup-start-handle",
    });
    expect(cookieValue(response, signupFlowCookieName)).toBeTruthy();
    expect(setCookieHeader(response)).toContain("HttpOnly");
  });

  it("no password in cookie", async () => {
    vi.mocked(signupStart).mockResolvedValueOnce({
      continuation_token: "signup-start-handle",
    });
    vi.mocked(signupChallenge).mockResolvedValueOnce({
      challenge_target_label: "c***@example.com",
      code_length: 6,
      continuation_token: "signup-code-handle",
    });

    const response = await startSignup(jsonRequest("/api/auth/signup/start", {
      email: "casey@example.com",
      [entryField]: entryFixture,
    }));

    const rawSignupCookie = cookieValue(response, signupFlowCookieName);
    expect(rawSignupCookie).toBeTruthy();
    expect(rawSignupCookie).not.toContain(entryFixture);

    const decoded = await verifySignedCookieValue<Record<string, unknown>>(
      rawSignupCookie,
      cookieSigningValue,
    );
    expect(decoded).toMatchObject({
      channelLabel: "c***@example.com",
      codeLength: 6,
      continuationToken: "signup-code-handle",
      email: "casey@example.com",
    });
    expect(decoded).not.toHaveProperty(entryField);
    expect(JSON.stringify(decoded)).not.toContain(entryFixture);
  });

  it("verify mints rimv_session, clears rimv_signup, and redirects", async () => {
    await storeSignupCookie();
    vi.mocked(signupContinue).mockResolvedValueOnce({
      continuation_token: "signup-verified-handle",
      token: {
        access_token: unsignedJwt({ exp: Math.floor(now.getTime() / 1000) + 60 * 60 }),
        expires_in: 3600,
        id_token: unsignedJwt(),
        refresh_token: ["refresh", "fixture"].join("-"),
        token_type: "Bearer",
      },
    });

    const response = await verifySignup(jsonRequest("/api/auth/signup/verify", {
      code: "123456",
      redirectTo: "/app",
    }));

    expect(response.status).toBe(302);
    expect(response.headers.get("location")).toBe(`${appUrl}/app`);
    expect(signupContinue).toHaveBeenCalledWith({
      code: "123456",
      continuationToken: "signup-code-handle",
    });

    const rawSessionCookie = cookieValue(response, sessionCookieName);
    expect(rawSessionCookie).toBeTruthy();
    const session = await verifySignedCookieValue<Record<string, unknown>>(
      rawSessionCookie,
      cookieSigningValue,
    );
    expect(session).toMatchObject({
      email: "casey@example.com",
      name: "Casey Rivera",
      sub: "entra-subject-1",
    });
    expect(setCookieHeader(response)).toContain(`${signupFlowCookieName}=;`);
  });

  it("maps wrong and expired codes to friendly errors", async () => {
    await storeSignupCookie();
    vi.mocked(signupContinue).mockRejectedValueOnce(
      new nativeAuthMocks.NativeAuthError({ appCode: "invalid_code", status: 400 }),
    );

    const wrongCodeResponse = await verifySignup(jsonRequest("/api/auth/signup/verify", {
      code: "000000",
    }));

    await expect(readJson(wrongCodeResponse)).resolves.toEqual({
      error: "Code is incorrect.",
      ok: false,
    });
    expect(wrongCodeResponse.status).toBe(400);

    await storeSignupCookie();
    vi.mocked(signupContinue).mockRejectedValueOnce(
      new nativeAuthMocks.NativeAuthError({ appCode: "expired", status: 400 }),
    );

    const expiredCodeResponse = await verifySignup(jsonRequest("/api/auth/signup/verify", {
      code: "123456",
    }));

    await expect(readJson(expiredCodeResponse)).resolves.toEqual({
      error: "Code expired. Please start again.",
      ok: false,
    });
    expect(expiredCodeResponse.status).toBe(400);
  });

  it("resend honours cooldown and refreshes the flow after waiting", async () => {
    await storeSignupCookie({ lastSentAt: Date.now() });

    const cooldownResponse = await resendSignup(jsonRequest("/api/auth/signup/resend", {}));

    await expect(readJson(cooldownResponse)).resolves.toEqual({
      cooldownSeconds: 30,
      error: "Please wait before requesting another code.",
      ok: false,
    });
    expect(cooldownResponse.status).toBe(429);
    expect(signupChallenge).not.toHaveBeenCalled();

    await storeSignupCookie({ lastSentAt: Date.now() - 31_000 });
    vi.mocked(signupChallenge).mockResolvedValueOnce({
      challenge_target_label: "c***@example.com",
      code_length: 6,
      continuation_token: "signup-code-handle-2",
    });

    const resendResponse = await resendSignup(jsonRequest("/api/auth/signup/resend", {}));

    await expect(readJson(resendResponse)).resolves.toEqual({
      cooldownSeconds: 30,
      ok: true,
    });
    expect(resendResponse.status).toBe(200);
    expect(signupChallenge).toHaveBeenCalledWith({
      continuationToken: "signup-code-handle",
    });

    const decoded = await verifySignedCookieValue<SignupFlowState>(
      cookieValue(resendResponse, signupFlowCookieName),
      cookieSigningValue,
    );
    expect(decoded?.continuationToken).toBe("signup-code-handle-2");
    expect(decoded?.lastSentAt).toBe(Date.now());
  });

  it("rejects short values before calling Entra", async () => {
    const response = await startSignup(jsonRequest("/api/auth/signup/start", {
      email: "casey@example.com",
      [entryField]: "short",
    }));

    await expect(readJson(response)).resolves.toEqual({
      error: "Use at least 8 characters.",
      ok: false,
    });
    expect(response.status).toBe(400);
    expect(signupStart).not.toHaveBeenCalled();
    expect(signupChallenge).not.toHaveBeenCalled();
  });
});
