import { describe, expect, it } from "vitest";

import {
  scenarioOptions,
  tonePresetOptions,
  tonePresetToTone,
} from "../../lib/rewrite-presets";

describe("rewrite presets", () => {
  it("keeps scenario options internal and defaults to the reply workflow", () => {
    expect([...scenarioOptions]).toContain("General reply");
    expect(scenarioOptions[0]).toBe("General reply");
  });

  it("shows only the two stable tone presets", () => {
    expect([...tonePresetOptions]).toEqual(["Warm", "Direct"]);

    const mapped = tonePresetOptions.map((option) => tonePresetToTone(option));

    expect(mapped.every((value) => value === "warm" || value === "direct")).toBe(
      true,
    );
    expect(tonePresetToTone("Warm")).toBe("warm");
    expect(tonePresetToTone("Direct")).toBe("direct");
  });
});
