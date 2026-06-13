using System.Text.Json;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public static class StripeNotificationOutboxMessageTypes
{
    public const string PaymentFailed = "PaymentFailedNotification";
    public const string PaymentRecovered = "PaymentRecoveredNotification";
    public const string SubscriptionPaused = "SubscriptionPausedNotification";
    public const string PaymentGraceReminder = "PaymentGraceReminderNotification";
    public const string PaymentActionRequired = "StripePaymentActionRequiredNotification";
    public const string CardExpiring = "StripeCardExpiringNotification";
}

public sealed record StripeNotificationOutboxPayload(
    Guid UserId,
    DateTimeOffset OccurredAt,
    string? InvoiceId = null,
    string? HostedInvoiceUrl = null,
    string? Brand = null,
    string? Last4 = null,
    int? ExpMonth = null,
    int? ExpYear = null);

public static class StripeNotificationOutboxMessageFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static OutboxMessage Create(
        string messageType,
        Guid userId,
        DateTimeOffset now,
        string correlationId) =>
        Create(messageType, new StripeNotificationOutboxPayload(userId, now), now, correlationId);

    public static OutboxMessage CreatePaymentActionRequired(
        Guid userId,
        string? invoiceId,
        string? hostedInvoiceUrl,
        DateTimeOffset now,
        string correlationId) =>
        Create(
            StripeNotificationOutboxMessageTypes.PaymentActionRequired,
            new StripeNotificationOutboxPayload(
                userId,
                now,
                InvoiceId: invoiceId,
                HostedInvoiceUrl: hostedInvoiceUrl),
            now,
            correlationId);

    public static OutboxMessage CreateCardExpiring(
        Guid userId,
        string? brand,
        string? last4,
        int? expMonth,
        int? expYear,
        DateTimeOffset now,
        string correlationId) =>
        Create(
            StripeNotificationOutboxMessageTypes.CardExpiring,
            new StripeNotificationOutboxPayload(
                userId,
                now,
                Brand: brand,
                Last4: last4,
                ExpMonth: expMonth,
                ExpYear: expYear),
            now,
            correlationId);

    private static OutboxMessage Create(
        string messageType,
        StripeNotificationOutboxPayload payload,
        DateTimeOffset now,
        string correlationId) =>
        new()
        {
            MessageType = messageType,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            AttemptCount = 0,
            MaxAttempts = 10,
            CorrelationId = correlationId,
        };
}
