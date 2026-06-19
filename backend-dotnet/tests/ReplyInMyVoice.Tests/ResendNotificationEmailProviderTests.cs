using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.StripeEvent;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Notifications;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Tests;

public sealed class ResendNotificationEmailProviderTests
{
    [Fact]
    public async Task ResendNotificationEmailProvider_TransientError_throws_and_outbox_retries()
    {
        var directHandler = new RecordingResendHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var directProvider = CreateProvider(directHandler);
        var directEmail = CreateEmail(Guid.Parse("9916e6c2-895f-4bf2-a6f5-2f07df8dd089"));

        await directProvider
            .Invoking(x => x.SendAsync(directEmail, CancellationToken.None))
            .Should()
            .ThrowAsync<HttpRequestException>();

        var networkProvider = CreateProvider(
            new RecordingResendHandler(_ => throw new HttpRequestException("network unavailable")));
        await networkProvider
            .Invoking(x => x.SendAsync(directEmail, CancellationToken.None))
            .Should()
            .ThrowAsync<HttpRequestException>();

        var timeoutProvider = CreateProvider(
            new RecordingResendHandler(_ => throw new TaskCanceledException("request timed out")));
        await timeoutProvider
            .Invoking(x => x.SendAsync(directEmail, CancellationToken.None))
            .Should()
            .ThrowAsync<TaskCanceledException>();

        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-19T00:00:00Z");
        var dispatchNow = now.AddSeconds(1);
        var messageId = await SeedNotificationOutboxAsync(fixture, user.Id, now);
        var outboxHandler = new RecordingResendHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await using var handlerDb = fixture.CreateContext();
        var dispatcher = CreateDispatchHandler(handlerDb, CreateProvider(outboxHandler));

        var dispatched = await dispatcher.HandleAsync(
            new DispatchDueOutboxCommand(dispatchNow, "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        outboxHandler.IdempotencyKeys.Should().Equal(messageId.ToString("D"));

        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(1);
        outbox.NextAttemptAt.Should().BeAfter(dispatchNow);
        outbox.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResendNotificationEmailProvider_PermanentError_returns_skipped_no_retry()
    {
        var directHandler = new RecordingResendHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));
        var directProvider = CreateProvider(directHandler);
        var directEmail = CreateEmail(Guid.Parse("97f8383c-a299-46f5-a35b-e678249f4c52"));

        var result = await directProvider.SendAsync(directEmail, CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.Provider.Should().Be("resend");
        result.Reason.Should().Be("provider_permanent_error");

        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-19T00:05:00Z");
        var dispatchNow = now.AddSeconds(1);
        var messageId = await SeedNotificationOutboxAsync(fixture, user.Id, now);
        var outboxHandler = new RecordingResendHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));

        await using var handlerDb = fixture.CreateContext();
        var dispatcher = CreateDispatchHandler(handlerDb, CreateProvider(outboxHandler));

        var dispatched = await dispatcher.HandleAsync(
            new DispatchDueOutboxCommand(dispatchNow, "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        outboxHandler.IdempotencyKeys.Should().Equal(messageId.ToString("D"));

        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.AttemptCount.Should().Be(0);
        outbox.NextAttemptAt.Should().Be(now);
        outbox.LastError.Should().BeNull();
    }

    [Fact]
    public async Task ResendNotificationEmailProvider_idempotency_key_header_sent_and_stable()
    {
        var outboxMessageId = Guid.Parse("83f2d850-ecad-4739-89fa-065bca9e8908");
        var handler = new RecordingResendHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"resend_email_123"}"""),
        });
        var provider = CreateProvider(handler);

        var first = await provider.SendAsync(CreateEmail(outboxMessageId), CancellationToken.None);
        var second = await provider.SendAsync(CreateEmail(outboxMessageId), CancellationToken.None);

        first.Sent.Should().BeTrue();
        second.Sent.Should().BeTrue();
        first.OperationId.Should().Be("resend_email_123");
        handler.IdempotencyKeys.Should().Equal(outboxMessageId.ToString("D"), outboxMessageId.ToString("D"));

        var fallbackHandler = new RecordingResendHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"resend_email_456"}"""),
        });
        var fallbackProvider = CreateProvider(fallbackHandler);

        await fallbackProvider.SendAsync(CreateEmail(), CancellationToken.None);

        var fallbackKey = fallbackHandler.IdempotencyKeys.Should().ContainSingle().Subject;
        fallbackKey.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(fallbackKey, out _).Should().BeTrue();
    }

    private static ResendNotificationEmailProvider CreateProvider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            "resend-test-key",
            "Reply In My Voice <notifications@replyinmyvoice.test>",
            null,
            NullLogger<ResendNotificationEmailProvider>.Instance);

    private static NotificationEmail CreateEmail(Guid? outboxMessageId = null) =>
        new(
            "failed-payment",
            new NotificationRecipient("customer@replyinmyvoice.test"),
            "Payment issue",
            "Please update your payment method.",
            "<p>Please update your payment method.</p>",
            outboxMessageId);

    private static async Task<Guid> SeedNotificationOutboxAsync(
        DbFixture fixture,
        Guid userId,
        DateTimeOffset now)
    {
        await using var db = fixture.CreateContext();
        var message = StripeNotificationOutboxMessageFactory.Create(
            StripeNotificationOutboxMessageTypes.PaymentFailed,
            userId,
            now,
            $"evt_{userId:N}");
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private static DispatchDueOutboxHandler CreateDispatchHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        INotificationEmailProvider provider)
    {
        var appUsers = new AppUserRepository(db);
        var notificationService = new NotificationService(
            provider,
            NullLogger<NotificationService>.Instance);
        var notifier = new StripeEventNotifier(notificationService);

        return new DispatchDueOutboxHandler(
            new OutboxMessageRepository(db),
            [new PaymentFailedNotificationOutboxMessageHandler(appUsers, notifier)],
            new NoOpOutboxDispatchObserver(),
            new UnitOfWork(db));
    }

    private sealed class RecordingResendHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<string?> IdempotencyKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            IdempotencyKeys.Add(
                request.Headers.TryGetValues("Idempotency-Key", out var values)
                    ? values.SingleOrDefault()
                    : null);

            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class NoOpOutboxDispatchObserver : IOutboxDispatchObserver
    {
        public Task OnTerminalFailureAsync(
            OutboxMessage message,
            string error,
            CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
