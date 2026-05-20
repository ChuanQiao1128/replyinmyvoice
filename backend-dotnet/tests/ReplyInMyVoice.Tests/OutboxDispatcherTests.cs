using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task DispatchDueAsync_sends_due_message_and_marks_sent()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var attemptId = Guid.NewGuid();
        await SeedOutboxAsync(fixture, attemptId, DateTimeOffset.Parse("2026-05-20T00:00:00Z"));
        var publisher = new RecordingRewriteJobPublisher();
        var dispatcher = new OutboxDispatcherService(fixture.CreateContext, publisher);

        var dispatched = await dispatcher.DispatchDueAsync(
            DateTimeOffset.Parse("2026-05-20T00:00:01Z"),
            "test-worker",
            batchSize: 10,
            CancellationToken.None);

        dispatched.Should().Be(1);
        publisher.PublishedJobs.Should().ContainSingle(x => x.AttemptId == attemptId);
        await using var db = fixture.CreateContext();
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.SentAt.Should().NotBeNull();
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueAsync_reschedules_failed_publish_with_backoff()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedOutboxAsync(fixture, Guid.NewGuid(), DateTimeOffset.Parse("2026-05-20T00:00:00Z"));
        var dispatcher = new OutboxDispatcherService(
            fixture.CreateContext,
            new RecordingRewriteJobPublisher(fail: true));

        var dispatched = await dispatcher.DispatchDueAsync(
            DateTimeOffset.Parse("2026-05-20T00:00:01Z"),
            "test-worker",
            batchSize: 10,
            CancellationToken.None);

        dispatched.Should().Be(1);
        await using var db = fixture.CreateContext();
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(1);
        outbox.NextAttemptAt.Should().BeAfter(DateTimeOffset.Parse("2026-05-20T00:00:01Z"));
        outbox.LastError.Should().Contain("publish failed");
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueAsync_marks_failed_after_max_attempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-05-20T00:00:00Z"),
            attemptCount: 9,
            maxAttempts: 10);
        var dispatcher = new OutboxDispatcherService(
            fixture.CreateContext,
            new RecordingRewriteJobPublisher(fail: true));

        await dispatcher.DispatchDueAsync(
            DateTimeOffset.Parse("2026-05-20T00:00:01Z"),
            "test-worker",
            batchSize: 10,
            CancellationToken.None);

        await using var db = fixture.CreateContext();
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Failed);
        outbox.AttemptCount.Should().Be(10);
        outbox.LastError.Should().Contain("publish failed");
    }

    [Fact]
    public async Task DispatchDueAsync_skips_locked_message_until_lock_expires()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var attemptId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await SeedOutboxAsync(
            fixture,
            attemptId,
            now,
            lockedUntil: now.AddSeconds(30),
            lockedBy: "other-worker");
        var publisher = new RecordingRewriteJobPublisher();
        var dispatcher = new OutboxDispatcherService(fixture.CreateContext, publisher);

        var skipped = await dispatcher.DispatchDueAsync(now.AddSeconds(1), "test-worker", 10, CancellationToken.None);
        var reclaimed = await dispatcher.DispatchDueAsync(now.AddSeconds(31), "test-worker", 10, CancellationToken.None);

        skipped.Should().Be(0);
        reclaimed.Should().Be(1);
        publisher.PublishedJobs.Should().ContainSingle(x => x.AttemptId == attemptId);
        await using var db = fixture.CreateContext();
        (await db.OutboxMessages.SingleAsync()).Status.Should().Be(OutboxMessageStatus.Sent);
    }

    private static async Task SeedOutboxAsync(
        DbFixture fixture,
        Guid attemptId,
        DateTimeOffset now,
        int attemptCount = 0,
        int maxAttempts = 10,
        DateTimeOffset? lockedUntil = null,
        string? lockedBy = null)
    {
        await using var db = fixture.CreateContext();
        db.OutboxMessages.Add(new OutboxMessage
        {
            MessageType = "RewriteJobCreated",
            PayloadJson = $$"""{"attemptId":"{{attemptId}}"}""",
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            LockedUntil = lockedUntil,
            LockedBy = lockedBy,
            CorrelationId = attemptId.ToString(),
        });
        await db.SaveChangesAsync();
    }
}

internal sealed class RecordingRewriteJobPublisher(bool fail = false) : IRewriteJobPublisher
{
    private readonly List<RewriteJob> _publishedJobs = [];

    public IReadOnlyList<RewriteJob> PublishedJobs => _publishedJobs;

    public Task PublishAsync(RewriteJob job, CancellationToken cancellationToken)
    {
        if (fail)
        {
            throw new InvalidOperationException("publish failed");
        }

        _publishedJobs.Add(job);
        return Task.CompletedTask;
    }
}
