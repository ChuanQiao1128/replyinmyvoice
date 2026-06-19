namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record ListDeadLettersQuery(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    int Page,
    int PageSize,
    string? SourceType,
    DateTimeOffset Now);
