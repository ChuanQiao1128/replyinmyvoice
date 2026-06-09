namespace ReplyInMyVoice.Application.UseCases.BillingSupport;

public sealed record CreateBillingSupportRequestCommand(
    Guid UserId,
    string? Type,
    string? RelatedPaymentIntentId,
    string? Message,
    DateTimeOffset Now);
