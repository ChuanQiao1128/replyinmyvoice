using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey apiKey, CancellationToken ct = default);

    Task<ApiKey?> GetByIdForUserAsync(Guid userId, Guid keyId, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListByUserIdAsync(Guid userId, CancellationToken ct = default);
}
