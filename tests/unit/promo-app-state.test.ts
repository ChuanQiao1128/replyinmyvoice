import { describe, expect, it } from "vitest";

import {
  labelForQuotaSource,
  selectAppExperience,
  trialCreditSummary,
  trialExpiryLabel,
} from "../../lib/promo-app-state";

describe("promo app state", () => {
  it("selects an in-workspace redeem banner for a new signed-in user with no quota", () => {
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
    ).toBe("needsRedeem");
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
    ).toBe("ok");
  });

  it("selects an in-workspace buy banner after a redeemed trial is exhausted", () => {
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
    ).toBe("needsBuy");
  });

  it("selects an in-workspace buy banner for paid exhausted accounts", () => {
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
    ).toBe("needsBuy");
  });

  it("maps PROMO quota sources to trial copy", () => {
    expect(labelForQuotaSource("PROMO", "Promo")).toBe("Trial rewrites");
    expect(labelForQuotaSource("PURCHASE", "Value Pack")).toBe("Value Pack");
  });

  it("formats trial expiry as remaining days with the exact expiry time", () => {
    const expiresAt = "2026-06-12T00:00:00.000Z";
    const expectedExactExpiry = new Intl.DateTimeFormat(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(expiresAt));

    expect(
      trialExpiryLabel(
        expiresAt,
        new Date("2026-06-02T00:00:00.000Z"),
      ),
    ).toBe(`expires in 10 days (expires ${expectedExactExpiry})`);
  });

  it("sums configured trial grant totals from promo quota sources", () => {
    expect(
      trialCreditSummary(
        [
          {
            source: "PROMO",
            label: "Promo",
            remaining: 7,
            limit: 10,
          },
          {
            source: "promo",
            label: "Trial rewrites",
            remaining: 2,
            limit: 5,
          },
          {
            source: "PURCHASE",
            label: "Value Pack",
            remaining: 30,
            limit: 30,
          },
        ],
        9,
      ),
    ).toEqual({ granted: 15, remaining: 9 });
  });

  it("falls back to a denominator that never drops below trial remaining", () => {
    expect(
      trialCreditSummary(
        [
          {
            source: "PROMO",
            label: "Promo",
            remaining: 10,
          },
        ],
        10,
      ),
    ).toEqual({ granted: 10, remaining: 10 });
  });

  it("uses source remaining for only the promo sources missing grant totals", () => {
    expect(
      trialCreditSummary(
        [
          {
            source: "PROMO",
            label: "Promo",
            remaining: 7,
            limit: 10,
          },
          {
            source: "PROMO",
            label: "Promo",
            remaining: 2,
          },
        ],
        9,
      ),
    ).toEqual({ granted: 12, remaining: 9 });
  });
});
