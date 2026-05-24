import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const routeSource = readFileSync(
  new URL("../../app/api/rewrite/route.ts", import.meta.url),
  "utf8",
);
const attemptRouteSource = readFileSync(
  new URL("../../app/api/rewrite-attempts/[attemptId]/route.ts", import.meta.url),
  "utf8",
);

describe("rewrite API quality failure handling", () => {
  it("proxies rewrite requests to the Azure Functions C# backend", () => {
    expect(routeSource).toContain("getAzureApiBaseUrl");
    expect(routeSource).toContain("getCurrentAccessToken");
    expect(routeSource).toContain("X-Idempotency-Key");
    expect(routeSource).toContain("/api/rewrite");
    expect(routeSource).toContain("Authorization");
    expect(routeSource).not.toContain("rewriteWithFactReconstruct");
    expect(routeSource).not.toContain("rewriteWithOptimization");
    expect(routeSource).not.toContain("isFactReconstructV2Enabled");
    expect(routeSource).not.toContain("chargeSuccessfulRewrite");
    expect(routeSource).not.toContain("ensureQuotaAvailable");
  });

  it("normalizes direct success payloads and keeps pending attempts distinct", () => {
    expect(routeSource).toContain("normalizeRewriteResponse");
    expect(routeSource).toContain('code: "rewrite_pending"');
    expect(routeSource).toContain("attemptId");
    expect(attemptRouteSource).toContain("normalizeRewriteResponse");
    expect(attemptRouteSource).toContain('code: "rewrite_pending"');
  });
});
