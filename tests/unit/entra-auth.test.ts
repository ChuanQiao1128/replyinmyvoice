import { createSign, generateKeyPairSync } from "node:crypto";

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { mockCookieStore } = vi.hoisted(() => ({
  mockCookieStore: {
    delete: vi.fn(),
    get: vi.fn(),
    set: vi.fn(),
  },
}));

vi.mock("next/headers", () => ({
  cookies: vi.fn(async () => mockCookieStore),
}));

import {
  buildEntraAuthorizeUrl,
  buildEntraTokenRequestBody,
  createSessionFromTokens,
  createSignedCookieValue,
  getCurrentAccessToken,
  sessionCookieName,
  validateEntraBearerToken,
  verifySignedCookieValue,
} from "../../lib/entra-auth";

const authority = "https://login.example.test/tenant/v2.0";
const clientId = "reply-api-client";
const requiredScope = "rewrite.use";
const jwksUrl = "https://login.example.test/tenant/discovery/v2.0/keys";
const now = Math.floor(new Date("2026-05-23T00:00:00.000Z").getTime() / 1000);

const { privateKey, publicKey } = generateKeyPairSync("rsa", {
  modulusLength: 2048,
});
const publicJwk = {
  ...publicKey.export({ format: "jwk" }),
  alg: "RS256",
  kid: "unit-key-1",
  use: "sig",
};

function encodeJwtPart(value: unknown) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function signedToken(claimOverrides: Record<string, unknown> = {}) {
  const claims = {
    aud: clientId,
    email: "casey@example.com",
    exp: now + 60 * 60,
    iss: authority,
    name: "Casey Rivera",
    oid: "entra-user-1",
    scp: requiredScope,
    sub: "entra-subject-1",
    ...claimOverrides,
  };
  const header = encodeJwtPart({ alg: "RS256", kid: "unit-key-1", typ: "JWT" });
  const payload = encodeJwtPart(claims);
  const signingInput = `${header}.${payload}`;
  const signature = createSign("RSA-SHA256")
    .update(signingInput)
    .sign(privateKey)
    .toString("base64url");

  return `${signingInput}.${signature}`;
}

function unsignedToken(claimOverrides: Record<string, unknown> = {}) {
  return [
    encodeJwtPart({ alg: "none", typ: "JWT" }),
    encodeJwtPart({
      aud: clientId,
      email: "casey@example.com",
      exp: now + 60 * 60,
      name: "Casey Rivera",
      sub: "entra-subject-1",
      ...claimOverrides,
    }),
    "signature",
  ].join(".");
}

function mockJwksEndpoint() {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url: string | URL | Request) => {
      expect(String(url)).toBe(jwksUrl);
      return Response.json({ keys: [publicJwk] });
    }),
  );
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-05-23T00:00:00.000Z"));
  mockCookieStore.delete.mockReset();
  mockCookieStore.get.mockReset();
  mockCookieStore.set.mockReset();
  process.env.AUTH_SESSION_SECRET = "unit-session-secret";
  process.env.NEXT_PUBLIC_ENTRA_AUTHORITY = authority;
  process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID = clientId;
  process.env.NEXT_PUBLIC_ENTRA_API_SCOPE = requiredScope;
  process.env.NEXT_PUBLIC_APP_URL = "https://replyinmyvoice.com";
  mockJwksEndpoint();
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  delete process.env.AUTH_SESSION_SECRET;
  delete process.env.NEXT_PUBLIC_ENTRA_AUTHORITY;
  delete process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID;
  delete process.env.NEXT_PUBLIC_ENTRA_API_SCOPE;
  delete process.env.NEXT_PUBLIC_APP_URL;
});

