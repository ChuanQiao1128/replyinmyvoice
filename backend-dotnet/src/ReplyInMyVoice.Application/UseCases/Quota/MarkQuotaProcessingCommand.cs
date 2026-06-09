namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed record MarkQuotaProcessingCommand(
    Guid AttemptId,
    DateTimeOffset Now);
