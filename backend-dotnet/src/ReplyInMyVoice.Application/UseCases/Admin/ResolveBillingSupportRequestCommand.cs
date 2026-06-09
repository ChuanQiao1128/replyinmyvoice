namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record ResolveBillingSupportRequestCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid RequestId,
    DateTimeOffset Now);
