namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record RotateApiKeyCommand(Guid UserId, Guid KeyId);
