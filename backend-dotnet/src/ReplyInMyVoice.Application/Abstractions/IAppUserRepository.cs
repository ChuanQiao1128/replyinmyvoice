using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IAppUserRepository
{
    Task AddAsync(AppUser user, CancellationToken ct = default);

    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<AppUser?> GetByExternalAuthUserIdAsync(string externalAuthUserId, CancellationToken ct = default);
}
