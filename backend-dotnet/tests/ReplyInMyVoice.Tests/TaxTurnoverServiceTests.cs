using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class TaxTurnoverServiceTests
{
    [Fact]
    public async Task GetRollingTwelveMonthReportAsync_sums_gross_nzd_purchases_and_fires_threshold_warning()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
        await SeedCreditAsync(fixture, user.Id, "PURCHASE", 5_000, "nzd", now.AddMonths(-2));
        await SeedCreditAsync(fixture, user.Id, "PURCHASE", 3_100, "NZD", now.AddMonths(-11));
        await SeedCreditAsync(fixture, user.Id, "PURCHASE", 9_000, "nzd", now.AddMonths(-13));
        await SeedCreditAsync(fixture, user.Id, "ADMIN", 7_500, "nzd", now.AddMonths(-1));
        await SeedCreditAsync(fixture, user.Id, "PURCHASE", 4_000, "usd", now.AddMonths(-1));
        var provider = new RecordingNotificationEmailProvider();
        var notificationService = new NotificationService(provider, NullLogger<NotificationService>.Instance);
        var service = new TaxTurnoverService(
            fixture.CreateContext,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["GST_TURNOVER_THRESHOLD_NZD"] = "100",
                ["GST_TURNOVER_WARNING_FRACTION"] = "0.80",
                ["GST_TURNOVER_NOTIFICATION_EMAIL"] = "owner@example.com",
            }),
            notificationService);

        var report = await service.GetRollingTwelveMonthReportAsync(now, CancellationToken.None);

        report.WindowStart.Should().Be(now.AddMonths(-12));
        report.WindowEnd.Should().Be(now);
        report.Currency.Should().Be("nzd");
        report.GrossAmountTotal.Should().Be(8_100);
        report.RegistrationThresholdAmountTotal.Should().Be(10_000);
        report.WarningFraction.Should().Be(0.80m);
        report.WarningAmountTotal.Should().Be(8_000);
        report.Warning.Should().NotBeNull();
        report.Warning!.Code.Should().Be("nz_gst_turnover_threshold_approaching");
        report.Warning.Severity.Should().Be("warning");
        report.Notification.Should().NotBeNull();
        report.Notification!.Attempted.Should().BeTrue();
        report.Notification.Sent.Should().BeTrue();
        provider.SentMessages.Should().ContainSingle();
        provider.SentMessages[0].TemplateName.Should().Be("gst-turnover-threshold");
        provider.SentMessages[0].Recipient.Email.Should().Be("owner@example.com");
    }

    private static async Task SeedCreditAsync(
        DbFixture fixture,
        Guid userId,
        string source,
        long? amountTotal,
        string? currency,
        DateTimeOffset grantedAt)
    {
        await using var db = fixture.CreateContext();
        db.RewriteCredits.Add(new RewriteCredit
        {
            UserId = userId,
            Source = source,
            AmountGranted = 10,
            AmountConsumed = 0,
            GrantedAt = grantedAt,
            StripePaymentIntentId = $"pi_{Guid.NewGuid():N}",
            StripeAmountTotal = amountTotal,
            StripeCurrency = currency,
        });
        await db.SaveChangesAsync();
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

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
