import { createHmac } from "node:crypto";
import { createServer, type IncomingMessage, type Server } from "node:http";

import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const appUrl = "http://127.0.0.1:3001";
const azureMockPort = 45934;
const authSessionSecret = "playwright-auth-session-secret";
const sessionCookieName = "rimv_session";
const trialExpiresAt = "2026-08-31T00:00:00.000Z";

type PromoState = {
  redeemed: boolean;
  rewriteCount: number;
  trialRemaining: number;
};

function accountSummary(state: PromoState) {
  const hasTrial = state.redeemed && state.trialRemaining > 0;

  return {
    currentPeriodEnd: null,
    email: "casey@example.test",
    externalAuthUserId: "promo-user",
    promo: {
      eligible: !state.redeemed,
      hasRedeemed: state.redeemed,
      trialExpiresAt: state.redeemed ? trialExpiresAt : null,
      trialRemaining: state.trialRemaining,
    },
    subscriptionStatus: "none",
    usage: {
      exhausted: state.trialRemaining <= 0,
      periodEnd: null,
      periodKey: "free:lifetime",
      quota: 0,
      remaining: state.trialRemaining,
      reserved: 0,
      scope: "free",
      sources: hasTrial
        ? [
            {
              expiresAt: trialExpiresAt,
              expiresInDays: 90,
              label: "Promo",
              remaining: state.trialRemaining,
              source: "PROMO",
            },
          ]
        : [],
      used: 0,
    },
    userId: "promo-user",
  };
}

function rewriteResponse(index: number) {
  return {
    changeSummary: ["Kept the original facts and made the reply warmer."],
    naturalness: {
      changePoints: 42,
      draftAiLikePercent: 72,
      label: "lower",
      rewriteAiLikePercent: 30,
    },
    optimization: {
      internalStrategiesTried: 1,
      selectionStatus: "passed",
      userUsageCharged: 1,
    },
    rewrittenText: `Rewrite ${index}: Thanks for the update. I can make this work and will follow up with the details.`,
    riskNotes: [],
  };
}

async function readBody(request: IncomingMessage) {
  let body = "";
  for await (const chunk of request) {
    body += String(chunk);
  }
  return body;
}

async function startAzureMock(state: PromoState) {
  const server = createServer((request, response) => {
    function send(status: number, body: unknown) {
      response.writeHead(status, { "Content-Type": "application/json" });
      response.end(JSON.stringify(body));
    }

    if (request.method === "GET" && request.url === "/api/me") {
      send(200, accountSummary(state));
      return;
    }

    if (request.method === "POST" && request.url === "/api/rewrite") {
      void readBody(request).then(() => {
        if (state.trialRemaining <= 0) {
          send(402, { error: "Trial used." });
          return;
        }

        state.trialRemaining -= 1;
        state.rewriteCount += 1;
        send(200, rewriteResponse(state.rewriteCount));
      });
      return;
    }

    send(404, { error: "not_found" });
  });

  await new Promise<void>((resolve) => {
    server.listen(azureMockPort, "127.0.0.1", resolve);
  });

  return server;
}

async function closeServer(server: Server | undefined) {
  if (!server) {
    return;
  }

  await new Promise<void>((resolve, reject) => {
    server.close((error) => {
      if (error) {
        reject(error);
        return;
      }
      resolve();
    });
  });
}

async function addSignedInSession(context: BrowserContext) {
  const exp = Math.floor(Date.now() / 1000) + 60 * 60;
  const sessionCookie = createSignedCookieValue(
    {
      email: "casey@example.test",
      exp,
      name: "Casey Preview",
      sub: "promo-user",
    },
    authSessionSecret,
  );
  const accessCookie = createSignedCookieValue(
    {
      accessToken: "playwright-access-token",
      accessTokenExp: exp,
      exp,
    },
    authSessionSecret,
  );
  const accessMetaCookie = createSignedCookieValue(
    {
      chunks: 1,
      exp,
    },
    authSessionSecret,
  );

  await context.addCookies([
    {
      httpOnly: true,
      name: sessionCookieName,
      sameSite: "Lax",
      url: appUrl,
      value: sessionCookie,
    },
    {
      httpOnly: true,
      name: "rimv_access_0",
      sameSite: "Lax",
      url: appUrl,
      value: accessCookie,
    },
    {
      httpOnly: true,
      name: "rimv_access_meta",
      sameSite: "Lax",
      url: appUrl,
      value: accessMetaCookie,
    },
  ]);
}

function createSignedCookieValue(payload: unknown, secret: string) {
  const encodedPayload = Buffer.from(JSON.stringify(payload), "utf8").toString(
    "base64url",
  );
  const signature = createHmac("sha256", secret)
    .update(encodedPayload)
    .digest("base64url");
  return `${encodedPayload}.${signature}`;
}

async function installTurnstileStub(page: Page) {
  await page.addInitScript(() => {
    const widgetCallbacks = new Map<string, (token: string) => void>();
    const target = window as Window & typeof globalThis & {
      turnstile: {
        remove: (widgetId: string) => void;
        render: (
          container: HTMLElement,
          options: { callback: (token: string) => void },
        ) => string;
        reset: (widgetId: string) => void;
      };
    };
    target.turnstile = {
      remove(widgetId: string) {
        widgetCallbacks.delete(widgetId);
      },
      render(container: HTMLElement, options: { callback: (token: string) => void }) {
        const widgetId = `playwright-widget-${widgetCallbacks.size + 1}`;
        widgetCallbacks.set(widgetId, options.callback);
        const marker = document.createElement("div");
        marker.textContent = "Verification ready";
        marker.style.minHeight = "65px";
        marker.style.display = "grid";
        marker.style.placeItems = "center";
        container.appendChild(marker);
        window.setTimeout(() => options.callback("playwright-turnstile-token"), 0);
        return widgetId;
      },
      reset(widgetId: string) {
        const callback = widgetCallbacks.get(widgetId);
        if (callback) {
          window.setTimeout(() => callback("playwright-turnstile-token-reset"), 0);
        }
      },
    };
  });
}

