using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey apiKey, CancellationToken ct = default);

    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListRehashPendingAsync(int batchSize, CancellationToken ct = default);

    Task<ApiKey?> GetByIdForUserAsync(Guid userId, Guid keyId, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListByUserIdAsync(Guid userId, CancellationToken ct = default);

    void TouchLastUsed(ApiKey apiKey, DateTimeOffset now);

    void DiscardPendingChanges(ApiKey apiKey);
}
