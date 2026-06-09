using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ExpiredReservationCleanupServiceTests
{
    [Fact]
    public async Task RunOnceAsync_releases_expired_reservations_without_consuming_quota()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await using (var reserveDb = fixture.CreateContext())
        {
            await CreateReserveHandler(reserveDb).HandleAsync(ReserveCommand(
                user.Id,
                "idem-cleanup",
                "hash-cleanup",
                now,
                TimeSpan.FromMinutes(1)));
        }

        await using var cleanupDb = fixture.CreateContext();
        var cleanup = new ExpiredReservationCleanupService(CreateExpiredHandler(cleanupDb));

        var released = await cleanup.RunOnceAsync(now.AddMinutes(2), CancellationToken.None);

        released.Should().Be(1);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Expired);
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Expired);
    }

    [Fact]
    public async Task RunOnceAsync_does_not_release_processing_reservations_before_expiry()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        Guid attemptId;
        await using (var reserveDb = fixture.CreateContext())
        {
            var reserved = await CreateReserveHandler(reserveDb).HandleAsync(ReserveCommand(
                user.Id,
                "idem-processing-not-expired",
                "hash-processing-not-expired",
                now,
                TimeSpan.FromMinutes(10)));
            attemptId = reserved.AttemptId;
        }

        await using (var processingDb = fixture.CreateContext())
        {
            await CreateMarkProcessingHandler(processingDb).HandleAsync(new MarkQuotaProcessingCommand(
                attemptId,
                now.AddMinutes(1)));
        }

        await using var cleanupDb = fixture.CreateContext();
        var cleanup = new ExpiredReservationCleanupService(CreateExpiredHandler(cleanupDb));

        var released = await cleanup.RunOnceAsync(now.AddMinutes(2), CancellationToken.None);

        released.Should().Be(0);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(1);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Pending);
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Processing);
    }

    private static ReserveQuotaHandler CreateReserveHandler(AppDbContext db) =>
        new(
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db));

    private static MarkQuotaProcessingHandler CreateMarkProcessingHandler(AppDbContext db) =>
        new(new RewriteAttemptRepository(db), new UnitOfWork(db));

    private static ReleaseExpiredReservationsHandler CreateExpiredHandler(AppDbContext db) =>
        new(new UsageReservationRepository(db), new UnitOfWork(db));

    private static ReserveQuotaCommand ReserveCommand(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset now,
        TimeSpan reservationTtl) =>
        new(
            userId,
            idempotencyKey,
            requestHash,
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            1,
            now,
            reservationTtl);
}
