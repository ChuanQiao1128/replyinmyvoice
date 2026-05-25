import { optionalEnv } from "./env";
import { estimateSaplingCostUsd } from "./observability/rewrite-cost";
import type { RewriteTelemetryCollector } from "./observability/rewrite-telemetry";

export type SignalLabel = "lower" | "low_signal" | "still_high" | "unavailable";

export type WritingSignalResult = {
  aiLikePercent: number | null;
  rawAiLikePercent?: number;
  unavailableReason?: string;
  chars?: number;
  callCount?: number;
  latencyMs?: number;
  sentenceScores?: Array<{
    sentence: string;
    aiLikePercent: number;
  }>;
  tokens?: string[];
  tokenProbabilities?: number[];
};

type MeasureWritingSignalOptions = {
  // Retained for call-site compatibility. Robust sentence-level aggregation
  // (see robustAiLikePercent) is now always applied, so this flag is a no-op.
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
    return {
      aiLikePercent: null,
      unavailableReason: "not_configured",
      chars: 0,
      callCount: 0,
      latencyMs: 0,
    };
  }

  const measurementStartedAt = Date.now();
  let callCount = 0;
  const measurementMeta = () => ({
    chars: text.length,
    callCount,
    latencyMs: Math.max(0, Date.now() - measurementStartedAt),
  });
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
    callCount += 1;

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

        return {
          aiLikePercent: null,
          unavailableReason: "provider_error",
          ...measurementMeta(),
        };
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
        return {
          aiLikePercent: null,
          unavailableReason: "schema_changed",
          ...measurementMeta(),
        };
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
      const aiLikePercent = robustAiLikePercent({
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
        aiLikePercent,
        ...measurementMeta(),
        ...(aiLikePercent !== rawAiLikePercent ? { rawAiLikePercent } : {}),
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

      return {
        aiLikePercent: null,
        unavailableReason: "timeout_or_network",
        ...measurementMeta(),
      };
    } finally {
      timeout.clear();
    }
  }

  return {
    aiLikePercent: null,
    unavailableReason: "provider_error",
    ...measurementMeta(),
  };
}

// Bare list markers ("1.", "2)", "a.", "-", "•") that Sapling's sentence
// splitter emits as standalone "sentences" and then scores wildly. They are not
// real sentences and must not vote in the overall signal.
const LIST_MARKER_PATTERN = /^(\d+[.)]|[a-z][.)]|[-*•·–—]+)$/i;

function isScorableSentence(sentence: string): boolean {
  const trimmed = sentence.trim();
  if (trimmed.length === 0 || LIST_MARKER_PATTERN.test(trimmed)) {
    return false;
  }

  // Drop single-token fragments (stray markers, lone sign-offs) the splitter
  // over-segments; they carry no reliable signal but skew aggregates. Genuine
  // short sentences ("Thanks again.", "No rush.") are 2+ words and kept.
  const wordCount = trimmed.split(/\s+/).filter(Boolean).length;
  return wordCount >= 2;
}

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);

  return sorted.length % 2 === 1
    ? sorted[mid]
    : Math.round((sorted[mid - 1] + sorted[mid]) / 2);
}

// Sapling's raw overall AI-score behaves like a max: one boilerplate or
// list-marker sentence pins it to 100% even when every factual sentence reads
// human (scores 0%). That emptied fact-perfect emails. Use the median of the
// real (scorable) sentence scores instead — it only crosses the threshold when
// at least half the email reads AI-like, so a single outlier can no longer veto
// an otherwise-human reply. Robustness is always applied; the previous timid
// calibration (mean, only when *every* sentence was already clean) was a no-op
// in exactly the outlier cases that needed it.
function robustAiLikePercent({
  rawAiLikePercent,
  sentenceScores,
}: {
  rawAiLikePercent: number;
  sentenceScores?: Array<{ sentence: string; aiLikePercent: number }>;
}): number {
  const scorable = (sentenceScores ?? []).filter((sentence) =>
    isScorableSentence(sentence.sentence),
  );

  // Only fall back to the raw overall when there is no real sentence to score.
  // Even 1–2 sentences are more trustworthy than the outlier-dominated overall.
  if (scorable.length === 0) {
    return rawAiLikePercent;
  }

  return median(scorable.map((sentence) => sentence.aiLikePercent));
}
