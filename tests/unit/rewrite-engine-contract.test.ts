import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

import { normalizeRewriteResponse } from "../../lib/rewrite-response";

const frozenQualityFailureCodes = [
  "quality_signal_unavailable",
  "structure_gate_failed",
  "naturalness_gate_failed",
  "fact_gate_failed",
  "policy_intent_gate_failed",
];

const rewriteRouteSource = readFileSync(
  new URL("../../app/api/rewrite/route.ts", import.meta.url),
  "utf8",
);
const rewriteAttemptRouteSource = readFileSync(
  new URL("../../app/api/rewrite-attempts/[attemptId]/route.ts", import.meta.url),
  "utf8",
);

function extractQualityFailureCodes(source: string) {
  const setLiteral = source.match(
    /const qualityFailureCodes = new Set\(\[\s*([\s\S]*?)\s*\]\);/,
  );
  expect(setLiteral).not.toBeNull();

  return Array.from(setLiteral![1].matchAll(/"([^"]+)"/g), (match) => match[1]);
}

describe("rewrite engine contract", () => {
  it("quality failure code sets in both rewrite proxies match the frozen engine contract", () => {
    const rewriteCodes = extractQualityFailureCodes(rewriteRouteSource);
    const attemptCodes = extractQualityFailureCodes(rewriteAttemptRouteSource);

    expect(rewriteCodes).toEqual(frozenQualityFailureCodes);
    expect(attemptCodes).toEqual(frozenQualityFailureCodes);
    expect(rewriteCodes).toEqual(attemptCodes);
  });

  it("normalizeRewriteResponse tolerates a minimal engine payload", () => {
    const normalized = normalizeRewriteResponse({ rewrittenText: "x" });

    expect(normalized).not.toBeNull();
    expect(normalized?.naturalness.label).toBe("unavailable");
  });
});
