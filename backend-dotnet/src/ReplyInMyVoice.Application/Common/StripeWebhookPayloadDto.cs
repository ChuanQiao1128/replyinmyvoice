namespace ReplyInMyVoice.Application.Common;

public sealed record StripeWebhookPayloadDto(
    string EventId,
    string Type,
    StripeWebhookObjectDto Object);

public sealed record StripeWebhookObjectDto(
    string? Id = null,
    string? CustomerId = null,
    string? SubscriptionId = null,
    string? ExternalAuthUserId = null,
    string? Status = null,
    DateTimeOffset? CurrentPeriodEnd = null,
    string? CheckoutMode = null,
    string? PaymentStatus = null,
    string? Sku = null,
    int? GrantedRewrites = null,
    string? PaymentIntentId = null,
    string? ReceiptUrl = null,
    long? AmountTotal = null,
    string? Currency = null,
    long? Amount = null,
    long? AmountRefunded = null,
    bool? Refunded = null,
    long? AmountDue = null,
    long? AmountPaid = null,
    DateTimeOffset? PeriodStart = null,
    DateTimeOffset? PeriodEnd = null,
    int AttemptCount = 0,
    DateTimeOffset? NextPaymentAttempt = null,
    DateTimeOffset? DueDate = null,
    string? HostedInvoiceUrl = null,
    string? InvoicePdf = null);
