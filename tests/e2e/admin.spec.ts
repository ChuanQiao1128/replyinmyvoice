import { createHmac } from "crypto";

import { expect, test } from "@playwright/test";
import type { BrowserContext } from "@playwright/test";

const sessionSecret = "playwright-admin-session-secret";
const adminEmail = "admin@example.test";
const userId = "6ee67a08-a662-4c2b-9225-dc4b18e3a73c";

function base64UrlJson(value: unknown) {
  return Buffer.from(JSON.stringify(value), "utf8").toString("base64url");
}

function signedSessionCookie(email: string) {
  const payload = base64UrlJson({
    email,
    exp: Math.floor(Date.now() / 1000) + 60 * 60,
    name: "Admin Tester",
    sub: email,
  });
  const signature = createHmac("sha256", sessionSecret)
    .update(payload)
    .digest("base64url");
  return `${payload}.${signature}`;
}

async function addSessionCookie(context: BrowserContext, email: string) {
  await context.addCookies([
    {
      httpOnly: true,
      name: "rimv_session",
      path: "/",
      sameSite: "Lax",
      url: "http://127.0.0.1:3000",
      value: signedSessionCookie(email),
    },
  ]);
}

function adminUsersPayload() {
  return {
    page: 1,
    pageSize: 25,
    totalCount: 2,
    totalPages: 1,
    users: [
      {
        costToDateUsd: 0.42,
        createdAt: "2026-05-30T10:00:00Z",
        creditRemaining: 14,
        email: "customer@example.test",
        externalAuthUserId: "external-customer-1",
        id: userId,
        reservedRewrites: 1,
        subscriptionStatus: "active",
        updatedAt: "2026-05-31T10:00:00Z",
        usedRewrites: 6,
      },
      {
        costToDateUsd: 0,
        createdAt: "2026-05-29T10:00:00Z",
        creditRemaining: 0,
        email: null,
        externalAuthUserId: "erased:test-account-1",
        id: "erased-user-id",
        reservedRewrites: 0,
        subscriptionStatus: "canceled",
        updatedAt: "2026-05-31T10:00:00Z",
        usedRewrites: 0,
      },
    ],
  };
}

function adminStatsPayload() {
  return {
    costToDateUsd: 1.25,
    creditRemaining: 14,
    freeUsers: 0,
    paidUsers: 1,
    paymentAmountTotal: 900,
    paymentCount: 1,
    refundReview: {
      flaggedUserCount: 2,
      refundAmountThreshold: 2500,
      refundCountThreshold: 3,
      totalRefundAmount: 4200,
      totalRefundCount: 4,
    },
    totalUsers: 1,
    usageReserved: 1,
    usageUsed: 6,
  };
}

function adminBillingSupportPayload() {
  return [
    {
      createdAt: "2026-05-31T10:00:00Z",
      externalAuthUserId: "external-customer-1",
      id: "2b0bf65e-e5fc-4df4-9397-05d7139a4225",
      message: "I was charged twice for the same rewrite pack.",
      relatedPaymentIntentId: "pi_test_admin_refund",
      resolvedAt: null,
      status: "open",
      type: "refund",
      updatedAt: "2026-05-31T10:00:00Z",
      userEmail: "customer@example.test",
      userId,
    },
  ];
}

function adminUserDetailPayload() {
  return {
    costToDateUsd: 0.42,
    createdAt: "2026-05-30T10:00:00Z",
    credits: [
      {
        amountConsumed: 1,
        amountGranted: 10,
        amountTotal: 900,
        currency: "nzd",
        expiresAt: "2026-08-30T10:00:00Z",
        grantedAt: "2026-05-30T10:00:00Z",
        id: "credit-1",
        paymentIntentId: "pi_test_admin_refund",
        remaining: 9,
        source: "PURCHASE",
        stripeEventId: "evt_1",
        sku: "quick-pack",
      },
    ],
    email: "customer@example.test",
    externalAuthUserId: "external-customer-1",
    id: userId,
    payments: [
      {
        amountTotal: 900,
        creditId: "credit-1",
        creditsConsumed: 1,
        creditsGranted: 10,
        creditsRemaining: 9,
        currency: "nzd",
        eventId: "evt_1",
        expiresAt: "2026-08-30T10:00:00Z",
        grantedAt: "2026-05-30T10:00:00Z",
        paymentIntentId: "pi_test_admin_refund",
        receiptUrl: "https://payments.example.test/receipt",
        sku: "quick-pack",
        source: "PURCHASE",
      },
    ],
    subscription: {
      currentPeriodEnd: "2026-06-30T00:00:00Z",
      status: "active",
      stripeCustomerId: "cus_test_customer",
      stripeSubscriptionId: null,
    },
    updatedAt: "2026-05-31T10:00:00Z",
    usage: [
      {
        createdAt: "2026-05-30T10:00:00Z",
        id: "usage-1",
        periodEnd: "2026-06-30T00:00:00Z",
        periodKey: "2026-06",
        periodStart: "2026-06-01T00:00:00Z",
        quota: 40,
        reserved: 1,
        updatedAt: "2026-05-31T10:00:00Z",
        used: 6,
      },
    ],
  };
}

