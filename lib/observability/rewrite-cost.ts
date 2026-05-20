export type ProviderName = "openai" | "sapling";

export type ProviderCallTelemetry = {
  provider: ProviderName;
  role: string;
  model?: string;
  inputTokens?: number;
  outputTokens?: number;
  characters?: number;
  estimatedCostUsd: number;
  latencyMs?: number;
  success: boolean;
  errorCode?: string;
};

export type ProviderCallSummary = {
  openAiInputTokens: number;
  openAiOutputTokens: number;
  openAiCostUsd: number;
  saplingCallCount: number;
  saplingCharacters: number;
  saplingCostUsd: number;
  totalEstimatedCostUsd: number;
  modelsUsed: string[];
};

function safeNumber(value: number | undefined) {
  return Number.isFinite(value) ? Number(value) : 0;
}

export function estimateOpenAiCostUsd({
  inputPer1M,
  inputTokens,
  outputPer1M,
  outputTokens,
}: {
  inputTokens: number;
  outputTokens: number;
  inputPer1M: number;
  outputPer1M: number;
}) {
  return (inputTokens / 1_000_000) * inputPer1M +
    (outputTokens / 1_000_000) * outputPer1M;
}

export function estimateSaplingCostUsd({
  characters,
  pricePer1000Chars,
}: {
  characters: number;
  pricePer1000Chars: number;
}) {
  return (characters / 1000) * pricePer1000Chars;
}

export function summarizeProviderCalls(
  calls: ProviderCallTelemetry[],
): ProviderCallSummary {
  const modelsUsed = new Set<string>();
  let openAiInputTokens = 0;
  let openAiOutputTokens = 0;
  let openAiCostUsd = 0;
  let saplingCallCount = 0;
  let saplingCharacters = 0;
  let saplingCostUsd = 0;

  for (const call of calls) {
    if (call.model) {
      modelsUsed.add(call.model);
    }

    if (call.provider === "openai") {
      openAiInputTokens += safeNumber(call.inputTokens);
      openAiOutputTokens += safeNumber(call.outputTokens);
      openAiCostUsd += safeNumber(call.estimatedCostUsd);
    }

    if (call.provider === "sapling") {
      saplingCallCount += 1;
      saplingCharacters += safeNumber(call.characters);
      saplingCostUsd += safeNumber(call.estimatedCostUsd);
    }
  }

  return {
    openAiInputTokens,
    openAiOutputTokens,
    openAiCostUsd,
    saplingCallCount,
    saplingCharacters,
    saplingCostUsd,
    totalEstimatedCostUsd: openAiCostUsd + saplingCostUsd,
    modelsUsed: [...modelsUsed],
  };
}
