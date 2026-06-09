namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed record FinalizeQuotaSuccessCommand(
    Guid AttemptId,
    string ResultJson,
    DateTimeOffset Now);
