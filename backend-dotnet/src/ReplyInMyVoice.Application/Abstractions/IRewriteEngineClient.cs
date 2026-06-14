using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Application.Abstractions;

/// <summary>
/// Frozen boundary for rewrite-engine swaps. Implementations receive the Domain
/// <see cref="RewriteRequest"/> DTO and must return a <see cref="RewriteEngineResult"/> without
/// requiring consumers to reference engine-internal types.
/// </summary>
public interface IRewriteEngineClient
{
    /// <summary>
    /// Rewrites the supplied request for an existing attempt.
    /// </summary>
    /// <remarks>
    /// Success requires <see cref="RewriteEngineResult.ResultJson"/> to contain
    /// <c>rewrittenText</c> as a non-empty string plus <c>changeSummary</c> and <c>riskNotes</c>
    /// JSON arrays; otherwise the job handler releases quota with
    /// <see cref="RewriteEngineErrorCodes.ProviderJsonParseFailed"/>. v1 API and webhook consumers
    /// also require <c>naturalness.draftAiLikePercent</c> and
    /// <c>naturalness.rewriteAiLikePercent</c> as JSON numbers, or they report the succeeded
    /// attempt as failed with <see cref="RewriteEngineErrorCodes.EngineUnavailableFallback"/>.
    /// <c>naturalness.changePoints</c>, <c>naturalness.label</c>, <c>optimization</c>, and extra
    /// fields are optional pass-through metadata.
    ///
    /// Failure is <c>Success=false</c> with an open-set <c>ErrorCode</c>. New engines should prefer
    /// <see cref="RewriteEngineErrorCodes.EngineEmittable"/>, but consumers must tolerate current
    /// pass-through model codes such as <c>model_http_&lt;status&gt;</c>, <c>model_timeout</c>,
    /// <c>model_empty</c>, <c>model_candidate_missing</c>, <c>model_json_parse_failed</c>,
    /// <c>model_network_failed</c>, <c>model_not_configured</c>, and
    /// <c>rewrite_model_failed</c>. Expected failures should be returned instead of thrown so quota
    /// can be released without charging. <c>ProviderCalls</c> must be populated on success and
    /// failure; the cost logger writes no row when it is empty.
    /// </remarks>
    Task<RewriteEngineResult> RewriteAsync(
        Guid attemptId,
        RewriteRequest request,
        CancellationToken ct = default);
}

public sealed record RewriteEngineResult(
    string? ResultJson,
    bool Success,
    string? ErrorCode,
    IReadOnlyList<RewriteEngineCallMetric> ProviderCalls);

public sealed record RewriteEngineCallMetric(
    string Provider,
    string Role,
    string? Model,
    int? InputTokens,
    int? OutputTokens,
    int? Characters,
    int? LatencyMs,
    bool Success,
    string? ErrorCode);
