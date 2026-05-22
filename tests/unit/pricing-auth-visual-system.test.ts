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

    expect(pricingPage).toContain("bg-sky");
    expect(pricingPage).toContain("40 rewrites");
    expect(pricingPage).not.toContain("unlimited rewrites");
  });

  it("keeps auth cards within the refreshed shape and keyboard-focus system", () => {
    const authCard = source("components/auth/google-oauth-card.tsx");

    expect(authCard).toContain("rounded-lg");
    expect(authCard).toContain("shadow-soft");
    expect(authCard).toContain("focus-visible:ring");
    expect(authCard).not.toContain("rounded-2xl");
    expect(authCard).not.toContain("rounded-xl");
  });

  it("keeps shared button controls visibly focusable", () => {
    const button = source("components/ui/button.tsx");

    expect(button).toContain("focus-visible:outline-none");
    expect(button).toContain("focus-visible:ring-2");
    expect(button).toContain("focus-visible:ring-clay");
  });
});
