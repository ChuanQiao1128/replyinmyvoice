using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class CreditExpiryReminderServiceTests
{
    [Fact]
    public async Task RunOnceAsync_sends_one_reminder_for_unconsumed_expiring_credit_and_skips_reruns_consumed_and_expired_credits()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var provider = new RecordingNotificationEmailProvider();
        var notifications = new NotificationService(provider, NullLogger<NotificationService>.Instance);
        var service = new CreditExpiryReminderService(
            fixture.CreateContext,
            notifications,
            BuildConfiguration(),
            NullLogger<CreditExpiryReminderService>.Instance);

        Guid eligibleCreditId;
        Guid consumedCreditId;
        Guid expiredCreditId;
        await using (var db = fixture.CreateContext())
        {
            var eligible = CreateCredit(user.Id, now.AddDays(3), amountGranted: 10, amountConsumed: 4);
            var consumed = CreateCredit(user.Id, now.AddDays(3), amountGranted: 10, amountConsumed: 10);
            var expired = CreateCredit(user.Id, now.AddSeconds(-1), amountGranted: 10, amountConsumed: 2);
            db.RewriteCredits.AddRange(eligible, consumed, expired);
            await db.SaveChangesAsync();
            eligibleCreditId = eligible.Id;
            consumedCreditId = consumed.Id;
            expiredCreditId = expired.Id;
        }

        var firstRun = await service.RunOnceAsync(now, TimeSpan.FromDays(7), CancellationToken.None);
        var secondRun = await service.RunOnceAsync(now, TimeSpan.FromDays(7), CancellationToken.None);

        firstRun.Should().Be(1);
        secondRun.Should().Be(0);
        provider.SentMessages.Should().ContainSingle();
        provider.SentMessages[0].TemplateName.Should().Be("credit-expiring");
        provider.SentMessages[0].Recipient.Email.Should().Be(user.Email);
        provider.SentMessages[0].PlainTextBody.Should().Contain("6 Reply In My Voice credit(s)");
        await using var verifyDb = fixture.CreateContext();
        var eligibleCredit = await verifyDb.RewriteCredits.SingleAsync(x => x.Id == eligibleCreditId);
        var consumedCredit = await verifyDb.RewriteCredits.SingleAsync(x => x.Id == consumedCreditId);
        var expiredCredit = await verifyDb.RewriteCredits.SingleAsync(x => x.Id == expiredCreditId);
        eligibleCredit.ExpiryReminderSentAt.Should().Be(now);
        consumedCredit.ExpiryReminderSentAt.Should().BeNull();
        expiredCredit.ExpiryReminderSentAt.Should().BeNull();
    }

    private static RewriteCredit CreateCredit(
        Guid userId,
        DateTimeOffset expiresAt,
        int amountGranted,
        int amountConsumed) =>
        new()
        {
            UserId = userId,
            Source = "PURCHASE",
            AmountGranted = amountGranted,
            AmountConsumed = amountConsumed,
            GrantedAt = expiresAt.AddDays(-90),
            ExpiresAt = expiresAt
        };

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NOTIFICATIONS_SUPPORT_EMAIL"] = "support@example.com"
            })
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
