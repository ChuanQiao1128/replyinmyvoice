namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record RequeueDeadLetterCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid DeadLetterId,
    DateTimeOffset Now);
