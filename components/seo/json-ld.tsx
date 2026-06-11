export type FaqJsonLdItem = {
  q: string;
  a: string;
};

export type ProductOfferJsonLdItem = {
  name: string;
  price: string;
  priceCurrency: string;
};

export type BreadcrumbJsonLdItem = {
  name: string;
  item: string;
};

export function buildFaqPageJsonLd(items: readonly FaqJsonLdItem[]) {
  return {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    mainEntity: items.map((item) => ({
      "@type": "Question",
      name: item.q,
      acceptedAnswer: {
        "@type": "Answer",
        text: item.a,
      },
    })),
  };
}

export function buildProductOfferJsonLd(
  items: readonly ProductOfferJsonLdItem[],
) {
  return {
    "@context": "https://schema.org",
    "@graph": items.map((item) => ({
      "@type": "Product",
      name: item.name,
      offers: {
        "@type": "Offer",
        price: item.price,
        priceCurrency: item.priceCurrency,
      },
    })),
  };
}

export function buildBreadcrumbListJsonLd(
  items: readonly BreadcrumbJsonLdItem[],
) {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: items.map((item, index) => ({
      "@type": "ListItem",
      position: index + 1,
      name: item.name,
      item: item.item,
    })),
  };
}
