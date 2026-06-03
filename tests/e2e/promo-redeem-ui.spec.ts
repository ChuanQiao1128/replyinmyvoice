import { createServer, type Server } from "node:http";
import { createHmac } from "node:crypto";

import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const appUrl = "http://127.0.0.1:3001";
const azureMockPort = 45934;
const authSessionSecret = "playwright-auth-session-secret";
const sessionCookieName = "rimv_session";
const trialExpiresAt = "2026-08-31T00:00:00.000Z";

type MockAccountState = "new" | "trial" | "exhausted" | "paidExhausted";

function accountSummary(state: MockAccountState) {
  const trialRemaining = state === "trial" ? 3 : 0;
  const hasRedeemed = state !== "new";
  const paid = state === "paidExhausted";

  return {
    currentPeriodEnd: null,
    email: "promo-user@example.test",
    externalAuthUserId: "promo-user",
    promo: {
      eligible: !hasRedeemed,
      hasRedeemed,
      trialExpiresAt: hasRedeemed ? trialExpiresAt : null,
      trialRemaining,
    },
    subscriptionStatus: paid ? "active" : "none",
    usage: {
      exhausted: trialRemaining <= 0,
      periodEnd: null,
      periodKey: paid ? "paid:2026-06" : "free:lifetime",
      quota: paid ? 90 : 0,
      remaining: paid ? 0 : trialRemaining,
      reserved: 0,
      scope: paid ? "paid" : "free",
      sources:
        trialRemaining > 0
          ? [
              {
                expiresAt: trialExpiresAt,
                expiresInDays: 90,
                label: "Promo",
                remaining: trialRemaining,
                source: "PROMO",
              },
            ]
          : [],
      used: 0,
    },
    userId: "promo-user",
  };
}

async function startAzureMock(getState: () => MockAccountState) {
  const server = createServer((request, response) => {
    if (request.method === "GET" && request.url === "/api/me") {
      response.writeHead(200, { "Content-Type": "application/json" });
      response.end(JSON.stringify(accountSummary(getState())));
      return;
    }

    response.writeHead(404, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ error: "not_found" }));
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
  const sessionCookie = await createSignedCookieValue(
    {
      email: "promo-user@example.test",
      exp,
      name: "Promo User",
      sub: "promo-user",
    },
    authSessionSecret,
  );
  const accessCookie = await createSignedCookieValue(
    {
      accessToken: "playwright-access-token",
      accessTokenExp: exp,
      exp,
    },
    authSessionSecret,
  );
  const accessMetaCookie = await createSignedCookieValue(
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

async function routeRedeem(page: Page, setState: (state: MockAccountState) => void) {
  await page.route("**/api/promo/redeem", async (route) => {
    const payload = route.request().postDataJSON() as { code?: string };
    const code = payload.code?.trim().toUpperCase();

    if (code === "TRIAL") {
      setState("trial");
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

    const errorByCode: Record<string, { error: string; status: number }> = {
      EXPIRED: { error: "code_expired", status: 422 },
      FULL: { error: "code_exhausted", status: 409 },
      FAST: { error: "ip_velocity", status: 429 },
      USED: { error: "already_redeemed", status: 409 },
    };
    const result = errorByCode[code ?? ""] ?? { error: "invalid_code", status: 422 };
    await route.fulfill({
      contentType: "application/json",
      json: { error: result.error },
      status: result.status,
    });
  });
}

test.describe("promo redeem UI", () => {
  test.describe.configure({ mode: "serial" });

  let state: MockAccountState;
  let azureMock: Server | undefined;

  test.beforeEach(async ({ context, page }) => {
    state = "new";
    azureMock = await startAzureMock(() => state);
    await addSignedInSession(context);
    await installTurnstileStub(page);
    await routeRedeem(page, (nextState) => {
      state = nextState;
    });
  });

  test.afterEach(async () => {
    await closeServer(azureMock);
  });

  test("new signed-in users land on the workspace with redeem available", async ({
    page,
  }) => {
    await page.goto("/app");

    await expect(
      page.getByRole("heading", { name: "Rewrite workspace" }),
    ).toBeVisible();
    await expect(page.locator("#roughDraftReply")).toBeVisible();
    await expect(page.getByText("You have 0 rewrites.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Redeem code" })).toHaveCount(2);
    await expect(page.getByRole("link", { name: "Buy rewrites" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Redeem your code" })).toHaveCount(
      0,
    );
  });

  test("redeem success refetches account state and closes the modal", async ({
    page,
  }) => {
    await page.goto("/app");
    await page.getByRole("button", { name: "Redeem code" }).first().click();
    await expect(page.getByRole("heading", { name: "Redeem your code" })).toBeVisible();
    await expect(page.getByTestId("turnstile-widget")).toContainText(
      "Verification ready",
    );
    await page.getByLabel("Code").fill("TRIAL");
    await expect(page.getByRole("button", { name: "Redeem" })).toBeEnabled();
    await page.getByRole("button", { name: "Redeem" }).click();

    await expect(
      page.getByRole("heading", { name: "Rewrite workspace" }),
    ).toBeVisible();
    await expect(page.getByText("3 of 3 trial rewrites remaining")).toBeVisible();
    await expect(page.getByText(/expire in \d+ days/)).toBeVisible();
    await expect(page.getByRole("heading", { name: "Redeem your code" })).toHaveCount(
      0,
    );
  });

  test("redeem errors stay inline in the modal", async ({ page }) => {
    await page.goto("/app");
    await page.getByRole("button", { name: "Redeem code" }).first().click();

    const cases = [
      ["NOPE", "That code is not valid. Check it and try again."],
      ["EXPIRED", "That code has expired."],
      ["USED", "This account has already redeemed a trial code."],
      ["FULL", "That code has no trial credits left."],
      ["FAST", "Too many redemption attempts from this network. Try again later."],
    ] as const;

    for (const [code, message] of cases) {
      await page.getByLabel("Code").fill(code);
      await expect(page.getByRole("button", { name: "Redeem" })).toBeEnabled();
      await page.getByRole("button", { name: "Redeem" }).click();
      await expect(page.getByText(message)).toBeVisible();
    }
  });

  test("exhausted trial users see the buy paywall", async ({ page }) => {
    state = "exhausted";
    await page.goto("/app");

    await expect(
      page.getByRole("heading", { name: "Rewrite workspace" }),
    ).toBeVisible();
    await expect(page.getByText("You have 0 rewrites.")).toBeVisible();
    await expect(page.getByRole("link", { name: "Buy rewrites" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Redeem your code" })).toHaveCount(
      0,
    );
  });

  test("paid users out of monthly quota see an in-workspace buy and manage nudge", async ({
    page,
  }) => {
    state = "paidExhausted";
    await page.goto("/app");

    await expect(
      page.getByRole("heading", { name: "Rewrite workspace" }),
    ).toBeVisible();
    await expect(
      page.getByText("Your monthly rewrite quota has been used for this billing period."),
    ).toBeVisible();
    await expect(page.getByRole("link", { name: "Buy rewrites" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Manage billing" })).toHaveCount(2);
    await expect(page.getByRole("heading", { name: "Your monthly limit has been reached." })).toHaveCount(
      0,
    );
  });

  test("mobile redeem modal has no horizontal overflow and keeps verification visible", async ({
    page,
  }) => {
    await page.setViewportSize({ height: 740, width: 375 });
    await page.goto("/app");
    await page.getByRole("button", { name: "Redeem code" }).first().click();

    await expect(page.getByTestId("turnstile-widget")).toContainText(
      "Verification ready",
    );
    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
    expect(hasHorizontalOverflow).toBe(false);
  });
});
