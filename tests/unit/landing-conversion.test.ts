import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("landing conversion details", () => {
  it("labels the hero demo link as a no-sign-up example", () => {
    const hero = source("components/landing/hero.tsx");

    expect(hero).toContain("See an example");
    expect(hero).toContain("no sign-up");
    expect(hero).not.toContain("See before &amp; after");
  });

  it("does not render a tone toggle unless it changes the sample reply", () => {
    const demo = source("components/landing/interactive-demo.tsx");

    expect(demo).toContain("sample.rewrite");
    expect(demo).not.toContain("const TONES");
    expect(demo).not.toContain("tone-toggle");
    expect(demo).not.toContain("Tone preset");
  });

  it("wires a dismissible mobile sticky CTA to the same auth-aware destination as the hero", () => {
    const stickyPath = join(root, "components", "landing", "sticky-cta.tsx");

    expect(existsSync(stickyPath)).toBe(true);

    const page = source("app/page.tsx");
    const sticky = source("components/landing/sticky-cta.tsx");
    const css = source("app/globals.css");

    expect(page).toContain('import { StickyCta } from "../components/landing/sticky-cta"');
    expect(page).toContain("<StickyCta signedIn={signedIn} />");
    expect(sticky).toContain('"use client"');
    expect(sticky).toContain("IntersectionObserver");
    expect(sticky).toContain('document.querySelector(".hero")');
    expect(sticky).toContain('signedIn ? "/app" : "/sign-up"');
    expect(sticky).toContain("setDismissed(true)");
    expect(css).toContain(".sticky-mobile-cta");
    expect(css).toContain("@media (max-width: 680px)");
    expect(css).toContain(".sticky-mobile-cta.visible");
  });
});
