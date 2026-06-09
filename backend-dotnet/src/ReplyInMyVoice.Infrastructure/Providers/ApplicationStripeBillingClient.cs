using Microsoft.Extensions.Configuration;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Infrastructure.Services;
using Stripe;
using AppStripeBillingClient = ReplyInMyVoice.Application.Abstractions.IStripeBillingClient;
using AppStripeCheckoutSessionCreateRequest = ReplyInMyVoice.Application.Abstractions.StripeCheckoutSessionCreateRequest;
using AppStripeCheckoutSessionResult = ReplyInMyVoice.Application.Abstractions.StripeCheckoutSessionResult;
using AppStripePortalSessionResult = ReplyInMyVoice.Application.Abstractions.StripePortalSessionResult;
using AppStripeRefundClient = ReplyInMyVoice.Application.Abstractions.IStripeRefundClient;
using AppStripeRefundRequest = ReplyInMyVoice.Application.Abstractions.StripeRefundRequest;
using AppStripeRefundResult = ReplyInMyVoice.Application.Abstractions.StripeRefundResult;
using LegacyStripeBillingClient = ReplyInMyVoice.Infrastructure.Services.IStripeBillingClient;
using LegacyStripeCheckoutSessionCreateRequest = ReplyInMyVoice.Infrastructure.Services.StripeCheckoutSessionCreateRequest;
using LegacyStripeRefundRequest = ReplyInMyVoice.Infrastructure.Services.StripeRefundRequest;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class ApplicationStripeBillingClient(
    IConfiguration configuration,
    LegacyStripeBillingClient? stripeBillingClient = null) : AppStripeBillingClient, AppStripeRefundClient
{
    private const string LegacyPriceEnvVar = "STRIPE_PRICE_ID";
    private readonly LegacyStripeBillingClient _stripeBillingClient =
        stripeBillingClient ?? new StripeBillingClient();

    public async Task<AppStripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        AppStripeCheckoutSessionCreateRequest request,
        CancellationToken ct = default)
    {
        var skuDefinition = ResolveSkuDefinition(request.Sku);
        var priceId = GetRequiredConfiguration(skuDefinition?.PriceEnvVar ?? LegacyPriceEnvVar);
        var appUrl = GetRequiredConfiguration("NEXT_PUBLIC_APP_URL").TrimEnd('/');
        var session = await _stripeBillingClient.CreateCheckoutSessionAsync(
            new LegacyStripeCheckoutSessionCreateRequest(
                request.CustomerId,
                request.CustomerEmail,
                skuDefinition?.Mode ?? "subscription",
                priceId,
                appUrl,
                request.ExternalAuthUserId,
                CreateCheckoutMetadata(request.ExternalAuthUserId, skuDefinition),
                IsAutomaticTaxEnabled(configuration)),
            CreateStripeClient(),
            ct);

        return new AppStripeCheckoutSessionResult(
            session.Url,
            session.CustomerId);
    }

    public async Task<AppStripePortalSessionResult> CreatePortalSessionAsync(
        string customerId,
        CancellationToken ct = default)
    {
        var appUrl = GetRequiredConfiguration("NEXT_PUBLIC_APP_URL").TrimEnd('/');
        var session = await _stripeBillingClient.CreatePortalSessionAsync(
            customerId,
            $"{appUrl}/app",
            CreateStripeClient(),
            ct);

        return new AppStripePortalSessionResult(session.Url);
    }

    public async Task CancelSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken ct = default)
    {
        var normalizedSubscriptionId = stripeSubscriptionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSubscriptionId))
        {
            return;
        }

        await _stripeBillingClient.CancelSubscriptionAsync(
            normalizedSubscriptionId,
            CreateStripeClient(),
            ct);
    }

    public async Task<AppStripeRefundResult> RefundPaymentAsync(
        AppStripeRefundRequest request,
        CancellationToken ct = default)
    {
        var refund = await _stripeBillingClient.RefundPaymentAsync(
            new LegacyStripeRefundRequest(
                request.PaymentIntentId,
                request.Amount,
                request.Currency,
                request.IdempotencyKey,
                request.TargetUserId),
            CreateStripeClient(),
            ct);

        return new AppStripeRefundResult(
            refund.RefundId,
            refund.PaymentIntentId,
            refund.Amount,
            refund.Currency,
            refund.Status);
    }

    public async Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        if (windowEnd <= windowStart)
        {
            throw new ArgumentException("reconciliation_window_invalid", nameof(windowEnd));
        }

        var paymentIntentService = new PaymentIntentService(CreateStripeClient());
        var options = new PaymentIntentListOptions
        {
            Created = new DateRangeOptions
            {
                GreaterThanOrEqual = windowStart.UtcDateTime,
                LessThan = windowEnd.UtcDateTime,
            },
            Limit = 100,
        };

        var payments = new List<StripePaidPaymentDto>();
        await foreach (var paymentIntent in paymentIntentService.ListAutoPagingAsync(
                           options,
                           requestOptions: null,
                           cancellationToken: ct))
        {
            if (!string.Equals(paymentIntent.Status, "succeeded", StringComparison.Ordinal) ||
                paymentIntent.AmountReceived <= 0 ||
                string.IsNullOrWhiteSpace(paymentIntent.Id))
            {
                continue;
            }

            payments.Add(new StripePaidPaymentDto(
                paymentIntent.Id,
                paymentIntent.AmountReceived,
                paymentIntent.Currency ?? string.Empty,
                ToUtcDateTimeOffset(paymentIntent.Created)));
        }

        return payments;
    }

    private StripeClient CreateStripeClient()
    {
        StripeBillingService.EnsureStripeApiVersionPinned();
        var secretKey = GetRequiredConfiguration("STRIPE_SECRET_KEY");
        if (!secretKey.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("stripe_test_key_required");
        }

        return new StripeClient(secretKey);
    }

    private string GetRequiredConfiguration(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key}_missing");

    private static CheckoutSkuDefinition? ResolveSkuDefinition(string? sku) =>
        StripeBillingService.TryGetSkuDefinition(sku, out var definition) ? definition : null;

    private static Dictionary<string, string> CreateCheckoutMetadata(
        string externalAuthUserId,
        CheckoutSkuDefinition? skuDefinition)
    {
        var metadata = new Dictionary<string, string>
        {
            ["externalAuthUserId"] = externalAuthUserId,
        };

        if (skuDefinition is not null)
        {
            metadata["sku"] = skuDefinition.Sku;
            metadata["rewrites"] = skuDefinition.Rewrites.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return metadata;
    }

    private static bool IsAutomaticTaxEnabled(IConfiguration configuration)
    {
        var value = configuration["STRIPE_AUTOMATIC_TAX_ENABLED"]?.Trim();
        return value is not null &&
            (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));
    }

    private static DateTimeOffset ToUtcDateTimeOffset(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return new DateTimeOffset(utc);
    }
}
