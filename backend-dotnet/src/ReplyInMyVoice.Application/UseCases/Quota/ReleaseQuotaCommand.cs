namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed record ReleaseQuotaCommand(
    Guid AttemptId,
    string ErrorCode,
    DateTimeOffset Now);
