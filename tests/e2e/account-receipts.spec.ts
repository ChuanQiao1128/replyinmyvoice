import { expect, test } from "@playwright/test";

function accountPayload() {
  return {
    currentPeriodEnd: null,
    email: "buyer@example.test",
    externalAuthUserId: "external-buyer-1",
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
      receiptUrl: "https://payments.example.test/receipt/quick-pack",
      remaining: 9,
      sku: "quick_pack",
    },
  ];
}

test("account page shows purchase history with receipt link", async ({ page }) => {
  await page.route("**/api/me", async (route) => {
    await route.fulfill({ json: accountPayload() });
  });
  await page.route("**/api/me/payments", async (route) => {
    await route.fulfill({ json: paymentsPayload() });
  });

  await page.goto("/app/account");

  await expect(page.getByRole("heading", { name: "Your account" })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Receipts / Purchase history" }),
  ).toBeVisible();
  await expect(page.getByText("Quick Pack")).toBeVisible();
  await expect(page.getByText("NZD 2.50")).toBeVisible();
  await expect(page.getByText("9 remaining")).toBeVisible();

  const receipt = page.getByRole("link", { name: "View receipt" });
  await expect(receipt).toBeVisible();
  await expect(receipt).toHaveAttribute(
    "href",
    "https://payments.example.test/receipt/quick-pack",
  );
});
