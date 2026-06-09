namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed record TryMarkStripeEventProcessedCommand(
    string EventId,
    string Type,
    DateTimeOffset Now);
