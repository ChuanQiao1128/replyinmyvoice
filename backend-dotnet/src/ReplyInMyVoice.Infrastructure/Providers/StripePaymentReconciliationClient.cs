using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using Stripe;
using Stripe.Checkout;
using AppStripeBillingClient = ReplyInMyVoice.Application.Abstractions.IStripeBillingClient;
using AppStripePaymentReconciliationClient = ReplyInMyVoice.Application.Abstractions.IStripePaymentReconciliationClient;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class StripePaymentReconciliationClient(
    AppStripeBillingClient stripeBillingClient,
    IConfiguration configuration) : AppStripePaymentReconciliationClient
{
    public async Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default) =>
        await stripeBillingClient.ListPaidPaymentIntentsAsync(
            windowStart,
            windowEnd,
            ct);

    public async Task<StripeCheckoutSessionSnapshotDto?> FindCheckoutSessionForPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken ct = default)
    {
        var sessionService = new SessionService(CreateStripeClient());
        var sessions = await sessionService.ListAsync(
            new SessionListOptions
            {
                PaymentIntent = paymentIntentId,
                Limit = 1,
            },
            requestOptions: null,
            cancellationToken: ct);
        var session = sessions.Data.FirstOrDefault();
        if (session is null)
        {
            return null;
        }

        var metadata = session.Metadata ?? new Dictionary<string, string>();
        metadata.TryGetValue("externalAuthUserId", out var externalAuthUserId);
        metadata.TryGetValue("sku", out var sku);
        metadata.TryGetValue("rewrites", out var rewritesRaw);

        return new StripeCheckoutSessionSnapshotDto(
            session.Id,
            session.PaymentIntentId,
            session.Mode,
            session.PaymentStatus,
            string.IsNullOrWhiteSpace(externalAuthUserId) ? session.ClientReferenceId : externalAuthUserId,
            session.CustomerId,
            sku,
            int.TryParse(rewritesRaw, out var rewrites) ? rewrites : null,
            session.AmountTotal,
            session.Currency);
    }

    public async Task<IReadOnlyList<StripeSubscriptionSnapshotDto>> ListSubscriptionsAsync(
        CancellationToken ct = default)
    {
        var subscriptionService = new SubscriptionService(CreateStripeClient());
        var subscriptions = new List<StripeSubscriptionSnapshotDto>();
        await foreach (var subscription in subscriptionService.ListAutoPagingAsync(
                           new SubscriptionListOptions
                           {
                               Status = "all",
                               Limit = 100,
                           },
                           requestOptions: null,
                           cancellationToken: ct))
        {
            subscriptions.Add(new StripeSubscriptionSnapshotDto(
                subscription.Id,
                subscription.CustomerId,
                subscription.Status));
        }

        return subscriptions;
    }

    private StripeClient CreateStripeClient()
    {
        Services.StripeBillingService.EnsureStripeApiVersionPinned();
        var secretKey = GetRequiredConfiguration("STRIPE_SECRET_KEY");
        if (!secretKey.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("stripe_test_key_required");
        }

        return new StripeClient(secretKey);
    }

    private string GetRequiredConfiguration(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key}_missing");
}
