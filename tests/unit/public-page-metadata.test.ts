import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

const publicPages = [
  "app/pricing/page.tsx",
  "app/privacy/page.tsx",
  "app/terms/page.tsx",
  "app/developers/page.tsx",
  "app/developers/mcp/page.tsx",
];

describe("public page metadata", () => {
  it("wires the root metadata base and share image", () => {
    const layoutSource = source("app/layout.tsx");

    expect(layoutSource).toContain(
      'metadataBase: new URL("https://replyinmyvoice.com")',
    );
    expect(layoutSource).toContain('images: "/og.png"');
    expect(layoutSource).toContain('card: "summary_large_image"');
  });

  it("exports page-specific metadata with Open Graph and Twitter fields", () => {
    for (const pagePath of publicPages) {
      const pageSource = source(pagePath);

      expect(pageSource).toContain("export const metadata");
      expect(pageSource).toContain("title:");
      expect(pageSource).toContain("description:");
      expect(pageSource).toContain("openGraph:");
      expect(pageSource).toContain("twitter:");
      expect(pageSource).toContain('images: "/og.png"');
      expect(pageSource).toContain('card: "summary_large_image"');
    }
  });
});
