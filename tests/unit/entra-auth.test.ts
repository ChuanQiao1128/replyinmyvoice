import { describe, expect, it } from "vitest";

import {
  buildEntraAuthorizeUrl,
  buildEntraTokenRequestBody,
  createSignedCookieValue,
  verifySignedCookieValue,
} from "../../lib/entra-auth";

describe("Entra auth helpers", () => {
  it("builds a Google-directed authorization URL with the production callback", async () => {
    const url = await buildEntraAuthorizeUrl({
      authority: "https://replyinmyvoicecustomers.ciamlogin.com/tenant/v2.0",
      clientId: "frontend-client",
      redirectUri: "https://replyinmyvoice.com/auth/callback",
      state: "state-test",
      nonce: "nonce-test",
      codeVerifier: "verifier-test",
      domainHint: "google",
    });

    expect(url.origin).toBe("https://replyinmyvoicecustomers.ciamlogin.com");
    expect(url.pathname).toBe("/tenant/oauth2/v2.0/authorize");
    expect(url.searchParams.get("client_id")).toBe("frontend-client");
    expect(url.searchParams.get("redirect_uri")).toBe("https://replyinmyvoice.com/auth/callback");
    expect(url.searchParams.get("response_type")).toBe("code");
    expect(url.searchParams.get("scope")).toBe("openid profile email");
    expect(url.searchParams.get("domain_hint")).toBe("google");
    expect(url.searchParams.get("code_challenge_method")).toBe("S256");
    expect(url.searchParams.get("code_challenge")).toBeTruthy();
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
    expect(body.get("scope")).toBe("openid profile email");
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
});
