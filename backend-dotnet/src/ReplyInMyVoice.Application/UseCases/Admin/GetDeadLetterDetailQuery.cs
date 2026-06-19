namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record GetDeadLetterDetailQuery(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid DeadLetterId,
    DateTimeOffset Now);
