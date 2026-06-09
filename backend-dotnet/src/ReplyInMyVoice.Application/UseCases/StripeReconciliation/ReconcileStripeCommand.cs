namespace ReplyInMyVoice.Application.UseCases.StripeReconciliation;

public sealed record ReconcileStripeCommand(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset CompletedAt);
