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
    expect(hubSource).toContain("A trial code unlocks 3 trial rewrites — no card.");
    expect(hubSource).toContain("same engine the API calls");
    expect(hubSource).toContain("≈ NZ$0.22 / rewrite");
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
    expect(pageSource).toContain("Browse the full spec");
    expect(pageSource).toContain("Raw OpenAPI JSON");
    expect(pageSource).toContain("Get your API key");

    expect(pageSource).toContain("POST /api/v1/rewrite");
    expect(pageSource).toContain("GET /api/v1/rewrite/{id}");
    expect(pageSource).toContain("GET /api/v1/usage");
    expect(pageSource).toContain("202 Accepted");
    expect(pageSource).toContain('"status": "processing"');
    expect(pageSource).toContain('"status": "succeeded"');
    expect(pageSource).toContain('"rewrittenText"');
    expect(pageSource).toContain('"signal"');
    expect(pageSource).toContain('"code": "engine_unavailable"');
    expect(pageSource).toContain("7f3c2c1a-9d4e-4b8a-b1f2-3a5d8e9c0f11");
    expect(pageSource).not.toContain("rw_123");

    expect(pageSource).toContain("Authorization: Bearer rmv_live_");
    expect(pageSource).toContain("Idempotency-Key");
    expect(pageSource).toContain("Idempotency-Key must be 120 characters or fewer.");
    expect(pageSource).toContain("X-RateLimit-");
    expect(pageSource).toContain("X-RateLimit-Limit: 60");
    expect(pageSource).toContain("X-RateLimit-Remaining: 59");
    expect(pageSource).toContain("X-RateLimit-Reset: 1812345678");
    expect(pageSource).toContain("Retry-After: 42");
    expect(pageSource).toContain("60 requests per minute");
    expect(pageSource).toContain("Submit request - Node (fetch)");
    expect(pageSource).toContain("Submit request - Python (requests)");
    expect(pageSource).toContain("Poll request - Node (fetch)");
    expect(pageSource).toContain("Poll request - Python (requests)");
    expect(pageSource).toContain("process.env.RIMV_API_KEY");
    expect(pageSource).toContain("requests.post");
    expect(pageSource).toContain("requests.get");

    for (const errorCode of [
      "invalid_request",
      "input_too_long",
      "invalid_key",
      "quota_exhausted",
      "idempotency_conflict",
      "not_found",
      "rate_limited",
      "rewrite_failed",
      "proxy_request_failed",
    ]) {
      expect(pageSource).toContain(errorCode);
    }

    expect(pageSource).toContain("Drafts under 10 characters are rejected before a job is accepted and are uncharged.");
    expect(pageSource).toContain("Unknown job id, or a job owned by another account/key.");
    expect(pageSource).toContain("Submit-time failure before a rewrite job is accepted.");
    expect(pageSource).toContain("Gateway error between the website API route and backend.");
    expect(pageSource).toContain("429 responses also include Retry-After so clients know how many seconds to wait.");
    expect(pageSource).toContain("periodEnd is string | null");
    expect(pageSource).toContain("The REST API is path-versioned under /api/v1.");
    expect(pageSource).toContain("Breaking changes ship under a new path version");
    expect(pageSource).toContain("What to expect");
    expect(pageSource).toContain("Jobs usually finish in seconds and may take up to about 50 s under load.");
    expect(pageSource).toContain("If a job is still processing after about 60 s, treat the attempt as failed; it is uncharged.");
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
    expect(pageSource).toContain("Install in your host");
    expect(pageSource).toContain("Add to Cursor");
    expect(pageSource).toContain("cursor://anysphere.cursor-deeplink/mcp/install");
    expect(pageSource).toContain("Copy Claude install command");
    expect(pageSource).toContain("remoteAuthorizationHeader");
    expect(pageSource).toContain('--header "${remoteAuthorizationHeader}"');
    expect(pageSource).toContain("code --add-mcp");
    expect(pageSource).toContain("Copy VS Code install command");
    expect(pageSource).toContain("Replace the rmv_live_xxx placeholder with your key before using the installed config.");
    expect(pageSource).toContain("1 credit per rewrite");
    expect(pageSource).toContain("402");
    expect(pageSource).toContain('href="/pricing"');
    expect(pageSource).toContain("Copy local target");
    expect(pageSource).toContain("Copy remote target");
    expect(pageSource).toContain("Copy local config");
    expect(pageSource).toContain("Copy remote config");
    expect(pageSource).toContain("Replace rmv_live_xxx with your key");
    expect(pageSource).toContain("Tool reference");
    expect(pageSource).toContain("draft");
    expect(pageSource).toContain("10 to 2400 characters");
    expect(pageSource).toContain("300-word draft limit");
    expect(pageSource).toContain("attempt_id");
    expect(pageSource).toContain("rewritten");
    expect(pageSource).toContain("optional changes");
    expect(pageSource).toContain("usually finish in a few seconds");
    expect(pageSource).toContain("about 50 seconds");
    expect(pageSource).toContain("Local stdio polls for up to about 2 minutes and currently does not return an attempt_id on timeout; prefer remote HTTP for long-running jobs.");
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
