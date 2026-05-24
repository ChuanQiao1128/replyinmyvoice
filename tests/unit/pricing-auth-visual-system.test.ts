import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("pricing and auth visual system", () => {
  it("keeps the pricing route aligned with the rewrite-packs model and graceful gating", () => {
    const pricingPage = source("app/pricing/page.tsx");

    expect(pricingPage).toContain('<main className="rimv">');
    expect(pricingPage).toContain('className="page"');
    expect(pricingPage).toContain('className="pricing-wrap"');
    expect(pricingPage).toContain("Start free. Buy rewrites when you need them.");
    expect(pricingPage).toContain("Rewrite packs & Pro/API");
    expect(pricingPage).toContain("Quick Pack");
    expect(pricingPage).toContain("NZ$2.50");
    expect(pricingPage).toContain("10 rewrites");
    expect(pricingPage).toContain("Value Pack");
    expect(pricingPage).toContain("NZ$6.90");
    expect(pricingPage).toContain("30 rewrites");
    expect(pricingPage).toContain("Most popular");
    expect(pricingPage).toContain("Pro/API");
    expect(pricingPage).toContain("NZ$19.90/mo");
    expect(pricingPage).toContain("90 rewrites");
    expect(pricingPage).toContain("Available soon");
    expect(pricingPage).toContain("isPriceConfigured");
    // Old subscription-era copy must be gone.
    expect(pricingPage).not.toContain("Starter");
    expect(pricingPage).not.toContain("Exam Week Pass");
    expect(pricingPage).not.toContain("NZ$9.90");
    expect(pricingPage).not.toContain("55 rewrites");
    expect(pricingPage).not.toContain("110 rewrites");
    expect(pricingPage).not.toContain("unlimited rewrites");
    expect(pricingPage).not.toContain("NZD $9");
    expect(pricingPage).not.toContain("40 rewrites");
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
