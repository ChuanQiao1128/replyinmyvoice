/**
 * PAY-06 payment E2E.
 *
 * Required test env names:
 * - PAYMENT_E2E_STRIPE_WEBHOOK_SECRET
 *
 * Playwright starts a local mock payment API through playwright.config.ts and
 * points the Next.js dev server at it. The spec signs webhook payloads with the
 * env value above, uses mocked Stripe Checkout URLs, and never calls Stripe.
 */
import { createHmac } from "node:crypto";

import { expect, test, type BrowserContext, type APIRequestContext } from "@playwright/test";

const appUrl = "http://127.0.0.1:3000";
const sessionSecret = "playwright-admin-session-secret";
const paymentApiUrl =
  process.env.PAYMENT_E2E_MOCK_API_URL ??
  `http://127.0.0.1:${process.env.PAYMENT_E2E_MOCK_API_PORT ?? "43183"}`;

function configuredForPaymentE2e() {
  return Boolean(process.env.PAYMENT_E2E_STRIPE_WEBHOOK_SECRET);
}

function base64UrlJson(value: unknown) {
  return Buffer.from(JSON.stringify(value), "utf8").toString("base64url");
}

function signedCookieValue(value: unknown) {
  const payload = base64UrlJson(value);
  const signature = createHmac("sha256", sessionSecret)
    .update(payload)
    .digest("base64url");
  return `${payload}.${signature}`;
}

function unsignedJwt(claims: Record<string, unknown>) {
  return [
    base64UrlJson({ alg: "none", typ: "JWT" }),
    base64UrlJson(claims),
    "signature",
  ].join(".");
}

async function addSignedInSession(
  context: BrowserContext,
  input: { email: string; name: string; userId: string },
) {
  const exp = Math.floor(Date.now() / 1000) + 60 * 60;
  const accessToken = unsignedJwt({
    email: input.email,
    exp,
    name: input.name,
    sub: input.userId,
  });

  await context.addCookies([
    {
      httpOnly: true,
      name: "rimv_session",
      path: "/",
      sameSite: "Lax",
      url: appUrl,
      value: signedCookieValue({
        email: input.email,
        exp,
        name: input.name,
        sub: input.userId,
      }),
    },
    {
      httpOnly: true,
      name: "rimv_access_0",
      path: "/",
      sameSite: "Lax",
      url: appUrl,
      value: signedCookieValue({
        accessToken,
        accessTokenExp: exp,
        exp,
      }),
    },
    {
      httpOnly: true,
      name: "rimv_access_meta",
      path: "/",
      sameSite: "Lax",
      url: appUrl,
      value: signedCookieValue({
        chunks: 1,
        exp,
      }),
    },
  ]);
}

function signStripePayload(payload: string) {
  const secret = process.env.PAYMENT_E2E_STRIPE_WEBHOOK_SECRET;
  if (!secret) {
    throw new Error("PAYMENT_E2E_STRIPE_WEBHOOK_SECRET is required.");
  }

  const timestamp = Math.floor(Date.now() / 1000);
  const signature = createHmac("sha256", secret)
    .update(`${timestamp}.${payload}`)
    .digest("hex");
  return `t=${timestamp},v1=${signature}`;
}

async function postSignedWebhook(request: APIRequestContext, payload: unknown) {
  const body = JSON.stringify(payload);
  const response = await request.post("/api/stripe/webhook", {
    data: body,
    headers: {
      "content-type": "application/json",
      "stripe-signature": signStripePayload(body),
    },
  });

  expect(response.ok()).toBeTruthy();
  return response.json() as Promise<{ processed: boolean; received: boolean }>;
}

async function resetPaymentApi(request: APIRequestContext) {
  const response = await request.post(`${paymentApiUrl}/__reset`);
  expect([204, 404]).toContain(response.status());
}

test.describe("PAY-06 payment checkout and refund flow", () => {
  test.skip(
    !configuredForPaymentE2e(),
    "PAYMENT_E2E_STRIPE_WEBHOOK_SECRET is required for signed payment E2E.",
  );

  test.beforeEach(async ({ request }) => {
    await resetPaymentApi(request);
  });

  test("checkout grants Quick Pack credits and full refund claws them back", async ({
    context,
    page,
    request,
  }) => {
    const user = {
      email: "pay-06-buyer@example.test",
      name: "PAY 06 Buyer",
      userId: "pay-06-buyer",
    };
    const paymentIntentId = "pi_pay_06_quick_pack";

    await addSignedInSession(context, user);

    await page.goto("/app");
    await expect(
      page.getByText("3 of 3 free rewrites remaining"),
    ).toBeVisible();

    await page.goto("/pricing");
    await page.getByRole("button", { name: /Get Quick Pack/ }).click();
    await expect(page).toHaveURL(/https:\/\/checkout\.stripe\.com\/c\/pay\/cs_test_/);

    await expect(
      await postSignedWebhook(request, {
        data: {
          object: {
            amount_total: 250,
            client_reference_id: user.userId,
            currency: "nzd",
            mode: "payment",
            payment_intent: paymentIntentId,
            payment_status: "paid",
            metadata: {
              externalAuthUserId: user.userId,
              rewrites: "10",
              sku: "quick_pack",
            },
          },
        },
        id: "evt_pay_06_checkout_completed",
        object: "event",
        type: "checkout.session.completed",
      }),
    ).toMatchObject({ processed: true, received: true });

    await page.goto("/app");
    await expect(
      page.getByText("13 of 13 free rewrites remaining"),
    ).toBeVisible();

    await expect(
      await postSignedWebhook(request, {
        data: {
          object: {
            amount: 250,
            amount_refunded: 250,
            id: "ch_pay_06_quick_pack",
            payment_intent: paymentIntentId,
            refunded: true,
          },
        },
        id: "evt_pay_06_charge_refunded",
        object: "event",
        type: "charge.refunded",
      }),
    ).toMatchObject({ processed: true, received: true });

    await page.goto("/app");
    await expect(
      page.getByText("3 of 3 free rewrites remaining"),
    ).toBeVisible();
  });

  test("anonymous pricing checkout redirects to sign in with pricing purchase return", async ({
    page,
  }) => {
    await page.goto("/pricing");

    await page.getByRole("button", { name: /Get Quick Pack/ }).click();

    await expect(page).toHaveURL(
      /\/sign-in\?redirectTo=%2Fpricing&intent=buy&sku=quick_pack$/,
    );
  });
});
