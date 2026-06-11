import { describe, expect, it } from "vitest";

import {
  buildBreadcrumbListJsonLd,
  buildFaqPageJsonLd,
  buildProductOfferJsonLd,
} from "../../components/seo/json-ld";

describe("SEO structured data builders", () => {
  it("builds FAQPage data from the provided question and answer pairs", () => {
    expect(
      buildFaqPageJsonLd([
        {
          q: "What does this do?",
          a: "It helps rewrite practical replies while preserving supplied facts.",
        },
      ]),
    ).toEqual({
      "@context": "https://schema.org",
      "@type": "FAQPage",
      mainEntity: [
        {
          "@type": "Question",
          name: "What does this do?",
          acceptedAnswer: {
            "@type": "Answer",
            text: "It helps rewrite practical replies while preserving supplied facts.",
          },
        },
      ],
    });
  });

  it("builds Product data with factual NZD Offer prices", () => {
    expect(
      buildProductOfferJsonLd([
        {
          name: "Quick Pack",
          price: "2.50",
          priceCurrency: "NZD",
        },
      ]),
    ).toEqual({
      "@context": "https://schema.org",
      "@graph": [
        {
          "@type": "Product",
          name: "Quick Pack",
          offers: {
            "@type": "Offer",
            price: "2.50",
            priceCurrency: "NZD",
          },
        },
      ],
    });
  });

  it("builds BreadcrumbList data with stable positions", () => {
    expect(
      buildBreadcrumbListJsonLd([
        { name: "Home", item: "https://replyinmyvoice.com/" },
        { name: "Terms", item: "https://replyinmyvoice.com/terms" },
      ]),
    ).toEqual({
      "@context": "https://schema.org",
      "@type": "BreadcrumbList",
      itemListElement: [
        {
          "@type": "ListItem",
          position: 1,
          name: "Home",
          item: "https://replyinmyvoice.com/",
        },
        {
          "@type": "ListItem",
          position: 2,
          name: "Terms",
          item: "https://replyinmyvoice.com/terms",
        },
      ],
    });
  });
});