async function routeAuth(page: Page, context: BrowserContext) {
  await page.route("**/api/auth/signup/start", async (route) => {
    const payload = route.request().postDataJSON() as { email?: string };
    if (payload.email?.toLowerCase().endsWith("@mailinator.com")) {
      await route.fulfill({
        contentType: "application/json",
        json: {
          error: "Use a long-term email address for your account.",
          ok: false,
        },
        status: 400,
      });
      return;
    }

    await route.fulfill({
      contentType: "application/json",
      json: {
        channelLabel: "c***@example.test",
        codeLength: 6,
        ok: true,
      },
      status: 200,
    });
  });

  await page.route("**/api/auth/signup/verify", async (route) => {
    await route.fulfill({
      contentType: "application/json",
      json: { ok: true },
      status: 200,
    });
  });

  await page.route("**/api/auth/signin", async (route) => {
    await addSignedInSession(context);
    await route.fulfill({
      contentType: "application/json",
      json: { ok: true },
      status: 200,
    });
  });
}

async function routeRedeem(page: Page, state: PromoState) {
  await page.route("**/api/promo/redeem", async (route) => {
    const payload = route.request().postDataJSON() as {
      code?: string;
      turnstileToken?: string;
    };
    expect(payload.turnstileToken).toMatch(/^playwright-turnstile-token/);

    if (payload.code?.trim().toUpperCase() === "TRIAL") {
      state.redeemed = true;
      state.trialRemaining = 3;
      await route.fulfill({
        contentType: "application/json",
        json: {
          alreadyRedeemed: false,
          creditsGranted: 3,
          expiresAt: trialExpiresAt,
          totalRemaining: 3,
        },
        status: 200,
      });
      return;
    }

    await route.fulfill({
      contentType: "application/json",
      json: { error: "invalid_code" },
      status: 422,
    });
  });
}

async function submitRewrite(page: Page, index: number) {
  await page.locator("#roughDraftReply").fill(
    `Please rewrite this customer reply number ${index} in a warm but direct style.`,
  );
  await page.getByRole("button", { name: "Rewrite" }).click();
  await expect(page.getByText(`Rewrite ${index}:`)).toBeVisible();
}

test.describe("promo full loop", () => {
  let state: PromoState;
  let azureMock: Server | undefined;

  test.beforeEach(async ({ context, page }) => {
    state = {
      redeemed: false,
      rewriteCount: 0,
      trialRemaining: 0,
    };
    azureMock = await startAzureMock(state);
    await installTurnstileStub(page);
    await routeAuth(page, context);
    await routeRedeem(page, state);
  });

  test.afterEach(async () => {
    await closeServer(azureMock);
  });

  test("signup, login, redeem, trial rewrites, and paywall", async ({ page }) => {
    await page.goto("/sign-up");
    await expect(page.getByTestId("signup-turnstile-widget")).toContainText(
      "Verification ready",
    );

    await page.getByLabel("Email address").fill("casey@mailinator.com");
    await page.getByLabel("Password").fill("long-enough-password");
    await page.getByRole("button", { name: "Create account" }).click();
    await expect(
      page.getByText("Use a long-term email address for your account."),
    ).toBeVisible();

    await expect(page.getByRole("button", { name: "Create account" })).toBeEnabled();
    await page.getByLabel("Email address").fill("casey@example.test");
    await page.getByRole("button", { name: "Create account" }).click();
    await expect(
      page.getByRole("heading", { name: "Enter the verification code" }),
    ).toBeVisible();
    await page.getByLabel("Verification code").fill("123456");
    await page.getByRole("button", { name: "Verify and continue" }).click();
    await expect(page).toHaveURL(/\/sign-in/);

    await page.getByLabel("Email address").fill("casey@example.test");
    await page.getByLabel("Password").fill("long-enough-password");
    await page.getByRole("button", { name: "Continue with email" }).click();
    await expect(
      page.getByRole("heading", { name: "Rewrite workspace" }),
    ).toBeVisible();
    await expect(page.getByText("You're out of rewrites.")).toBeVisible();

    await page.getByRole("button", { name: "Redeem code" }).first().click();
    await page.getByLabel("Code").fill("TRIAL");
    await page.getByRole("button", { name: "Redeem" }).click();
    await expect(
      page.getByRole("heading", { name: "Rewrite workspace" }),
    ).toBeVisible();
    await expect(page.getByText("3 of 3 trial rewrites remaining")).toBeVisible();

    await submitRewrite(page, 1);
    await submitRewrite(page, 2);
    await submitRewrite(page, 3);

    await page.goto("/app");
    await expect(page.getByText("You're out of rewrites.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Buy rewrites" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Buy rewrites" })).toHaveCount(0);
    expect(state.rewriteCount).toBe(3);
    expect(state.trialRemaining).toBe(0);
  });
});
