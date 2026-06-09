namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record GrantCreditsCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid TargetUserId,
    int? Amount,
    string? Reason,
    DateTimeOffset Now);
