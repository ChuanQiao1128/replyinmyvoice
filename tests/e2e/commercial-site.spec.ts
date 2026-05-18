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
  await expect(page.getByText("Pick quick context")).toBeVisible();
  await expect(page.getByText("Choose a tone preset")).toBeVisible();
  await expect(page.getByRole("link", { name: "Contact" })).toHaveAttribute(
    "href",
    "mailto:info@timeawake.co.nz",
  );
});

test("privacy page explains local history and database storage boundaries", async ({
  page,
}) => {
  await page.goto("/privacy");

  await expect(page.getByRole("heading", { name: "Privacy" })).toBeVisible();
  await expect(
    page.getByText("Reply content and quality improvement"),
  ).toBeVisible();
  await expect(
    page.getByText("may store submitted message context"),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Local history" }),
  ).toBeVisible();
  await expect(page.getByText("TimeAwake Ltd.", { exact: true })).toBeVisible();
});
