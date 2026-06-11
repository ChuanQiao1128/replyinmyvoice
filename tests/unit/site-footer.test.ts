import * as React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { SiteFooter } from "../../components/site-footer";

function renderFooter() {
  return renderToStaticMarkup(React.createElement(SiteFooter));
}

beforeEach(() => {
  vi.stubGlobal("React", React);
});

describe("SiteFooter", () => {
  it("links to the developer pages from the Product group", () => {
    const html = renderFooter();

    expect(html).toContain('href="/developers"');
    expect(html).toContain('href="/developers/api"');
    expect(html).toContain('href="/developers/api#quickstart"');
    expect(html).toContain('href="/developers/mcp"');
    expect(html).toContain('href="/developers/keys"');
  });

  it("points Billing at the account page", () => {
    const html = renderFooter();

    expect(html).toContain('href="/app/account"');
    expect(html).not.toContain('href="/app">Billing</a>');
  });
});
