using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeEventNotifier
{
    Task EnqueueFailedPaymentNotificationAsync(
        AppUser user,
        CancellationToken ct = default);

    Task EnqueueSubscriptionPausedNotificationAsync(
        AppUser user,
        CancellationToken ct = default);

    Task EnqueuePaymentGraceReminderNotificationAsync(
        AppUser user,
        CancellationToken ct = default);

    Task EnqueuePaymentRecoveredNotificationAsync(
        AppUser user,
        CancellationToken ct = default);

    Task EnqueuePaymentActionRequiredNotificationAsync(
        AppUser user,
        string? hostedInvoiceUrl,
        CancellationToken ct = default);

    Task EnqueueCardExpiringNotificationAsync(
        AppUser user,
        string? brand,
        string? last4,
        int? expMonth,
        int? expYear,
        CancellationToken ct = default);
}
