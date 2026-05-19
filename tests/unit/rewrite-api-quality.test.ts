import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const routeSource = readFileSync(
  new URL("../../app/api/rewrite/route.ts", import.meta.url),
  "utf8",
);

describe("rewrite API quality failure handling", () => {
  it("uses only the fact-reconstruct pipeline and charges after quality gates pass", () => {
    expect(routeSource).toContain("FactReconstructQualityError");
    expect(routeSource).toContain("rewriteWithFactReconstruct");
    expect(routeSource).not.toContain("rewriteWithOptimization");
    expect(routeSource).not.toContain("isFactReconstructV2Enabled");
    expect(
      routeSource.indexOf("const rewrite = await rewriteWithFactReconstruct(input)"),
    ).toBeLessThan(
      routeSource.indexOf("await chargeSuccessfulRewrite"),
    );
    expect(routeSource).toContain("quality_gate_failed");
    expect(routeSource).toContain("422");
  });
});
