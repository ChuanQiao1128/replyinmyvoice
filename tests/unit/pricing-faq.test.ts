import * as React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { PricingFaq } from "../../components/landing/pricing-faq";

beforeEach(() => {
  vi.stubGlobal("React", React);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function renderFaq() {
  return renderToStaticMarkup(React.createElement(PricingFaq));
}

function summaryLabels(markup: string) {
  return Array.from(markup.matchAll(/<summary\b[^>]*>(.*?)<\/summary>/g)).map(
    (match) => match[1],
  );
}

describe("PricingFaq", () => {
  it("renders the pricing FAQ section", () => {
    const html = renderFaq();

    expect(html).toContain('aria-labelledby="pricing-faq-heading"');
    expect(html).toContain("Questions &amp; answers");
  });

  it("renders at least six FAQ questions including rewrite usage", () => {
    const labels = summaryLabels(renderFaq());

    expect(labels.length).toBeGreaterThanOrEqual(6);
    expect(labels).toContain("What counts as one rewrite?");
  });

  it("uses native disclosure controls for each question", () => {
    const html = renderFaq();
    const labels = summaryLabels(html);
    const detailCount = (html.match(/<details\b/g) ?? []).length;

    expect(html).toContain("<summary");
    expect(detailCount).toBe(labels.length);
  });
});
