using FluentAssertions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class StripeEventNotifierTests
{
    [Fact]
    public async Task FailedPaymentNotification_uses_billing_portal_url_and_recipient_email()
    {
        var notifications = new RecordingNotificationService();
        var billing = new RecordingStripeBillingService("https://billing.test/portal");
        var notifier = new StripeEventNotifier(notifications, billing);
        var user = CreateUser("customer@example.com");

        await notifier.EnqueueFailedPaymentNotificationAsync(user);

        notifications.Messages.Should().ContainSingle();
        notifications.Messages[0].TemplateName.Should().Be("failed-payment");
        notifications.Messages[0].Recipient.Email.Should().Be("customer@example.com");
        notifications.Messages[0].Model.Should().BeOfType<FailedPaymentNotificationModel>()
            .Which.BillingPortalUrl.Should().Be("https://billing.test/portal");
        billing.PortalRequests.Should().Equal(user.ExternalAuthUserId);
    }

    [Fact]
    public async Task EnqueueFailedPaymentNotification_forwards_idempotency_key_to_notification_service()
    {
        var notifications = new RecordingNotificationService();
        var notifier = new StripeEventNotifier(
            notifications,
            new RecordingStripeBillingService("https://billing.test/portal"));
        var user = CreateUser("customer@example.com");

        await notifier.EnqueueFailedPaymentNotificationAsync(user, "outbox-key-123");

        notifications.Messages.Should().ContainSingle();
        notifications.Messages[0].IdempotencyKey.Should().Be("outbox-key-123");
    }

    [Fact]
    public async Task PaymentGraceReminderNotification_uses_grace_end_and_billing_portal_url()
    {
        var notifications = new RecordingNotificationService();
        var billing = new RecordingStripeBillingService("https://billing.test/portal");
        var notifier = new StripeEventNotifier(notifications, billing);
        var graceEndsAt = DateTimeOffset.Parse("2026-06-16T00:00:00Z");
        var user = CreateUser("due@example.com");
        user.PaymentGraceEndsAt = graceEndsAt;

        await notifier.EnqueuePaymentGraceReminderNotificationAsync(user);

        notifications.Messages.Should().ContainSingle();
        notifications.Messages[0].TemplateName.Should().Be("payment-grace-reminder");
        notifications.Messages[0].Recipient.Email.Should().Be("due@example.com");
        var model = notifications.Messages[0].Model.Should().BeOfType<PaymentGraceReminderNotificationModel>().Subject;
        model.BillingPortalUrl.Should().Be("https://billing.test/portal");
        model.PaymentGraceEndsAtUtc.Should().Be(graceEndsAt);
        billing.PortalRequests.Should().Equal(user.ExternalAuthUserId);
    }

    [Fact]
    public async Task SubscriptionPausedAndPaymentRecoveredNotifications_use_expected_templates()
    {
        var notifications = new RecordingNotificationService();
        var notifier = new StripeEventNotifier(notifications);
        var user = CreateUser("customer@example.com");

        await notifier.EnqueueSubscriptionPausedNotificationAsync(user);
        await notifier.EnqueuePaymentRecoveredNotificationAsync(user);

        notifications.Messages.Select(x => x.TemplateName)
            .Should()
            .Equal("subscription-paused", "payment-recovered");
        notifications.Messages.Select(x => x.Recipient.Email)
            .Should()
            .Equal("customer@example.com", "customer@example.com");
    }

    [Fact]
    public async Task EnqueuePaymentActionRequiredNotificationAsync_prefers_hosted_invoice_url_and_falls_back_to_billing_portal()
    {
        var notifications = new RecordingNotificationService();
        var billing = new RecordingStripeBillingService("https://billing.test/portal");
        var notifier = new StripeEventNotifier(notifications, billing);
        var user = CreateUser("action-required@example.com");

        await notifier.EnqueuePaymentActionRequiredNotificationAsync(
            user,
            "https://billing.test/in_action_required");
        await notifier.EnqueuePaymentActionRequiredNotificationAsync(user, " ");
        await notifier.EnqueuePaymentActionRequiredNotificationAsync(CreateUser(null), "https://billing.test/skipped");

        notifications.Messages.Should().HaveCount(2);
        notifications.Messages.Select(x => x.TemplateName)
            .Should()
            .Equal("payment-action-required", "payment-action-required");
        notifications.Messages[0].Model.Should().BeOfType<PaymentActionRequiredNotificationModel>()
            .Which.PaymentUrl.Should().Be("https://billing.test/in_action_required");
        notifications.Messages[1].Model.Should().BeOfType<PaymentActionRequiredNotificationModel>()
            .Which.PaymentUrl.Should().Be("https://billing.test/portal");
        billing.PortalRequests.Should().Equal(user.ExternalAuthUserId);
    }

    [Fact]
    public async Task EnqueueCardExpiringNotificationAsync_renders_card_summary_and_skips_without_email()
    {
        var notifications = new RecordingNotificationService();
        var billing = new RecordingStripeBillingService("https://billing.test/portal");
        var notifier = new StripeEventNotifier(notifications, billing);
        var user = CreateUser("card-expiring@example.com");

        await notifier.EnqueueCardExpiringNotificationAsync(user, "visa", "4242", 4, 2027);
        await notifier.EnqueueCardExpiringNotificationAsync(CreateUser(null), "mastercard", "1111", 5, 2027);

        notifications.Messages.Should().ContainSingle();
        var message = notifications.Messages[0];
        message.TemplateName.Should().Be("card-expiring");
        message.Recipient.Email.Should().Be("card-expiring@example.com");
        var model = message.Model.Should().BeOfType<CardExpiringNotificationModel>().Subject;
        model.BillingPortalUrl.Should().Be("https://billing.test/portal");
        model.Brand.Should().Be("visa");
        model.Last4.Should().Be("4242");
        model.ExpMonth.Should().Be(4);
        model.ExpYear.Should().Be(2027);
        var rendered = NotificationTemplates.CardExpiring.Render(model);
        rendered.Subject.Should().Be("Your card on file expires soon");
        rendered.PlainTextBody.Should().Contain("Visa card ending in 4242");
        rendered.PlainTextBody.Should().Contain("04/2027");
        billing.PortalRequests.Should().Equal(user.ExternalAuthUserId);
    }

    private static AppUser CreateUser(string? email) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExternalAuthUserId = $"clerk_{Guid.NewGuid():N}",
            Email = email,
            SubscriptionStatus = SubscriptionStatus.PastDue,
            CreatedAt = DateTimeOffset.Parse("2026-06-09T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-06-09T00:00:00Z"),
        };

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<RecordedNotification> Messages { get; } = [];

        public Task<NotificationSendResult> SendAsync<TModel>(
            NotificationTemplate<TModel> template,
            NotificationRecipient recipient,
            TModel model,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new RecordedNotification(template.Name, recipient, model!, idempotencyKey));
            return Task.FromResult(NotificationSendResult.Delivered("recording"));
        }
    }

    private sealed record RecordedNotification(
        string TemplateName,
        NotificationRecipient Recipient,
        object Model,
        string? IdempotencyKey = null);

    private sealed class RecordingStripeBillingService(string portalUrl) : IStripeBillingService
    {
        public List<string> PortalRequests { get; } = [];

        public Task<string> CreateCheckoutSessionUrlAsync(
            string externalAuthUserId,
            string? email,
            string? sku,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Checkout is not used by notifier tests.");

        public Task<string> CreatePortalSessionUrlAsync(
            string externalAuthUserId,
            CancellationToken cancellationToken)
        {
            PortalRequests.Add(externalAuthUserId);
            return Task.FromResult(portalUrl);
        }

        public Task CancelSubscriptionAsync(
            string stripeSubscriptionId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Cancellation is not used by notifier tests.");
    }
}
