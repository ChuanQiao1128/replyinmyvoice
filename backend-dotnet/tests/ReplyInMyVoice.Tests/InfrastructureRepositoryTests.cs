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

    [Fact]
    public async Task TryReserveSlotAsync_increments_and_refreshes_quota_limit_when_capacity_remains()
    {
        await using var seedDb = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var updatedAt = now.AddMinutes(5);
        var user = CreateUser("period-reserve-user", "period-reserve@example.com", now);
        var period = new UsagePeriod
        {
            User = user,
            PeriodKey = "free:lifetime",
            QuotaLimit = 2,
            UsedCount = 1,
            ReservedCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        seedDb.UsagePeriods.Add(period);
        await seedDb.SaveChangesAsync();
        var originalRowVersion = period.RowVersion;

        await using (var updateDb = CreateContext())
        {
            var repository = new UsagePeriodRepository(updateDb);

            var affected = await repository.TryReserveSlotAsync(period.Id, quotaLimit: 3, updatedAt);

            affected.Should().Be(1);
        }

        await using var verifyDb = CreateContext();
        var updated = await verifyDb.UsagePeriods.SingleAsync(x => x.Id == period.Id);
        updated.ReservedCount.Should().Be(1);
        updated.UsedCount.Should().Be(1);
        updated.QuotaLimit.Should().Be(3);
        updated.UpdatedAt.Should().Be(updatedAt);
        updated.RowVersion.Should().NotBe(originalRowVersion);
    }

    [Fact]
    public async Task TryReserveSlotAsync_returns_zero_and_writes_nothing_when_used_plus_reserved_reaches_limit()
    {
        await using var seedDb = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var user = CreateUser("period-full-user", "period-full@example.com", now);
        var period = new UsagePeriod
        {
            User = user,
            PeriodKey = "free:lifetime",
            QuotaLimit = 3,
            UsedCount = 2,
            ReservedCount = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        seedDb.UsagePeriods.Add(period);
        await seedDb.SaveChangesAsync();
        var originalRowVersion = period.RowVersion;

        await using (var updateDb = CreateContext())
        {
            var repository = new UsagePeriodRepository(updateDb);

            var affected = await repository.TryReserveSlotAsync(period.Id, quotaLimit: 3, now.AddMinutes(5));

            affected.Should().Be(0);
        }

        await using var verifyDb = CreateContext();
        var unchanged = await verifyDb.UsagePeriods.SingleAsync(x => x.Id == period.Id);
        unchanged.QuotaLimit.Should().Be(3);
        unchanged.UsedCount.Should().Be(2);
        unchanged.ReservedCount.Should().Be(1);
        unchanged.UpdatedAt.Should().Be(now);
        unchanged.RowVersion.Should().Be(originalRowVersion);
    }

    [Fact]
    public async Task TryConsumeForReservationAsync_stops_exactly_at_amount_granted()
    {
        await using var seedDb = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var user = CreateUser("credit-consume-user", "credit-consume@example.com", now);
        var credit = new RewriteCredit
        {
            User = user,
            Source = "PROMO",
            AmountGranted = 2,
            AmountConsumed = 0,
            GrantedAt = now,
            ExpiresAt = now.AddDays(1),
        };
        seedDb.RewriteCredits.Add(credit);
        await seedDb.SaveChangesAsync();
        var originalRowVersion = credit.RowVersion;

        await using (var updateDb = CreateContext())
        {
            var repository = new RewriteCreditRepository(updateDb);

            (await repository.TryConsumeForReservationAsync(credit.Id)).Should().Be(1);
            (await repository.TryConsumeForReservationAsync(credit.Id)).Should().Be(1);
            (await repository.TryConsumeForReservationAsync(credit.Id)).Should().Be(0);
        }

        await using var verifyDb = CreateContext();
        var updated = await verifyDb.RewriteCredits.SingleAsync(x => x.Id == credit.Id);
        updated.AmountConsumed.Should().Be(2);
        updated.RowVersion.Should().NotBe(originalRowVersion);
    }

    [Fact]
    public async Task ReleaseConsumedAsync_and_ReleaseReservedSlotAsync_floor_at_zero()
    {
        await using var seedDb = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var user = CreateUser("release-floor-user", "release-floor@example.com", now);
        var period = new UsagePeriod
        {
            User = user,
            PeriodKey = "free:lifetime",
            QuotaLimit = 3,
            UsedCount = 0,
            ReservedCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var credit = new RewriteCredit
        {
            User = user,
            Source = "PROMO",
            AmountGranted = 1,
            AmountConsumed = 0,
            GrantedAt = now,
            ExpiresAt = now.AddDays(1),
        };
        seedDb.AddRange(period, credit);
        await seedDb.SaveChangesAsync();

        await using (var updateDb = CreateContext())
        {
            var periods = new UsagePeriodRepository(updateDb);
            var credits = new RewriteCreditRepository(updateDb);

            (await periods.ReleaseReservedSlotAsync(period.Id, now.AddMinutes(1))).Should().Be(1);
            (await credits.ReleaseConsumedAsync(credit.Id)).Should().Be(1);
        }

        await using var verifyDb = CreateContext();
        (await verifyDb.UsagePeriods.SingleAsync(x => x.Id == period.Id)).ReservedCount.Should().Be(0);
        (await verifyDb.RewriteCredits.SingleAsync(x => x.Id == credit.Id)).AmountConsumed.Should().Be(0);
    }

    [Fact]
    public async Task FinalizeReservedSlotAsync_moves_reserved_to_used()
    {
        await using var seedDb = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var finalizedAt = now.AddMinutes(3);
        var user = CreateUser("period-finalize-user", "period-finalize@example.com", now);
        var period = new UsagePeriod
        {
            User = user,
            PeriodKey = "free:lifetime",
            QuotaLimit = 3,
            UsedCount = 2,
            ReservedCount = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        seedDb.UsagePeriods.Add(period);
        await seedDb.SaveChangesAsync();
        var originalRowVersion = period.RowVersion;

        await using (var updateDb = CreateContext())
        {
            var repository = new UsagePeriodRepository(updateDb);

            var affected = await repository.FinalizeReservedSlotAsync(period.Id, finalizedAt);

            affected.Should().Be(1);
        }

        await using var verifyDb = CreateContext();
        var updated = await verifyDb.UsagePeriods.SingleAsync(x => x.Id == period.Id);
        updated.ReservedCount.Should().Be(0);
        updated.UsedCount.Should().Be(3);
        updated.UpdatedAt.Should().Be(finalizedAt);
        updated.RowVersion.Should().NotBe(originalRowVersion);
    }

    [Fact]
    public async Task TryTransitionFromPendingAsync_allows_exactly_one_transition_and_stamps_correct_timestamp()
    {
        await using var seedDb = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var user = CreateUser("reservation-transition-user", "reservation-transition@example.com", now);
        var period = new UsagePeriod
        {
            User = user,
            PeriodKey = "free:lifetime",
            QuotaLimit = 5,
            ReservedCount = 3,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var finalized = CreateReservation(user, period, "transition-finalized", now);
        var released = CreateReservation(user, period, "transition-released", now);
        var expired = CreateReservation(user, period, "transition-expired", now);
        seedDb.AddRange(period, finalized, released, expired);
        await seedDb.SaveChangesAsync();

        await using (var updateDb = CreateContext())
        {
            var repository = new UsageReservationRepository(updateDb);

            (await repository.TryTransitionFromPendingAsync(finalized.Id, UsageReservationStatus.Finalized, now.AddMinutes(1)))
                .Should()
                .Be(1);
            (await repository.TryTransitionFromPendingAsync(finalized.Id, UsageReservationStatus.Released, now.AddMinutes(2)))
                .Should()
                .Be(0);
            (await repository.TryTransitionFromPendingAsync(released.Id, UsageReservationStatus.Released, now.AddMinutes(3)))
                .Should()
                .Be(1);
            (await repository.TryTransitionFromPendingAsync(expired.Id, UsageReservationStatus.Expired, now.AddMinutes(4)))
                .Should()
                .Be(1);
            await FluentActions
                .Invoking(() => repository.TryTransitionFromPendingAsync(expired.Id, UsageReservationStatus.Pending, now.AddMinutes(5)))
                .Should()
                .ThrowAsync<ArgumentOutOfRangeException>();
        }

        await using var verifyDb = CreateContext();
        var finalizedRow = await verifyDb.UsageReservations.SingleAsync(x => x.Id == finalized.Id);
        finalizedRow.Status.Should().Be(UsageReservationStatus.Finalized);
        finalizedRow.FinalizedAt.Should().Be(now.AddMinutes(1));
        finalizedRow.ReleasedAt.Should().BeNull();

        var releasedRow = await verifyDb.UsageReservations.SingleAsync(x => x.Id == released.Id);
        releasedRow.Status.Should().Be(UsageReservationStatus.Released);
        releasedRow.FinalizedAt.Should().BeNull();
        releasedRow.ReleasedAt.Should().Be(now.AddMinutes(3));

        var expiredRow = await verifyDb.UsageReservations.SingleAsync(x => x.Id == expired.Id);
        expiredRow.Status.Should().Be(UsageReservationStatus.Expired);
        expiredRow.FinalizedAt.Should().BeNull();
        expiredRow.ReleasedAt.Should().Be(now.AddMinutes(4));
    }

    [Fact]
    public async Task ListUsableForReservationIdsAsync_orders_expiring_usable_credits_first()
    {
        await using var db = CreateContext();
        var now = DateTimeOffset.Parse("2026-06-09T00:00:00Z");
        var user = CreateUser("credit-list-user", "credit-list@example.com", now);
        var otherUser = CreateUser("credit-list-other-user", "credit-list-other@example.com", now);
        db.AppUsers.AddRange(user, otherUser);
        await db.SaveChangesAsync();

        var first = CreateCredit(user, now.AddDays(-1), now.AddHours(2));
        var second = CreateCredit(user, now.AddDays(-3), now.AddDays(1));
        var third = CreateCredit(user, now.AddDays(-5), null);
        db.RewriteCredits.AddRange(
            CreateCredit(user, now.AddDays(-6), now.AddHours(-1)),
            CreateCredit(user, now.AddDays(-7), now.AddHours(1), amountConsumed: 1),
            CreateCredit(otherUser, now.AddDays(-8), now.AddMinutes(30)),
            third,
            second,
            first);
        await db.SaveChangesAsync();

        var repository = new RewriteCreditRepository(db);

        var ids = await repository.ListUsableForReservationIdsAsync(user.Id, now);

        ids.Should().Equal(first.Id, second.Id, third.Id);
    }

    private static AppUser CreateUser(string externalAuthUserId, string email, DateTimeOffset now) =>
        new()
        {
            ExternalAuthUserId = externalAuthUserId,
            Email = email,
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static RewriteCredit CreateCredit(
        AppUser user,
        DateTimeOffset grantedAt,
        DateTimeOffset? expiresAt,
        int amountConsumed = 0) =>
        new()
        {
            User = user,
            Source = "PROMO",
            AmountGranted = 1,
            AmountConsumed = amountConsumed,
            GrantedAt = grantedAt,
            ExpiresAt = expiresAt,
        };

    private static UsageReservation CreateReservation(
        AppUser user,
        UsagePeriod period,
        string idempotencyKey,
        DateTimeOffset now)
    {
        var attempt = new RewriteAttempt
        {
            User = user,
            IdempotencyKey = idempotencyKey,
            RequestHash = $"hash-{idempotencyKey}",
            RequestJson = "{\"roughDraftReply\":\"Thanks for the update.\",\"tone\":\"warm\"}",
            Status = RewriteAttemptStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };

        return new UsageReservation
        {
            User = user,
            UsagePeriod = period,
            RewriteAttempt = attempt,
            Status = UsageReservationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }
}
