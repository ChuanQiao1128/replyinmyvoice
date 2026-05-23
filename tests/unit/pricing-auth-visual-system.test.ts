import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("pricing and auth visual system", () => {
  it("keeps the pricing route aligned with the M4-012 section bands and quota copy", () => {
    const pricingPage = source("app/pricing/page.tsx");

    expect(pricingPage).toContain('<main className="rimv">');
    expect(pricingPage).toContain('className="page"');
    expect(pricingPage).toContain('className="pricing-wrap"');
    expect(pricingPage).toContain("plan plan-paid");
    expect(pricingPage).toContain("40 rewrites");
    expect(pricingPage).not.toContain("unlimited rewrites");
  });

  it("keeps auth cards within the refreshed shape and keyboard-focus system", () => {
    const authCard = source("components/auth/google-oauth-card.tsx");

    expect(authCard).toContain('className="rimv"');
    expect(authCard).toContain('className="brand"');
    expect(authCard).toContain('className="btn btn-primary btn-lg"');
    expect(authCard).toContain("var(--card)");
    expect(authCard).toContain("var(--rule)");
    expect(authCard).toContain("boxShadow");
  });

  it("keeps shared button controls visibly focusable", () => {
    const button = source("components/ui/button.tsx");

    expect(button).toContain("focus-visible:outline-none");
    expect(button).toContain("focus-visible:ring-2");
    expect(button).toContain("focus-visible:ring-clay");
  });
});
