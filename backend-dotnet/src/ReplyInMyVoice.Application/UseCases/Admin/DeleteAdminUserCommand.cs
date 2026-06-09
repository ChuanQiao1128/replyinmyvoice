namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record DeleteAdminUserCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid UserId,
    DateTimeOffset Now);
