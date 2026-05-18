import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const routeSource = readFileSync(
  new URL("../../app/api/rewrite/route.ts", import.meta.url),
  "utf8",
);

describe("rewrite API quality failure handling", () => {
  it("handles rewrite quality failures before usage is charged", () => {
    expect(routeSource).toContain("RewriteQualityError");
    expect(
      routeSource.indexOf("const rewrite = await rewriteWithOptimization"),
    ).toBeLessThan(
      routeSource.indexOf("await chargeSuccessfulRewrite"),
    );
    expect(routeSource).toContain("quality_gate_failed");
    expect(routeSource).toContain("422");
  });
});
