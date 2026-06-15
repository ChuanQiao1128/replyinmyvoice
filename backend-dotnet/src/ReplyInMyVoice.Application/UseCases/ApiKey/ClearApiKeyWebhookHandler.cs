using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class ClearApiKeyWebhookHandler(
    IApiKeyRepository apiKeys,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(
        ClearApiKeyWebhookCommand command,
        CancellationToken ct = default)
    {
        var apiKey = await apiKeys.GetByIdForUserAsync(command.UserId, command.KeyId, ct);
        if (apiKey is null)
        {
            return false;
        }

        if (apiKey.WebhookUrl is not null || apiKey.WebhookSecret is not null)
        {
            apiKey.WebhookUrl = null;
            apiKey.WebhookSecret = null;
            apiKey.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(ct);
        }

        return true;
    }
}
