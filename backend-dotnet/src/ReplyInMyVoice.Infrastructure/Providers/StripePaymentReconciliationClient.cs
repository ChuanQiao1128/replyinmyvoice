using ReplyInMyVoice.Application.Common;
using AppStripeBillingClient = ReplyInMyVoice.Application.Abstractions.IStripeBillingClient;
using AppStripePaymentReconciliationClient = ReplyInMyVoice.Application.Abstractions.IStripePaymentReconciliationClient;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class StripePaymentReconciliationClient(
    AppStripeBillingClient stripeBillingClient) : AppStripePaymentReconciliationClient
{
    public async Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default) =>
        await stripeBillingClient.ListPaidPaymentIntentsAsync(
            windowStart,
            windowEnd,
            ct);
}
