import { createHmac } from "crypto";

import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const appUrl = "http://127.0.0.1:3000";
const sessionSecret = "playwright-admin-session-secret";

function base64UrlJson(value: unknown) {
  return Buffer.from(JSON.stringify(value), "utf8").toString("base64url");
}

function signedSessionCookie() {
  const payload = base64UrlJson({
    email: "developer@example.test",
    exp: Math.floor(Date.now() / 1000) + 60 * 60,
    name: "Developer Tester",
    sub: "developer-user",
  });
  const signature = createHmac("sha256", sessionSecret)
    .update(payload)
    .digest("base64url");
  return `${payload}.${signature}`;
}

async function addSessionCookie(context: BrowserContext) {
  await context.addCookies([
    {
      httpOnly: true,
      name: "rimv_session",
      path: "/",
      sameSite: "Lax",
      url: appUrl,
      value: signedSessionCookie(),
    },
  ]);
}

function accountPayload() {
  return {
    currentPeriodEnd: "2026-06-30T00:00:00Z",
    email: "developer@example.test",
    externalAuthUserId: "developer-user",
    subscriptionStatus: "active",
    usage: {
      exhausted: false,
      periodEnd: "2026-06-30T00:00:00Z",
      periodKey: "paid:2026-06",
      quota: 90,
      remaining: 72,
      reserved: 0,
      scope: "paid",
      used: 18,
    },
    userId: "developer-user",
  };
}

function billingHistoryPayload() {
  return [
    {
      amount: 250,
      currency: "nzd",
      date: "2026-06-01T09:00:00Z",
      description: "Quick Pack",
      receiptUrl: "https://payments.example.test/receipt/quick-pack",
      status: "paid",
      type: "pack",
    },
    {
      amount: 1990,
      currency: "nzd",
      date: "2026-06-02T09:00:00Z",
      description: "Pro/API invoice for June",
      hostedInvoiceUrl: "https://payments.example.test/invoice/pro-api",
      status: "open",
      type: "subscription",
    },
  ];
}

async function mockBillingRoutes(page: Page) {
  await page.route("**/api/keys", async (route) => {
    await route.fulfill({ json: [] });
  });
  await page.route("**/api/me", async (route) => {
    await route.fulfill({ json: accountPayload() });
  });
  await page.route("**/api/me/billing/history", async (route) => {
    await route.fulfill({ json: billingHistoryPayload() });
  });
}

async function openBillingTab(page: Page, context: BrowserContext) {
  await addSessionCookie(context);
  await mockBillingRoutes(page);

  await page.goto("/developers/keys");
  await page.getByRole("tab", { name: "Billing" }).click();
}

test("developer billing tab renders mocked history and opens the portal route", async ({
  context,
  page,
}) => {
  let portalCalled = false;
  let exportCalled = false;
  await page.route("**/api/me/billing/export", async (route) => {
    exportCalled = true;
    await route.fulfill({
      body: "date,type,description\n2026-06-01T09:00:00Z,pack,Quick Pack",
      contentType: "text/csv",
    });
  });
  await page.route("**/api/stripe/portal", async (route) => {
    portalCalled = true;
    await route.fulfill({
      json: { url: "https://payments.example.test/portal/session" },
    });
  });
  await page.route("https://payments.example.test/portal/session", async (route) => {
    await route.fulfill({
      body: "<!doctype html><title>Payment management</title>",
      contentType: "text/html",
    });
  });

  await openBillingTab(page, context);

  await expect(page.getByRole("heading", { name: "Plan and payments" })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Unified billing history" }),
  ).toBeVisible();
  await expect(page.getByText("Pro/API", { exact: true })).toBeVisible();
  await expect(page.getByText("72 of 90 rewrites remaining")).toBeVisible();
  await expect(page.getByText("Quick Pack")).toBeVisible();
  await expect(page.getByText("Pro/API invoice for June")).toBeVisible();
  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export CSV" }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe("billing-history.csv");
  await expect.poll(() => exportCalled).toBe(true);
  await expect(page.getByRole("link", { name: "View receipt" })).toHaveAttribute(
    "href",
    "https://payments.example.test/receipt/quick-pack",
  );
  await expect(page.getByRole("link", { name: "View invoice" })).toHaveAttribute(
    "href",
    "https://payments.example.test/invoice/pro-api",
  );

  await page.getByRole("button", { name: "Manage payment method" }).click();
  await expect.poll(() => portalCalled).toBe(true);
});

test("developer billing tab stays within the mobile viewport", async ({
  context,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await openBillingTab(page, context);

  await expect(
    page.getByRole("heading", { name: "Unified billing history" }),
  ).toBeVisible();

  const horizontalOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(horizontalOverflow).toBeLessThanOrEqual(4);
});
