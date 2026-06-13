using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Repositories;
using ReplyInMyVoice.Tests.TestDoubles;

namespace ReplyInMyVoice.Tests.Application;

public sealed class WebhookOutboxUseCaseTests
{
    [Fact]
    public async Task DispatchDueWebhooksAsync_sends_success_payload_and_marks_delivered()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var signingValue = new string('b', 64);
        var now = DateTimeOffset.Parse("2026-06-06T02:00:00Z");
        var attemptId = await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            signingValue,
            RewriteAttemptStatus.Succeeded,
            "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24},\"changeSummary\":[],\"riskNotes\":[]}");
        var sender = new RecordingWebhookDeliverySender(new WebhookSendResult(200));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, sender);

        var dispatched = await handler.HandleAsync(
            new DispatchDueWebhooksCommand(now, "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        var sent = sender.Requests.Should().ContainSingle().Subject;
        sent.Url.Should().Be("https://93.184.216.34/rewrite");

        using var body = JsonDocument.Parse(sent.RawBody);
        body.RootElement.GetProperty("id").GetGuid().Should().Be(attemptId);
        body.RootElement.GetProperty("status").GetString().Should().Be("succeeded");
        body.RootElement.GetProperty("rewrittenText").GetString().Should().Be("Hi Sam, the report is ready.");
        var signal = body.RootElement.GetProperty("signal");
        signal.GetProperty("draft").GetDecimal().Should().Be(78);
        signal.GetProperty("rewrite").GetDecimal().Should().Be(24);

        sent.Timestamp.Should().Be(now.ToUnixTimeSeconds().ToString());
        sent.DeliveryId.Should().NotBeEmpty();
        sent.EventId.Should().Be(attemptId);
        sent.Signature.Should().Be(ComputeSignature(signingValue, sent.Timestamp, sent.RawBody));

        await using var verifyDb = fixture.CreateContext();
        var delivery = await verifyDb.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        delivery.AttemptCount.Should().Be(1);
        delivery.DeliveredAt.Should().Be(now);
        delivery.LastError.Should().BeNull();
        delivery.LockedBy.Should().BeNull();
        delivery.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueWebhooksAsync_reschedules_failed_attempt_with_backoff()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-06T02:00:00Z");
        await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            new string('c', 64),
            RewriteAttemptStatus.Failed,
            resultJson: null,
            errorCode: "provider_failed");
        var sender = new RecordingWebhookDeliverySender(new WebhookSendResult(500));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, sender);

        var dispatched = await handler.HandleAsync(
            new DispatchDueWebhooksCommand(now, "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        var delivery = await verifyDb.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.AttemptCount.Should().Be(1);
        delivery.NextAttemptAt.Should().Be(now.AddSeconds(2));
        delivery.LastError.Should().Contain("HTTP 500");
        delivery.LockedBy.Should().BeNull();
        delivery.LockedUntil.Should().BeNull();
        delivery.DeliveredAt.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueWebhooksAsync_marks_failed_after_max_attempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-06T02:00:00Z");
        await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            new string('c', 64),
            RewriteAttemptStatus.Failed,
            resultJson: null,
            errorCode: "provider_failed",
            attemptCount: 4,
            maxAttempts: 5);
        var sender = new RecordingWebhookDeliverySender(new WebhookSendResult(500));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, sender);

        var dispatched = await handler.HandleAsync(
            new DispatchDueWebhooksCommand(now, "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        var delivery = await verifyDb.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(5);
        delivery.LastError.Should().Contain("HTTP 500");
        delivery.LockedBy.Should().BeNull();
        delivery.LockedUntil.Should().BeNull();
        delivery.DeliveredAt.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueWebhooksAsync_terminalizes_delivery_when_required_data_is_missing()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-06T02:00:00Z");
        await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            new string('d', 64),
            RewriteAttemptStatus.Succeeded,
            "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24},\"changeSummary\":[],\"riskNotes\":[]}",
            attemptCount: 4,
            maxAttempts: 5,
            includeWebhookSecret: false);
        var sender = new RecordingWebhookDeliverySender(new WebhookSendResult(200));
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateWebhookHandler(handlerDb, sender);

        var dispatched = await handler.HandleAsync(
            new DispatchDueWebhooksCommand(now, "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        sender.Requests.Should().BeEmpty();
        await using var verifyDb = fixture.CreateContext();
        var delivery = await verifyDb.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(5);
        delivery.LastError.Should().Contain("missing required data");
        delivery.LockedBy.Should().BeNull();
        delivery.LockedUntil.Should().BeNull();
        delivery.DeliveredAt.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueWebhooksAsync_does_not_double_dispatch_concurrently_claimed_delivery()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var now = DateTimeOffset.Parse("2026-06-06T02:00:00Z");
        await SeedWebhookDeliveryAsync(
            fixture,
            user.Id,
            new string('d', 64),
            RewriteAttemptStatus.Succeeded,
            "{\"rewrittenText\":\"Hi Sam, the report is ready.\",\"naturalness\":{\"draftAiLikePercent\":78,\"rewriteAiLikePercent\":24},\"changeSummary\":[],\"riskNotes\":[]}");
        var sender = new BlockingWebhookDeliverySender();

        await using var firstDb = fixture.CreateContext();
        await using var secondDb = fixture.CreateContext();
        var firstHandler = CreateWebhookHandler(firstDb, sender);
        var secondHandler = CreateWebhookHandler(secondDb, sender);

        var firstDispatch = firstHandler.HandleAsync(
            new DispatchDueWebhooksCommand(now, "first-worker", BatchSize: 10),
            CancellationToken.None);
        await sender.WaitForFirstSendAsync();

        var secondDispatch = await secondHandler.HandleAsync(
            new DispatchDueWebhooksCommand(now.AddSeconds(30), "second-worker", BatchSize: 10),
            CancellationToken.None);

        secondDispatch.Should().Be(0);
        sender.Requests.Should().ContainSingle();
        await using (var verifyLockedDb = fixture.CreateContext())
        {
            var delivery = await verifyLockedDb.WebhookDeliveries.SingleAsync();
            delivery.Status.Should().Be(WebhookDeliveryStatus.InProgress);
            delivery.LockedBy.Should().Be("first-worker");
            delivery.LockedUntil.Should().Be(now.AddSeconds(45));
        }

        sender.ReleaseFirstSend();
        (await firstDispatch).Should().Be(1);
    }

    [Fact]
    public async Task DispatchDueOutboxAsync_dispatches_due_message_and_marks_sent()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var attemptId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await SeedOutboxAsync(fixture, attemptId, now);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated");
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, outboxHandler);

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        var handled = outboxHandler.Messages.Should().ContainSingle().Subject;
        handled.MessageType.Should().Be("RewriteJobCreated");
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
    public async Task DispatchDueOutboxAsync_reschedules_failed_attempt_with_backoff()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await SeedOutboxAsync(fixture, Guid.NewGuid(), now);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated", fail: true);
        var observer = new RecordingOutboxDispatchObserver();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, observer, outboxHandler);

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(1);
        outbox.NextAttemptAt.Should().Be(now.AddSeconds(3));
        outbox.LastError.Should().Contain("handler failed");
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
        outbox.SentAt.Should().BeNull();
        observer.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchDueOutboxAsync_marks_failed_after_max_attempts()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            now,
            attemptCount: 9,
            maxAttempts: 10);
        var outboxHandler = new RecordingOutboxMessageHandler("RewriteJobCreated", fail: true);
        var observer = new RecordingOutboxDispatchObserver();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, observer, outboxHandler);

        var dispatched = await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        dispatched.Should().Be(1);
        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Failed);
        outbox.AttemptCount.Should().Be(10);
        outbox.LastError.Should().Contain("handler failed");
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
        var failure = observer.Failures.Should().ContainSingle().Subject;
        failure.MessageId.Should().Be(outbox.Id);
        failure.MessageType.Should().Be("RewriteJobCreated");
        failure.Error.Should().Contain("handler failed");
    }

    [Fact]
    public async Task DispatchDueOutboxAsync_does_not_double_dispatch_concurrently_claimed_message()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await SeedOutboxAsync(fixture, Guid.NewGuid(), now);
        var outboxHandler = new BlockingOutboxMessageHandler("RewriteJobCreated");

        await using var firstDb = fixture.CreateContext();
        await using var secondDb = fixture.CreateContext();
        var firstHandler = CreateOutboxHandler(firstDb, outboxHandler);
        var secondHandler = CreateOutboxHandler(secondDb, outboxHandler);

        var firstDispatch = firstHandler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "first-worker", BatchSize: 10),
            CancellationToken.None);
        await outboxHandler.WaitForFirstHandleAsync();

        var secondDispatch = await secondHandler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(20), "second-worker", BatchSize: 10),
            CancellationToken.None);

        secondDispatch.Should().Be(0);
        outboxHandler.Messages.Should().ContainSingle();
        await using (var verifyLockedDb = fixture.CreateContext())
        {
            var outbox = await verifyLockedDb.OutboxMessages.SingleAsync();
            outbox.Status.Should().Be(OutboxMessageStatus.Processing);
            outbox.LockedBy.Should().Be("first-worker");
            outbox.LockedUntil.Should().Be(now.AddSeconds(31));
        }

        outboxHandler.ReleaseFirstHandle();
        (await firstDispatch).Should().Be(1);
    }

    [Fact]
    public async Task DispatchDueOutboxAsync_records_backlog_age_of_oldest_unsent_message()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            now.AddSeconds(-120),
            messageType: "UnknownMessageType");
        var metrics = new RecordingBusinessMetrics();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, metrics: metrics);

        await handler.HandleAsync(
            new DispatchDueOutboxCommand(now, "test-worker", BatchSize: 10),
            CancellationToken.None);

        metrics.Records.Should().Contain(record =>
            record.Name == BusinessMetricNames.OutboxBacklogAgeSeconds &&
            record.Value >= 120);
    }

    [Fact]
    public async Task DispatchDueOutboxAsync_records_zero_backlog_age_when_outbox_is_empty()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        var metrics = new RecordingBusinessMetrics();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, metrics: metrics);

        await handler.HandleAsync(
            new DispatchDueOutboxCommand(now, "test-worker", BatchSize: 10),
            CancellationToken.None);

        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.OutboxBacklogAgeSeconds &&
            record.Value == 0 &&
            record.DimensionName == null &&
            record.DimensionValue == null);
    }

    [Fact]
    public async Task DispatchDueOutboxAsync_records_failed_metric_with_message_type_dimension()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        await SeedOutboxAsync(
            fixture,
            Guid.NewGuid(),
            now,
            messageType: "UnknownMessageType");
        var metrics = new RecordingBusinessMetrics();
        await using var handlerDb = fixture.CreateContext();
        var handler = CreateOutboxHandler(handlerDb, metrics: metrics);

        await handler.HandleAsync(
            new DispatchDueOutboxCommand(now.AddSeconds(1), "test-worker", BatchSize: 10),
            CancellationToken.None);

        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.OutboxFailedTotal &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.MessageType &&
            record.DimensionValue == "UnknownMessageType");
    }

    private static DispatchDueWebhooksHandler CreateWebhookHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IWebhookDeliverySender sender) =>
        new(
            new WebhookDeliveryRepository(db),
            sender,
            new UnitOfWork(db));

    private static DispatchDueOutboxHandler CreateOutboxHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        params IOutboxMessageHandler[] handlers) =>
        CreateOutboxHandler(db, new RecordingOutboxDispatchObserver(), null, handlers);

    private static DispatchDueOutboxHandler CreateOutboxHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IOutboxDispatchObserver observer,
        params IOutboxMessageHandler[] handlers) =>
        CreateOutboxHandler(db, observer, null, handlers);

    private static DispatchDueOutboxHandler CreateOutboxHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IBusinessMetrics metrics,
        params IOutboxMessageHandler[] handlers) =>
        CreateOutboxHandler(db, new RecordingOutboxDispatchObserver(), metrics, handlers);

    private static DispatchDueOutboxHandler CreateOutboxHandler(
        ReplyInMyVoice.Infrastructure.Data.AppDbContext db,
        IOutboxDispatchObserver observer,
        IBusinessMetrics? metrics,
        params IOutboxMessageHandler[] handlers) =>
        new(
            new OutboxMessageRepository(db),
            handlers,
            observer,
            new UnitOfWork(db),
            metrics);

    private static async Task<Guid> SeedWebhookDeliveryAsync(
        DbFixture fixture,
        Guid userId,
        string signingValue,
        RewriteAttemptStatus status,
        string? resultJson,
        string? errorCode = null,
        int attemptCount = 0,
        int maxAttempts = 5,
        bool includeWebhookSecret = true)
    {
        await using var db = fixture.CreateContext();
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = "Server key",
            KeyHash = Guid.NewGuid().ToString("N"),
            Last4 = "abcd",
            WebhookUrl = "https://93.184.216.34/rewrite",
            WebhookSecret = includeWebhookSecret ? signingValue : null,
            CreatedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
        };
        var attempt = new RewriteAttempt
        {
            UserId = userId,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{\"roughDraftReply\":\"Thanks for your message. I will reply soon.\",\"tone\":\"warm\"}",
            Status = status,
            ResultJson = resultJson,
            ErrorCode = errorCode,
            CreatedAt = DateTimeOffset.Parse("2026-06-06T01:58:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
            ExpiresAt = DateTimeOffset.Parse("2026-06-06T02:08:00Z"),
        };
        var delivery = new WebhookDelivery
        {
            ApiKey = apiKey,
            RewriteAttempt = attempt,
            Url = apiKey.WebhookUrl,
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            CreatedAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
            NextAttemptAt = DateTimeOffset.Parse("2026-06-06T01:59:00Z"),
        };
        db.ApiKeys.Add(apiKey);
        db.RewriteAttempts.Add(attempt);
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    private static async Task SeedOutboxAsync(
        DbFixture fixture,
        Guid attemptId,
        DateTimeOffset now,
        int attemptCount = 0,
        int maxAttempts = 10,
        string messageType = "RewriteJobCreated")
    {
        await using var db = fixture.CreateContext();
        db.OutboxMessages.Add(new OutboxMessage
        {
            MessageType = messageType,
            PayloadJson = $$"""{"attemptId":"{{attemptId}}"}""",
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            CorrelationId = attemptId.ToString(),
        });
        await db.SaveChangesAsync();
    }

    private static string ComputeSignature(string signingValue, string timestamp, string rawBody)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingValue),
            Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

internal sealed class RecordingWebhookDeliverySender(params WebhookSendResult[] results) : IWebhookDeliverySender
{
    private readonly Queue<WebhookSendResult> _results = new(results);
    private readonly List<WebhookSendRequest> _requests = [];

    public IReadOnlyList<WebhookSendRequest> Requests => _requests;

    public Task<WebhookSendResult> SendAsync(
        WebhookSendRequest request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(_results.Count == 0 ? new WebhookSendResult(200) : _results.Dequeue());
    }
}

internal sealed class BlockingWebhookDeliverySender : IWebhookDeliverySender
{
    private readonly TaskCompletionSource _firstSendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstSend = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<WebhookSendRequest> _requests = [];
    private int _sendCount;

    public IReadOnlyList<WebhookSendRequest> Requests => _requests;

    public Task WaitForFirstSendAsync() => _firstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void ReleaseFirstSend() => _releaseFirstSend.TrySetResult();

    public async Task<WebhookSendResult> SendAsync(
        WebhookSendRequest request,
        CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add(request);
        }

        if (Interlocked.Increment(ref _sendCount) == 1)
        {
            _firstSendStarted.TrySetResult();
            await _releaseFirstSend.Task.WaitAsync(cancellationToken);
        }

        return new WebhookSendResult(200);
    }
}

