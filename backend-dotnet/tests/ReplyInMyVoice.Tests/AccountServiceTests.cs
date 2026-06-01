using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class AccountServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetOrCreateAccountSummary_creates_user_and_reports_free_usage()
    {
        var service = new AccountService(CreateContext);

        var summary = await service.GetOrCreateAccountSummaryAsync(
            "entra-user-1",
            "casey@example.com",
            CancellationToken.None);

        summary.ExternalAuthUserId.Should().Be("entra-user-1");
        summary.Email.Should().Be("casey@example.com");
        summary.SubscriptionStatus.Should().Be("Inactive");
        summary.Usage.Scope.Should().Be("free");
        summary.Usage.PeriodKey.Should().Be("free:lifetime");
        summary.Usage.Quota.Should().Be(3);
        summary.Usage.Used.Should().Be(0);
        summary.Usage.Reserved.Should().Be(0);
        summary.Usage.Remaining.Should().Be(3);
        summary.Usage.Exhausted.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrCreateAccountSummary_counts_reserved_paid_usage_for_current_period()
    {
        var userId = Guid.NewGuid();
        var periodEnd = DateTimeOffset.Parse("2026-06-20T00:00:00Z");

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-paid",
                Email = "paid@example.com",
                StripeSubscriptionId = "sub_paid",
                SubscriptionStatus = SubscriptionStatus.Active,
                CurrentPeriodEnd = periodEnd,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userId,
                PeriodKey = $"paid:sub_paid:{periodEnd:O}",
                QuotaLimit = 90,
                UsedCount = 7,
                ReservedCount = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var service = new AccountService(CreateContext);

        var summary = await service.GetOrCreateAccountSummaryAsync(
            "entra-paid",
            "paid-updated@example.com",
            CancellationToken.None);

        summary.Email.Should().Be("paid-updated@example.com");
        summary.Usage.Scope.Should().Be("paid");
        summary.Usage.PeriodKey.Should().Be($"paid:sub_paid:{periodEnd:O}");
        summary.Usage.Quota.Should().Be(90);
        summary.Usage.Used.Should().Be(7);
        summary.Usage.Reserved.Should().Be(2);
        summary.Usage.Remaining.Should().Be(81);
        summary.Usage.Exhausted.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrCreateAccountSummary_includes_valid_rewrite_credits_and_ignores_expired_credits()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-credit",
                Email = "credit@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userId,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 3,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = userId,
                    Source = "PURCHASE",
                    AmountGranted = 10,
                    AmountConsumed = 7,
                    GrantedAt = now.AddDays(-1),
                    ExpiresAt = now.AddDays(30),
                },
                new RewriteCredit
                {
                    UserId = userId,
                    Source = "PURCHASE",
                    AmountGranted = 20,
                    AmountConsumed = 0,
                    GrantedAt = now.AddDays(-100),
                    ExpiresAt = now.AddDays(-1),
                });
            await db.SaveChangesAsync();
        }

        var service = new AccountService(CreateContext);

        var summary = await service.GetOrCreateAccountSummaryAsync(
            "entra-credit",
            "credit@example.com",
            CancellationToken.None);

        summary.Usage.Scope.Should().Be("free");
        summary.Usage.Quota.Should().Be(6);
        summary.Usage.Used.Should().Be(3);
        summary.Usage.Reserved.Should().Be(0);
        summary.Usage.Remaining.Should().Be(3);
        summary.Usage.Exhausted.Should().BeFalse();
    }

    [Fact]
    public async Task AccountSummaryReturnsQuotaSources()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var validExpiry = now.AddDays(30);

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-source",
                Email = "source@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userId,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 1,
                ReservedCount = 1,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = userId,
                    Source = "quick-pack",
                    AmountGranted = 10,
                    AmountConsumed = 4,
                    GrantedAt = now.AddDays(-2),
                    ExpiresAt = validExpiry,
                },
                new RewriteCredit
                {
                    UserId = userId,
                    Source = "expired-pack",
                    AmountGranted = 10,
                    AmountConsumed = 0,
                    GrantedAt = now.AddDays(-40),
                    ExpiresAt = now.AddDays(-1),
                });
            await db.SaveChangesAsync();
        }

        var service = new AccountService(CreateContext);

        var summary = await service.GetOrCreateAccountSummaryAsync(
            "entra-source",
            "source@example.com",
            CancellationToken.None);

        summary.Usage.Sources.Should().HaveCount(2);
        summary.Usage.Sources[0].Source.Should().Be("free");
        summary.Usage.Sources[0].Used.Should().Be(1);
        summary.Usage.Sources[0].Limit.Should().Be(3);
        summary.Usage.Sources[0].Remaining.Should().Be(1);
        summary.Usage.Sources[0].ExpiresAt.Should().BeNull();

        summary.Usage.Sources[1].Source.Should().Be("quick-pack");
        summary.Usage.Sources[1].Used.Should().Be(4);
        summary.Usage.Sources[1].Limit.Should().Be(10);
        summary.Usage.Sources[1].Remaining.Should().Be(6);
        summary.Usage.Sources[1].ExpiresAt.Should().Be(validExpiry);
    }

    [Fact]
    public async Task PaymentsListsCallerPurchases()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-05-30T01:02:03Z");
        var expiry = now.AddDays(90);
        var receiptUrl = "https://pay.stripe.com/receipts/test_receipt";

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-buyer",
                Email = "buyer@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = userId,
                    Source = "PURCHASE",
                    AmountGranted = 10,
                    AmountConsumed = 3,
                    GrantedAt = now,
                    ExpiresAt = expiry,
                    StripeSku = "quick_pack",
                    StripeAmountTotal = 900,
                    StripeCurrency = "nzd",
                    StripeReceiptUrl = receiptUrl,
                },
                new RewriteCredit
                {
                    UserId = userId,
                    Source = "PROMO",
                    AmountGranted = 5,
                    AmountConsumed = 0,
                    GrantedAt = now.AddMinutes(1),
                    ExpiresAt = expiry,
                    StripeSku = "promo_pack",
                    StripeAmountTotal = 0,
                    StripeCurrency = "nzd",
                });
            await db.SaveChangesAsync();
        }

        var service = new AccountService(CreateContext);

        var payments = await service.GetPurchaseHistoryAsync(
            "entra-buyer",
            "buyer@example.com",
            CancellationToken.None);

        payments.Should().ContainSingle();
        var payment = payments.Single();
        payment.Sku.Should().Be("quick_pack");
        payment.Amount.Should().Be(900);
        payment.Currency.Should().Be("nzd");
        payment.ReceiptUrl.Should().Be(receiptUrl);
        payment.Date.Should().Be(now);
        payment.Expiry.Should().Be(expiry);
        payment.Remaining.Should().Be(7);

        var json = JsonSerializer.Serialize(
            payments,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Contain("\"receiptUrl\":\"https://pay.stripe.com/receipts/test_receipt\"");
    }

    [Fact]
    public async Task PaymentsCrossUserDenied()
    {
        var callerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-05-30T02:03:04Z");

        await using (var db = CreateContext())
        {
            db.AppUsers.AddRange(
                new AppUser
                {
                    Id = callerUserId,
                    ExternalAuthUserId = "entra-caller",
                    Email = "caller@example.com",
                    SubscriptionStatus = SubscriptionStatus.Inactive,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new AppUser
                {
                    Id = otherUserId,
                    ExternalAuthUserId = "entra-other",
                    Email = "other@example.com",
                    SubscriptionStatus = SubscriptionStatus.Inactive,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            db.RewriteCredits.AddRange(
                new RewriteCredit
                {
                    UserId = callerUserId,
                    Source = "PURCHASE",
                    AmountGranted = 10,
                    AmountConsumed = 1,
                    GrantedAt = now,
                    ExpiresAt = now.AddDays(90),
                    StripeSku = "quick_pack",
                    StripeAmountTotal = 900,
                    StripeCurrency = "nzd",
                },
                new RewriteCredit
                {
                    UserId = otherUserId,
                    Source = "PURCHASE",
                    AmountGranted = 30,
                    AmountConsumed = 0,
                    GrantedAt = now.AddMinutes(1),
                    ExpiresAt = now.AddDays(90),
                    StripeSku = "value_pack",
                    StripeAmountTotal = 1900,
                    StripeCurrency = "nzd",
                });
            await db.SaveChangesAsync();
        }

        var service = new AccountService(CreateContext);

        var payments = await service.GetPurchaseHistoryAsync(
            "entra-caller",
            "caller@example.com",
            CancellationToken.None);

        payments.Should().ContainSingle();
        payments[0].Sku.Should().Be("quick_pack");
        payments[0].Remaining.Should().Be(9);
    }

    [Fact]
    public async Task DeleteAccountErasesUserAndChildren()
    {
        var userId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var usagePeriodId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-delete",
                Email = "delete@example.com",
                StripeCustomerId = "cus_delete",
                StripeSubscriptionId = "sub_delete",
                SubscriptionStatus = SubscriptionStatus.Active,
                CurrentPeriodEnd = now.AddDays(20),
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-1),
            });
            db.RewriteAttempts.Add(new RewriteAttempt
            {
                Id = attemptId,
                UserId = userId,
                IdempotencyKey = "idem-delete",
                RequestHash = "hash-delete",
                RequestJson = "{\"roughDraftReply\":\"private draft\"}",
                ResultJson = "{\"reply\":\"private result\"}",
                Status = RewriteAttemptStatus.Succeeded,
                ExpiresAt = now.AddDays(1),
                CreatedAt = now.AddDays(-1),
                CompletedAt = now,
            });
            db.UsagePeriods.Add(new UsagePeriod
            {
                Id = usagePeriodId,
                UserId = userId,
                PeriodKey = "paid:sub_delete:2026-06-30T00:00:00.0000000+00:00",
                QuotaLimit = 90,
                UsedCount = 4,
                ReservedCount = 1,
                PeriodStart = now.AddDays(-10),
                PeriodEnd = now.AddDays(20),
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now,
            });
            db.UsageReservations.Add(new UsageReservation
            {
                Id = reservationId,
                UserId = userId,
                UsagePeriodId = usagePeriodId,
                RewriteAttemptId = attemptId,
                RewriteCreditId = creditId,
                Status = UsageReservationStatus.Pending,
                ExpiresAt = now.AddMinutes(30),
                CreatedAt = now.AddMinutes(-10),
            });
            db.RewriteCredits.Add(new RewriteCredit
            {
                Id = creditId,
                UserId = userId,
                Source = "PURCHASE",
                AmountGranted = 10,
                AmountConsumed = 2,
                GrantedAt = now.AddDays(-5),
                ExpiresAt = now.AddDays(25),
                StripeEventId = "evt_credit",
            });
            await db.SaveChangesAsync();
        }

        var canceledSubscriptions = new List<string>();
        var service = new AccountService(
            CreateContext,
            cancelSubscriptionAsync: (subscriptionId, _) =>
            {
                canceledSubscriptions.Add(subscriptionId);
                return Task.CompletedTask;
            });

        await service.DeleteAccountAsync("entra-delete", CancellationToken.None);

        canceledSubscriptions.Should().Equal("sub_delete");

        await using (var db = CreateContext())
        {
            var user = await db.AppUsers.SingleAsync(x => x.Id == userId);
            user.ExternalAuthUserId.Should().StartWith("erased:");
            user.ExternalAuthUserId.Should().NotBe("entra-delete");
            user.Email.Should().BeNull();
            user.StripeCustomerId.Should().Be("cus_delete");
            user.StripeSubscriptionId.Should().Be("sub_delete");
            user.SubscriptionStatus.Should().Be(SubscriptionStatus.Canceled);
            user.CurrentPeriodEnd.Should().BeNull();

            var attempt = await db.RewriteAttempts.SingleAsync(x => x.Id == attemptId);
            attempt.IdempotencyKey.Should().StartWith("erased:");
            attempt.RequestHash.Should().Be("erased");
            attempt.RequestJson.Should().Be("{}");
            attempt.ResultJson.Should().BeNull();
            attempt.ErrorMessage.Should().BeNull();

            var period = await db.UsagePeriods.SingleAsync(x => x.Id == usagePeriodId);
            period.PeriodKey.Should().StartWith("erased:");
            period.QuotaLimit.Should().Be(0);
            period.UsedCount.Should().Be(0);
            period.ReservedCount.Should().Be(0);
            period.PeriodStart.Should().BeNull();
            period.PeriodEnd.Should().BeNull();

            var reservation = await db.UsageReservations.SingleAsync(x => x.Id == reservationId);
            reservation.Status.Should().Be(UsageReservationStatus.Released);
            reservation.ReleasedAt.Should().NotBeNull();

            var credit = await db.RewriteCredits.SingleAsync(x => x.Id == creditId);
            credit.Source.Should().Be("ERASED");
            credit.AmountGranted.Should().Be(0);
            credit.AmountConsumed.Should().Be(0);
            credit.ExpiresAt.Should().NotBeNull();
            credit.StripeEventId.Should().Be("evt_credit");
        }

        var freshUser = await service.GetOrCreateUserAsync(
            "entra-delete",
            "fresh@example.com",
            CancellationToken.None);

        freshUser.Id.Should().NotBe(userId);
        freshUser.Email.Should().Be("fresh@example.com");
    }

    [Fact]
    public async Task DeleteAccountIsIdempotent()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateContext())
        {
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-idempotent-delete",
                Email = "idempotent@example.com",
                StripeCustomerId = "cus_idempotent",
                StripeSubscriptionId = "sub_idempotent",
                SubscriptionStatus = SubscriptionStatus.Active,
                CurrentPeriodEnd = now.AddDays(10),
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var canceledSubscriptions = new List<string>();
        var service = new AccountService(
            CreateContext,
            cancelSubscriptionAsync: (subscriptionId, _) =>
            {
                canceledSubscriptions.Add(subscriptionId);
                return Task.CompletedTask;
            });

        await service.DeleteAccountAsync("entra-idempotent-delete", CancellationToken.None);
        await service.DeleteAccountAsync("entra-idempotent-delete", CancellationToken.None);

        canceledSubscriptions.Should().Equal("sub_idempotent");

        await using var verifyDb = CreateContext();
        var user = await verifyDb.AppUsers.SingleAsync(x => x.Id == userId);
        user.ExternalAuthUserId.Should().StartWith("erased:");
        user.Email.Should().BeNull();
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Canceled);
        (await verifyDb.AppUsers.CountAsync(x => x.ExternalAuthUserId == "entra-idempotent-delete")).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAccountWorksWhenProviderUsesRetryingExecutionStrategy()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateRetryingStrategyContext())
        {
            await db.Database.EnsureCreatedAsync();
            db.AppUsers.Add(new AppUser
            {
                Id = userId,
                ExternalAuthUserId = "entra-retrying-delete",
                Email = "retrying-delete@example.com",
                SubscriptionStatus = SubscriptionStatus.Inactive,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
            });
            db.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userId,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 1,
                ReservedCount = 1,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var service = new AccountService(CreateRetryingStrategyContext);

        await service.DeleteAccountAsync("entra-retrying-delete", CancellationToken.None);

        await using var verifyDb = CreateRetryingStrategyContext();
        var user = await verifyDb.AppUsers.SingleAsync(x => x.Id == userId);
        user.ExternalAuthUserId.Should().StartWith("erased:");
        user.Email.Should().BeNull();
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Canceled);

        var period = await verifyDb.UsagePeriods.SingleAsync(x => x.UserId == userId);
        period.PeriodKey.Should().StartWith("erased:");
        period.QuotaLimit.Should().Be(0);
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private AppDbContext CreateRetryingStrategyContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IExecutionStrategyFactory, TestExecutionStrategyFactory>()
            .Options;
        return new AppDbContext(options);
    }

    private sealed class TestExecutionStrategyFactory(
        ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new TestExecutionStrategy(dependencies);
    }

    private sealed class TestExecutionStrategy(
        ExecutionStrategyDependencies dependencies) : ExecutionStrategy(
            dependencies,
            maxRetryCount: 1,
            maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }
}
