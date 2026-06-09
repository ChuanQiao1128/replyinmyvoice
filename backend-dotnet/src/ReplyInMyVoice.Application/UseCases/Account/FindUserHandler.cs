using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed class FindUserHandler(IAppUserRepository appUsers)
{
    public async Task<AppUser?> HandleAsync(
        FindUserQuery query,
        CancellationToken ct = default)
    {
        var normalizedExternalId = AccountUseCaseSupport.NormalizeExternalAuthUserId(query.ExternalAuthUserId);
        return await appUsers.GetByExternalAuthUserIdAsync(normalizedExternalId, ct);
    }
}
