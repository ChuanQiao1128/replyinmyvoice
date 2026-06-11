import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("pricing redesign foundation", () => {
  it("renders downstream pricing sections from the pricing route", () => {
    const pricingPage = source("app/pricing/page.tsx");

    expect(pricingPage).toContain(
      'import { PricingComparison } from "../../components/landing/pricing-comparison"',
    );
    expect(pricingPage).toContain(
      'import { PricingTrust } from "../../components/landing/pricing-trust"',
    );
    expect(pricingPage).toContain(
      'import { PricingFaq } from "../../components/landing/pricing-faq"',
    );
    expect(pricingPage).toContain("<PricingComparison />");
    expect(pricingPage).toContain("<PricingTrust />");
    expect(pricingPage).toContain("<PricingFaq />");
  });

  it("adds pack grouping and unit-price helper lines", () => {
    const pricingPage = source("app/pricing/page.tsx");

    expect(pricingPage).toContain("One-time packs");
    expect(pricingPage).toContain("Monthly subscription");
    expect(pricingPage).toContain("≈ NZ$0.25 / rewrite");
    expect(pricingPage).toContain("≈ NZ$0.23 / rewrite");
    expect(pricingPage).toContain("≈ NZ$0.22 / rewrite");
    expect(pricingPage).toContain('aria-disabled="true"');
    expect(pricingPage).toContain("Focus Pack — available soon");
  });

  it("provides downstream component markers and starter content", () => {
    const comparison = source("components/landing/pricing-comparison.tsx");
    const trust = source("components/landing/pricing-trust.tsx");
    const faq = source("components/landing/pricing-faq.tsx");

    expect(comparison).toContain("export function PricingComparison()");
    expect(comparison).toContain("Compare what you get");
    expect(comparison).toContain("Trial");
    expect(comparison).toContain("Quick Pack");
    expect(comparison).toContain("Value Pack");
    expect(comparison).toContain("Pro·API");
    expect(comparison).toContain("Price per rewrite");
    expect(comparison).toContain("Packs expire 90 days after purchase");
    expect(comparison).toContain(
      "{/* PRICING-COMPARISON: fleshed out in PRICE-02 */}",
    );

    expect(trust).toContain("export function PricingTrust()");
    expect(trust).toContain("Every plan includes");
    expect(trust).toContain("AI Signal");
    expect(trust).toContain("Facts preserved");
    expect(trust).toContain("Server-backed history");
    expect(trust).toContain("Delete anytime");
    expect(trust).toContain("{/* PRICING-TRUST: expanded in PRICE-04 */}");

    expect(faq).toContain("export function PricingFaq()");
    expect(faq).toContain("Questions & answers");
    expect(faq).toContain("What counts as one rewrite?");
    expect(faq).toContain("Do packs expire?");
    expect(faq).toContain("{/* PRICING-FAQ: expanded in PRICE-03 */}");
  });

  it("adds the pricing redesign CSS region for this wave", () => {
    const globals = source("app/globals.css");

    expect(globals).toContain(
      "/* ── pricing-redesign ─────────────────────────────────────── */",
    );
    expect(globals).toContain(
      "/* (PRICE-01: hero retune, pack cards, unit-price, one-time/subscription grouping) */",
    );
    expect(globals).toContain(
      "/* ── pricing-redesign: comparison (PRICE-02 fills) ────────── */",
    );
    expect(globals).toContain(
      "/* ── pricing-redesign: trust (PRICE-04 fills) ─────────────── */",
    );
    expect(globals).toContain(
      "/* ── pricing-redesign: faq (PRICE-03 fills) ───────────────── */",
    );
    expect(globals).toContain(
      "/* ── end pricing-redesign ─────────────────────────────────── */",
    );
  });
});
