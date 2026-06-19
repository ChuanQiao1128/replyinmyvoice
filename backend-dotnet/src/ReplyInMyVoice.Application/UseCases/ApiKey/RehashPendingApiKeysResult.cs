namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record RehashPendingApiKeysResult(
    int Examined,
    int Cleared);
