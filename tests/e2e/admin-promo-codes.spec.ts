import { createHmac } from "node:crypto";
import { createServer, type Server } from "node:http";

import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const appUrl = "http://127.0.0.1:3001";
const azureMockPort = 45934;
const authSessionSecret = "playwright-auth-session-secret";
const sessionCookieName = "rimv_session";
const promoCodeId = "11111111-1111-4111-8111-111111111111";

type MockPromoCode = {
  code: string;
  createdAt: string;
  creditsGranted: number;
  displayCode: string;
  grantTtlDays: number;
  id: string;
  isActive: boolean;
  kind: string;
  maxRedemptionsGlobal: number | null;
  maxRedemptionsPerUser: number;
  redemptionCount: number;
  status: string;
  updatedAt: string;
  validFrom: string;
  validUntil: string;
};

type MockMode = "admin" | "non-admin";

function activePromoCode(input: Partial<MockPromoCode> = {}): MockPromoCode {
  const isActive = input.isActive ?? true;
  return {
    code: "SPRING2026",
    createdAt: "2026-06-01T00:00:00.000Z",
    creditsGranted: 3,
    displayCode: "SPRING-2026",
    grantTtlDays: 90,
    id: promoCodeId,
    isActive,
    kind: "TrialCredits",
    maxRedemptionsGlobal: 100,
    maxRedemptionsPerUser: 1,
    redemptionCount: 0,
    status: isActive ? "active" : "disabled",
    updatedAt: "2026-06-01T00:00:00.000Z",
    validFrom: "2026-06-01T00:00:00.000Z",
    validUntil: "2026-06-30T00:00:00.000Z",
    ...input,
  };
}

function problem(title: string, detail: string, status: number) {
  return {
    body: JSON.stringify({
      detail,
      status,
      title,
      type: "about:blank",
    }),
    headers: { "Content-Type": "application/problem+json" },
    status,
  };
}

