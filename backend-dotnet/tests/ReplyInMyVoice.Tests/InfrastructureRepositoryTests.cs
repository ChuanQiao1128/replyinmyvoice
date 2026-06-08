using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests;

public sealed class InfrastructureRepositoryTests : IAsyncLifetime
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
    public async Task Repositories_share_scoped_context_until_unit_of_work_saves()
    {
        await using var db = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var user = new AppUser
        {
            ExternalAuthUserId = "repo-user",
            Email = "repo@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        IAppUserRepository appUsers = new AppUserRepository(db);
        IUsagePeriodRepository usagePeriods = new UsagePeriodRepository(db);
        IRewriteAttemptRepository attempts = new RewriteAttemptRepository(db);
        IUsageReservationRepository reservations = new UsageReservationRepository(db);
        IUnitOfWork unitOfWork = new UnitOfWork(db);

        var period = new UsagePeriod
        {
            UserId = user.Id,
            PeriodKey = "free:lifetime",
            QuotaLimit = 3,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var activeAttempt = new RewriteAttempt
        {
            UserId = user.Id,
            IdempotencyKey = "repo-idem",
            RequestHash = "repo-hash",
            RequestJson = "{\"roughDraftReply\":\"Thanks for the note.\"}",
            Status = RewriteAttemptStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };
        var deletedAttempt = new RewriteAttempt
        {
            UserId = user.Id,
            IdempotencyKey = "repo-deleted-idem",
            RequestHash = "repo-deleted-hash",
            RequestJson = "{\"roughDraftReply\":\"Thanks for the earlier note.\"}",
            Status = RewriteAttemptStatus.Succeeded,
            CreatedAt = now.AddMinutes(-5),
            CompletedAt = now.AddMinutes(-4),
            DeletedAt = now.AddMinutes(-1),
            ExpiresAt = now.AddMinutes(5),
        };
        var reservation = new UsageReservation
        {
            UserId = user.Id,
            UsagePeriod = period,
            RewriteAttempt = activeAttempt,
            Status = UsageReservationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };

        await usagePeriods.AddAsync(period);
        await attempts.AddAsync(activeAttempt);
        await attempts.AddAsync(deletedAttempt);
        await reservations.AddAsync(reservation);
        var saved = await unitOfWork.SaveChangesAsync();

        saved.Should().BeGreaterThan(0);

        await using var verifyDb = CreateContext();
        IAppUserRepository verifyUsers = new AppUserRepository(verifyDb);
        IUsagePeriodRepository verifyPeriods = new UsagePeriodRepository(verifyDb);
        IRewriteAttemptRepository verifyAttempts = new RewriteAttemptRepository(verifyDb);

        (await verifyUsers.GetByIdAsync(user.Id)).Should().NotBeNull();
        (await verifyUsers.GetByExternalAuthUserIdAsync("repo-user")).Should().NotBeNull();
        (await verifyPeriods.GetByUserIdAndPeriodKeyAsync(user.Id, "free:lifetime"))
            .Should()
            .NotBeNull();
        (await verifyAttempts.GetByUserIdAndIdempotencyKeyAsync(user.Id, "repo-idem"))
            .Should()
            .NotBeNull();
        (await verifyAttempts.GetByUserIdAndIdempotencyKeyAsync(user.Id, "repo-deleted-idem"))
            .Should()
            .NotBeNull();
        (await verifyAttempts.GetByIdForUserAsync(activeAttempt.Id, user.Id))
            .Should()
            .NotBeNull();
        (await verifyAttempts.GetByIdForUserAsync(deletedAttempt.Id, user.Id))
            .Should()
            .BeNull();
        (await verifyDb.UsageReservations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RewriteCreditRepository_selects_earliest_usable_credit_for_user()
    {
        await using var db = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var user = new AppUser
        {
            ExternalAuthUserId = "credit-user",
            Email = "credit@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var otherUser = new AppUser
        {
            ExternalAuthUserId = "credit-other-user",
            Email = "other@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.AppUsers.AddRange(user, otherUser);
        await db.SaveChangesAsync();

        var expected = new RewriteCredit
        {
            UserId = user.Id,
            Source = "PROMO",
            AmountGranted = 1,
            AmountConsumed = 0,
            GrantedAt = now.AddDays(-1),
            ExpiresAt = now.AddDays(1),
        };
        db.RewriteCredits.AddRange(
            new RewriteCredit
            {
                UserId = user.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 1,
                GrantedAt = now.AddDays(-3),
                ExpiresAt = now.AddHours(1),
            },
            new RewriteCredit
            {
                UserId = user.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-2),
                ExpiresAt = now.AddSeconds(-1),
            },
            new RewriteCredit
            {
                UserId = user.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-4),
                ExpiresAt = null,
            },
            new RewriteCredit
            {
                UserId = user.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-5),
                ExpiresAt = now.AddDays(3),
            },
            new RewriteCredit
            {
                UserId = otherUser.Id,
                Source = "PROMO",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-6),
                ExpiresAt = now.AddMinutes(1),
            },
            expected);
        await db.SaveChangesAsync();

        IRewriteCreditRepository credits = new RewriteCreditRepository(db);

        var credit = await credits.GetUsableForReservationAsync(user.Id, now);

        credit.Should().NotBeNull();
        credit!.Id.Should().Be(expected.Id);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }
}
