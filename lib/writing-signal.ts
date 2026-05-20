import { optionalEnv } from "./env";
import { estimateSaplingCostUsd } from "./observability/rewrite-cost";
import type { RewriteTelemetryCollector } from "./observability/rewrite-telemetry";

export type SignalLabel = "lower" | "low_signal" | "still_high" | "unavailable";

export type WritingSignalResult = {
  aiLikePercent: number | null;
  rawAiLikePercent?: number;
  unavailableReason?: string;
  sentenceScores?: Array<{
    sentence: string;
    aiLikePercent: number;
  }>;
  tokens?: string[];
  tokenProbabilities?: number[];
};

type MeasureWritingSignalOptions = {
  calibrateSentenceScores?: boolean;
  role?: "draft_signal" | "candidate_signal" | "repair_signal" | "final_signal";
  telemetry?: RewriteTelemetryCollector;
};

export function scoreToPercent(score: number): number {
  if (!Number.isFinite(score)) {
    return 0;
  }

  return Math.round(Math.min(Math.max(score, 0), 1) * 100);
}

export function getSignalLabel(
  draftAiLikePercent: number | null,
  rewriteAiLikePercent: number | null,
): SignalLabel {
  if (draftAiLikePercent === null || rewriteAiLikePercent === null) {
    return "unavailable";
  }

  if (rewriteAiLikePercent < draftAiLikePercent) {
    return "lower";
  }

  if (rewriteAiLikePercent <= 50) {
    return "low_signal";
  }

  return "still_high";
}

export function formatNaturalness(
  draftAiLikePercent: number | null,
  rewriteAiLikePercent: number | null,
) {
  const changePoints =
    draftAiLikePercent === null || rewriteAiLikePercent === null
      ? null
      : rewriteAiLikePercent - draftAiLikePercent;

  return {
    draftAiLikePercent,
    rewriteAiLikePercent,
    changePoints,
    label: getSignalLabel(draftAiLikePercent, rewriteAiLikePercent),
  };
}

function timeoutSignal(seconds: number) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), seconds * 1000);

  return {
    signal: controller.signal,
    clear: () => clearTimeout(timeout),
  };
}

function sleep(milliseconds: number) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

