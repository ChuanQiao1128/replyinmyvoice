namespace ReplyInMyVoice.Domain.Contracts;

/// <summary>
/// Canonical rewrite-engine error-code wire values consumed by rewrite jobs, API responses,
/// webhooks, cost logging, and Next proxy quality-failure handling.
/// </summary>
public static class RewriteEngineErrorCodes
{
    /// <summary>Provider timeout fallback used by the job handler and provider adapter.</summary>
    public const string ProviderTimeout = "provider_timeout";

    /// <summary>Provider failure fallback used by the job handler and provider adapter.</summary>
    public const string ProviderFailed = "provider_failed";

    /// <summary>Result-shape parse failure used by the job handler when success JSON is invalid.</summary>
    public const string ProviderJsonParseFailed = "provider_json_parse_failed";

    /// <summary>Stored request parse failure used by the job handler before engine execution.</summary>
    public const string RequestJsonParseFailed = "request_json_parse_failed";

    /// <summary>Expired reservation failure used by the job handler before engine execution.</summary>
    public const string ReservationExpired = "reservation_expired";

    /// <summary>Expired in-progress reservation failure used by reservation cleanup.</summary>
    public const string ProcessingTimedOut = "processing_timed_out";

    /// <summary>Quality-gate failure consumed by Next proxies as not charged.</summary>
    public const string QualitySignalUnavailable = "quality_signal_unavailable";

    /// <summary>Quality-gate failure consumed by Next proxies as not charged.</summary>
    public const string NaturalnessGateFailed = "naturalness_gate_failed";

    /// <summary>Quality-gate failure consumed by Next proxies as not charged.</summary>
    public const string FactGateFailed = "fact_gate_failed";

    /// <summary>Quality-gate failure consumed by Next proxies as not charged.</summary>
    public const string StructureGateFailed = "structure_gate_failed";

    /// <summary>
    /// Reserved quality-gate failure consumed by Next proxies as not charged; the current engine
    /// folds this case into <see cref="FactGateFailed"/>.
    /// </summary>
    public const string PolicyIntentGateFailed = "policy_intent_gate_failed";

    /// <summary>Recommended terminal quality failure emitted by rewrite-engine implementations.</summary>
    public const string RewriteQualityFailed = "rewrite_quality_failed";

    /// <summary>Consumer fallback for missing or unknown engine failure details.</summary>
    public const string EngineUnavailableFallback = "engine_unavailable";

    /// <summary>
    /// Exact quality-failure codes that the Next proxies map to the not-charged 422 response.
    /// </summary>
    public static readonly IReadOnlySet<string> QualityGateNotCharged =
        new HashSet<string>(StringComparer.Ordinal)
        {
            QualitySignalUnavailable,
            StructureGateFailed,
            NaturalnessGateFailed,
            FactGateFailed,
            PolicyIntentGateFailed,
        };

    /// <summary>
    /// Canonical recommended failure codes for new rewrite-engine implementations. Failure
    /// ErrorCode is intentionally an open set, and consumers must tolerate other values.
    /// </summary>
    public static readonly IReadOnlySet<string> EngineEmittable =
        new HashSet<string>(StringComparer.Ordinal)
        {
            QualitySignalUnavailable,
            NaturalnessGateFailed,
            FactGateFailed,
            StructureGateFailed,
            RewriteQualityFailed,
            ProviderTimeout,
            ProviderFailed,
        };
}
