using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeBillingClient
{
    Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionCreateRequest request,
        CancellationToken ct = default);

    Task<StripePortalSessionResult> CreatePortalSessionAsync(
        string customerId,
        CancellationToken ct = default);

    Task CancelSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken ct = default);

    Task<StripeRefundResult> RefundPaymentAsync(
        StripeRefundRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<StripePaidPaymentDto>> ListPaidPaymentIntentsAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);
}

public sealed record StripeCheckoutSessionCreateRequest(
    string? CustomerId,
    string? CustomerEmail,
    string? Sku,
    string ExternalAuthUserId);

public sealed record StripeCheckoutSessionResult(
    string? Url,
    string? CustomerId);

public sealed record StripePortalSessionResult(string? Url);

public sealed record StripeRefundRequest(
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string IdempotencyKey,
    Guid TargetUserId);

public sealed record StripeRefundResult(
    string RefundId,
    string PaymentIntentId,
    long Amount,
    string? Currency,
    string? Status);
