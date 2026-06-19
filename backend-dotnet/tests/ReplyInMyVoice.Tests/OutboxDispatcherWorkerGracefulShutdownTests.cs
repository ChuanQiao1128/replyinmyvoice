extern alias WorkerAssembly;

using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Repositories;
using WorkerAssembly::ReplyInMyVoice.Worker;

namespace ReplyInMyVoice.Tests;

public sealed class OutboxDispatcherWorkerGracefulShutdownTests
{
    [Fact]
    public async Task WhenCancelledMidIteration_CompletesInProgressMessageBeforeShuttingDown()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var messageId = await SeedOutboxAsync(fixture, DateTimeOffset.Parse("2026-06-19T01:00:00Z"));
        var messageHandler = new BlockingOutboxMessageHandler("RewriteJobCreated");
        await using var provider = BuildOutboxProvider(fixture, messageHandler);
        var logger = new RecordingLogger<OutboxDispatcherWorker>();
        var worker = new OutboxDispatcherWorker(
            ServiceBusConfigured(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            TimeSpan.FromMilliseconds(10));

        await worker.StartAsync(CancellationToken.None);
        await messageHandler.WaitForFirstHandleAsync();

        var stopTask = worker.StopAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        stopTask.IsCompleted.Should().BeFalse("shutdown should wait for the active outbox dispatch to finish");

        messageHandler.ReleaseFirstHandle();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Sent);
        outbox.SentAt.Should().NotBeNull();
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
        outbox.LastError.Should().BeNull();
    }

    [Fact]
    public async Task WhenCancelledMidIteration_RecordsFailedAttemptBeforeShuttingDown()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var messageId = await SeedOutboxAsync(fixture, DateTimeOffset.Parse("2026-06-19T01:02:00Z"));
        var messageHandler = new BlockingOutboxMessageHandler("RewriteJobCreated", failAfterRelease: true);
        await using var provider = BuildOutboxProvider(fixture, messageHandler);
        var logger = new RecordingLogger<OutboxDispatcherWorker>();
        var worker = new OutboxDispatcherWorker(
            ServiceBusConfigured(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            TimeSpan.FromMilliseconds(10));

        await worker.StartAsync(CancellationToken.None);
        await messageHandler.WaitForFirstHandleAsync();

        var stopTask = worker.StopAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        stopTask.IsCompleted.Should().BeFalse("shutdown should wait for the active failed dispatch to finish");

        messageHandler.ReleaseFirstHandle();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        await using var verifyDb = fixture.CreateContext();
        var outbox = await verifyDb.OutboxMessages.SingleAsync(x => x.Id == messageId);
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.AttemptCount.Should().Be(1);
        outbox.LastError.Should().Contain("handler failed");
        outbox.LockedBy.Should().BeNull();
        outbox.LockedUntil.Should().BeNull();
        outbox.SentAt.Should().BeNull();
    }

    [Fact]
    public async Task WhenStopAsync_TimeoutAfter60Seconds_ProceedsWithShutdown()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await SeedOutboxAsync(fixture, DateTimeOffset.Parse("2026-06-19T01:05:00Z"));
        var messageHandler = new NeverCompletingOutboxMessageHandler("RewriteJobCreated");
        await using var provider = BuildOutboxProvider(fixture, messageHandler);
        var logger = new RecordingLogger<OutboxDispatcherWorker>();
        var worker = new OutboxDispatcherWorker(
            ServiceBusConfigured(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            TimeSpan.FromMilliseconds(10));

        await worker.StartAsync(CancellationToken.None);
        await messageHandler.WaitForFirstHandleAsync();

        var stopwatch = Stopwatch.StartNew();
        await worker.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(55));
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(65));
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("60", StringComparison.Ordinal) &&
            entry.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
    }

    private static ServiceProvider BuildOutboxProvider(
        DbFixture fixture,
        IOutboxMessageHandler messageHandler)
    {
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => fixture.CreateContext());
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutboxDispatchObserver, NoopOutboxDispatchObserver>();
        services.AddSingleton(messageHandler);
        services.AddScoped<DispatchDueOutboxHandler>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration ServiceBusConfigured() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceBus"] =
                    "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            })
            .Build();

    private static async Task<Guid> SeedOutboxAsync(DbFixture fixture, DateTimeOffset now)
    {
        await using var db = fixture.CreateContext();
        var message = new OutboxMessage
        {
            MessageType = "RewriteJobCreated",
            PayloadJson = $$"""{"attemptId":"{{Guid.NewGuid()}}"}""",
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            CorrelationId = Guid.NewGuid().ToString("N"),
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }
}

public sealed class ExpiredReservationCleanupWorkerGracefulShutdownTests
{
    [Fact]
    public async Task WhenCancelledMidIteration_CompletesInProgressCleanupBeforeShuttingDown()
    {
        var reservations = new BlockingUsageReservationRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var services = new ServiceCollection();
        services.AddSingleton<IUsageReservationRepository>(reservations);
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        services.AddScoped<ReleaseExpiredReservationsHandler>();
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger<ExpiredReservationCleanupWorker>();
        var worker = new ExpiredReservationCleanupWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            TimeSpan.FromMilliseconds(10));

