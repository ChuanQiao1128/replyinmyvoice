export const scenarioOptions = [
  "Blank / custom",
  "Email or message reply",
  "Customer support",
  "Cover letter",
  "Work update",
] as const;

export const tonePresetOptions = [
  "Warm",
  "Professional",
  "Friendly",
  "Concise",
] as const;

export type ScenarioOption = (typeof scenarioOptions)[number];
export type TonePreset = (typeof tonePresetOptions)[number];

export function tonePresetToTone(tonePreset: TonePreset): "warm" | "direct" {
  return tonePreset === "Warm" || tonePreset === "Friendly" ? "warm" : "direct";
}
