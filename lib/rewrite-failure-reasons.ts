export const REWRITE_QUALITY_FAILURE_REASONS = [
  "signal_unavailable",
  "naturalness_gate_failed",
  "fact_check_failed",
  "reviewer_threshold_failed",
] as const;

export type RewriteQualityFailureReason =
  (typeof REWRITE_QUALITY_FAILURE_REASONS)[number];

export type RewriteRequestFailureStatus =
  | "success"
  | "quality_failed"
  | "server_failed";

const QUALITY_FAILURE_REASON_SET = new Set<string>(
  REWRITE_QUALITY_FAILURE_REASONS,
);

const GENERIC_SERVER_FAILURE_CODES = new Set([
  "abort_error",
  "error",
  "range_error",
  "reference_error",
  "server_error",
  "server_failed",
  "syntax_error",
  "type_error",
  "unknown_error",
  "unknownerror",
]);

const SIGNAL_UNAVAILABLE_CODES = new Set([
  "provider_error",
  "provider_failed",
  "schema_changed",
  "sapling_timeout",
  "timeout",
  "timeout_or_network",
]);

export function normalizeFailureCodeText(value: string | null | undefined) {
  const normalized = String(value ?? "")
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .replace(/[^a-zA-Z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .toLowerCase();

  return normalized.length > 0 ? normalized : null;
}

export function isRewriteQualityFailureReason(
  value: string | null | undefined,
): value is RewriteQualityFailureReason {
  const normalized = normalizeFailureCodeText(value);

  return normalized !== null && QUALITY_FAILURE_REASON_SET.has(normalized);
}

export function normalizeRewriteRequestFailureReason({
  errorCode,
  qualityReason,
  status,
}: {
  errorCode?: string | null;
  qualityReason?: string | null;
  status: RewriteRequestFailureStatus;
}) {
  if (status === "success") {
    return null;
  }

  const normalizedQualityReason = normalizeFailureCodeText(qualityReason);
  const normalizedErrorCode = normalizeFailureCodeText(errorCode);

  if (status === "quality_failed") {
    if (isRewriteQualityFailureReason(normalizedQualityReason)) {
      return normalizedQualityReason;
    }

    if (isRewriteQualityFailureReason(normalizedErrorCode)) {
      return normalizedErrorCode;
    }

    if (
      normalizedQualityReason !== null &&
      SIGNAL_UNAVAILABLE_CODES.has(normalizedQualityReason)
    ) {
      return "signal_unavailable";
    }

    if (
      normalizedErrorCode !== null &&
      SIGNAL_UNAVAILABLE_CODES.has(normalizedErrorCode)
    ) {
      return "signal_unavailable";
    }

    return "naturalness_gate_failed";
  }

  if (
    normalizedErrorCode === null ||
    GENERIC_SERVER_FAILURE_CODES.has(normalizedErrorCode)
  ) {
    return "server_failed";
  }

  return normalizedErrorCode;
}
