namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record RehashPendingApiKeysCommand(int BatchSize = 100);
