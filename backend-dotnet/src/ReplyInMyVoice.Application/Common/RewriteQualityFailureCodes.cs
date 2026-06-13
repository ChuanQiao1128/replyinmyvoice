namespace ReplyInMyVoice.Application.Common;

public static class RewriteQualityFailureCodes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "quality_signal_unavailable",
        "naturalness_gate_failed",
        "fact_gate_failed",
        "structure_gate_failed",
        "rewrite_quality_failed",
        "policy_intent_gate_failed",
    };
}