internal class RecordingOutboxMessageHandler(
    string messageType,
    bool fail = false) : IOutboxMessageHandler
{
    private readonly List<OutboxMessage> _messages = [];

    public string MessageType { get; } = messageType;

    public IReadOnlyList<OutboxMessage> Messages => _messages;

    public virtual Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
    {
        if (fail)
        {
            throw new InvalidOperationException("handler failed");
        }

        _messages.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class BlockingOutboxMessageHandler(string messageType) : RecordingOutboxMessageHandler(messageType)
{
    private readonly TaskCompletionSource _firstHandleStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstHandle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<OutboxMessage> _messages = [];
    private int _handleCount;

    public new IReadOnlyList<OutboxMessage> Messages => _messages;

    public Task WaitForFirstHandleAsync() => _firstHandleStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void ReleaseFirstHandle() => _releaseFirstHandle.TrySetResult();

    public override async Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
    {
        lock (_messages)
        {
            _messages.Add(message);
        }

        if (Interlocked.Increment(ref _handleCount) == 1)
        {
            _firstHandleStarted.TrySetResult();
            await _releaseFirstHandle.Task.WaitAsync(ct);
        }
    }
}

internal sealed record OutboxDispatchFailure(Guid MessageId, string MessageType, string Error);

internal sealed class RecordingOutboxDispatchObserver : IOutboxDispatchObserver
{
    private readonly List<OutboxDispatchFailure> _failures = [];

    public IReadOnlyList<OutboxDispatchFailure> Failures => _failures;

    public Task OnTerminalFailureAsync(
        OutboxMessage message,
        string error,
        CancellationToken ct = default)
    {
        _failures.Add(new OutboxDispatchFailure(message.Id, message.MessageType, error));
        return Task.CompletedTask;
    }
}
