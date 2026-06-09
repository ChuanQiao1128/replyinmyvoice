namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record ClearApiKeyWebhookCommand(Guid UserId, Guid KeyId);
