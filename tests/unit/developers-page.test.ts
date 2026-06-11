import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();
const sharedRetentionSentence =
  "Workspace history may be retained for up to 90 days, while API request and result records use a separate 30-day retention window.";

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("/developers hub and API documentation pages", () => {
  it("keeps /developers as a two-path commercial hub", () => {
    const hubSource = source("app/developers/page.tsx");
    const headerSource = source("components/site-header.tsx");
    const footerSource = source("components/site-footer.tsx");
    const sitemapSource = source("app/sitemap.ts");
    const pricingSource = source("app/pricing/page.tsx");

    expect(headerSource).toContain('href="/developers"');
    expect(footerSource).toContain('href="/developers"');
    expect(sitemapSource).toContain("/developers/api");
    expect(pricingSource).toContain('href="/developers/api"');

    expect(hubSource).toContain("REST API");
    expect(hubSource).toContain("MCP server");
    expect(hubSource).toContain('href="/developers/api"');
    expect(hubSource).toContain('href="/developers/mcp"');
    expect(hubSource).toContain('href="/developers/api#quickstart"');
    expect(hubSource).toContain('href="/api/v1/openapi.json"');
    expect(hubSource).toContain("OpenAPI specification");

    expect(hubSource).toContain("One API key works for both paths");
    expect(hubSource).toContain("Website and API rewrites share one balance");
    expect(hubSource).toContain("Pro/API NZ$19.90/mo");
    expect(hubSource).toContain("90 rewrites/month shared across web + API");
    expect(hubSource).toContain("No free API tier");
    expect(hubSource).toContain("60 requests/min per key");
    expect(hubSource).toContain('href="/pricing#pro"');

    expect(hubSource).toContain('href: "/developers/terms"');
    expect(hubSource).toContain('href: "/developers/acceptable-use"');
    expect(hubSource).toContain('href: "/developers/data"');
    expect(hubSource).toContain("DevelopersAnchorRedirect");

    expect(hubSource).not.toContain("POST /api/v1/rewrite");
    expect(hubSource).not.toContain("GET /api/v1/rewrite/{id}");
    expect(hubSource).not.toContain("GET /api/v1/usage");
    expect(hubSource).not.toContain('id="quickstart"');
    expect(hubSource).not.toContain("200 OK");
  });

  it("redirects legacy /developers section fragments to the API reference route", () => {
    const redirectSource = source("app/developers/developers-anchor-redirect.tsx");

    for (const hash of [
      "#quickstart",
      "#auth",
      "#api",
      "#errors",
      "#guides",
      "#pricing",
    ]) {
      expect(redirectSource).toContain(hash);
    }

    expect(redirectSource).toContain("window.location.hash");
    expect(redirectSource).toContain("window.location.replace(`/developers/api${hash}`)");
  });

  it("moves the live async v1 API reference to /developers/api", () => {
    const pageSource = source("app/developers/api/page.tsx");

    for (const sectionId of [
      "quickstart",
      "auth",
      "api",
      "errors",
      "guides",
      "pricing",
    ]) {
      expect(pageSource).toContain(`id="${sectionId}"`);
    }

    expect(pageSource).toContain('href="/developers/keys"');
    expect(pageSource).toContain('href="/developers/mcp"');
    expect(pageSource).toContain('href="/api/v1/openapi.json"');
    expect(pageSource).toContain("OpenAPI specification");
    expect(pageSource).toContain("Get your API key");

    expect(pageSource).toContain("POST /api/v1/rewrite");
    expect(pageSource).toContain("GET /api/v1/rewrite/{id}");
    expect(pageSource).toContain("GET /api/v1/usage");
    expect(pageSource).toContain("202 Accepted");
    expect(pageSource).toContain('"status": "processing"');
    expect(pageSource).toContain('"status": "succeeded"');
    expect(pageSource).toContain('"rewrittenText"');
    expect(pageSource).toContain('"signal"');

    expect(pageSource).toContain("Authorization: Bearer rmv_live_");
    expect(pageSource).toContain("Idempotency-Key");
    expect(pageSource).toContain("X-RateLimit-");
    expect(pageSource).toContain("60 requests per minute");

    for (const errorCode of [
      "invalid_request",
      "input_too_long",
      "invalid_key",
      "quota_exhausted",
      "idempotency_conflict",
      "rate_limited",
    ]) {
      expect(pageSource).toContain(errorCode);
    }

    expect(pageSource).toContain("only a succeeded rewrite costs 1");
    expect(pageSource).toContain("30-day retention");
    expect(pageSource).toContain("naturalness reference");
    expect(pageSource).toContain("not accepted in the request");
    expect(pageSource).not.toContain("Two ways to integrate");
    expect(pageSource).not.toContain("Overview");

    expect(pageSource).not.toContain("analyze-signal");
    expect(pageSource).not.toContain("not yet public");
    expect(pageSource).not.toContain("Trial codes unlock 3 rewrites");
    expect(pageSource).not.toContain("200 OK");
  });

  it("documents MCP connection options and host snippets", () => {
    const pageSource = source("app/developers/mcp/page.tsx");

    expect(pageSource).toContain("Reply In My Voice MCP server");
    expect(pageSource).toContain("npx @replyinmyvoiceashuman/mcp-server");
    expect(pageSource).toContain("https://replyinmyvoice.com/api/mcp");
    expect(pageSource).toContain("Authorization");
    expect(pageSource).toContain("Bearer rmv_live_xxx");

    for (const host of ["Claude Code", "Codex", "Claude Desktop", "Cursor"]) {
      expect(pageSource).toContain(host);
    }

    expect(pageSource).toContain('href="/developers/keys"');
    expect(pageSource).toContain('href="/developers/api"');
    expect(pageSource).toContain("Get a key");
    expect(pageSource).toContain("1 credit per rewrite");
    expect(pageSource).toContain("402");
    expect(pageSource).toContain('href="/pricing"');
    expect(pageSource).toContain("Copy local config");
    expect(pageSource).toContain("Copy remote config");
    expect(pageSource).toContain("Replace rmv_live_xxx with your key");
    expect(pageSource).toContain("Tool reference");
    expect(pageSource).toContain("draft");
    expect(pageSource).toContain("10 to 2400 characters");
    expect(pageSource).toContain("attempt_id");
    expect(pageSource).toContain("rewritten");
    expect(pageSource).toContain("optional changes");
    expect(pageSource).toContain("usually finish in a few seconds");
    expect(pageSource).toContain("about 50 seconds");
    expect(pageSource).toContain("poll again");
    expect(pageSource).toContain("meaning and facts intact");

    expect(pageSource).not.toContain("analyze_signal");
    expect(pageSource).not.toContain("list_scenarios");
    expect(pageSource).not.toContain("scenario");
  });

  it("publishes draft API legal and data pages", () => {
    const termsSource = source("app/developers/terms/page.tsx");
    const acceptableUseSource = source("app/developers/acceptable-use/page.tsx");
    const dataSource = source("app/developers/data/page.tsx");

    for (const pageSource of [termsSource, acceptableUseSource, dataSource]) {
      expect(pageSource).toContain("export default function");
      expect(pageSource).toContain("Effective 6 June 2026");
      expect(pageSource).not.toContain("Draft — pending review");
      expect(pageSource).toContain("TimeAwake Ltd");
      expect(pageSource).toContain("replyinmyvoice.com");
    }

    expect(termsSource).toContain("API Terms of Use");
    expect(termsSource).toContain("per succeeded rewrite");
    expect(termsSource).toContain("shared quota");
    expect(termsSource).toContain("no free tier");
    expect(termsSource).toContain("informational naturalness reference");
    expect(termsSource).toContain("not a guarantee");

    expect(acceptableUseSource).toContain("Acceptable Use Policy");
    expect(acceptableUseSource).toContain("illegal, deceptive, abusive, or harassing");
    expect(acceptableUseSource).toContain("overload or reverse-engineer");
    expect(acceptableUseSource).toContain("resell raw access");
    expect(acceptableUseSource).toContain("Allowed example");
    expect(acceptableUseSource).toContain("value-add integration");
    expect(acceptableUseSource).toContain("Disallowed example");
    expect(acceptableUseSource).toContain("bare pass-through service");
    expect(acceptableUseSource).toContain("key sharing");

    expect(dataSource).toContain("Data & Retention");
    expect(dataSource).toContain("RewriteAttempt");
    expect(dataSource).toContain("input and output");
    expect(dataSource).toContain("bounded 30-day retention");
    expect(dataSource).toContain(sharedRetentionSentence);
    expect(dataSource).toContain("purged");
    expect(dataSource).toContain("rewrite and naturalness providers");
    expect(dataSource).toContain("Processing and residency");
    expect(dataSource).toContain("does not promise a specific country, region, or single-region residency");
    expect(dataSource).toContain("/app/account");
    expect(dataSource).toContain("within 30 days");
  });
});
