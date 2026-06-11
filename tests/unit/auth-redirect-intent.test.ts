import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { mockCookieStore } = vi.hoisted(() => ({
  mockCookieStore: {
    get: vi.fn(),
    set: vi.fn(),
  },
}));

vi.mock("next/headers", () => ({
  cookies: vi.fn(async () => mockCookieStore),
}));

import {
  buildAuthRedirectSearchParams,
  buildPostAuthRedirectPath,
  normalizeAuthRedirectParams,
  safeRedirectTo,
} from "../../lib/auth-redirect-intent";
import {
  createLoginRedirectUrl,
  decodeOAuthRedirectState,
  oauthStateCookieName,
  verifySignedCookieValue,
} from "../../lib/entra-auth";

const appUrl = "https://replyinmyvoice.com";
const authEnvName = "AUTH_SESSION_SECRET";
const cookieSigningValue = ["unit", "session", "signing", "value"].join("-");

beforeEach(() => {
  mockCookieStore.get.mockReset();
  mockCookieStore.set.mockReset();
  process.env[authEnvName] = cookieSigningValue;
  process.env.NEXT_PUBLIC_APP_URL = appUrl;
  process.env.NEXT_PUBLIC_ENTRA_API_SCOPE = "rewrite.use";
  process.env.NEXT_PUBLIC_ENTRA_AUTHORITY = "https://login.example.test/tenant/v2.0";
  process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID = "frontend-client";
});

afterEach(() => {
  delete process.env[authEnvName];
  delete process.env.NEXT_PUBLIC_APP_URL;
  delete process.env.NEXT_PUBLIC_ENTRA_API_SCOPE;
  delete process.env.NEXT_PUBLIC_ENTRA_AUTHORITY;
  delete process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID;
});

describe("auth redirect intent helpers", () => {
  it("allows only same-origin app and pricing destinations", () => {
    expect(safeRedirectTo("//evil.com")).toBe("/app");
    expect(safeRedirectTo("/a/../b")).toBe("/app");
    expect(safeRedirectTo("https://evil.com")).toBe("/app");
    expect(safeRedirectTo("/pricing")).toBe("/pricing");
    expect(safeRedirectTo("/app")).toBe("/app");
    expect(safeRedirectTo("/app/keys")).toBe("/app/keys");
  });

  it("drops unknown SKU values while preserving known buy intent", () => {
    const normalized = normalizeAuthRedirectParams({
      intent: "buy",
      redirectTo: "/pricing",
      sku: "unknown_pack",
    });
    const params = buildAuthRedirectSearchParams(normalized);

    expect(normalized).toEqual({
      intent: "buy",
      redirectTo: "/pricing",
    });
    expect(params.get("intent")).toBe("buy");
    expect(params.has("sku")).toBe(false);
  });

  it("appends known buy intent and SKU to the post-auth redirect path", () => {
    expect(buildPostAuthRedirectPath({
      intent: "buy",
      redirectTo: "/pricing",
      sku: "value_pack",
    })).toBe("/pricing?intent=buy&sku=value_pack");
  });

  it("encodes sanitized redirect intent details in OAuth state", async () => {
    const target = await createLoginRedirectUrl("/pricing", {
      intent: "buy",
      loginHint: "casey@example.com",
      sku: "quick_pack",
    });

    const targetUrl = new URL(target);
    const encodedState = targetUrl.searchParams.get("state");
    expect(encodedState).toBeTruthy();
    expect(targetUrl.searchParams.get("login_hint")).toBe("casey@example.com");

    const decodedState = decodeOAuthRedirectState(encodedState ?? "");
    expect(decodedState).toMatchObject({
      intent: "buy",
      redirectTo: "/pricing",
      sku: "quick_pack",
    });
    expect(decodedState?.csrf).toBeTruthy();

    const [, cookieValue] = mockCookieStore.set.mock.calls.find(
      ([name]) => name === oauthStateCookieName,
    )!;
    const signedState = await verifySignedCookieValue<Record<string, unknown>>(
      cookieValue,
      cookieSigningValue,
    );
    expect(signedState).toMatchObject({
      intent: "buy",
      redirectTo: "/pricing",
      sku: "quick_pack",
      state: encodedState,
    });
  });
});