        await worker.StartAsync(CancellationToken.None);
        await reservations.WaitForFirstListAsync();

        var stopTask = worker.StopAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        stopTask.IsCompleted.Should().BeFalse("shutdown should wait for the active cleanup iteration to finish");

        reservations.ReleaseFirstList();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        reservations.TransitionCount.Should().Be(1);
        reservations.ReleaseCounterCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
        reservations.Reservation.Status.Should().Be(UsageReservationStatus.Expired);
        reservations.Reservation.RewriteAttempt!.Status.Should().Be(RewriteAttemptStatus.Expired);
        reservations.Reservation.RewriteAttempt.CompletedAt.Should().NotBeNull();
    }
}

internal sealed class BlockingOutboxMessageHandler(
    string messageType,
    bool failAfterRelease = false) : IOutboxMessageHandler
{
    private readonly TaskCompletionSource _firstHandleStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstHandle =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string MessageType { get; } = messageType;

    public Task WaitForFirstHandleAsync() => _firstHandleStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void ReleaseFirstHandle() => _releaseFirstHandle.TrySetResult();

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _firstHandleStarted.TrySetResult();
        await _releaseFirstHandle.Task.WaitAsync(ct);
        if (failAfterRelease)
        {
            throw new InvalidOperationException("handler failed");
        }
    }
}

internal sealed class NeverCompletingOutboxMessageHandler(string messageType) : IOutboxMessageHandler
{
    private readonly TaskCompletionSource _firstHandleStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string MessageType { get; } = messageType;

    public Task WaitForFirstHandleAsync() => _firstHandleStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _firstHandleStarted.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
}

internal sealed class NoopOutboxDispatchObserver : IOutboxDispatchObserver
{
    public Task OnTerminalFailureAsync(OutboxMessage message, string error, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal sealed class BlockingUsageReservationRepository : IUsageReservationRepository
{
    private readonly TaskCompletionSource _firstListStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstList =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _returnedReservation;

    public BlockingUsageReservationRepository()
    {
        var userId = Guid.NewGuid();
        Reservation = new UsageReservation
        {
            UserId = userId,
            UsagePeriodId = Guid.NewGuid(),
            RewriteAttemptId = Guid.NewGuid(),
            Status = UsageReservationStatus.Pending,
            ExpiresAt = DateTimeOffset.Parse("2026-06-19T01:10:00Z"),
            RewriteAttempt = new RewriteAttempt
            {
                UserId = userId,
                IdempotencyKey = "cleanup-stop",
                RequestHash = "cleanup-stop-hash",
                RequestJson = "{\"roughDraftReply\":\"Thanks for your message.\",\"tone\":\"warm\"}",
                Status = RewriteAttemptStatus.Pending,
                ExpiresAt = DateTimeOffset.Parse("2026-06-19T01:10:00Z"),
            },
        };
    }

    public UsageReservation Reservation { get; }

    public int TransitionCount { get; private set; }

    public int ReleaseCounterCount { get; private set; }

    public Task WaitForFirstListAsync() => _firstListStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void ReleaseFirstList() => _releaseFirstList.TrySetResult();

    public Task AddAsync(UsageReservation reservation, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<UsageReservation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<UsageReservation>> ListByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public async Task<IReadOnlyList<UsageReservation>> ListExpiredPendingBatchAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default)
    {
        if (_returnedReservation)
        {
            return Array.Empty<UsageReservation>();
        }

        _firstListStarted.TrySetResult();
        await _releaseFirstList.Task.WaitAsync(ct);
        _returnedReservation = true;
        return [Reservation];
    }

    public Task<int> TryTransitionFromPendingAsync(
        Guid reservationId,
        UsageReservationStatus targetStatus,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        reservationId.Should().Be(Reservation.Id);
        targetStatus.Should().Be(UsageReservationStatus.Expired);
        TransitionCount++;
        Reservation.Status = targetStatus;
        Reservation.ReleasedAt = now;
        return Task.FromResult(1);
    }

    public Task<int> ReleaseClaimedCounterAsync(
        Guid reservationId,
        Guid usagePeriodId,
        Guid? rewriteCreditId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        reservationId.Should().Be(Reservation.Id);
        usagePeriodId.Should().Be(Reservation.UsagePeriodId);
        ReleaseCounterCount++;
        return Task.FromResult(1);
    }
}

internal sealed class RecordingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default) =>
        await operation(ct);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel isolationLevel,
        CancellationToken ct = default) =>
        await operation(ct);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel,
        CancellationToken ct = default) =>
        await operation(ct);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel,
        int maxAttempts,
        CancellationToken ct = default) =>
        await operation(ct);
}

internal sealed record RecordingLogEntry(LogLevel Level, string Message, Exception? Exception);

internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<RecordingLogEntry> _entries = new();

    public IReadOnlyCollection<RecordingLogEntry> Entries => _entries.ToArray();

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull =>
        NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Enqueue(new RecordingLogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
