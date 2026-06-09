namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record GenerateApiKeyCommand(
    Guid UserId,
    string Name,
    bool IsTest = false);
