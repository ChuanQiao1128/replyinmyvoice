import { expect, test, type Page } from "@playwright/test";

async function installTurnstileStub(
  page: Page,
  options: { token?: string | null } = {},
) {
  const token = options.token === undefined
    ? "playwright-turnstile-token"
    : options.token;

  await page.addInitScript((resolvedToken) => {
    const target = window as Window & typeof globalThis & {
      turnstile: {
        remove: (widgetId: string) => void;
        render: (
          container: HTMLElement,
          options: { callback: (token: string) => void },
        ) => string;
        reset: (widgetId: string) => void;
      };
    };
    const callbacks = new Map<string, (token: string) => void>();

    target.turnstile = {
      remove(widgetId: string) {
        callbacks.delete(widgetId);
      },
      render(container: HTMLElement, renderOptions: { callback: (token: string) => void }) {
        const widgetId = `signup-widget-${callbacks.size + 1}`;
        callbacks.set(widgetId, renderOptions.callback);
        const marker = document.createElement("div");
        marker.textContent = "Verification ready";
        marker.style.minHeight = "65px";
        marker.style.display = "grid";
        marker.style.placeItems = "center";
        container.appendChild(marker);

        if (resolvedToken) {
          window.setTimeout(() => renderOptions.callback(resolvedToken), 0);
        }
        return widgetId;
      },
      reset(widgetId: string) {
        const callback = callbacks.get(widgetId);
        if (callback && resolvedToken) {
          window.setTimeout(() => callback(resolvedToken), 0);
        }
      },
    };
  }, token);
}

async function fillSignupForm(page: Page, email = "casey@example.com") {
  await page.getByLabel("Email address").fill(email);
  await page.getByLabel("Password", { exact: true }).fill("signup value 123");
}

test("signed-out users are sent to sign in before opening the workspace", async ({
  page,
}) => {
  await page.goto("/app");
  await expect(page).toHaveURL(/sign-in/);
});

test("sign-in offers email entry and Google options", async ({
  page,
}) => {
  await page.goto("/sign-in");

  await expect(page.getByLabel("Email address")).toBeVisible();
  await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Forgot password?" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Continue with Google" }),
  ).toBeVisible();
  await expect(page.getByText("Email code sign-in")).toHaveCount(0);
  await expect(page.getByText("Continue with email code")).toHaveCount(0);
});

test("sign-up starts with email and entry fields", async ({ page }) => {
  await installTurnstileStub(page);
  await page.goto("/sign-up");

  await expect(page.getByLabel("Email address")).toBeVisible();
  await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
  await expect(page.getByTestId("signup-turnstile-widget")).toContainText(
    "Verification ready",
  );
  await expect(page.getByText("Email code sign-in")).toHaveCount(0);
  await expect(page.getByText("Continue with email code")).toHaveCount(0);
});

test("sign-up blocks blank verification before account creation", async ({
  page,
}) => {
  let startCalled = false;
  await installTurnstileStub(page, { token: null });
  await page.route("**/api/auth/signup/start", async (route) => {
    startCalled = true;
    await route.fulfill({
      contentType: "application/json",
      json: { error: "Complete the verification and try again.", ok: false },
      status: 403,
    });
  });
  await page.goto("/sign-up");
  await fillSignupForm(page);

  await expect(page.getByRole("button", { name: "Create account" })).toBeDisabled();
  expect(startCalled).toBe(false);
});

test("sign-up shows guidance for listed email domains", async ({ page }) => {
  await installTurnstileStub(page);
  await page.route("**/api/auth/signup/start", async (route) => {
    const payload = route.request().postDataJSON() as {
      email?: string;
      turnstileToken?: string;
    };
    expect(payload.email).toBe("casey@mailinator.com");
    expect(payload.turnstileToken).toBe("playwright-turnstile-token");
    await route.fulfill({
      contentType: "application/json",
      json: {
        error: "Use a long-term email address for your account.",
        ok: false,
      },
      status: 400,
    });
  });
  await page.goto("/sign-up");
  await fillSignupForm(page, "casey@mailinator.com");
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(
    page.getByText("Use a long-term email address for your account."),
  ).toBeVisible();
});

test("sign-up continues to email verification for normal addresses", async ({
  page,
}) => {
  await installTurnstileStub(page);
  await page.route("**/api/auth/signup/start", async (route) => {
    const payload = route.request().postDataJSON() as {
      email?: string;
      turnstileToken?: string;
    };
    expect(payload.email).toBe("casey@example.com");
    expect(payload.turnstileToken).toBe("playwright-turnstile-token");
    await route.fulfill({
      contentType: "application/json",
      json: {
        channelLabel: "c***@example.com",
        codeLength: 6,
        ok: true,
      },
      status: 200,
    });
  });
  await page.goto("/sign-up");
  await fillSignupForm(page);
  await expect(page.getByRole("button", { name: "Create account" })).toBeEnabled();
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(
    page.getByRole("heading", { name: "Enter the verification code" }),
  ).toBeVisible();
  await expect(page.getByText("c***@example.com")).toBeVisible();
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
