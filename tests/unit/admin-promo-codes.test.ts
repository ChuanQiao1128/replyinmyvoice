import { describe, expect, it } from "vitest";

import {
  adminPromoDetailFromPayload,
  derivePromoCodeStatus,
  fieldErrorsFromAdminError,
  validatePromoCreateForm,
} from "../../lib/admin-promo-codes";

const activeCode = {
  code: "SPRING2026",
  createdAt: "2026-06-01T00:00:00.000Z",
  creditsGranted: 3,
  displayCode: "SPRING-2026",
  grantTtlDays: 90,
  id: "11111111-1111-4111-8111-111111111111",
  isActive: true,
  kind: "TrialCredits",
  maxRedemptionsGlobal: 100,
  maxRedemptionsPerUser: 1,
  redemptionCount: 2,
  status: "active",
  updatedAt: "2026-06-01T00:00:00.000Z",
  validFrom: "2026-06-01T00:00:00.000Z",
  validUntil: "2026-06-30T00:00:00.000Z",
};

describe("admin promo code helpers", () => {
  it("derives disabled, pending, expired, exhausted, and active states", () => {
    const now = new Date("2026-06-15T00:00:00.000Z");

    expect(derivePromoCodeStatus({ ...activeCode, isActive: false }, now)).toBe(
      "disabled",
    );
    expect(
      derivePromoCodeStatus({
        ...activeCode,
        validFrom: "2026-07-01T00:00:00.000Z",
        validUntil: "2026-08-01T00:00:00.000Z",
      }, now),
    ).toBe("pending");
    expect(
      derivePromoCodeStatus({
        ...activeCode,
        validUntil: "2026-06-01T00:00:00.000Z",
      }, now),
    ).toBe("expired");
    expect(
      derivePromoCodeStatus({
        ...activeCode,
        maxRedemptionsGlobal: 2,
        redemptionCount: 2,
      }, now),
    ).toBe("exhausted");
    expect(derivePromoCodeStatus({ ...activeCode }, now)).toBe("active");
  });

  it("returns field validation errors before create is submitted", () => {
    const result = validatePromoCreateForm({
      code: "SPRING2026",
      credits: "0",
      displayCode: "SPRING-2026",
      globalCap: "-2",
      perUserCap: "0",
      ttlDays: "0",
      validFrom: "2026-06-30T10:00",
      validUntil: "2026-06-01T10:00",
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.fieldErrors).toMatchObject({
        credits: "Credits must be greater than zero.",
        globalCap: "Global cap must be greater than zero.",
        perUserCap: "Per-user cap must be at least one.",
        ttlDays: "TTL days must be greater than zero.",
        validUntil: "Valid until must be after valid from.",
      });
    }
  });

  it("builds the backend create payload from the display code", () => {
    const result = validatePromoCreateForm({
      code: "SPRING2026",
      credits: "3",
      displayCode: "SPRING-2026",
      globalCap: "100",
      perUserCap: "1",
      ttlDays: "90",
      validFrom: "2026-06-01T10:00",
      validUntil: "2026-06-30T10:00",
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.payload).toMatchObject({
        code: "SPRING-2026",
        creditsGranted: 3,
        grantTtlDays: 90,
        maxRedemptionsGlobal: 100,
        maxRedemptionsPerUser: 1,
      });
    }
  });

  it("maps duplicate create errors to the code field", () => {
    expect(
      fieldErrorsFromAdminError(400, {
        detail: "A promo code with that normalized code already exists.",
        title: "Duplicate promo code",
      }),
    ).toEqual({
      code: "A promo code with that normalized code already exists.",
    });
  });

  it("keeps stats limited to hash clusters even if extra fields are present", () => {
    const detail = adminPromoDetailFromPayload({
      promoCode: activeCode,
      stats: {
        activationRate: 0.5,
        dailyCurve: [{ date: "2026-06-02", redemptions: 2 }],
        distinctUsers: 2,
        ipHashClusters: [
          {
            distinctUsers: 2,
            firstRedeemedAt: "2026-06-02T00:00:00.000Z",
            ipHash: "hash_cluster_1",
            lastRedeemedAt: "2026-06-03T00:00:00.000Z",
            rawIp: "203.0.113.55",
            redemptions: 2,
          },
        ],
        rawIp: "203.0.113.55",
        totalRedemptions: 2,
      },
    });

    expect(detail?.stats.ipHashClusters).toEqual([
      {
        distinctUsers: 2,
        firstRedeemedAt: "2026-06-02T00:00:00.000Z",
        ipHash: "hash_cluster_1",
        lastRedeemedAt: "2026-06-03T00:00:00.000Z",
        redemptions: 2,
      },
    ]);
    expect(JSON.stringify(detail)).not.toContain("203.0.113.55");
  });
});
