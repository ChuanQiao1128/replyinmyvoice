export type Naturalness = {
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  label: "lower" | "low_signal" | "still_high" | "unavailable";
};

export type RewriteResponse = {
  rewrittenText: string;
  changeSummary: string[];
  riskNotes: string[];
  naturalness: Naturalness;
  optimization: {
    internalStrategiesTried: number;
    userUsageCharged: 1;
    selectionStatus?: "passed" | "best_available";
    diagnosisTags?: string[];
    rewritePlanSummary?: string;
    candidateSignals?: Array<{
      stage: "initial" | "targeted_repair" | "repair" | "fallback";
      aiLikePercent: number | null;
      status: string;
      rejected: boolean;
      reason: string;
    }>;
  };
};

const naturalnessLabels = new Set<Naturalness["label"]>([
  "lower",
  "low_signal",
  "still_high",
  "unavailable",
]);

const candidateSignalStages = new Set<
  NonNullable<RewriteResponse["optimization"]["candidateSignals"]>[number]["stage"]
>(["initial", "targeted_repair", "repair", "fallback"]);

export function isRewritePendingPayload(payload: unknown) {
  const record = asRecord(payload);
  return record?.code === "rewrite_pending";
}

export function getRewriteAttemptId(payload: unknown) {
  const record = asRecord(payload);
  const attemptId = record?.attemptId ?? record?.AttemptId;
  return typeof attemptId === "string" && attemptId.trim() ? attemptId : null;
}

export function normalizeRewriteResponse(
  payload: unknown,
): RewriteResponse | null {
  const record = asRecord(payload);
  const rewrittenText =
    typeof record?.rewrittenText === "string" ? record.rewrittenText : "";

  if (!rewrittenText.trim()) {
    return null;
  }

  return {
    rewrittenText,
    changeSummary: stringArray(record?.changeSummary),
    riskNotes: stringArray(record?.riskNotes),
    naturalness: normalizeNaturalness(record?.naturalness),
    optimization: normalizeOptimization(record?.optimization),
  };
}

export function normalizeNaturalness(payload: unknown): Naturalness {
  const record = asRecord(payload);
  const label = record?.label;

  return {
    draftAiLikePercent: nullableNumber(record?.draftAiLikePercent),
    rewriteAiLikePercent: nullableNumber(record?.rewriteAiLikePercent),
    changePoints: nullableNumber(record?.changePoints),
    label:
      typeof label === "string" &&
      naturalnessLabels.has(label as Naturalness["label"])
        ? (label as Naturalness["label"])
        : "unavailable",
  };
}

function normalizeOptimization(
  payload: unknown,
): RewriteResponse["optimization"] {
  const record = asRecord(payload);
  const selectionStatus = record?.selectionStatus;
  const rewritePlanSummary = record?.rewritePlanSummary;
  const diagnosisTags = stringArray(record?.diagnosisTags);
  const candidateSignals = normalizeCandidateSignals(record?.candidateSignals);

  return {
    internalStrategiesTried:
      typeof record?.internalStrategiesTried === "number" &&
      Number.isFinite(record.internalStrategiesTried) &&
      record.internalStrategiesTried > 0
        ? Math.floor(record.internalStrategiesTried)
        : 1,
    userUsageCharged: 1,
    ...(selectionStatus === "passed" || selectionStatus === "best_available"
      ? { selectionStatus }
      : {}),
    ...(diagnosisTags.length ? { diagnosisTags } : {}),
    ...(typeof rewritePlanSummary === "string" && rewritePlanSummary.trim()
      ? { rewritePlanSummary }
      : {}),
    ...(candidateSignals.length ? { candidateSignals } : {}),
  };
}

function normalizeCandidateSignals(payload: unknown) {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload
    .map((item) => {
      const record = asRecord(item);
      if (!record) {
        return null;
      }

      const stage = record?.stage;
      if (
        typeof stage !== "string" ||
        !candidateSignalStages.has(
          stage as NonNullable<
            RewriteResponse["optimization"]["candidateSignals"]
          >[number]["stage"],
        )
      ) {
        return null;
      }

      return {
        stage: stage as NonNullable<
          RewriteResponse["optimization"]["candidateSignals"]
        >[number]["stage"],
        aiLikePercent: nullableNumber(record.aiLikePercent),
        status: typeof record.status === "string" ? record.status : "",
        rejected: record.rejected === true,
        reason: typeof record.reason === "string" ? record.reason : "",
      };
    })
    .filter((item): item is NonNullable<typeof item> => item !== null);
}

function stringArray(payload: unknown) {
  return Array.isArray(payload)
    ? payload.filter((item): item is string => typeof item === "string")
    : [];
}

function nullableNumber(payload: unknown) {
  return typeof payload === "number" && Number.isFinite(payload)
    ? payload
    : null;
}

function asRecord(payload: unknown): Record<string, unknown> | null {
  return payload !== null && typeof payload === "object" && !Array.isArray(payload)
    ? (payload as Record<string, unknown>)
    : null;
}
