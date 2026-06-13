using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests.Application;

public sealed class StripeNotificationOutboxHandlerTests
{
    public static TheoryData<string, string> NotificationCases => new()
    {
        { StripeNotificationOutboxMessageTypes.PaymentFailed, "failed-payment" },
        { StripeNotificationOutboxMessageTypes.PaymentRecovered, "payment-recovered" },
        { StripeNotificationOutboxMessageTypes.SubscriptionPaused, "subscription-paused" },
        { StripeNotificationOutboxMessageTypes.PaymentGraceReminder, "payment-grace-reminder" },
    };

    [Theory]
    [MemberData(nameof(NotificationCases))]
    public async Task Dispatch_invokes_matching_notifier_method_and_marks_sent(
        string messageType,
        string expectedKind)
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:00:00Z");
        var messageId = await SeedNotificationOutboxAsync(fixture, messageType, user.Id, now);
        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDispatchHandler(handlerDb, notifier, new RecordingOutboxDispatchObserver());

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        notifier.Messages.Should().Equal((expectedKind, user.Id));
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.SentAt.Should().Be(now.AddSeconds(1));
        outbox.LastError.Should().BeNull();
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task Dispatch_for_missing_user_marks_sent_without_notifying()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:05:00Z");
        var messageId = await SeedNotificationOutboxAsync(
            fixture,
            StripeNotificationOutboxMessageTypes.PaymentFailed,
            Guid.NewGuid(),
            now);
        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDispatchHandler(handlerDb, notifier, new RecordingOutboxDispatchObserver());

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        notifier.Messages.Should().BeEmpty();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.LastError.Should().BeNull();
    }

    [Fact]
    public async Task Dispatch_retries_with_backoff_when_notifier_throws()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:10:00Z");
        var dispatchNow = now.AddSeconds(1);
        var messageId = await SeedNotificationOutboxAsync(
            fixture,
            StripeNotificationOutboxMessageTypes.PaymentFailed,
            user.Id,
            now);
        var observer = new RecordingOutboxDispatchObserver();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDispatchHandler(handlerDb, new ThrowingStripeEventNotifier(), observer);

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(dispatchNow, "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        observer.Failures.Should().BeEmpty();
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(1);
        outbox.NextAttemptAt.Should().Be(dispatchNow.AddSeconds(2));
        outbox.LastError.Should().Contain(ThrowingStripeEventNotifier.ErrorMessage);
    }

    [Fact]
    public async Task Dispatch_marks_failed_and_reports_terminal_failure_at_max_attempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:15:00Z");
        var messageId = await SeedNotificationOutboxAsync(
            fixture,
            StripeNotificationOutboxMessageTypes.SubscriptionPaused,
            user.Id,
            now,
            attemptCount: 9,
            maxAttempts: 10);
        var observer = new RecordingOutboxDispatchObserver();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDispatchHandler(handlerDb, new ThrowingStripeEventNotifier(), observer);

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Failed);
        outbox.AttemptCount.Should().Be(10);
        outbox.LastError.Should().Contain(ThrowingStripeEventNotifier.ErrorMessage);
        var failure = observer.Failures.Should().ContainSingle().Subject;
        failure.MessageId.Should().Be(messageId);
        failure.MessageType.Should().Be(StripeNotificationOutboxMessageTypes.SubscriptionPaused);
        failure.Error.Should().Contain(ThrowingStripeEventNotifier.ErrorMessage);
    }

    private static DispatchDueOutboxHandler CreateDispatchHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IStripeEventNotifier notifier,
        IOutboxDispatchObserver observer)
    {
        var appUsers = new AppUserRepository(db);
        return new DispatchDueOutboxHandler(
            new OutboxMessageRepository(db),
            [
                new PaymentFailedNotificationOutboxMessageHandler(appUsers, notifier),
                new PaymentRecoveredNotificationOutboxMessageHandler(appUsers, notifier),
                new SubscriptionPausedNotificationOutboxMessageHandler(appUsers, notifier),
                new PaymentGraceReminderNotificationOutboxMessageHandler(appUsers, notifier),
            ],
            observer,
            new UnitOfWork(db));
    }

    private static async Task<Guid> SeedNotificationOutboxAsync(
        DbFixture fixture,
        string messageType,
        Guid userId,
        DateTimeOffset now,
        int attemptCount = 0,
        int maxAttempts = 10)
    {
        await using var db = fixture.CreateContext();
        var message = StripeNotificationOutboxMessageFactory.Create(
            messageType,
            userId,
            now,
            userId.ToString());
        message.AttemptCount = attemptCount;
        message.MaxAttempts = maxAttempts;
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private sealed class RecordingStripeEventNotifier : IStripeEventNotifier
    {
        public List<(string Kind, Guid UserId)> Messages { get; } = [];

        public Task EnqueueFailedPaymentNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("failed-payment", user.Id));
            return Task.CompletedTask;
        }

        public Task EnqueueSubscriptionPausedNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("subscription-paused", user.Id));
            return Task.CompletedTask;
        }

        public Task EnqueuePaymentGraceReminderNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("payment-grace-reminder", user.Id));
            return Task.CompletedTask;
        }

        public Task EnqueuePaymentRecoveredNotificationAsync(AppUser user, CancellationToken ct = default)
        {
            Messages.Add(("payment-recovered", user.Id));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStripeEventNotifier : IStripeEventNotifier
    {
        public const string ErrorMessage = "notification send failed";

        public Task EnqueueFailedPaymentNotificationAsync(AppUser user, CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException(ErrorMessage));

        public Task EnqueueSubscriptionPausedNotificationAsync(AppUser user, CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException(ErrorMessage));

        public Task EnqueuePaymentGraceReminderNotificationAsync(AppUser user, CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException(ErrorMessage));

        public Task EnqueuePaymentRecoveredNotificationAsync(AppUser user, CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException(ErrorMessage));
    }
}
