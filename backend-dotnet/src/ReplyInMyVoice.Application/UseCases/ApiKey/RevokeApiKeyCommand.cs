namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record RevokeApiKeyCommand(Guid UserId, Guid KeyId);
