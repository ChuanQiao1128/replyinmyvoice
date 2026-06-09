namespace ReplyInMyVoice.Application.UseCases.WebhookOutbox;

public sealed record DispatchDueOutboxCommand(
    DateTimeOffset Now,
    string LockedBy,
    int BatchSize);
