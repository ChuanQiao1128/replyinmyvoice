using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class QuotaServiceTests
{
    [Fact]
    public async Task ReserveAsync_creates_pending_attempt_and_reservation_without_charging_used_count()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);

        var result = await service.ReserveAsync(
            user.Id,
            "idem-1",
            "hash-1",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            quotaLimit: 3,
            now: DateTimeOffset.Parse("2026-05-19T00:00:00Z"),
            reservationTtl: TimeSpan.FromMinutes(10));

        result.Kind.Should().Be(ReserveRewriteResultKind.Created);

        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(1);

        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Pending);

        var reservation = await db.UsageReservations.SingleAsync();
        reservation.Status.Should().Be(UsageReservationStatus.Pending);

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.MessageType.Should().Be("RewriteJobCreated");
        outbox.CorrelationId.Should().Be(attempt.Id.ToString());
        outbox.PayloadJson.Should().Contain(attempt.Id.ToString());
    }

    [Fact]
    public async Task ReserveAsync_returns_existing_attempt_for_duplicate_idempotency_key_without_second_reservation()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);

        var first = await service.ReserveAsync(user.Id, "idem-dup", "hash-1", "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}", "free:lifetime", 3, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        var second = await service.ReserveAsync(user.Id, "idem-dup", "hash-1", "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}", "free:lifetime", 3, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));

        second.Kind.Should().Be(ReserveRewriteResultKind.Existing);
        second.AttemptId.Should().Be(first.AttemptId);

        await using var db = fixture.CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(1);
        (await db.UsageReservations.CountAsync()).Should().Be(1);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
        (await db.UsagePeriods.SingleAsync()).ReservedCount.Should().Be(1);
    }

    [Fact]
    public async Task ReserveAsync_rejects_same_idempotency_key_with_different_request_hash()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);

        var first = await service.ReserveAsync(
            user.Id,
            "idem-conflict",
            "hash-1",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));
        var second = await service.ReserveAsync(
            user.Id,
            "idem-conflict",
            "hash-2",
            "{\"roughDraftReply\":\"This is a different request body.\",\"tone\":\"direct\"}",
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));

        first.Kind.Should().Be(ReserveRewriteResultKind.Created);
        second.Kind.Should().Be(ReserveRewriteResultKind.Conflict);
        second.AttemptId.Should().Be(first.AttemptId);

        await using var db = fixture.CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(1);
        (await db.UsageReservations.CountAsync()).Should().Be(1);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReleaseAsync_releases_pending_reservation_without_consuming_quota()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);
        var reservation = await service.ReserveAsync(user.Id, "idem-fail", "hash-1", "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}", "free:lifetime", 3, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));

        await service.ReleaseAsync(reservation.AttemptId, "openai_failed", DateTimeOffset.UtcNow);

        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);

        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Failed);
        attempt.ErrorCode.Should().Be("openai_failed");

        var usageReservation = await db.UsageReservations.SingleAsync();
        usageReservation.Status.Should().Be(UsageReservationStatus.Released);
    }

    [Fact]
    public async Task FinalizeSuccessAsync_charges_quota_once_even_when_called_twice()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);
        var reservation = await service.ReserveAsync(user.Id, "idem-success", "hash-1", "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}", "free:lifetime", 3, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));

        await service.FinalizeSuccessAsync(reservation.AttemptId, "{\"rewrittenText\":\"hello\"}", DateTimeOffset.UtcNow);
        await service.FinalizeSuccessAsync(reservation.AttemptId, "{\"rewrittenText\":\"hello\"}", DateTimeOffset.UtcNow);

        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Finalized);
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Succeeded);
    }

    [Fact]
    public async Task ReserveAsync_rejects_second_distinct_request_when_only_one_quota_slot_remains()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);

        await service.ReserveAsync(user.Id, "idem-a", "hash-a", "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}", "free:lifetime", 1, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        var second = await service.ReserveAsync(user.Id, "idem-b", "hash-b", "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}", "free:lifetime", 1, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));

        second.Kind.Should().Be(ReserveRewriteResultKind.QuotaExceeded);

        await using var db = fixture.CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(1);
        (await db.UsageReservations.CountAsync()).Should().Be(1);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReserveAsync_uses_valid_credit_when_period_quota_is_full_and_success_keeps_credit_consumed()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-25T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 3,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 2,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(30),
            });
            await seedDb.SaveChangesAsync();
        }

        var service = new QuotaService(fixture.CreateContext);

        var result = await service.ReserveAsync(
            user.Id,
            "idem-credit-success",
            "hash-credit-success",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            quotaLimit: 3,
            now,
            TimeSpan.FromMinutes(10));

        result.Kind.Should().Be(ReserveRewriteResultKind.Created);
        await using (var reservedDb = fixture.CreateContext())
        {
            var period = await reservedDb.UsagePeriods.SingleAsync();
            period.UsedCount.Should().Be(3);
            period.ReservedCount.Should().Be(0);
            (await reservedDb.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(1);
            (await reservedDb.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Pending);
            (await reservedDb.OutboxMessages.CountAsync()).Should().Be(1);
        }

        await service.FinalizeSuccessAsync(result.AttemptId, "{\"rewrittenText\":\"hello\"}", now.AddMinutes(1));

        await using var finalizedDb = fixture.CreateContext();
        var finalizedPeriod = await finalizedDb.UsagePeriods.SingleAsync();
        finalizedPeriod.UsedCount.Should().Be(3);
        finalizedPeriod.ReservedCount.Should().Be(0);
        (await finalizedDb.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(1);
        (await finalizedDb.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Finalized);
        (await finalizedDb.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Succeeded);
    }

    [Fact]
    public async Task ReleaseAsync_refunds_credit_backed_reservation_without_touching_period_counters()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-25T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 3,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(30),
            });
            await seedDb.SaveChangesAsync();
        }

        var service = new QuotaService(fixture.CreateContext);
        var result = await service.ReserveAsync(
            user.Id,
            "idem-credit-release",
            "hash-credit-release",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            now,
            TimeSpan.FromMinutes(10));

        result.Kind.Should().Be(ReserveRewriteResultKind.Created);

        await service.ReleaseAsync(result.AttemptId, "provider_failed", now.AddMinutes(1));

        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(3);
        period.ReservedCount.Should().Be(0);
        (await db.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Released);
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Failed);
    }

    [Fact]
    public async Task ReserveAsync_returns_quota_exceeded_when_period_full_and_no_usable_credit()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-25T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 3,
                UsedCount = 3,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 5,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-100),
                ExpiresAt = now.AddSeconds(-1),
            });
            await seedDb.SaveChangesAsync();
        }

        var service = new QuotaService(fixture.CreateContext);

        var result = await service.ReserveAsync(
            user.Id,
            "idem-no-credit",
            "hash-no-credit",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            now,
            TimeSpan.FromMinutes(10));

        result.Kind.Should().Be(ReserveRewriteResultKind.QuotaExceeded);

        await using var db = fixture.CreateContext();
        (await db.RewriteAttempts.CountAsync()).Should().Be(0);
        (await db.UsageReservations.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
        (await db.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(0);
    }

    [Fact]
    public async Task MarkProcessingAsync_allows_only_one_pending_claim()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);
        var reservation = await service.ReserveAsync(
            user.Id,
            "idem-claim",
            "hash-claim",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(10));

        var first = await service.MarkProcessingAsync(reservation.AttemptId, DateTimeOffset.UtcNow);
        var second = await service.MarkProcessingAsync(reservation.AttemptId, DateTimeOffset.UtcNow);

        first.Should().BeTrue();
        second.Should().BeFalse();
        await using var db = fixture.CreateContext();
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Processing);
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_releases_stale_pending_reservations_without_consuming_quota()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-19T00:00:00Z");
        await service.ReserveAsync(
            user.Id,
            "idem-expired",
            "hash-expired",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            1,
            now,
            TimeSpan.FromMinutes(1));

        var released = await service.ReleaseExpiredReservationsAsync(now.AddMinutes(2));

        released.Should().Be(1);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Expired);
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Expired);
        attempt.ErrorCode.Should().Be("reservation_expired");
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_releases_stale_processing_reservations_without_consuming_quota()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-19T00:00:00Z");
        var reserved = await service.ReserveAsync(
            user.Id,
            "idem-processing-expired",
            "hash-processing-expired",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            1,
            now,
            TimeSpan.FromMinutes(1));
        await service.MarkProcessingAsync(reserved.AttemptId, now.AddSeconds(10));

        var released = await service.ReleaseExpiredReservationsAsync(now.AddMinutes(2));

        released.Should().Be(1);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Expired);
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Expired);
        attempt.ErrorCode.Should().Be("processing_timed_out");
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_refunds_credit_backed_stale_processing_reservations()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-19T00:00:00Z");
        await using (var seedDb = fixture.CreateContext())
        {
            seedDb.UsagePeriods.Add(new UsagePeriod
            {
                UserId = user.Id,
                PeriodKey = "free:lifetime",
                QuotaLimit = 1,
                UsedCount = 1,
                ReservedCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            seedDb.RewriteCredits.Add(new RewriteCredit
            {
                UserId = user.Id,
                Source = "PURCHASE",
                AmountGranted = 1,
                AmountConsumed = 0,
                GrantedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(30),
            });
            await seedDb.SaveChangesAsync();
        }

        var reserved = await service.ReserveAsync(
            user.Id,
            "idem-credit-processing-expired",
            "hash-credit-processing-expired",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            1,
            now,
            TimeSpan.FromMinutes(1));
        await service.MarkProcessingAsync(reserved.AttemptId, now.AddSeconds(10));

        var released = await service.ReleaseExpiredReservationsAsync(now.AddMinutes(2));

        released.Should().Be(1);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(1);
        period.ReservedCount.Should().Be(0);
        (await db.RewriteCredits.SingleAsync()).AmountConsumed.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Expired);
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.Status.Should().Be(RewriteAttemptStatus.Expired);
        attempt.ErrorCode.Should().Be("processing_timed_out");
    }

    [Fact]
    public async Task FinalizeSuccessAsync_does_not_succeed_after_reservation_expired()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new QuotaService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-19T00:00:00Z");
        var reserved = await service.ReserveAsync(
            user.Id,
            "idem-finalize-expired",
            "hash-finalize-expired",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            1,
            now,
            TimeSpan.FromMinutes(1));
        await service.ReleaseExpiredReservationsAsync(now.AddMinutes(2));

        await service.FinalizeSuccessAsync(
            reserved.AttemptId,
            "{\"rewrittenText\":\"late success\",\"changeSummary\":[],\"riskNotes\":[]}",
            now.AddMinutes(3));

        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Expired);
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Expired);
    }
}

internal sealed class DbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private DbFixture(SqliteConnection connection)
    {
        _connection = connection;
    }

    public static async Task<DbFixture> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var fixture = new DbFixture(connection);
        await using var db = fixture.CreateContext();
        await db.Database.EnsureCreatedAsync();
        return fixture;
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    public async Task<AppUser> CreateUserAsync()
    {
        await using var db = CreateContext();
        var user = new AppUser
        {
            ExternalAuthUserId = $"clerk_{Guid.NewGuid():N}",
            Email = "test@example.com",
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
