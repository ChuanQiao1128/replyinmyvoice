import React, { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { derivePromoCodeStatus } from "../../lib/admin-promo-codes";
import type { AdminPromoCode } from "../../lib/admin-promo-codes";

function localDateTimeValue(value: string) {
  const parsed = new Date(value);
  expect(Number.isNaN(parsed.getTime())).toBe(false);
  return parsed;
}

function promoCode(overrides: Partial<AdminPromoCode> = {}): AdminPromoCode {
  return {
    archivedAt: null,
    code: "SPRING2026",
    createdAt: "2026-06-01T00:00:00.000Z",
    creditsGranted: 3,
    description: null,
    displayCode: "SPRING-2026",
    grantTtlDays: 90,
    id: "promo_1",
    isActive: true,
    kind: "TrialCredits",
    maxRedemptionsGlobal: 1000,
    maxRedemptionsPerUser: 1,
    redemptionCount: 0,
    status: "active",
    updatedAt: "2026-06-01T00:00:00.000Z",
    validFrom: "2026-06-01T00:00:00.000Z",
    validUntil: "2026-09-01T00:00:00.000Z",
    ...overrides,
  };
}

describe("PromoCodesAdmin", () => {
  it("renders create defaults that are active immediately with status guidance", async () => {
    (globalThis as { React?: typeof React }).React = React;
    const { initialFormValues, PromoCodesAdmin } = await import(
      "../../components/admin/promo-codes-admin"
    );

    const beforeDefaults = new Date();
    const defaults = initialFormValues();
    const afterDefaults = new Date();
    const markup = renderToStaticMarkup(
      createElement(PromoCodesAdmin, { initialCodes: [] }),
    );

    expect(markup).toContain('href="/admin"');
    expect(markup).toContain("Back to Admin");
    expect(markup).toContain('aria-label="Promo code status legend"');
    expect(markup).toContain("Active");
    expect(markup).toContain("redeemable now");
    expect(markup).toContain("Pending");
    expect(markup).toContain("not yet active (valid-from is in the future)");
    expect(markup).toContain("Expired");
    expect(markup).toContain("past valid-until");
    expect(markup).toContain("Exhausted");
    expect(markup).toContain("global cap reached");
    expect(markup).toContain("Disabled");
    expect(markup).toContain("turned off by an admin");
    expect(markup).toContain("Archived");
    expect(markup).toContain("soft-deleted and hidden");
    expect(markup).not.toContain("Active = redeemable now");
    expect(markup).not.toContain("Pending = not yet active");

    const validFrom = localDateTimeValue(defaults.validFrom);
    const validUntil = localDateTimeValue(defaults.validUntil);

    expect(validFrom.getTime()).toBeGreaterThanOrEqual(
      beforeDefaults.getTime() - 60_000,
    );
    expect(validFrom.getTime()).toBeLessThanOrEqual(afterDefaults.getTime());

    const daysBetween = (validUntil.getTime() - validFrom.getTime()) /
      (24 * 60 * 60 * 1000);
    expect(daysBetween).toBeGreaterThanOrEqual(89.9);
    expect(daysBetween).toBeLessThanOrEqual(90.1);
    expect(
      derivePromoCodeStatus(
        {
          isActive: true,
          maxRedemptionsGlobal: 1000,
          redemptionCount: 0,
          validFrom: validFrom.toISOString(),
          validUntil: validUntil.toISOString(),
        },
        afterDefaults,
      ),
    ).toBe("active");
  });

  it("renders legend chips and table-level quick actions", async () => {
    (globalThis as { React?: typeof React }).React = React;
    const { PromoCodesAdmin } = await import(
      "../../components/admin/promo-codes-admin"
    );

    const markup = renderToStaticMarkup(
      createElement(PromoCodesAdmin, { initialCodes: [promoCode()] }),
    );

    expect(markup).toContain('role="table"');
    expect(markup).toContain("New code");
    expect(markup).not.toContain("Selected code");
    expect(markup).toContain('aria-label="Copy code SPRING-2026"');
    expect(markup).toContain('aria-label="View stats for SPRING-2026"');
    expect(markup).toContain('aria-label="Archive SPRING-2026"');
  });
});
