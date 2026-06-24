import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

import { homepageSampleCases } from "../../components/landing/sample-cases";

const root = process.cwd();
const sampleCasesDocument = readFileSync(
  join(root, "docs", "sample-cases.md"),
  "utf8",
);

function escaped(text: string) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function salutationNames(text: string) {
  return Array.from(text.matchAll(/\b(?:Hi|Hello|Dear)\s+([A-Z][a-z]+)/g)).map(
    (match) => match[1],
  );
}

describe("landing demo samples", () => {
  it("uses documented sample fixtures instead of inline copy", () => {
    const source = readFileSync(
      join(root, "components", "landing", "interactive-demo.tsx"),
      "utf8",
    );

    expect(source).toContain(
      'import { homepageSampleCases } from "./sample-cases";',
    );
    expect(source).not.toContain("const scenarios = [");
  });

  it("keeps homepage sample values aligned with docs/sample-cases.md", () => {
    for (const sample of homepageSampleCases) {
      expect(sample.sourceDocument).toBe("docs/sample-cases.md");
      expect(sampleCasesDocument).toMatch(
        new RegExp(`## ${escaped(sample.label)}[\\s\\S]*Used on homepage: yes`),
      );
      expect(sampleCasesDocument).toContain(
        `Draft naturalness: ${sample.before}%`,
      );
      expect(sampleCasesDocument).toContain(
        `Rewrite naturalness: ${sample.after}%`,
      );
      expect(sampleCasesDocument).toContain(sample.context);
      expect(sampleCasesDocument).toContain(sample.draft);
      expect(sampleCasesDocument).toContain(sample.rewrite);
    }
  });

  it("does not introduce salutation names missing from the documented sample", () => {
    for (const sample of homepageSampleCases) {
      const suppliedText = `${sample.context}\n${sample.draft}`;

      for (const name of salutationNames(sample.rewrite)) {
        expect(suppliedText).toContain(name);
      }
    }
  });
});
