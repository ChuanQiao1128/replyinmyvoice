import { expect, test } from "@playwright/test";

test("signed-out users are sent to sign in before opening the workspace", async ({
  page,
}) => {
  await page.goto("/app");
  await expect(page).toHaveURL(/sign-in/);
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
