using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IRewriteEngineClient
{
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
