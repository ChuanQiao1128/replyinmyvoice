import { readFileSync } from "node:fs";
import { join } from "node:path";

import * as React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { PricingComparison } from "../../components/landing/pricing-comparison";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

beforeEach(() => {
  vi.stubGlobal("React", React);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("PricingComparison", () => {
  it("renders the plan comparison table from the component", () => {
    const html = renderToStaticMarkup(React.createElement(PricingComparison));
    const comparison = source("components/landing/pricing-comparison.tsx");

    expect(html).toContain("<table");
    expect(comparison).toContain("<table");

    for (const heading of ["Trial", "Quick Pack", "Value Pack", "Pro·API"]) {
      expect(html).toContain(heading);
    }

    expect(html).toContain("REST API + MCP access");
    expect(html).toContain("Price per rewrite");
    expect(html).toContain("≈ NZ$0.25");
    expect(html).toContain("≈ NZ$0.23");
    expect(html).toContain("≈ NZ$0.22");
    expect(html).toContain("Packs expire 90 days after purchase");
    expect(html).toContain("Pro/API rewrites reset monthly");
  });
});
