import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const routeSource = readFileSync(
  new URL("../../app/api/rewrite/route.ts", import.meta.url),
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
});