test("admin can open a user detail", async ({ context, page }) => {
  await addSessionCookie(context, adminEmail);

  await page.route("**/api/admin/stats", async (route) => {
    await route.fulfill({ json: adminStatsPayload() });
  });
  await page.route("**/api/admin/users?page=*", async (route) => {
    await route.fulfill({ json: adminUsersPayload() });
  });
  await page.route("**/api/admin/billing-support-requests", async (route) => {
    await route.fulfill({ json: adminBillingSupportPayload() });
  });
  await page.route(`**/api/admin/users/${userId}`, async (route) => {
    await route.fulfill({ json: adminUserDetailPayload() });
  });

  await page.goto("/admin");

  await expect(page.getByRole("heading", { name: "Admin" })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Billing support queue" }),
  ).toBeVisible();
  await expect(page.getByText("I was charged twice")).toBeVisible();
  const refundReviewTile = page.getByText("Refund review").locator("..");
  await expect(refundReviewTile.getByText("2", { exact: true })).toBeVisible();
  await expect(page.getByText("customer@example.test")).toBeVisible();

  await page
    .locator("table")
    .nth(1)
    .getByRole("link", { name: /customer@example\.test/ })
    .click();

  await expect(page).toHaveURL(new RegExp(`/admin/users/${userId}`));
  await expect(
    page.getByRole("heading", { name: "customer@example.test" }),
  ).toBeVisible();
  await expect(page.getByText("Subscription")).toBeVisible();
  await expect(page.getByText("pi_test_admin_refund")).toBeVisible();
});

test("admin dashboard exposes promo navigation and user erase controls", async ({
  context,
  page,
}) => {
  await addSessionCookie(context, adminEmail);

  let deleteCalled = false;

  await page.route("**/api/admin/stats", async (route) => {
    await route.fulfill({ json: adminStatsPayload() });
  });
  await page.route("**/api/admin/users?page=*", async (route) => {
    await route.fulfill({ json: adminUsersPayload() });
  });
  await page.route("**/api/admin/billing-support-requests", async (route) => {
    await route.fulfill({ json: adminBillingSupportPayload() });
  });
  await page.route(`**/api/admin/users/${userId}`, async (route) => {
    if (route.request().method() === "DELETE") {
      deleteCalled = true;
      await route.fulfill({ status: 204 });
      return;
    }

    await route.fulfill({ json: adminUserDetailPayload() });
  });

  page.on("dialog", async (dialog) => {
    expect(dialog.message()).toBe(
      "Permanently erase this account? This anonymizes the user and removes their data. This cannot be undone.",
    );
    await dialog.accept();
  });

  await page.goto("/admin");

  await expect(page.getByRole("link", { name: "Promo codes" })).toHaveAttribute(
    "href",
    "/admin/promo-codes",
  );
  await expect(
    page.getByText("Inactive / Free = no active subscription (normal). Canceled = account erased."),
  ).toBeVisible();
  await expect(page.getByText("1 erased account hidden")).toBeVisible();
  await expect(page.getByText("erased:test-account-1")).toHaveCount(0);

  const usersTable = page.locator("table").nth(1);
  const customerRow = usersTable.getByRole("row", {
    name: /customer@example\.test/,
  });
  await customerRow.getByRole("button", { name: "Delete" }).click();

  expect(deleteCalled).toBe(true);
  await expect(customerRow).toHaveCount(0);
});

test("non-admin users are blocked from admin pages", async ({ context, page }) => {
  await addSessionCookie(context, "reader@example.test");

  await page.goto("/admin");

  await expect(
    page.getByRole("heading", { name: "Admin access required" }),
  ).toBeVisible();
  await expect(page.getByText("customer@example.test")).toHaveCount(0);
});
