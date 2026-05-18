using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Providers;

public interface IRewriteProvider
{
    Task<RewriteProviderResult> RewriteAsync(Guid attemptId, RewriteRequest request, CancellationToken cancellationToken);
}

public sealed record RewriteProviderResult(
    string? ResultJson,
    bool Success,
    string? ErrorCode);
