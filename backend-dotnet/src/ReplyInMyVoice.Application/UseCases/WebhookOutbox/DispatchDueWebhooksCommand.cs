namespace ReplyInMyVoice.Application.UseCases.WebhookOutbox;

public sealed record DispatchDueWebhooksCommand(
    DateTimeOffset Now,
    string LockedBy,
    int BatchSize);
