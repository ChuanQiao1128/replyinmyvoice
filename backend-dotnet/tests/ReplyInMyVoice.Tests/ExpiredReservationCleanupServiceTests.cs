using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ExpiredReservationCleanupServiceTests
{
    [Fact]
    public async Task RunOnceAsync_releases_expired_reservations_without_consuming_quota()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var quota = new QuotaService(fixture.CreateContext);
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await quota.ReserveAsync(
            user.Id,
            "idem-cleanup",
            "hash-cleanup",
            "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            "free:lifetime",
            1,
            now,
            TimeSpan.FromMinutes(1));
        var cleanup = new ExpiredReservationCleanupService(quota);

        var released = await cleanup.RunOnceAsync(now.AddMinutes(2), CancellationToken.None);

        released.Should().Be(1);
        await using var db = fixture.CreateContext();
        var period = await db.UsagePeriods.SingleAsync();
        period.UsedCount.Should().Be(0);
        period.ReservedCount.Should().Be(0);
        (await db.UsageReservations.SingleAsync()).Status.Should().Be(UsageReservationStatus.Released);
        (await db.RewriteAttempts.SingleAsync()).Status.Should().Be(RewriteAttemptStatus.Expired);
    }
}
