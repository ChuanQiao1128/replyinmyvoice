using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IRewriteCostLogger
{
    Task WriteAsync(
        RewriteCostLogEntry entry,
        CancellationToken ct = default);
}

public sealed record RewriteCostLogEntry(
    Guid AttemptId,
    RewriteRequest Request,
    string? ResultJson,
    IReadOnlyList<RewriteEngineCallMetric> ProviderCalls,
    string Status,
    string? ErrorCode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);
