import { describe, expect, it } from "vitest";

import {
  labelForQuotaSource,
  selectAppExperience,
  trialExpiryLabel,
} from "../../lib/promo-app-state";

describe("promo app state", () => {
  it("shows the redeem card for a new signed-in user with no remaining quota", () => {
    expect(
      selectAppExperience({
        paid: false,
        promo: {
          hasRedeemed: false,
          trialExpiresAt: null,
          trialRemaining: 0,
        },
        usageExhausted: true,
        usageRemaining: 0,
      }),
    ).toBe("redeem");
  });

  it("shows the workspace when promo trial credits are available", () => {
    expect(
      selectAppExperience({
        paid: false,
        promo: {
          hasRedeemed: true,
          trialExpiresAt: "2026-08-31T00:00:00Z",
          trialRemaining: 3,
        },
        usageExhausted: false,
        usageRemaining: 3,
      }),
    ).toBe("workspace");
  });

  it("shows the paywall after a redeemed trial is exhausted", () => {
    expect(
      selectAppExperience({
        paid: false,
        promo: {
          hasRedeemed: true,
          trialExpiresAt: "2026-08-31T00:00:00Z",
          trialRemaining: 0,
        },
        usageExhausted: true,
        usageRemaining: 0,
      }),
    ).toBe("free-paywall");
  });

  it("keeps paid exhausted accounts on the billing-management paywall", () => {
    expect(
      selectAppExperience({
        paid: true,
        promo: {
          hasRedeemed: false,
          trialExpiresAt: null,
          trialRemaining: 0,
        },
        usageExhausted: true,
        usageRemaining: 0,
      }),
    ).toBe("paid-paywall");
  });

  it("maps PROMO quota sources to trial copy", () => {
    expect(labelForQuotaSource("PROMO", "Promo")).toBe("Trial rewrites");
    expect(labelForQuotaSource("PURCHASE", "Value Pack")).toBe("Value Pack");
  });

  it("formats trial expiry as remaining days", () => {
    expect(
      trialExpiryLabel(
        "2026-06-12T00:00:00.000Z",
        new Date("2026-06-02T00:00:00.000Z"),
      ),
    ).toBe("expire in 10 days");
  });
});