async function startAzureMock(options: {
  getCodes: () => MockPromoCode[];
  getMode: () => MockMode;
  setCodes: (codes: MockPromoCode[]) => void;
}) {
  const server = createServer((request, response) => {
    function send(input: { body: string; headers: Record<string, string>; status: number }) {
      response.writeHead(input.status, input.headers);
      response.end(input.body);
    }

    if (options.getMode() === "non-admin" && request.url?.startsWith("/api/admin/")) {
      send(problem(
        "Admin access required",
        "The authenticated user is not allowed to access admin endpoints.",
        403,
      ));
      return;
    }

    if (request.method === "GET" && request.url === "/api/admin/promo-codes") {
      send({
        body: JSON.stringify({ promoCodes: options.getCodes() }),
        headers: { "Content-Type": "application/json" },
        status: 200,
      });
      return;
    }

    if (request.method === "POST" && request.url === "/api/admin/promo-codes") {
      let body = "";
      request.on("data", (chunk) => {
        body += String(chunk);
      });
      request.on("end", () => {
        const payload = JSON.parse(body) as { code?: string };
        const normalized = (payload.code ?? "").replace(/[\s-]/g, "").toUpperCase();
        if (options.getCodes().some((code) => code.code === normalized)) {
          send(problem(
            "Duplicate promo code",
            "A promo code with that normalized code already exists.",
            400,
          ));
          return;
        }

        const nextCode = activePromoCode({
          code: normalized,
          displayCode: payload.code ?? normalized,
        });
        options.setCodes([nextCode, ...options.getCodes()]);
        send({
          body: JSON.stringify(nextCode),
          headers: { "Content-Type": "application/json" },
          status: 200,
        });
      });
      return;
    }

    if (request.method === "GET" && request.url === `/api/admin/promo-codes/${promoCodeId}`) {
      const code = options.getCodes()[0] ?? activePromoCode();
      send({
        body: JSON.stringify({
          promoCode: code,
          stats: {
            activationRate: 0.5,
            dailyCurve: [
              { date: "2026-06-02", redemptions: 1 },
              { date: "2026-06-03", redemptions: 1 },
            ],
            distinctUsers: 2,
            ipHashClusters: [
              {
                distinctUsers: 2,
                firstRedeemedAt: "2026-06-02T00:00:00.000Z",
                ipHash: "hash_cluster_alpha",
                lastRedeemedAt: "2026-06-03T00:00:00.000Z",
                redemptions: 2,
              },
            ],
            totalRedemptions: 2,
          },
        }),
        headers: { "Content-Type": "application/json" },
        status: 200,
      });
      return;
    }

    if (
      request.method === "POST" &&
      request.url === `/api/admin/promo-codes/${promoCodeId}/disable`
    ) {
      const disabled = activePromoCode({ isActive: false, status: "disabled" });
      options.setCodes([disabled]);
      send({
        body: JSON.stringify(disabled),
        headers: { "Content-Type": "application/json" },
        status: 200,
      });
      return;
    }

    if (request.method === "POST" && request.url === "/api/promo/redeem") {
      const code = options.getCodes()[0];
      const redeemable = code?.isActive === true;
      send({
        body: JSON.stringify(redeemable
          ? {
              alreadyRedeemed: false,
              creditsGranted: 3,
              expiresAt: "2026-08-31T00:00:00.000Z",
              totalRemaining: 3,
            }
          : { error: "invalid_code" }),
        headers: { "Content-Type": "application/json" },
        status: redeemable ? 200 : 422,
      });
      return;
    }

    send({
      body: JSON.stringify({ error: "not_found" }),
      headers: { "Content-Type": "application/json" },
      status: 404,
    });
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

async function addSignedInSession(
  context: BrowserContext,
  email = "admin@example.test",
) {
  const exp = Math.floor(Date.now() / 1000) + 60 * 60;
  const sessionCookie = createSignedCookieValue(
    {
      email,
      exp,
      name: "Promo Admin",
      sub: "promo-admin",
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

async function fillCreateForm(page: Page) {
  await page.getByLabel("Code", { exact: true }).fill("SPRING2026");
  await page.getByLabel("Display code").fill("SPRING-2026");
  await page.getByLabel("Credits").fill("3");
  await page.getByLabel("TTL days").fill("90");
  await page.getByLabel("Valid from").fill("2026-06-01T10:00");
  await page.getByLabel("Valid until").fill("2026-06-30T10:00");
  await page.getByLabel("Global cap").fill("100");
  await page.getByLabel("Per-user cap").fill("1");
}

test.describe("admin promo codes", () => {
  test.describe.configure({ mode: "serial" });

  let codes: MockPromoCode[];
  let mode: MockMode;
  let azureMock: Server | undefined;

  test.beforeEach(async () => {
    codes = [];
    mode = "admin";
    azureMock = await startAzureMock({
      getCodes: () => codes,
      getMode: () => mode,
      setCodes: (nextCodes) => {
        codes = nextCodes;
      },
    });
  });

  test.afterEach(async () => {
    await closeServer(azureMock);
  });

  test("signed-out users are sent to sign in with the admin redirect", async ({
    page,
  }) => {
    await page.goto("/admin/promo-codes");

    await expect(page).toHaveURL(/\/sign-in\?redirectTo=%2Fadmin%2Fpromo-codes/);
  });

  test("non-admin users see a no-permission view", async ({ context, page }) => {
    mode = "non-admin";
    await addSignedInSession(context, "casey@example.test");

    await page.goto("/admin/promo-codes");

    await expect(
      page.getByRole("heading", { name: "Admin access required" }),
    ).toBeVisible();
    await expect(page.getByText("You do not have permission")).toBeVisible();
  });

  test("admins can create, see duplicate field errors, view stats, and disable", async ({
    context,
    page,
    request,
  }) => {
    await addSignedInSession(context);

    await page.goto("/admin/promo-codes");
    await expect(page.getByText("No promo codes yet.")).toBeVisible();

    await fillCreateForm(page);
    await page.getByRole("button", { name: "Create code" }).click();
    await expect(page.getByText("SPRING-2026")).toBeVisible();
    await expect(page.getByText("Active")).toBeVisible();

    await fillCreateForm(page);
    await page.getByRole("button", { name: "Create code" }).click();
    await expect(
      page.getByText("A promo code with that normalized code already exists."),
    ).toBeVisible();

    await page.getByRole("button", { name: "View stats for SPRING-2026" }).click();
    await expect(page.getByText("2 redemptions")).toBeVisible();
    await expect(page.getByText("2 distinct users")).toBeVisible();
    await expect(page.getByText("50% activation")).toBeVisible();
    await expect(page.getByText("hash_cluster_alpha")).toBeVisible();
    await expect(page.locator("body")).not.toContainText(/\b\d{1,3}(\.\d{1,3}){3}\b/);

    await page.screenshot({
      fullPage: true,
      path: "tests/e2e/screenshots/admin-promo-codes-desktop.png",
    });
    await page.setViewportSize({ height: 900, width: 390 });
    await page.screenshot({
      fullPage: true,
      path: "tests/e2e/screenshots/admin-promo-codes-mobile.png",
    });

    await page.getByRole("button", { name: "Disable SPRING-2026" }).click();
    await expect(page.getByText("Disabled")).toBeVisible();

    const response = await request.post(
      `http://127.0.0.1:${azureMockPort}/api/promo/redeem`,
      {
        data: {
          code: "SPRING-2026",
          turnstileToken: "playwright-turnstile-token",
        },
      },
    );
    expect(response.status()).toBe(422);
  });
});
