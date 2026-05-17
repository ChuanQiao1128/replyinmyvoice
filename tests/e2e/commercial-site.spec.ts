import { expect, test } from "@playwright/test";

test("landing page carries commercial trust and company attribution", async ({
  page,
}) => {
  await page.goto("/");

  await expect(
    page.locator("footer").getByText(/Operated by TimeAwake Ltd\. Built/),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Privacy" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Terms" })).toBeVisible();
  await expect(page.getByText("Built for real communication workflows")).toBeVisible();
});

test("privacy page explains local history and database storage boundaries", async ({
  page,
}) => {
  await page.goto("/privacy");

  await expect(page.getByRole("heading", { name: "Privacy" })).toBeVisible();
  await expect(
    page.getByText("Reply content is not saved to our database"),
  ).toBeVisible();
  await expect(page.getByText("TimeAwake Ltd.", { exact: true })).toBeVisible();
});
