using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IStripeEventNotifier
{
    Task EnqueueFailedPaymentNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task EnqueueSubscriptionPausedNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task EnqueuePaymentGraceReminderNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task EnqueuePaymentRecoveredNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task EnqueuePaymentActionRequiredNotificationAsync(
        AppUser user,
        string? hostedInvoiceUrl,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task EnqueueCardExpiringNotificationAsync(
        AppUser user,
        string? brand,
        string? last4,
        int? expMonth,
        int? expYear,
        string? idempotencyKey = null,
        CancellationToken ct = default);
}
