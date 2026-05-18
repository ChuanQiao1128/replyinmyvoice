export type SignalQualityStatus =
  | "pass_below_threshold"
  | "pass_reduction"
  | "fail_worse"
  | "fail_insufficient_reduction"
  | "signal_unavailable";

export type SignalQualityResult = {
  status: SignalQualityStatus;
  draftPercent: number | null;
  rewritePercent: number | null;
  changePoints: number | null;
  reason: string;
};

export type SignalQualityInput = {
  draftPercent: number | null;
  rewritePercent: number | null;
};

export const SIGNAL_PASS_THRESHOLD = 50;
export const SIGNAL_REQUIRED_DROP_POINTS = 30;

export function evaluateSignalQuality({
  draftPercent,
  rewritePercent,
}: SignalQualityInput): SignalQualityResult {
  if (draftPercent === null || rewritePercent === null) {
    return {
      status: "signal_unavailable",
      draftPercent,
      rewritePercent,
      changePoints: null,
      reason: "Writing signal unavailable.",
    };
  }

  const changePoints = rewritePercent - draftPercent;

  if (rewritePercent >= draftPercent) {
    return {
      status: "fail_worse",
      draftPercent,
      rewritePercent,
      changePoints,
      reason: "Rewrite AI-like signal is not lower than the draft.",
    };
  }

  if (rewritePercent < SIGNAL_PASS_THRESHOLD) {
    return {
      status: "pass_below_threshold",
      draftPercent,
      rewritePercent,
      changePoints,
      reason: "Rewrite AI-like signal is below the target threshold.",
    };
  }

  if (draftPercent - rewritePercent >= SIGNAL_REQUIRED_DROP_POINTS) {
    return {
      status: "pass_reduction",
      draftPercent,
      rewritePercent,
      changePoints,
      reason: "Rewrite AI-like signal dropped by at least 30 points.",
    };
  }

  return {
    status: "fail_insufficient_reduction",
    draftPercent,
    rewritePercent,
    changePoints,
    reason: "Rewrite AI-like signal stayed high and did not drop enough.",
  };
}

export function shouldRejectCandidate(result: SignalQualityResult) {
  return (
    result.status === "fail_worse" ||
    result.status === "fail_insufficient_reduction"
  );
}

export function shouldRepairCandidate(result: SignalQualityResult) {
  return shouldRejectCandidate(result);
}
