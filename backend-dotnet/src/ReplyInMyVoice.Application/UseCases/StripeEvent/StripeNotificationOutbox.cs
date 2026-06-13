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
}

public sealed record StripeNotificationOutboxPayload(Guid UserId, DateTimeOffset OccurredAt);

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
        new()
        {
            MessageType = messageType,
            PayloadJson = JsonSerializer.Serialize(
                new StripeNotificationOutboxPayload(userId, now),
                JsonOptions),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            AttemptCount = 0,
            MaxAttempts = 10,
            CorrelationId = correlationId,
        };
}
