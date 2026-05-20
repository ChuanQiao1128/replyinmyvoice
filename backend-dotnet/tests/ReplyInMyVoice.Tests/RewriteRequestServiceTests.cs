using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class RewriteRequestServiceTests
{
    [Fact]
    public async Task CreateAttemptAsync_reserves_quota_and_creates_outbox_without_direct_queue_publish()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var service = new RewriteRequestService(
            fixture.CreateContext,
            new QuotaService(fixture.CreateContext));

        var result = await service.CreateAttemptAsync(
            user.Id,
            "idem-api",
            new RewriteRequest("message", "rough draft reply", "teacher", "reply", "facts", "preserve", "warm"),
            "free:lifetime",
            quotaLimit: 3,
            now: DateTimeOffset.UtcNow,
            CancellationToken.None);

        result.Kind.Should().Be(ReserveRewriteResultKind.Created);

        await using var db = fixture.CreateContext();
        var attempt = await db.RewriteAttempts.SingleAsync();
        attempt.RequestJson.Should().Contain("rough draft reply");
        attempt.RequestJson.Should().Contain("warm");
        (await db.UsageReservations.CountAsync()).Should().Be(1);
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.MessageType.Should().Be("RewriteJobCreated");
        outbox.PayloadJson.Should().Contain(result.AttemptId.ToString());
    }
}
