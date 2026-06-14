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
        { StripeNotificationOutboxMessageTypes.PaymentActionRequired, "payment-action-required" },
        { StripeNotificationOutboxMessageTypes.CardExpiring, "card-expiring" },
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
    public async Task PaymentActionRequiredOutboxHandler_sends_notification_with_hosted_invoice_url()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:06:00Z");
        var message = StripeNotificationOutboxMessageFactory.CreatePaymentActionRequired(
            user.Id,
            "in_action_required",
            "https://billing.test/in_action_required",
            now,
            "evt_action_required");
        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = new StripePaymentActionRequiredOutboxMessageHandler(
            new AppUserRepository(handlerDb),
            notifier);

        await handler.HandleAsync(message);

        notifier.PaymentActionRequiredMessages.Should().Equal(
            (user.Id, "https://billing.test/in_action_required"));
    }

    [Fact]
    public async Task PaymentActionRequiredOutboxHandler_missing_user_completes_without_send()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:07:00Z");
        var message = StripeNotificationOutboxMessageFactory.CreatePaymentActionRequired(
            Guid.NewGuid(),
            "in_missing_user",
            "https://billing.test/in_missing_user",
            now,
            "evt_missing_user");
        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = new StripePaymentActionRequiredOutboxMessageHandler(
            new AppUserRepository(handlerDb),
            notifier);

        await handler.HandleAsync(message);

        notifier.Messages.Should().BeEmpty();
        notifier.PaymentActionRequiredMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task CardExpiringOutboxHandler_sends_card_expiry_notification()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:08:00Z");
        var message = StripeNotificationOutboxMessageFactory.CreateCardExpiring(
            user.Id,
            "visa",
            "4242",
            4,
            2027,
            now,
            "evt_card_expiring");
        var notifier = new RecordingStripeEventNotifier();
        await using var handlerDb = fixture.CreateContext();
        var handler = new StripeCardExpiringOutboxMessageHandler(
            new AppUserRepository(handlerDb),
            notifier);

        await handler.HandleAsync(message);

        notifier.CardExpiringMessages.Should().Equal((user.Id, "visa", "4242", 4, 2027));
    }

    [Fact]
    public async Task DispatchDueOutboxHandler_routes_new_stripe_message_types_and_marks_sent()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-09T01:09:00Z");
        Guid actionMessageId;
        Guid cardMessageId;
        Guid invalidMessageId;
        await using (var db = fixture.CreateContext())
        {
            var actionMessage = StripeNotificationOutboxMessageFactory.CreatePaymentActionRequired(
                user.Id,
                "in_dispatch_action",
                "https://billing.test/in_dispatch_action",
                now,
                "evt_dispatch_action");
            var cardMessage = StripeNotificationOutboxMessageFactory.CreateCardExpiring(
                user.Id,
                "visa",
                "4242",
                4,
                2027,
                now,
                "evt_dispatch_card");
            var invalidMessage = new OutboxMessage
            {
                MessageType = StripeNotificationOutboxMessageTypes.PaymentActionRequired,
                PayloadJson = """{"userId":""}""",
                Status = OutboxMessageStatus.Pending,
                CreatedAt = now,
                NextAttemptAt = now,
                MaxAttempts = 10,
                CorrelationId = "evt_dispatch_invalid",
            };
            db.OutboxMessages.AddRange(actionMessage, cardMessage, invalidMessage);
            await db.SaveChangesAsync();
            actionMessageId = actionMessage.Id;
            cardMessageId = cardMessage.Id;
            invalidMessageId = invalidMessage.Id;
        }

        var notifier = new RecordingStripeEventNotifier();
        var observer = new RecordingOutboxDispatchObserver();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateDispatchHandler(handlerDb, notifier, observer);

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(3);
        notifier.PaymentActionRequiredMessages.Should().Equal(
            (user.Id, "https://billing.test/in_dispatch_action"));
        notifier.CardExpiringMessages.Should().Equal((user.Id, "visa", "4242", 4, 2027));
        observer.Failures.Should().BeEmpty();

        await using var verifyDb = fixture.CreateContext();
        var storedActionMessage = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == actionMessageId);
        var storedCardMessage = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == cardMessageId);
        var storedInvalidMessage = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == invalidMessageId);
        storedActionMessage.Status.Should().Be(OutboxMessageStatus.Sent);
        storedCardMessage.Status.Should().Be(OutboxMessageStatus.Sent);
        storedInvalidMessage.Status.Should().Be(OutboxMessageStatus.Pending);
        storedInvalidMessage.AttemptCount.Should().Be(1);
        storedInvalidMessage.LastError.Should().NotBeNullOrWhiteSpace();
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
                new StripePaymentActionRequiredOutboxMessageHandler(appUsers, notifier),
                new StripeCardExpiringOutboxMessageHandler(appUsers, notifier),
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
        public List<(Guid UserId, string? HostedInvoiceUrl)> PaymentActionRequiredMessages { get; } = [];
        public List<(Guid UserId, string? Brand, string? Last4, int? ExpMonth, int? ExpYear)> CardExpiringMessages { get; } = [];

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

        public Task EnqueuePaymentActionRequiredNotificationAsync(
            AppUser user,
            string? hostedInvoiceUrl,
            CancellationToken ct = default)
        {
            Messages.Add(("payment-action-required", user.Id));
            PaymentActionRequiredMessages.Add((user.Id, hostedInvoiceUrl));
            return Task.CompletedTask;
        }

        public Task EnqueueCardExpiringNotificationAsync(
            AppUser user,
            string? brand,
            string? last4,
            int? expMonth,
            int? expYear,
            CancellationToken ct = default)
        {
            Messages.Add(("card-expiring", user.Id));
            CardExpiringMessages.Add((user.Id, brand, last4, expMonth, expYear));
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

        public Task EnqueuePaymentActionRequiredNotificationAsync(
            AppUser user,
            string? hostedInvoiceUrl,
            CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException(ErrorMessage));

        public Task EnqueueCardExpiringNotificationAsync(
            AppUser user,
            string? brand,
            string? last4,
            int? expMonth,
            int? expYear,
            CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException(ErrorMessage));
    }
}
