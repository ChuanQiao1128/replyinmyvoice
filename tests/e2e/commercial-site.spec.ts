import { expect, test } from "@playwright/test";

test("landing page carries commercial trust and company attribution", async ({
  page,
}) => {
  await page.goto("/");

  await expect(
    page.locator("footer").getByText(/Operated by TimeAwake Ltd\. Built/),
  ).toBeVisible();
  const footer = page.locator("footer");
  await expect(
    footer.getByRole("link", { name: "Privacy", exact: true }),
  ).toBeVisible();
  await expect(
    footer.getByRole("link", { name: "Terms", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("Built for practical replies")).toBeVisible();
  await expect(page.getByText("Pick quick context")).toBeVisible();
  await expect(page.getByText("Choose a tone preset")).toBeVisible();
  await expect(footer.getByRole("link", { name: "Contact", exact: true })).toHaveAttribute(
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
