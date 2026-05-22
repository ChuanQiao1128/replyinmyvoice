import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const root = new URL("../..", import.meta.url);

function source(path: string) {
  return readFileSync(new URL(path, root), "utf8");
}

const customerFacingSources = [
  "components/site-header.tsx",
  "components/site-footer.tsx",
  "components/landing/hero.tsx",
  "components/landing/interactive-demo.tsx",
  "components/landing/trust-panel.tsx",
  "components/landing/use-cases.tsx",
  "components/landing/how-it-works.tsx",
  "components/landing/pricing.tsx",
  "components/landing/faq.tsx",
  "components/landing/closing-cta.tsx",
  "components/auth/google-oauth-card.tsx",
  "components/app/rewrite-workspace.tsx",
  "components/app/paywall-card.tsx",
  "components/app/subscription-status.tsx",
];

describe("M4 frontend redesign visual contracts", () => {
  it("uses the refreshed multi-accent token palette", () => {
    const tailwind = source("tailwind.config.ts");

    expect(tailwind).toContain("evergreen");
    expect(tailwind).toContain("brick");
    expect(tailwind).toContain("mist");
  });

  it("keeps customer-facing typography free of wide tracking treatments", () => {
    for (const path of customerFacingSources) {
      expect(source(path), path).not.toContain("tracking-[");
    }
  });

  it("makes the landing hero product-first and scene-led", () => {
    const hero = source("components/landing/hero.tsx");

    expect(hero).toContain("Reply In My Voice");
    expect(hero).toContain("Replies that still sound like you.");
    expect(hero).toContain("WritingDeskScene");
    expect(hero).not.toContain("md:grid-cols");
  });

  it("keeps auth surfaces within the 8px radius rule", () => {
    const auth = source("components/auth/google-oauth-card.tsx");

    expect(auth).not.toContain("rounded-xl");
    expect(auth).not.toContain("rounded-2xl");
  });
});
