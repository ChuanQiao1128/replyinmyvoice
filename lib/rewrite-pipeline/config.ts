import { optionalEnv } from "../env";
import type { FactReconstructConfig } from "./types";

function numberEnv(name: string, fallback: number) {
  const rawValue = Number(optionalEnv(name, String(fallback)));
  return Number.isFinite(rawValue) ? rawValue : fallback;
}

export function getFactReconstructConfig({
  strategyVersion = optionalEnv(
    "REWRITE_STRATEGY_VERSION",
    "adaptive_rewrite_orchestrator",
  ),
}: {
  strategyVersion?: string;
} = {}): FactReconstructConfig {
  const legacyModel = optionalEnv("OPENAI_MODEL", "gpt-4o-mini");
  const cheapStructured = optionalEnv(
    "OPENAI_MODEL_CHEAP_STRUCTURED",
    optionalEnv("OPENAI_MODEL_REPAIR", legacyModel),
  );
  const midWriter = optionalEnv(
    "OPENAI_MODEL_MID_WRITER",
    optionalEnv("OPENAI_MODEL_PRIMARY", legacyModel),
  );
  const strongEscalation = optionalEnv(
    "OPENAI_MODEL_STRONG_ESCALATION",
    optionalEnv("OPENAI_MODEL_ESCALATION", legacyModel),
  );

  return {
    strategyVersion,
    naturalnessThreshold: numberEnv("NATURALNESS_THRESHOLD", 40),
    maxEscalations: Math.max(0, numberEnv("MAX_ESCALATIONS", 1)),
    models: {
      cheap_structured: cheapStructured,
      mid_writer: midWriter,
      strong_escalation: strongEscalation,
    },
    pricing: {
      cheap_structured: {
        inputPer1M: numberEnv("OPENAI_PRICE_CHEAP_INPUT_PER_1M", 0.2),
        outputPer1M: numberEnv("OPENAI_PRICE_CHEAP_OUTPUT_PER_1M", 1.25),
      },
      mid_writer: {
        inputPer1M: numberEnv("OPENAI_PRICE_MID_INPUT_PER_1M", 0.75),
        outputPer1M: numberEnv("OPENAI_PRICE_MID_OUTPUT_PER_1M", 4.5),
      },
      strong_escalation: {
        inputPer1M: numberEnv("OPENAI_PRICE_STRONG_INPUT_PER_1M", 2.5),
        outputPer1M: numberEnv("OPENAI_PRICE_STRONG_OUTPUT_PER_1M", 15),
      },
    },
  };
}
