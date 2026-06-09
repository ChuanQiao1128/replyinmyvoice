using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IApiKeyUsageRepository
{
    Task<IReadOnlyDictionary<Guid, ApiUsageCountDto>> CountByApiKeyAsync(
        IReadOnlyCollection<Guid> apiKeyIds,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApiUsageRowDto>> ListUsageRowsAsync(
        Guid userId,
        DateTimeOffset windowStart,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApiUsageRecentItemDto>> ListRecentAsync(
        Guid userId,
        DateTimeOffset windowStart,
        int limit,
        CancellationToken ct = default);
}
