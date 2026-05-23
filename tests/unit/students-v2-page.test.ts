import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("students v2 landing page", () => {
  it("uses the v2 landing shell, header, metadata, and sign-up CTAs", () => {
    const page = source("app/students/page.tsx");

    expect(page).toContain('<main className="rimv">');
    expect(page).toContain("<SiteHeader />");
    expect(page).toContain('title: "Student email & message rewriter"');
    expect(page).toContain("Extension requests · lecturer emails · internship follow-ups");
    expect(page).toContain("Sound like yourself when the message matters.");
    expect(page).toContain("Try 3 free rewrites — no card");
    expect(page).toContain('href="/sign-up"');
    expect(page).not.toContain("NZD $9");
  });

  it("keeps the supplied student examples and non-cheating positioning", () => {
    const page = source("app/students/page.tsx");

    expect(page).toContain("For messages you actually need to send.");
    expect(page).toContain("Ask for an assignment extension");
    expect(page).toContain("Tell a group member they need to contribute");
    expect(page).toContain("Why not just use ChatGPT?");
    expect(page).toContain(
      "It keeps your supplied facts in view and should not add details you did not give it.",
    );
    expect(page).toContain(
      "Write the reply, then decide if it sounds right.",
    );
  });

  it("is present in the sitemap and not blocked by robots", () => {
    expect(source("app/sitemap.ts")).toContain("`${SITE_URL}/students`");
    expect(source("app/robots.ts")).not.toContain("/students");
  });
});
