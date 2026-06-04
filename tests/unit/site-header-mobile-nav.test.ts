import { readFileSync } from "node:fs";
import { join } from "node:path";

import * as React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

const { entraAuthMock } = vi.hoisted(() => ({
  entraAuthMock: {
    getCurrentSession: vi.fn(),
  },
}));

vi.mock("../../lib/entra-auth", () => entraAuthMock);

import { SiteHeader } from "../../components/site-header";
import { getCurrentSession } from "../../lib/entra-auth";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

async function renderHeader() {
  return renderToStaticMarkup(await SiteHeader());
}

beforeEach(() => {
  vi.stubGlobal("React", React);
  vi.mocked(getCurrentSession).mockReset();
});

describe("SiteHeader mobile navigation", () => {
  it("renders a mobile menu affordance in the shared header", () => {
    const headerSource = source("components/site-header.tsx");

    expect(headerSource).toContain('className="mobile-nav-menu"');
    expect(headerSource).toContain("<summary");
    expect(headerSource).toContain('aria-label="Menu"');
  });

  it("keeps Sign in reachable for signed-out visitors", async () => {
    vi.mocked(getCurrentSession).mockResolvedValue(null);

    const html = await renderHeader();

    expect(html).toContain('href="/sign-in"');
    expect(html).toContain(">Sign in</a>");
  });

  it("replaces the narrow-screen blanket link hide with mobile menu CSS", () => {
    const globals = source("app/globals.css");

    expect(globals).toContain(".mobile-nav-menu");
    expect(globals).toContain(".mobile-nav-panel");
    expect(globals).not.toMatch(
      /\.nav-links\s+a:not\(\.btn\)\s*\{[\s\S]*?display\s*:\s*none[\s\S]*?\}/,
    );
    expect(globals).toMatch(
      /@media\s*\(max-width:\s*680px\)\s*\{[\s\S]*?\.nav-inline-links\s*\{[\s\S]*?display\s*:\s*none/,
    );
    expect(globals).toMatch(
      /@media\s*\(max-width:\s*680px\)\s*\{[\s\S]*?\.mobile-nav-menu\s*\{[\s\S]*?display\s*:\s*block/,
    );
  });
});
