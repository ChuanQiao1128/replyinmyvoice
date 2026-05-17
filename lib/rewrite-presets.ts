export const audienceOptions = [
  "Student",
  "Parent or guardian",
  "Prospect",
  "Customer or client",
  "Teammate",
  "Manager",
  "Vendor or partner",
  "General professional contact",
  "Other",
] as const;

export const purposeOptions = [
  "Reply clearly",
  "Follow up",
  "Explain a delay",
  "Apologize",
  "Say no politely",
  "Ask for more information",
  "Schedule or reschedule",
  "Clarify a misunderstanding",
  "Summarize next steps",
  "Other",
] as const;

export const mustKeepOptions = [
  "Names",
  "Dates and times",
  "Prices or numbers",
  "Policy details",
  "Next step",
  "Apology",
  "No new promises",
  "No extra details",
] as const;

export const tonePresetOptions = [
  "Warm",
  "Direct",
  "Professional",
  "Friendly",
  "Firm but polite",
  "Apologetic",
  "Concise",
] as const;

export type AudienceOption = (typeof audienceOptions)[number];
export type PurposeOption = (typeof purposeOptions)[number];
export type MustKeepOption = (typeof mustKeepOptions)[number];
export type TonePreset = (typeof tonePresetOptions)[number];

export function tonePresetToTone(tonePreset: TonePreset): "warm" | "direct" {
  return ["Warm", "Friendly", "Apologetic"].includes(tonePreset)
    ? "warm"
    : "direct";
}

export function formatMustKeep(values: MustKeepOption[], custom: string) {
  const parts = [...values, custom].map((value) => value.trim()).filter(Boolean);
  return parts.join("; ");
}
