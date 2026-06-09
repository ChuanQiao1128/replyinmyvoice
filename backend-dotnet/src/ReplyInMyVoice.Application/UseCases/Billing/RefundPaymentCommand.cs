namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed record RefundPaymentCommand(
    Guid TargetUserId,
    string PaymentIntentId,
    long? Amount,
    string? Currency,
    string IdempotencyKey);
