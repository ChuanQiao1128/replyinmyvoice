using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeEventNotifier
{
    Task EnqueueFailedPaymentNotificationAsync(
        AppUser user,
        CancellationToken ct = default,
        Guid? outboxMessageId = null);

    Task EnqueueSubscriptionPausedNotificationAsync(
        AppUser user,
        CancellationToken ct = default,
        Guid? outboxMessageId = null);

    Task EnqueuePaymentGraceReminderNotificationAsync(
        AppUser user,
        CancellationToken ct = default,
        Guid? outboxMessageId = null);

    Task EnqueuePaymentRecoveredNotificationAsync(
        AppUser user,
        CancellationToken ct = default,
        Guid? outboxMessageId = null);

    Task EnqueuePaymentActionRequiredNotificationAsync(
        AppUser user,
        string? hostedInvoiceUrl,
        CancellationToken ct = default,
        Guid? outboxMessageId = null);

    Task EnqueueCardExpiringNotificationAsync(
        AppUser user,
        string? brand,
        string? last4,
        int? expMonth,
        int? expYear,
        CancellationToken ct = default,
        Guid? outboxMessageId = null);
}
