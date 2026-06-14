using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.Rewrite;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Queueing;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;
using ReplyInMyVoice.Tests;

namespace ReplyInMyVoice.Tests.Application;

public sealed class OutboxFastPathDispatchTests
{
    [Fact]
    public async Task TryDispatchOneAsync_dispatches_pending_message_and_marks_sent()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var attemptId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var messageId = await SeedOutboxAsync(fixture, attemptId, now);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, outboxHandler);

        var dispatched = await handler.TryDispatchOneAsync(
            new DispatchOutboxMessageCommand(messageId, now.AddSeconds(1), "test-fastpath"),
            CancellationToken.None);

        dispatched.Should().BeTrue();
        var handled = outboxHandler.Messages.Should().ContainSingle().Subject;
        handled.Id.Should().Be(messageId);
        handled.PayloadJson.Should().Contain(attemptId.ToString());

        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.SentAt.Should().Be(now.AddSeconds(1));
        outbox.LastError.Should().BeNull();
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task TryDispatchOneAsync_skips_message_locked_by_timer()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var messageId = await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            now,
            status: OutboxMessageStatus.Processing,
            lockedBy: "timer",
            lockedUntil: now.AddSeconds(25),
            lastAttemptAt: now);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, outboxHandler);

        var dispatched = await handler.TryDispatchOneAsync(
            new DispatchOutboxMessageCommand(messageId, now.AddSeconds(1), "test-fastpath"),
            CancellationToken.None);

        dispatched.Should().BeFalse();
        outboxHandler.Messages.Should().BeEmpty();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Processing);
        outbox.LockedBy.Should().Be("timer");
        outbox.LockedUntil.Should().Be(now.AddSeconds(25));
        outbox.LastAttemptAt.Should().Be(now);
    }

    [Fact]
    public async Task TryDispatchOneAsync_skips_already_sent_message()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var sentAt = now.AddSeconds(2);
        var messageId = await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            now,
            status: OutboxMessageStatus.Sent,
            sentAt: sentAt);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, outboxHandler);

        var dispatched = await handler.TryDispatchOneAsync(
            new DispatchOutboxMessageCommand(messageId, now.AddSeconds(3), "test-fastpath"),
            CancellationToken.None);

        dispatched.Should().BeFalse();
        outboxHandler.Messages.Should().BeEmpty();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.SentAt.Should().Be(sentAt);
    }

    [Fact]
    public async Task TryDispatchOneAsync_skips_message_not_yet_due()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var messageId = await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            now,
            nextAttemptAt: now.AddSeconds(60));
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, outboxHandler);

        var dispatched = await handler.TryDispatchOneAsync(
            new DispatchOutboxMessageCommand(messageId, now.AddSeconds(1), "test-fastpath"),
            CancellationToken.None);

        dispatched.Should().BeFalse();
        outboxHandler.Messages.Should().BeEmpty();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.NextAttemptAt.Should().Be(now.AddSeconds(60));
    }

    [Fact]
    public async Task TryDispatchOneAsync_marks_failed_attempt_with_backoff_when_publish_fails()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var dispatchNow = now.AddSeconds(1);
        var messageId = await SeedOutboxAsync(fixture, Guid.NewGuid(), now);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated", fail: true);
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, outboxHandler);

        var dispatched = await handler.TryDispatchOneAsync(
            new DispatchOutboxMessageCommand(messageId, dispatchNow, "test-fastpath"),
            CancellationToken.None);

        dispatched.Should().BeFalse();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(1);
        outbox.NextAttemptAt.Should().Be(dispatchNow.AddSeconds(2));
        outbox.LastError.Should().Contain("handler failed");
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task TryDispatchOneAsync_unknown_message_type_marks_failed_attempt()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var dispatchNow = now.AddSeconds(1);
        var messageId = await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            now,
            messageType: "UnknownMessage",
            payloadJson: "{}");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, new RecordingOutboxMessageHandler("RewriteJobCreated"));

        var dispatched = await handler.TryDispatchOneAsync(
            new DispatchOutboxMessageCommand(messageId, dispatchNow, "test-fastpath"),
            CancellationToken.None);

        dispatched.Should().BeFalse();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(1);
        outbox.NextAttemptAt.Should().Be(dispatchNow.AddSeconds(2));
        outbox.LastError.Should().Contain("Unsupported outbox message type");
    }

    [Fact]
    public async Task Timer_dispatch_after_fast_path_does_not_redispatch()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var messageId = await SeedOutboxAsync(fixture, Guid.NewGuid(), now);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, outboxHandler);

        var fastPathDispatched = await handler.TryDispatchOneAsync(
            new DispatchOutboxMessageCommand(messageId, now.AddSeconds(1), "test-fastpath"),
            CancellationToken.None);
        var timerDispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(20), "timer", BatchSize: 10),
            CancellationToken.None);

        fastPathDispatched.Should().BeTrue();
        timerDispatched.Should().Be(0);
        outboxHandler.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateRewriteAttempt_triggers_fast_path_dispatch_for_new_attempt()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var publisher = new InMemoryRewriteJobPublisher();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRewriteAttemptHandler(handlerDb, CreateFastPathDispatcher(handlerDb, publisher));

        var result = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-fastpath",
            Request("Please send an update soon."),
            "free:lifetime",
            QuotaLimit: 3,
            Now: DateTimeOffset.Parse("2026-06-13T00:00:00Z"),
            ApiKeyId: null));

        result.Kind.Should().Be(ApplicationResultKind.Created);
        publisher.PublishedJobs.Select(x => x.AttemptId).Should().Equal(result.Value!.AttemptId);
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.SentAt.Should().NotBeNull();
        outbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task CreateRewriteAttempt_idempotent_replay_does_not_redispatch()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var publisher = new InMemoryRewriteJobPublisher();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateRewriteAttemptHandler(handlerDb, CreateFastPathDispatcher(handlerDb, publisher));
        var request = Request("Please send an update soon.");

        var first = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-fastpath-replay",
            request,
            "free:lifetime",
            QuotaLimit: 3,
            Now: DateTimeOffset.Parse("2026-06-13T00:00:00Z"),
            ApiKeyId: null));
        var second = await handler.HandleAsync(new CreateRewriteAttemptCommand(
            user.Id,
            "idem-fastpath-replay",
            request,
            "free:lifetime",
            QuotaLimit: 3,
            Now: DateTimeOffset.Parse("2026-06-13T00:00:01Z"),
            ApiKeyId: null));

        first.Kind.Should().Be(ApplicationResultKind.Created);
        second.Kind.Should().Be(ApplicationResultKind.Existing);
        publisher.PublishedJobs.Select(x => x.AttemptId).Should().Equal(first.Value!.AttemptId);
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
    }

    [Fact]
    public async Task OutboxFastPathDispatcher_is_noop_when_disabled()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-13T00:00:00Z");
        var messageId = await SeedOutboxAsync(fixture, Guid.NewGuid(), now);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated");
        await using var handlerDb = fixture.CreateContext();
        var dispatcher = new OutboxFastPathDispatcher(
            CreateOutboxHandler(handlerDb, outboxHandler),
            new OutboxFastPathOptions(Enabled: false, TimeSpan.FromSeconds(5)),
            NullLogger<OutboxFastPathDispatcher>.Instance);

        await dispatcher.TryDispatchAsync(messageId, CancellationToken.None);

        outboxHandler.Messages.Should().BeEmpty();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.SentAt.Should().BeNull();
    }

    [Fact]
    public async Task OutboxFastPathDispatcher_swallows_dispatch_exceptions()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await using var handlerDb = fixture.CreateContext();
        await handlerDb.Database.ExecuteSqlRawAsync("DROP TABLE \"OutboxMessages\"");
        var dispatcher = new OutboxFastPathDispatcher(
            CreateOutboxHandler(handlerDb, new RecordingOutboxMessageHandler("RewriteJobCreated")),
            new OutboxFastPathOptions(Enabled: true, TimeSpan.FromSeconds(5)),
            NullLogger<OutboxFastPathDispatcher>.Instance);

        var act = () => dispatcher.TryDispatchAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static DispatchDueOutboxHandler CreateOutboxHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        params IOutboxMessageHandler[] handlers) =>
        new(
            new OutboxMessageRepository(db),
            handlers,
            new RecordingOutboxDispatchObserver(),
            new UnitOfWork(db));

    private static OutboxFastPathDispatcher CreateFastPathDispatcher(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IRewriteJobPublisher publisher)
    {
        var dispatchHandler = CreateOutboxHandler(
            db,
            new RewriteJobCreatedOutboxMessageHandler(publisher));
        return new OutboxFastPathDispatcher(
            dispatchHandler,
            new OutboxFastPathOptions(Enabled: true, TimeSpan.FromSeconds(5)),
            NullLogger<OutboxFastPathDispatcher>.Instance);
    }

    private static CreateRewriteAttemptHandler CreateRewriteAttemptHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IOutboxFastPathDispatcher fastPathDispatcher) =>
        new(
            new AppUserRepository(db),
            new UsagePeriodRepository(db),
            new RewriteAttemptRepository(db),
            new UsageReservationRepository(db),
            new RewriteCreditRepository(db),
            new OutboxMessageRepository(db),
            new UnitOfWork(db),
            fastPathDispatcher);

    private static async Task<Guid> SeedOutboxAsync(
        DbFixture fixture,
        Guid attemptId,
        DateTimeOffset now,
        OutboxMessageStatus status = OutboxMessageStatus.Pending,
        DateTimeOffset? nextAttemptAt = null,
        string? lockedBy = null,
        DateTimeOffset? lockedUntil = null,
        DateTimeOffset? lastAttemptAt = null,
        DateTimeOffset? sentAt = null,
        string messageType = "RewriteJobCreated",
        string? payloadJson = null)
    {
        await using var db = fixture.CreateContext();
        var message = new OutboxMessage
        {
            MessageType = messageType,
            PayloadJson = payloadJson ?? $$"""{"attemptId":"{{attemptId}}"}""",
            Status = status,
            CreatedAt = now,
            NextAttemptAt = nextAttemptAt ?? now,
            AttemptCount = 0,
            MaxAttempts = 10,
            LockedBy = lockedBy,
            LockedUntil = lockedUntil,
            LastAttemptAt = lastAttemptAt,
            SentAt = sentAt,
            CorrelationId = attemptId.ToString(),
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private static RewriteRequest Request(string roughDraftReply) =>
        new(
            MessageToReplyTo: "Can you send an update today?",
            RoughDraftReply: roughDraftReply,
            Audience: "client",
            Purpose: "reply",
            WhatHappened: "The update is ready.",
            FactsToPreserve: "No dates changed.",
            Tone: "warm");
}
