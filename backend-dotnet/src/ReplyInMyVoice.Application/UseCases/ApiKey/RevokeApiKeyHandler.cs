using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class RevokeApiKeyHandler(
    IApiKeyRepository apiKeys,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(
        RevokeApiKeyCommand command,
        CancellationToken ct = default)
    {
        var apiKey = await apiKeys.GetByIdForUserAsync(command.UserId, command.KeyId, ct);
        if (apiKey is null)
        {
            return false;
        }

        if (apiKey.RevokedAt is null)
        {
            apiKey.RevokedAt = DateTimeOffset.UtcNow;
            apiKey.UpdatedAt = apiKey.RevokedAt.Value;
            apiKey.RowVersion = Guid.NewGuid();
            await unitOfWork.SaveChangesAsync(ct);
        }

        return true;
    }
}
