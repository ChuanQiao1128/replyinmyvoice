import { createHmac } from "node:crypto";

import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const appUrl = "http://127.0.0.1:3000";
const sessionSecret = "playwright-admin-session-secret";

function base64UrlJson(value: unknown) {
  return Buffer.from(JSON.stringify(value), "utf8").toString("base64url");
}

function signedCookieValue(value: unknown) {
  const payload = base64UrlJson(value);
  const signature = createHmac("sha256", sessionSecret)
    .update(payload)
    .digest("base64url");
  return `${payload}.${signature}`;
}

function unsignedJwt(claims: Record<string, unknown>) {
  return [
    base64UrlJson({ alg: "none", typ: "JWT" }),
    base64UrlJson(claims),
    "signature",
  ].join(".");
}

async function addSignedInSession(
  context: BrowserContext,
  input: { email: string; name: string; userId: string },
) {
  const exp = Math.floor(Date.now() / 1000) + 60 * 60;
  const accessToken = unsignedJwt({
    email: input.email,
    exp,
    name: input.name,
    sub: input.userId,
  });

  await context.addCookies([
    {
      httpOnly: true,
      name: "rimv_session",
      path: "/",
      sameSite: "Lax",
      url: appUrl,
      value: signedCookieValue({
        email: input.email,
        exp,
        name: input.name,
        sub: input.userId,
      }),
    },
    {
      httpOnly: true,
      name: "rimv_access_0",
      path: "/",
      sameSite: "Lax",
      url: appUrl,
      value: signedCookieValue({
        accessToken,
        accessTokenExp: exp,
        exp,
      }),
    },
    {
      httpOnly: true,
      name: "rimv_access_meta",
      path: "/",
      sameSite: "Lax",
      url: appUrl,
      value: signedCookieValue({
        chunks: 1,
        exp,
      }),
    },
  ]);
}

function accountPayload() {
  return {
    currentPeriodEnd: null,
    email: "buyer@example.test",
    externalAuthUserId: "external-buyer-1",
    paymentGraceEndsAt: null,
    subscriptionStatus: "inactive",
    usage: {
      exhausted: false,
      periodEnd: null,
      periodKey: "free:lifetime",
      quota: 3,
      remaining: 3,
      reserved: 0,
      scope: "free",
      used: 0,
    },
    userId: "user_receipts",
  };
}

function paymentsPayload() {
  return [
    {
      amount: 250,
      currency: "nzd",
      date: "2026-05-30T10:00:00Z",
      expiry: "2026-08-30T10:00:00Z",
      paymentIntentId: "pi_quick_pack",
      receiptUrl: "https://payments.example.test/receipt/quick-pack",
      remaining: 9,
      sku: "quick_pack",
    },
  ];
}

function supportRequestsPayload() {
  return [
    {
      createdAt: "2026-06-02T10:00:00Z",
      id: "support-quick-pack",
      message: "Please check this purchase.",
      relatedPaymentIntentId: "pi_quick_pack",
      resolvedAt: null,
      status: "open",
      type: "billing-question",
      updatedAt: "2026-06-02T10:00:00Z",
      userId: "user_receipts",
    },
  ];
}

async function mockAccountRoutes(page: Page) {
  await page.route("**/api/me", async (route) => {
    await route.fulfill({ json: accountPayload() });
  });
  await page.route("**/api/me/payments", async (route) => {
    await route.fulfill({ json: paymentsPayload() });
  });
  await page.route("**/api/billing-support-requests", async (route) => {
    await route.fulfill({ json: supportRequestsPayload() });
  });
}

test("account page shows purchase support details and export before delete", async ({
  context,
  page,
}) => {
  await addSignedInSession(context, {
    email: "buyer@example.test",
    name: "Buyer Tester",
    userId: "external-buyer-1",
  });
  await mockAccountRoutes(page);

  let historyExportCalled = false;
  await page.route("**/api/me/rewrites?page=1&pageSize=1000", async (route) => {
    historyExportCalled = true;
    await route.fulfill({
      contentType: "application/json",
      json: {
        items: [
          {
            attemptId: "attempt-1",
            createdAt: "2026-06-11T00:00:00Z",
            preview: "Saved rewrite",
            status: "Succeeded",
          },
        ],
        page: 1,
        pageSize: 1000,
        totalCount: 1,
      },
    });
  });

  await page.goto("/app/account");

  await expect(page.getByRole("heading", { name: "Account" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Purchase history" })).toBeVisible();
  await expect(page.getByText("Quick Pack").first()).toBeVisible();
  await expect(page.getByText("NZD 2.50")).toBeVisible();
  await expect(page.getByText("9 remaining").first()).toBeVisible();
  await expect(page.getByText("Please check this purchase.")).toBeVisible();
  await expect(page.getByText("pi_quick_pack")).toHaveCount(0);

  const receipt = page.getByRole("link", { name: "View receipt" });
  await expect(receipt).toBeVisible();
  await expect(receipt).toHaveAttribute(
    "href",
    "https://payments.example.test/receipt/quick-pack",
  );

  await page.getByRole("button", { name: "Delete account" }).click();
  const dialog = page.getByRole("dialog", { name: "Delete this account?" });
  await expect(dialog).toBeVisible();
  await expect(
    dialog.getByRole("button", { name: "Export my rewrite history" }),
  ).toBeVisible();
  await expect(dialog.getByLabel("Confirmation phrase")).toBeVisible();

  const downloadPromise = page.waitForEvent("download");
  await dialog.getByRole("button", { name: "Export my rewrite history" }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(
    /^replyinmyvoice-rewrite-history-\d{4}-\d{2}-\d{2}\.json$/,
  );
  expect(historyExportCalled).toBe(true);
});
