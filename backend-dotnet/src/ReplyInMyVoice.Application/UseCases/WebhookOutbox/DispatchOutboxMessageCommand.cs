namespace ReplyInMyVoice.Application.UseCases.WebhookOutbox;

public sealed record DispatchOutboxMessageCommand(
    Guid OutboxMessageId,
    DateTimeOffset Now,
    string LockedBy);
