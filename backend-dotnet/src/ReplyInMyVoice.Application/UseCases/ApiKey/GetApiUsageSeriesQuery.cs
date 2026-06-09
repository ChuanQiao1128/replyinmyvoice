namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record GetApiUsageSeriesQuery(
    Guid UserId,
    DateTimeOffset Now,
    int Days);
