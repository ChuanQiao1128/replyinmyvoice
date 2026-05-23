import { afterEach, beforeEach, describe, expect, it } from "vitest";

import {
  buildStripeCheckoutSessionParams,
  getCreditGrantForPriceId,
  getPlanTierForPriceId,
} from "../../lib/stripe";

const managedPriceEnv = [
  "STRIPE_PRICE_STARTER",
  "STRIPE_PRICE_PRO",
  "STRIPE_PRICE_EXAM_PASS",
  "STRIPE_PRICE_TOPUP",
] as const;

const previousEnv: Partial<Record<(typeof managedPriceEnv)[number], string | undefined>> = {};

beforeEach(() => {
  for (const key of managedPriceEnv) {
    previousEnv[key] = process.env[key];
  }

  process.env.STRIPE_PRICE_STARTER = "price_starter_test";
  process.env.STRIPE_PRICE_PRO = "price_pro_test";
  process.env.STRIPE_PRICE_EXAM_PASS = "price_exam_test";
  process.env.STRIPE_PRICE_TOPUP = "price_topup_test";
});

afterEach(() => {
  for (const key of managedPriceEnv) {
    const previous = previousEnv[key];
    if (previous === undefined) {
      delete process.env[key];
    } else {
      process.env[key] = previous;
    }
  }
});

describe("Stripe price mapping", () => {
  it("maps recurring price IDs to Starter and Pro plan tiers", () => {
    expect(getPlanTierForPriceId("price_starter_test")).toBe("starter");
    expect(getPlanTierForPriceId("price_pro_test")).toBe("pro");
    expect(getPlanTierForPriceId("price_other")).toBeNull();
  });

  it("maps one-time price IDs to credit grants with the right expiry", () => {
    const now = new Date("2026-05-24T00:00:00.000Z");

    expect(getCreditGrantForPriceId("price_exam_test", now)).toMatchObject({
      source: "exam_pass",
      amountGranted: 25,
      expiresAt: new Date("2026-05-31T00:00:00.000Z"),
    });
    expect(getCreditGrantForPriceId("price_topup_test", now)).toMatchObject({
      source: "top_up",
      amountGranted: 10,
      expiresAt: null,
    });
    expect(getCreditGrantForPriceId("price_other", now)).toBeNull();
  });
});

describe("Stripe checkout session params", () => {
  const checkoutInput = {
    customerId: "cus_test",
    clerkUserId: "clerk_123",
    userId: "user_123",
    successUrl: "https://reply.example/app?checkout=success",
    cancelUrl: "https://reply.example/app?checkout=cancelled",
  };

  it("builds subscription checkout params for Starter and Pro SKUs", () => {
    const starter = buildStripeCheckoutSessionParams({
      ...checkoutInput,
      sku: "starter",
    });
    const pro = buildStripeCheckoutSessionParams({
      ...checkoutInput,
      sku: "pro",
    });

    expect(starter.get("mode")).toBe("subscription");
    expect(starter.get("line_items[0][price]")).toBe("price_starter_test");
    expect(starter.get("subscription_data[metadata][userId]")).toBe("user_123");
    expect(starter.get("metadata[checkoutSku]")).toBe("starter");

    expect(pro.get("mode")).toBe("subscription");
    expect(pro.get("line_items[0][price]")).toBe("price_pro_test");
    expect(pro.get("metadata[priceId]")).toBe("price_pro_test");
  });

  it("builds one-time payment checkout params for credit SKUs", () => {
    const examPass = buildStripeCheckoutSessionParams({
      ...checkoutInput,
      sku: "exam_pass",
    });
    const topUp = buildStripeCheckoutSessionParams({
      ...checkoutInput,
      sku: "top_up",
    });

    expect(examPass.get("mode")).toBe("payment");
    expect(examPass.get("line_items[0][price]")).toBe("price_exam_test");
    expect(examPass.get("metadata[checkoutSku]")).toBe("exam_pass");
    expect(examPass.has("subscription_data[metadata][userId]")).toBe(false);

    expect(topUp.get("mode")).toBe("payment");
    expect(topUp.get("line_items[0][price]")).toBe("price_topup_test");
    expect(topUp.get("metadata[priceId]")).toBe("price_topup_test");
  });

  it("throws a clear runtime error only when a requested SKU price env is missing", () => {
    delete process.env.STRIPE_PRICE_STARTER;

    expect(getPlanTierForPriceId("price_pro_test")).toBe("pro");
    expect(() =>
      buildStripeCheckoutSessionParams({
        ...checkoutInput,
        sku: "starter",
      }),
    ).toThrow("STRIPE_PRICE_STARTER");
  });
});
