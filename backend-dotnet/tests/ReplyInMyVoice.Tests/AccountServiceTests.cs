using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
                QuotaLimit = 40,
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
        summary.Usage.Quota.Should().Be(40);
        summary.Usage.Used.Should().Be(7);
        summary.Usage.Reserved.Should().Be(2);
        summary.Usage.Remaining.Should().Be(31);
        summary.Usage.Exhausted.Should().BeFalse();
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }
}
