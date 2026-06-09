namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record GetApiUsageRecentQuery(
    Guid UserId,
    DateTimeOffset Now,
    int Limit);
