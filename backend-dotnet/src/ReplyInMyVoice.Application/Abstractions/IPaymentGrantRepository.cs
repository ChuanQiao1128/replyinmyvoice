namespace ReplyInMyVoice.Application.Abstractions;

public interface IPaymentGrantRepository
{
    Task<IReadOnlyList<PaymentGrantSnapshot>> ListPurchaseGrantsForReconciliationAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        IReadOnlyCollection<string> paymentIntentIds,
        CancellationToken ct = default);
}

public sealed record PaymentGrantSnapshot(
    Guid CreditId,
    string? PaymentIntentId,
    long? AmountTotal,
    string? Currency,
    DateTimeOffset GrantedAt);
