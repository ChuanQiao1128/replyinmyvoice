using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Infrastructure;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Tests;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task SendAsync_renders_named_template_and_invokes_provider_with_recipient()
    {
        var provider = new RecordingNotificationEmailProvider();
        var service = new NotificationService(provider, NullLogger<NotificationService>.Instance);
        var recipient = new NotificationRecipient("customer@example.com", "Customer");

        var result = await service.SendAsync(
            NotificationTemplates.FailedPayment,
            recipient,
            new FailedPaymentNotificationModel(
                CustomerName: "Aroha",
                SupportEmail: "info@timeawake.co.nz",
                BillingPortalUrl: "https://replyinmyvoice.com/app"),
            cancellationToken: CancellationToken.None);

        result.Sent.Should().BeTrue();
        provider.SentMessages.Should().ContainSingle();
        provider.SentMessages[0].TemplateName.Should().Be("failed-payment");
        provider.SentMessages[0].Recipient.Should().Be(recipient);
        provider.SentMessages[0].Subject.Should().ContainEquivalentOf("payment");
    }

    [Fact]
    public async Task SendAsync_threads_idempotency_key_onto_the_email()
    {
        var provider = new RecordingNotificationEmailProvider();
        var service = new NotificationService(provider, NullLogger<NotificationService>.Instance);

        await service.SendAsync(
            NotificationTemplates.FailedPayment,
            new NotificationRecipient("customer@example.com", "Customer"),
            new FailedPaymentNotificationModel(
                CustomerName: "Aroha",
                SupportEmail: "info@timeawake.co.nz",
                BillingPortalUrl: "https://replyinmyvoice.com/app"),
            idempotencyKey: "outbox-abc",
            CancellationToken.None);

        provider.SentMessages.Should().ContainSingle();
        provider.SentMessages[0].IdempotencyKey.Should().Be("outbox-abc");
    }

    [Fact]
    public async Task AddReplyInMyVoiceInfrastructure_uses_noop_notification_provider_when_config_is_missing()
    {
        using var serviceProvider = BuildProvider([]);
        var service = serviceProvider.GetRequiredService<INotificationService>();

        var act = () => service.SendAsync(
            NotificationTemplates.RefundRequestReceived,
            new NotificationRecipient("customer@example.com"),
            new RefundRequestReceivedNotificationModel(
                CustomerName: "Customer",
                SupportEmail: "info@timeawake.co.nz",
                RequestReference: "refund-123"),
            cancellationToken: CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Which.Sent.Should().BeFalse();
        result.Which.Provider.Should().Be("noop");
    }

    [Fact]
    public void AddReplyInMyVoiceInfrastructure_registers_resend_provider_when_enabled()
    {
        using var serviceProvider = BuildProvider(new Dictionary<string, string?>
        {
            ["NOTIFICATIONS_PROVIDER"] = "resend",
            ["RESEND_API_KEY"] = "resend-test-key",
            ["NOTIFICATIONS_FROM_EMAIL"] = "Reply In My Voice <info@timeawake.co.nz>",
        });

        serviceProvider.GetRequiredService<INotificationEmailProvider>()
            .Should()
            .BeOfType<ResendNotificationEmailProvider>();
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddReplyInMyVoiceInfrastructure(configuration, "Testing");
        return services.BuildServiceProvider();
    }

    private sealed class RecordingNotificationEmailProvider : INotificationEmailProvider
    {
        public List<NotificationEmail> SentMessages { get; } = [];

        public Task<NotificationSendResult> SendAsync(
            NotificationEmail email,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add(email);
            return Task.FromResult(NotificationSendResult.Delivered("recording"));
        }
    }
}