export async function measureWritingSignal(
  text: string,
  options: MeasureWritingSignalOptions = {},
): Promise<WritingSignalResult> {
  const provider = optionalEnv("WRITING_SIGNAL_PROVIDER", "sapling");
  const apiKey = optionalEnv("SAPLING_API_KEY");

  if (provider !== "sapling" || !apiKey) {
    return { aiLikePercent: null, unavailableReason: "not_configured" };
  }

  const timeoutSeconds = Number(optionalEnv("WRITING_SIGNAL_TIMEOUT_SEC", "10"));

  const retryCount = Math.max(
    0,
    Math.min(Number(optionalEnv("WRITING_SIGNAL_RETRY_COUNT", "1")), 2),
  );
  const role = options.role ?? "candidate_signal";
  const pricePer1000Chars = Number(
    optionalEnv("SAPLING_PRICE_PER_1000_CHARS_USD", "0.005"),
  );
  const estimatedCostUsd = estimateSaplingCostUsd({
    characters: text.length,
    pricePer1000Chars: Number.isFinite(pricePer1000Chars)
      ? pricePer1000Chars
      : 0.005,
  });

  for (let attempt = 0; attempt <= retryCount; attempt += 1) {
    const timeout = timeoutSignal(
      Number.isFinite(timeoutSeconds) ? timeoutSeconds : 10,
    );
    const startedAt = Date.now();
    const latencyMs = () => Math.max(0, Date.now() - startedAt);

    try {
      const response = await fetch("https://api.sapling.ai/api/v1/aidetect", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          key: apiKey,
          text,
          sent_scores: true,
          score_string: false,
        }),
        signal: timeout.signal,
      });

      if (!response.ok) {
        options.telemetry?.recordProviderCall({
          provider: "sapling",
          role,
          characters: text.length,
          estimatedCostUsd,
          latencyMs: latencyMs(),
          success: false,
          errorCode: String(response.status),
        });

        if (
          attempt < retryCount &&
          (response.status === 429 || response.status >= 500)
        ) {
          timeout.clear();
          await sleep(800 * (attempt + 1));
          continue;
        }

        return { aiLikePercent: null, unavailableReason: "provider_error" };
      }

      const payload = (await response.json()) as {
        score?: unknown;
        sentence_scores?: unknown;
        tokens?: unknown;
        token_probs?: unknown;
      };
      if (typeof payload.score !== "number") {
        options.telemetry?.recordProviderCall({
          provider: "sapling",
          role,
          characters: text.length,
          estimatedCostUsd,
          latencyMs: latencyMs(),
          success: false,
          errorCode: "schema_changed",
        });
        return { aiLikePercent: null, unavailableReason: "schema_changed" };
      }

      const sentenceScores = Array.isArray(payload.sentence_scores)
        ? payload.sentence_scores.flatMap((item) => {
            if (
              typeof item === "object" &&
              item !== null &&
              typeof (item as { sentence?: unknown }).sentence === "string" &&
              typeof (item as { score?: unknown }).score === "number"
            ) {
              return [
                {
                  sentence: (item as { sentence: string }).sentence.trim(),
                  aiLikePercent: scoreToPercent((item as { score: number }).score),
                },
              ];
            }

            return [];
          })
        : undefined;
      const tokens = Array.isArray(payload.tokens)
        ? payload.tokens.filter((token): token is string => typeof token === "string")
        : undefined;
      const tokenProbabilities = Array.isArray(payload.token_probs)
        ? payload.token_probs.filter(
            (probability): probability is number =>
              typeof probability === "number" && Number.isFinite(probability),
          )
        : undefined;

      const rawAiLikePercent = scoreToPercent(payload.score);
      const calibratedAiLikePercent = calibrateWithSentenceScores({
        calibrate: options.calibrateSentenceScores === true,
        rawAiLikePercent,
        sentenceScores,
      });

      options.telemetry?.recordProviderCall({
        provider: "sapling",
        role,
        characters: text.length,
        estimatedCostUsd,
        latencyMs: latencyMs(),
        success: true,
      });

      return {
        aiLikePercent: calibratedAiLikePercent,
        ...(calibratedAiLikePercent !== rawAiLikePercent
          ? { rawAiLikePercent }
          : {}),
        ...(sentenceScores ? { sentenceScores } : {}),
        ...(tokens ? { tokens } : {}),
        ...(tokenProbabilities ? { tokenProbabilities } : {}),
      };
    } catch {
      options.telemetry?.recordProviderCall({
        provider: "sapling",
        role,
        characters: text.length,
        estimatedCostUsd,
        latencyMs: latencyMs(),
        success: false,
        errorCode: "timeout_or_network",
      });
      if (attempt < retryCount) {
        timeout.clear();
        await sleep(800 * (attempt + 1));
        continue;
      }

      return { aiLikePercent: null, unavailableReason: "timeout_or_network" };
    } finally {
      timeout.clear();
    }
  }

  return { aiLikePercent: null, unavailableReason: "provider_error" };
}

function calibrateWithSentenceScores({
  calibrate,
  rawAiLikePercent,
  sentenceScores,
}: {
  calibrate: boolean;
  rawAiLikePercent: number;
  sentenceScores?: Array<{ aiLikePercent: number }>;
}) {
  if (!calibrate || rawAiLikePercent <= 50 || !sentenceScores?.length) {
    return rawAiLikePercent;
  }

  if (sentenceScores.every((sentence) => sentence.aiLikePercent <= 40)) {
    const average =
      sentenceScores.reduce((sum, sentence) => sum + sentence.aiLikePercent, 0) /
      sentenceScores.length;

    return Math.round(average);
  }

  return rawAiLikePercent;
}
