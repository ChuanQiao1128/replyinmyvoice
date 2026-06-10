import { readFileSync } from "node:fs";
import { join } from "node:path";

import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { PricingTrust } from "../../components/landing/pricing-trust";

const root = process.cwd();

function componentSource() {
  return readFileSync(
    join(root, "components/landing/pricing-trust.tsx"),
    "utf8",
  );
}

describe("PricingTrust", () => {
  it("renders the trust band with the four every-plan includes", () => {
    const markup = renderToStaticMarkup(createElement(PricingTrust));

    expect(markup).toContain("Every plan includes");
    expect(markup).toContain("AI Signal");
    expect(markup).toContain("Facts preserved");
    expect(markup).toContain("Server-backed history");
    expect(markup).toContain("Delete anytime");
  });

  it("renders the reassurance items and honest credibility line", () => {
    const markup = renderToStaticMarkup(createElement(PricingTrust));

    expect(markup).toContain("No card needed to try");
    expect(markup).toContain("Packs don&#x27;t auto-renew");
    expect(markup).toContain("Cancel Pro/API anytime");
    expect(markup).toContain("Secure checkout via Stripe");
    expect(markup).toContain("History across your devices");
    expect(markup).toContain(
      "Built for teacher, sales, workplace, and client replies.",
    );
    expect(markup).toContain("Operated by TimeAwake Ltd.");
  });

  it("does not add fabricated social proof", () => {
    const source = componentSource();

    expect(source).not.toContain("★");
    expect(source.toLowerCase()).not.toContain("trusted by");
  });
});
