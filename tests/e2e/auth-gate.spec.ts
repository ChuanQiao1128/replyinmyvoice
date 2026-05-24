import { expect, test } from "@playwright/test";

test("signed-out users are sent to sign in before opening the workspace", async ({
  page,
}) => {
  await page.goto("/app");
  await expect(page).toHaveURL(/sign-in/);
});

test("sign-in offers email verification code and Google options", async ({
  page,
}) => {
  await page.goto("/sign-in");

  await expect(page.getByText("Email code sign-in", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Email address")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Continue with email code" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Continue with Google" }),
  ).toBeVisible();
});

test("rewrite API rejects signed-out requests", async ({ request }) => {
  const response = await request.post("/api/rewrite", {
    data: {
      roughDraftReply:
        "This is a draft response that should be long enough for validation.",
      tone: "warm",
    },
  });

  expect([401, 307, 403]).toContain(response.status());
});
