export const scenarioOptions = [
  "General reply",
  "Email or message reply",
  "Customer support",
  "Blank / custom",
  "Cover letter",
  "Work update",
] as const;

export const tonePresetOptions = ["Warm", "Direct"] as const;

export type ScenarioOption = (typeof scenarioOptions)[number];
export type TonePreset = (typeof tonePresetOptions)[number];

export function tonePresetToTone(tonePreset: TonePreset): "warm" | "direct" {
  return tonePreset === "Warm" ? "warm" : "direct";
}
