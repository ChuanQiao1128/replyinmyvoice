namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed record ProcessExpiredPaymentGraceCommand(
    DateTimeOffset Now,
    int BatchSize = 100);