describe("Entra auth helpers", () => {
  it("builds an Entra authorization URL without a brittle provider hint", async () => {
    const url = await buildEntraAuthorizeUrl({
      authority: "https://replyinmyvoicecustomers.ciamlogin.com/tenant/v2.0",
      clientId: "frontend-client",
      redirectUri: "https://replyinmyvoice.com/auth/callback",
      state: "state-test",
      nonce: "nonce-test",
      codeVerifier: "verifier-test",
      prompt: "select_account",
    });

    expect(url.origin).toBe("https://replyinmyvoicecustomers.ciamlogin.com");
    expect(url.pathname).toBe("/tenant/oauth2/v2.0/authorize");
    expect(url.searchParams.get("client_id")).toBe("frontend-client");
    expect(url.searchParams.get("redirect_uri")).toBe("https://replyinmyvoice.com/auth/callback");
    expect(url.searchParams.get("response_type")).toBe("code");
    expect(url.searchParams.get("scope")).toBe(`openid profile email ${requiredScope}`);
    expect(url.searchParams.has("domain_hint")).toBe(false);
    expect(url.searchParams.get("prompt")).toBe("select_account");
    expect(url.searchParams.get("code_challenge_method")).toBe("S256");
    expect(url.searchParams.get("code_challenge")).toBeTruthy();
  });

  it("passes a user email as login_hint for Entra email code sign-in", async () => {
    const url = await buildEntraAuthorizeUrl({
      authority: "https://replyinmyvoicecustomers.ciamlogin.com/tenant/v2.0",
      clientId: "frontend-client",
      redirectUri: "https://replyinmyvoice.com/auth/callback",
      state: "state-test",
      nonce: "nonce-test",
      codeVerifier: "verifier-test",
      loginHint: "casey@example.com",
      prompt: "select_account",
    });

    expect(url.searchParams.get("login_hint")).toBe("casey@example.com");
  });

  it("builds a confidential server-side token exchange body when a client secret is configured", () => {
    const body = buildEntraTokenRequestBody({
      clientId: "frontend-client",
      code: "auth-code",
      redirectUri: "https://replyinmyvoice.com/auth/callback",
      codeVerifier: "verifier",
      clientSecret: "secret-value",
    });

    expect(body.get("client_id")).toBe("frontend-client");
    expect(body.get("grant_type")).toBe("authorization_code");
    expect(body.get("redirect_uri")).toBe("https://replyinmyvoice.com/auth/callback");
    expect(body.get("code_verifier")).toBe("verifier");
    expect(body.get("scope")).toBe(`openid profile email ${requiredScope}`);
    expect(body.get("client_secret")).toBe("secret-value");
  });

  it("verifies signed cookie payloads and rejects tampering", async () => {
    const secret = "unit-test-secret";
    const value = await createSignedCookieValue({ sub: "user-1", email: "a@example.com" }, secret);

    await expect(verifySignedCookieValue<{ sub: string }>(value, secret)).resolves.toMatchObject({
      sub: "user-1",
    });

    const [payload, signature] = value.split(".");
    const tampered = `${payload.slice(0, -1)}x.${signature}`;
    await expect(verifySignedCookieValue(tampered, secret)).resolves.toBeNull();
  });

  it("keeps the browser session cookie below the 4KB browser limit", async () => {
    const session = await createSessionFromTokens({
      idToken: unsignedToken(),
      accessToken: unsignedToken({ pad: "x".repeat(3500) }),
      refreshToken: "refresh-token-" + "y".repeat(1200),
    });

    expect(session.accessToken).toBeTruthy();

    const sessionCookieCall = mockCookieStore.set.mock.calls.find(
      ([name]) => name === sessionCookieName,
    );
    expect(sessionCookieCall).toBeTruthy();

    const [, cookieValue] = sessionCookieCall!;
    expect(cookieValue.length).toBeLessThan(4096);

    const persistedSession = await verifySignedCookieValue<Record<string, unknown>>(
      cookieValue,
      "unit-session-secret",
    );
    expect(persistedSession).toMatchObject({
      sub: "entra-subject-1",
      email: "casey@example.com",
      name: "Casey Rivera",
    });
    expect(persistedSession).not.toHaveProperty("accessToken");
    expect(persistedSession).not.toHaveProperty("refreshToken");

    const accessTokenCookieCalls = mockCookieStore.set.mock.calls.filter(([name]) =>
      String(name).startsWith("rimv_access_"),
    );
    expect(accessTokenCookieCalls.length).toBeGreaterThan(1);
    for (const [, accessTokenCookieValue] of accessTokenCookieCalls) {
      expect(accessTokenCookieValue.length).toBeLessThan(4096);
    }

    const storedCookies = new Map(
      mockCookieStore.set.mock.calls.map(([name, value]) => [String(name), value]),
    );
    mockCookieStore.get.mockImplementation((name: string) => {
      const value = storedCookies.get(name);
      return value ? { value } : undefined;
    });

    await expect(getCurrentAccessToken()).resolves.toBe(session.accessToken);
  });
});

describe("Entra bearer token validation", () => {
  it("maps a valid signed access token to an authenticated user", async () => {
    const result = await validateEntraBearerToken(`Bearer ${signedToken()}`, {
      jwksUrl,
    });

    expect(result).toEqual({
      ok: true,
      user: {
        email: "casey@example.com",
        exp: now + 60 * 60,
        name: "Casey Rivera",
        sub: "entra-subject-1",
      },
    });
  });

  it("rejects an expired signed token with 401", async () => {
    const result = await validateEntraBearerToken(
      `Bearer ${signedToken({ exp: now - 1 })}`,
      { jwksUrl },
    );

    expect(result).toMatchObject({ ok: false, status: 401 });
  });

  it("rejects a signed token for the wrong audience with 401", async () => {
    const result = await validateEntraBearerToken(
      `Bearer ${signedToken({ aud: "other-client" })}`,
      { jwksUrl },
    );

    expect(result).toMatchObject({ ok: false, status: 401 });
  });

  it("rejects a signed token from the wrong issuer with 401", async () => {
    const result = await validateEntraBearerToken(
      `Bearer ${signedToken({ iss: "https://login.example.test/other/v2.0" })}`,
      { jwksUrl },
    );

    expect(result).toMatchObject({ ok: false, status: 401 });
  });

  it("rejects a signed token without the required scope with 403", async () => {
    const result = await validateEntraBearerToken(
      `Bearer ${signedToken({ scp: "profile.read" })}`,
      { jwksUrl },
    );

    expect(result).toMatchObject({ ok: false, status: 403 });
  });

  it("accepts Entra's short scp value when the configured scope is fully qualified", async () => {
    const fullScope = "api://reply-api-client/access_as_user";
    process.env.NEXT_PUBLIC_ENTRA_API_SCOPE = fullScope;

    const result = await validateEntraBearerToken(
      `Bearer ${signedToken({ scp: "access_as_user" })}`,
      { jwksUrl, requiredScope: fullScope },
    );

    expect(result).toMatchObject({ ok: true });
  });

  it("rejects a malformed JWT with 401", async () => {
    const result = await validateEntraBearerToken("Bearer not-a-jwt", {
      jwksUrl,
    });

    expect(result).toMatchObject({ ok: false, status: 401 });
  });
});
