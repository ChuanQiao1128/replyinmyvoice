namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed record ProcessPendingStripeEventsCommand(
    DateTimeOffset Now,
    int BatchSize,
    string? EventId = null);
