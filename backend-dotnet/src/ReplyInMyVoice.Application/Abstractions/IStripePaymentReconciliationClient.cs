using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripePaymentReconciliationClient
{
    Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);

    Task<StripeCheckoutSessionSnapshotDto?> FindCheckoutSessionForPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<StripeSubscriptionSnapshotDto>> ListSubscriptionsAsync(
        CancellationToken ct = default);
}
