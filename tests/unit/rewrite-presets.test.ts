import { describe, expect, it } from "vitest";

import {
  scenarioOptions,
  tonePresetOptions,
  tonePresetToTone,
} from "../../lib/rewrite-presets";

describe("rewrite presets", () => {
  it("offers the five broad workspace scenarios", () => {
    expect([...scenarioOptions]).toEqual([
      "Blank / custom",
      "Email or message reply",
      "Customer support",
      "Cover letter",
      "Work update",
    ]);
  });

  it("maps every visible tone preset to a valid API tone fallback", () => {
    expect([...tonePresetOptions]).toEqual([
      "Warm",
      "Professional",
      "Friendly",
      "Concise",
    ]);

    const mapped = tonePresetOptions.map((option) => tonePresetToTone(option));

    expect(mapped.every((value) => value === "warm" || value === "direct")).toBe(
      true,
    );
    expect(tonePresetToTone("Friendly")).toBe("warm");
    expect(tonePresetToTone("Professional")).toBe("direct");
    expect(tonePresetToTone("Concise")).toBe("direct");
  });
});
