namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record SetApiKeyWebhookCommand(
    Guid UserId,
    Guid KeyId,
    string WebhookUrl);
