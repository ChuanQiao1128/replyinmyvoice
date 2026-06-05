import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("/developers API documentation page", () => {
  it("documents the live async v1 API and key path", () => {
    const pageSource = source("app/developers/page.tsx");
    const headerSource = source("components/site-header.tsx");

    expect(headerSource).toContain('href="/developers"');
    expect(pageSource).toContain('href="/developers/keys"');
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

    expect(pageSource).not.toContain("analyze-signal");
    expect(pageSource).not.toContain("not yet public");
    expect(pageSource).not.toContain("Trial codes unlock 3 rewrites");
    expect(pageSource).not.toContain("200 OK");
  });
});
