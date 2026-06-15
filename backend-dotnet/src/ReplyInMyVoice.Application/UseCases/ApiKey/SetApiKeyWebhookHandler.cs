using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class SetApiKeyWebhookHandler(
    IApiKeyRepository apiKeys,
    IUnitOfWork unitOfWork)
{
    public async Task<ApiKeyWebhookResultDto?> HandleAsync(
        SetApiKeyWebhookCommand command,
        CancellationToken ct = default)
    {
        if (!ApiKeyCredential.TryNormalizeWebhookUrl(command.WebhookUrl, out var normalizedUrl))
        {
            throw new ArgumentException(
                "Webhook URL must be an absolute HTTPS URL that resolves to a public address.",
                nameof(command.WebhookUrl));
        }

        var apiKey = await apiKeys.GetByIdForUserAsync(command.UserId, command.KeyId, ct);
        if (apiKey is null || apiKey.RevokedAt is not null)
        {
            return null;
        }

        var webhookSecret = ApiKeyCredential.GenerateWebhookSecret();
        apiKey.WebhookUrl = normalizedUrl;
        apiKey.WebhookSecret = webhookSecret;
        apiKey.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);

        return new ApiKeyWebhookResultDto(apiKey.Id, normalizedUrl, webhookSecret);
    }
}
