import { optionalEnv } from "./env";

export type SignalLabel = "lower" | "still_high" | "unavailable";

export type WritingSignalResult = {
  aiLikePercent: number | null;
  unavailableReason?: string;
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

  if (rewriteAiLikePercent <= 50 || rewriteAiLikePercent <= draftAiLikePercent - 10) {
    return "lower";
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

export async function measureWritingSignal(
  text: string,
): Promise<WritingSignalResult> {
  const provider = optionalEnv("WRITING_SIGNAL_PROVIDER", "sapling");
  const apiKey = optionalEnv("SAPLING_API_KEY");

  if (provider !== "sapling" || !apiKey) {
    return { aiLikePercent: null, unavailableReason: "not_configured" };
  }

  const timeoutSeconds = Number(optionalEnv("WRITING_SIGNAL_TIMEOUT_SEC", "10"));
  const timeout = timeoutSignal(Number.isFinite(timeoutSeconds) ? timeoutSeconds : 10);

  try {
    const response = await fetch("https://api.sapling.ai/api/v1/aidetect", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        key: apiKey,
        text,
      }),
      signal: timeout.signal,
    });

    if (!response.ok) {
      return { aiLikePercent: null, unavailableReason: "provider_error" };
    }

    const payload = (await response.json()) as { score?: unknown };
    if (typeof payload.score !== "number") {
      return { aiLikePercent: null, unavailableReason: "schema_changed" };
    }

    return { aiLikePercent: scoreToPercent(payload.score) };
  } catch {
    return { aiLikePercent: null, unavailableReason: "timeout_or_network" };
  } finally {
    timeout.clear();
  }
}
