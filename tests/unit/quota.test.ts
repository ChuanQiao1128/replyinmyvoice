import { describe, expect, it } from "vitest";

import { getUsagePlan } from "../../lib/quota";

describe("getUsagePlan", () => {
  it("uses lifetime quota for inactive free users", () => {
    const plan = getUsagePlan({
      id: "user_1",
      subscriptionStatus: "inactive",
      stripeSubscriptionId: null,
      currentPeriodEnd: null,
    });

    expect(plan).toMatchObject({
      allowed: true,
      quota: 3,
      periodKey: "lifetime",
      scope: "free",
    });
  });

  it("uses the Stripe billing period for active paid users", () => {
    const periodEnd = new Date("2026-06-17T00:00:00.000Z");
    const plan = getUsagePlan({
      id: "user_1",
      subscriptionStatus: "active",
      stripeSubscriptionId: "sub_123",
      currentPeriodEnd: periodEnd,
    });

    expect(plan).toMatchObject({
      allowed: true,
      quota: 100,
      periodKey: "paid:sub_123:2026-06-17T00:00:00.000Z",
      scope: "paid",
      periodEnd,
    });
  });

  it("treats trialing users as paid quota users", () => {
    const plan = getUsagePlan({
      id: "user_1",
      subscriptionStatus: "trialing",
      stripeSubscriptionId: "sub_trial",
      currentPeriodEnd: new Date("2026-06-01T00:00:00.000Z"),
    });

    expect(plan.scope).toBe("paid");
    expect(plan.quota).toBe(100);
  });
});
