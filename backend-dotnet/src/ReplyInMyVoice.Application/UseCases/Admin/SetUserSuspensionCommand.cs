namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record SetUserSuspensionCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid TargetUserId,
    bool Suspended,
    DateTimeOffset Now);
