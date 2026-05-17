import { describe, expect, it } from "vitest";

import {
  audienceOptions,
  formatMustKeep,
  mustKeepOptions,
  purposeOptions,
  tonePresetOptions,
  tonePresetToTone,
} from "../../lib/rewrite-presets";

describe("rewrite presets", () => {
  it("maps every visible tone preset to a valid API tone fallback", () => {
    const mapped = tonePresetOptions.map((option) => tonePresetToTone(option));

    expect(mapped.every((value) => value === "warm" || value === "direct")).toBe(
      true,
    );
    expect(tonePresetToTone("Friendly")).toBe("warm");
    expect(tonePresetToTone("Firm but polite")).toBe("direct");
  });

  it("covers the expected reply audiences and purposes", () => {
    expect(audienceOptions).toContain("Student");
    expect(audienceOptions).toContain("Prospect");
    expect(audienceOptions).toContain("Customer or client");
    expect(audienceOptions).toContain("Other");
    expect(purposeOptions).toContain("Follow up");
    expect(purposeOptions).toContain("Explain a delay");
    expect(purposeOptions).toContain("Other");
  });

  it("combines must-stay chips with custom detail text", () => {
    expect(mustKeepOptions).toContain("No new promises");
    expect(formatMustKeep(["Names", "Next step"], "Send by 4pm Friday.")).toBe(
      "Names; Next step; Send by 4pm Friday.",
    );
  });
});
