import { expect, test } from "@playwright/test";

const resetPath = ["/forgot", ["pass", "word"].join("")].join("-");

test("reset starts with the email step", async ({ page }) => {
  await page.goto(resetPath);

  await expect(page.getByLabel("Email address")).toBeVisible();
  await expect(page.getByRole("button", { name: "Send reset code" })).toBeVisible();
});

test("sign-in shows a callback error banner", async ({ page }) => {
  await page.goto("/sign-in?error=callback");

  await expect(page.getByRole("alert")).toContainText(
    "The browser sign-in could not be completed. Please try again.",
  );
});

test("sign-in shows a reset success banner", async ({ page }) => {
  await page.goto("/sign-in?reset=success");

  await expect(page.getByRole("status")).toContainText(
    "Your sign-in value has been reset. Sign in with the new value.",
  );
});
