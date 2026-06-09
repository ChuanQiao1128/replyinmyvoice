using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class StripeSubscriptionCancellationService(
    IStripeBillingService billingService) : IStripeSubscriptionCancellationService
{
    public async Task CancelSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken ct = default)
    {
        await billingService.CancelSubscriptionAsync(stripeSubscriptionId, ct);
    }
}
