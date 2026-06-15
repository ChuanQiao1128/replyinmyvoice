using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests;

public sealed class CreditQuotaSchemaTests : IAsyncLifetime
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
    public async Task RewriteCreditChecksRejectConsumedGreaterThanGranted()
    {
        await using var db = CreateContext();
        var user = await SeedUserAsync(db, "credit-over-consumed");
        db.RewriteCredits.Add(CreateCredit(user.Id, amountGranted: 3, amountConsumed: 4));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task RewriteCreditChecksRejectNegativeConsumed()
    {
        await using var db = CreateContext();
        var user = await SeedUserAsync(db, "credit-negative-consumed");
        db.RewriteCredits.Add(CreateCredit(user.Id, amountGranted: 3, amountConsumed: -1));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task RewriteCreditChecksAllowValidCredit()
    {
        await using var db = CreateContext();
        var user = await SeedUserAsync(db, "credit-valid");
        db.RewriteCredits.Add(CreateCredit(user.Id, amountGranted: 3, amountConsumed: 1));

        await db.SaveChangesAsync();

        (await db.RewriteCredits.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UsagePeriodChecksRejectNegativeUsedCount()
    {
        await using var db = CreateContext();
        var user = await SeedUserAsync(db, "usage-negative-used");
        db.UsagePeriods.Add(CreateUsagePeriod(user.Id, usedCount: -1, reservedCount: 0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task UsagePeriodChecksRejectNegativeReservedCount()
    {
        await using var db = CreateContext();
        var user = await SeedUserAsync(db, "usage-negative-reserved");
        db.UsagePeriods.Add(CreateUsagePeriod(user.Id, usedCount: 0, reservedCount: -1));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task UsagePeriodChecksAllowValidPeriod()
    {
        await using var db = CreateContext();
        var user = await SeedUserAsync(db, "usage-valid");
        db.UsagePeriods.Add(CreateUsagePeriod(user.Id, usedCount: 1, reservedCount: 1));

        await db.SaveChangesAsync();

        (await db.UsagePeriods.CountAsync()).Should().Be(1);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<AppUser> SeedUserAsync(AppDbContext db, string suffix)
    {
        var now = DateTimeOffset.Parse("2026-06-14T00:00:00Z");
        var user = new AppUser
        {
            ExternalAuthUserId = $"clerk_{suffix}",
            Email = $"{suffix}@example.com",
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static RewriteCredit CreateCredit(Guid userId, int amountGranted, int amountConsumed) =>
        new()
        {
            UserId = userId,
            Source = "schema-test",
            AmountGranted = amountGranted,
            AmountConsumed = amountConsumed,
            GrantedAt = DateTimeOffset.Parse("2026-06-14T00:00:00Z"),
        };

    private static UsagePeriod CreateUsagePeriod(Guid userId, int usedCount, int reservedCount) =>
        new()
        {
            UserId = userId,
            PeriodKey = $"schema-test:{Guid.NewGuid():N}",
            QuotaLimit = 3,
            UsedCount = usedCount,
            ReservedCount = reservedCount,
            CreatedAt = DateTimeOffset.Parse("2026-06-14T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-06-14T00:00:00Z"),
        };
}
