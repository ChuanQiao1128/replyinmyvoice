import React, { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { derivePromoCodeStatus } from "../../lib/admin-promo-codes";

function inputTag(markup: string, id: string) {
  const match = new RegExp(`<input(?=[^>]*\\bid="${id}")[^>]*>`).exec(markup);
  expect(match?.[0]).toBeTruthy();
  return match?.[0] ?? "";
}

function inputValue(markup: string, id: string) {
  const match = /\bvalue="([^"]*)"/.exec(inputTag(markup, id));
  expect(match?.[1]).toBeTruthy();
  return match?.[1] ?? "";
}

function localDateTimeValue(value: string) {
  const parsed = new Date(value);
  expect(Number.isNaN(parsed.getTime())).toBe(false);
  return parsed;
}

describe("PromoCodesAdmin", () => {
  it("renders create defaults that are active immediately with status guidance", async () => {
    (globalThis as { React?: typeof React }).React = React;
    const { PromoCodesAdmin } = await import(
      "../../components/admin/promo-codes-admin"
    );

    const beforeRender = new Date();
    const markup = renderToStaticMarkup(
      createElement(PromoCodesAdmin, { initialCodes: [] }),
    );
    const afterRender = new Date();

    expect(markup).toContain('href="/admin"');
    expect(markup).toContain("Back to Admin");
    expect(markup).toContain("Active = redeemable now");
    expect(markup).toContain("Pending = not yet active (valid-from is in the future)");
    expect(markup).toContain("Expired = past valid-until");
    expect(markup).toContain("Exhausted = global cap reached");
    expect(markup).toContain("Disabled = turned off by an admin");
    expect(markup).toContain("Archived = soft-deleted and hidden");

    const validFrom = localDateTimeValue(inputValue(markup, "promo-from"));
    const validUntil = localDateTimeValue(inputValue(markup, "promo-until"));

    expect(validFrom.getTime()).toBeGreaterThanOrEqual(
      beforeRender.getTime() - 60_000,
    );
    expect(validFrom.getTime()).toBeLessThanOrEqual(afterRender.getTime());

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
        afterRender,
      ),
    ).toBe("active");
  });
});
