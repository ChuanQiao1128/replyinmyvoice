namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record GetApiUsageSummaryQuery(
    string ExternalAuthUserId,
    string? Email,
    DateTimeOffset Now);
