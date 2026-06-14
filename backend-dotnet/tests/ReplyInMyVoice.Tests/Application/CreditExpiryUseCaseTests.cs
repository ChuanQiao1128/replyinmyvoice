using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.CreditExpiry;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests.Application;

public sealed class CreditExpiryUseCaseTests
{
    [Fact]
    public async Task SendCreditExpiryRemindersAsync_sends_for_in_window_unreminded_remaining_credits_and_stamps_sent_rows()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-01T00:00:00Z");

        Guid eligibleCreditId;
        Guid consumedCreditId;
        Guid alreadyRemindedCreditId;
        Guid outsideWindowCreditId;
        Guid missingEmailCreditId;
        Guid eligibleOriginalRowVersion;
        await using (var seedDb = fixture.CreateContext())
        {
            var missingEmailUser = new AppUser
            {
                ExternalAuthUserId = $"clerk_no_email_{Guid.NewGuid():N}",
                Email = null,
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now
            };
            seedDb.AppUsers.Add(missingEmailUser);

            var eligible = CreateCredit(user.Id, now.AddDays(3), amountGranted: 10, amountConsumed: 4);
            var consumed = CreateCredit(user.Id, now.AddDays(2), amountGranted: 5, amountConsumed: 5);
            var alreadyReminded = CreateCredit(user.Id, now.AddDays(4), amountGranted: 8, amountConsumed: 1);
            alreadyReminded.ExpiryReminderSentAt = now.AddDays(-1);
            var outsideWindow = CreateCredit(user.Id, now.AddDays(8), amountGranted: 4, amountConsumed: 0);
            var missingEmail = CreateCredit(missingEmailUser.Id, now.AddDays(5), amountGranted: 3, amountConsumed: 0);

            seedDb.RewriteCredits.AddRange(
                eligible,
                consumed,
                alreadyReminded,
                outsideWindow,
                missingEmail);
            await seedDb.SaveChangesAsync();

            eligibleCreditId = eligible.Id;
            consumedCreditId = consumed.Id;
            alreadyRemindedCreditId = alreadyReminded.Id;
            outsideWindowCreditId = outsideWindow.Id;
            missingEmailCreditId = missingEmail.Id;
            eligibleOriginalRowVersion = eligible.RowVersion;
        }

        var notifier = new FakeCreditExpiryNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateHandler(handlerDb, notifier);

        var sentCount = await handler.HandleAsync(new SendCreditExpiryRemindersCommand(
            now,
            TimeSpan.FromDays(7)));

        sentCount.Should().Be(1);
        notifier.Requests.Should().ContainSingle().Which.Should().Be(new CreditExpiryNotificationRequest(
            user.Email!,
            CreditsExpiring: 6,
            ExpiresOnUtc: now.AddDays(3)));

        await using var verifyDb = fixture.CreateContext();
        var eligibleCredit = await verifyDb.RewriteCredits.SingleAsync(x => x.Id == eligibleCreditId);
        eligibleCredit.ExpiryReminderSentAt.Should().Be(now);
        eligibleCredit.RowVersion.Should().NotBe(eligibleOriginalRowVersion);

        (await verifyDb.RewriteCredits.SingleAsync(x => x.Id == consumedCreditId))
            .ExpiryReminderSentAt.Should().BeNull();
        (await verifyDb.RewriteCredits.SingleAsync(x => x.Id == alreadyRemindedCreditId))
            .ExpiryReminderSentAt.Should().Be(now.AddDays(-1));
        (await verifyDb.RewriteCredits.SingleAsync(x => x.Id == outsideWindowCreditId))
            .ExpiryReminderSentAt.Should().BeNull();
        (await verifyDb.RewriteCredits.SingleAsync(x => x.Id == missingEmailCreditId))
            .ExpiryReminderSentAt.Should().BeNull();
    }

    [Fact]
    public async Task SendCreditExpiryRemindersAsync_overlapping_runs_send_one_reminder_for_same_credit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var command = new SendCreditExpiryRemindersCommand(now, TimeSpan.FromDays(7));

        Guid creditId;
        await using (var seedDb = fixture.CreateContext())
        {
            var seededCredit = CreateCredit(user.Id, now.AddDays(3), amountGranted: 10, amountConsumed: 4);
            seedDb.RewriteCredits.Add(seededCredit);
            await seedDb.SaveChangesAsync();
            creditId = seededCredit.Id;
        }

        var notifier = new CoordinatedCreditExpiryNotifier();
        await using var firstDb = fixture.CreateContext();
        await using var secondDb = fixture.CreateContext();
        var secondHandler = CreateHandler(secondDb, notifier);
        notifier.RunSecondHandlerAsync = () => secondHandler.HandleAsync(command);
        var firstHandler = CreateHandler(firstDb, notifier);

        var firstSentCount = await firstHandler.HandleAsync(command);

        var totalSentCount = firstSentCount + notifier.SecondSentCount;
        totalSentCount.Should().Be(1);
        notifier.Requests.Should().ContainSingle().Which.Should().Be(new CreditExpiryNotificationRequest(
            user.Email!,
            CreditsExpiring: 6,
            ExpiresOnUtc: now.AddDays(3)));

        await using var verifyDb = fixture.CreateContext();
        var credit = await verifyDb.RewriteCredits.SingleAsync(x => x.Id == creditId);
        credit.ExpiryReminderSentAt.Should().Be(now);
    }

    private static SendCreditExpiryRemindersHandler CreateHandler(
        AppDbContext db,
        ICreditExpiryNotifier notifier) =>
        new(
            new RewriteCreditRepository(db),
            notifier);

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

    private sealed class FakeCreditExpiryNotifier : ICreditExpiryNotifier
    {
        public List<CreditExpiryNotificationRequest> Requests { get; } = [];

        public Task<bool> TrySendCreditExpiringAsync(
            CreditExpiryNotificationRequest request,
            CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(true);
        }
    }

    private sealed class CoordinatedCreditExpiryNotifier : ICreditExpiryNotifier
    {
        private int callCount;

        public List<CreditExpiryNotificationRequest> Requests { get; } = [];

        public Func<Task<int>>? RunSecondHandlerAsync { get; set; }

        public int SecondSentCount { get; private set; }

        public async Task<bool> TrySendCreditExpiringAsync(
            CreditExpiryNotificationRequest request,
            CancellationToken ct = default)
        {
            Requests.Add(request);
            callCount++;

            if (callCount == 1 && RunSecondHandlerAsync is not null)
            {
                SecondSentCount = await RunSecondHandlerAsync();
            }

            return true;
        }
    }
}
