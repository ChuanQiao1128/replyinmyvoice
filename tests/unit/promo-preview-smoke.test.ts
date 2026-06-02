import { describe, expect, it } from "vitest";

import {
  buildPreviewEnvFile,
  buildPreviewLaunchCommand,
  buildPreviewWranglerConfig,
  buildPromoRedeemCases,
  createPreviewSessionCookieHeader,
  validatePreviewSmokeEnv,
} from "../../scripts/promo-preview-smoke";

describe("promo preview smoke helpers", () => {
  it("requires Turnstile test-key env without exposing values in the launch command", () => {
    const validation = validatePreviewSmokeEnv({
      NEXT_PUBLIC_TURNSTILE_SITE_KEY: "",
      TURNSTILE_SECRET_KEY: "",
    });

    expect(validation.ok).toBe(false);
    if (validation.ok === false) {
      expect(validation.missing).toEqual([
        "NEXT_PUBLIC_TURNSTILE_SITE_KEY",
        "TURNSTILE_SECRET_KEY",
      ]);
    }

    const command = buildPreviewLaunchCommand({
      configPath: "/tmp/wrangler.promo-preview.jsonc",
      port: 8789,
    });

    expect(command.command).toBe("npm");
    expect(command.args).toContain("cf:preview");
    expect(command.args).toContain("--config");
    expect(command.args).toContain("/tmp/wrangler.promo-preview.jsonc");
    expect(command.args.join(" ")).not.toContain("turnstile-value");
  });

  it("builds a temporary preview env file with the mock backend and runtime guards", () => {
    const content = buildPreviewEnvFile({
      appUrl: "http://127.0.0.1:8789",
      authSessionSecret: "auth-value",
      azureApiBaseUrl: "http://127.0.0.1:45935",
      promoProxySharedSecret: "proxy-value",
      turnstileSecretKey: "turnstile-value",
      turnstileSiteKey: "site-value",
    });

    expect(content).toContain("NEXT_PUBLIC_APP_URL=http://127.0.0.1:8789");
    expect(content).toContain(
      "NEXT_PUBLIC_AZURE_API_BASE_URL=http://127.0.0.1:45935",
    );
    expect(content).toContain("AUTH_SESSION_SECRET=auth-value");
    expect(content).toContain("PROMO_PROXY_SHARED_SECRET=proxy-value");
    expect(content).toContain("NEXT_PUBLIC_TURNSTILE_SITE_KEY=site-value");
    expect(content).toContain("TURNSTILE_SECRET_KEY=turnstile-value");
  });

  it("builds a temporary Wrangler config with smoke-only runtime vars", () => {
    const config = buildPreviewWranglerConfig({
      baseConfig: {
        assets: {
          binding: "ASSETS",
          directory: ".open-next/assets",
        },
        compatibility_date: "2026-05-17",
        compatibility_flags: ["nodejs_compat"],
        main: "worker.js",
        name: "replyinmyvoice-app",
        vars: {
          NEXT_PRIVATE_MINIMAL_MODE: "1",
        },
      },
      previewEnv: {
        appUrl: "http://127.0.0.1:8789",
        authSessionSecret: "auth-value",
        azureApiBaseUrl: "http://127.0.0.1:45935",
        promoProxySharedSecret: "proxy-value",
        turnstileSecretKey: "turnstile-value",
        turnstileSiteKey: "site-value",
      },
      projectRoot: "/repo",
    });

    expect(config.main).toBe("/repo/worker.js");
    expect(config.assets).toMatchObject({
      directory: "/repo/.open-next/assets",
    });
    expect(config.vars).toMatchObject({
      AUTH_SESSION_SECRET: "auth-value",
      NEXT_PUBLIC_APP_URL: "http://127.0.0.1:8789",
      NEXT_PUBLIC_AZURE_API_BASE_URL: "http://127.0.0.1:45935",
      NEXT_PUBLIC_TURNSTILE_SITE_KEY: "site-value",
      PROMO_PROXY_SHARED_SECRET: "proxy-value",
      TURNSTILE_SECRET_KEY: "turnstile-value",
    });
    expect(config.routes).toEqual([]);
  });

  it("creates signed session cookies for authenticated preview API calls", () => {
    const header = createPreviewSessionCookieHeader({
      accessToken: "preview-access-token",
      authSessionSecret: "auth-value",
      email: "promo-preview@example.test",
      expiresAtEpochSeconds: 1_780_000_000,
      name: "Promo Preview",
      subject: "promo-preview-user",
    });

    expect(header).toContain("rimv_session=");
    expect(header).toContain("rimv_access_0=");
    expect(header).toContain("rimv_access_meta=");
  });

  it("covers the invalid, expired, and already-redeemed promo states", () => {
    expect(buildPromoRedeemCases()).toEqual([
      { code: "PROMO_INVALID", expectedError: "invalid_code", expectedStatus: 422 },
      { code: "PROMO_EXPIRED", expectedError: "code_expired", expectedStatus: 422 },
      {
        code: "PROMO_ALREADY",
        expectedError: "already_redeemed",
        expectedStatus: 409,
      },
    ]);
  });
});
