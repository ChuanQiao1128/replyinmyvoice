import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const routeSource = readFileSync(
  new URL("../../app/api/rewrite/route.ts", import.meta.url),
  "utf8",
);

const headerSource = readFileSync(
  new URL("../../lib/v1-response-headers.ts", import.meta.url),
  "utf8",
);

describe("consumer rewrite rate-limit proxy handling", () => {
  it("maps backend 429s to a friendly rate-limited payload and forwards standard headers", () => {
    expect(routeSource).toContain("response.status === 429");
    expect(routeSource).toContain('code: "rate_limited"');
    expect(routeSource).toContain("copyV1ResponseHeaders");
    expect(routeSource).toContain("You're sending rewrites too quickly.");
    expect(headerSource).toContain('"Retry-After"');
    expect(headerSource).toContain('"X-RateLimit-Limit"');
    expect(headerSource).toContain('"X-RateLimit-Remaining"');
    expect(headerSource).toContain('"X-RateLimit-Reset"');
  });
});
