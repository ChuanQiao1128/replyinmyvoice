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

    private static AppUser CreateUser(string email) =>
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
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new RecordedNotification(template.Name, recipient, model!));
            return Task.FromResult(NotificationSendResult.Delivered("recording"));
        }
    }

    private sealed record RecordedNotification(
        string TemplateName,
        NotificationRecipient Recipient,
        object Model);

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
