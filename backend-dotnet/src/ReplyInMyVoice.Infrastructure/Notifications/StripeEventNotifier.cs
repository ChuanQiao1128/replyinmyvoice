using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Infrastructure.Notifications;

public sealed class StripeEventNotifier(
    INotificationService notificationService,
    IStripeBillingService? stripeBillingService = null,
    ILogger<StripeEventNotifier>? logger = null) : IStripeEventNotifier
{
    private const string SupportEmail = "info@timeawake.co.nz";

    public async Task EnqueueFailedPaymentNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var billingPortalUrl = await ResolveBillingPortalUrlAsync(user.ExternalAuthUserId, ct);
        await notificationService.SendAsync(
            NotificationTemplates.FailedPayment,
            CreateRecipient(user),
            new FailedPaymentNotificationModel("there", SupportEmail, billingPortalUrl),
            idempotencyKey,
            ct);
    }

    public async Task EnqueueSubscriptionPausedNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        await notificationService.SendAsync(
            NotificationTemplates.SubscriptionPaused,
            CreateRecipient(user),
            new SubscriptionPausedNotificationModel("there", SupportEmail),
            idempotencyKey,
            ct);
    }

    public async Task EnqueuePaymentGraceReminderNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email) ||
            user.PaymentGraceEndsAt is null)
        {
            return;
        }

        var billingPortalUrl = await ResolveBillingPortalUrlAsync(user.ExternalAuthUserId, ct);
        await notificationService.SendAsync(
            NotificationTemplates.PaymentGraceReminder,
            CreateRecipient(user),
            new PaymentGraceReminderNotificationModel(
                "there",
                SupportEmail,
                billingPortalUrl,
                user.PaymentGraceEndsAt.Value),
            idempotencyKey,
            ct);
    }

    public async Task EnqueuePaymentRecoveredNotificationAsync(
        AppUser user,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        await notificationService.SendAsync(
            NotificationTemplates.PaymentRecovered,
            CreateRecipient(user),
            new PaymentRecoveredNotificationModel("there", SupportEmail),
            idempotencyKey,
            ct);
    }

    public async Task EnqueuePaymentActionRequiredNotificationAsync(
        AppUser user,
        string? hostedInvoiceUrl,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var paymentUrl = string.IsNullOrWhiteSpace(hostedInvoiceUrl)
            ? await ResolveBillingPortalUrlAsync(user.ExternalAuthUserId, ct)
            : hostedInvoiceUrl.Trim();
        await notificationService.SendAsync(
            NotificationTemplates.PaymentActionRequired,
            CreateRecipient(user),
            new PaymentActionRequiredNotificationModel("there", SupportEmail, paymentUrl),
            idempotencyKey,
            ct);
    }

    public async Task EnqueueCardExpiringNotificationAsync(
        AppUser user,
        string? brand,
        string? last4,
        int? expMonth,
        int? expYear,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var billingPortalUrl = await ResolveBillingPortalUrlAsync(user.ExternalAuthUserId, ct);
        await notificationService.SendAsync(
            NotificationTemplates.CardExpiring,
            CreateRecipient(user),
            new CardExpiringNotificationModel(
                "there",
                SupportEmail,
                billingPortalUrl,
                brand,
                last4,
                expMonth,
                expYear),
            idempotencyKey,
            ct);
    }

    private async Task<string> ResolveBillingPortalUrlAsync(
        string externalAuthUserId,
        CancellationToken ct)
    {
        if (stripeBillingService is null)
        {
            return "https://replyinmyvoice.com/app";
        }

        try
        {
            return await stripeBillingService.CreatePortalSessionUrlAsync(externalAuthUserId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(
                ex,
                "Could not create Stripe billing portal session for payment notification.");
            return "https://replyinmyvoice.com/app";
        }
    }

    private static NotificationRecipient CreateRecipient(AppUser user) =>
        new(user.Email!, null);
}
