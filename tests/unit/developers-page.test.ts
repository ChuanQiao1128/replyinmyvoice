import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();
const sharedRetentionSentence =
  "Workspace history may be retained for up to 90 days, while API request and result records use a separate 30-day retention window.";

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("/developers API documentation page", () => {
  it("documents the live async v1 API and key path", () => {
    const pageSource = source("app/developers/page.tsx");
    const headerSource = source("components/site-header.tsx");

    expect(headerSource).toContain('href="/developers"');
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
    expect(pageSource).toContain('href="/developers/terms"');
    expect(pageSource).toContain('href="/developers/acceptable-use"');
    expect(pageSource).toContain('href="/developers/data"');

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
    expect(pageSource).toContain("Get a key");
    expect(pageSource).toContain("1 credit per rewrite");
    expect(pageSource).toContain("402");
    expect(pageSource).toContain("top-up");
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

    expect(dataSource).toContain("Data & Retention");
    expect(dataSource).toContain("RewriteAttempt");
    expect(dataSource).toContain("input and output");
    expect(dataSource).toContain("bounded 30-day retention");
    expect(dataSource).toContain(sharedRetentionSentence);
    expect(dataSource).toContain("purged");
    expect(dataSource).toContain("rewrite and naturalness providers");
  });
});
