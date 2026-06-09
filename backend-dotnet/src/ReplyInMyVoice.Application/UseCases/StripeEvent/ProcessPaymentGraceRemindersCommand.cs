namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed record ProcessPaymentGraceRemindersCommand(
    DateTimeOffset Now,
    int BatchSize = 100);
