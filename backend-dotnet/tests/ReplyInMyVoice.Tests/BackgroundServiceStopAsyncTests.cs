extern alias WorkerProject;

using System.Data;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using WorkerProject::ReplyInMyVoice.Worker;

namespace ReplyInMyVoice.Tests;

public sealed class BackgroundServiceStopAsyncTests
{
    [Fact]
    public async Task OutboxDispatcherWorker_StopAsync_completes_in_flight_iteration()
    {
        var outboxMessages = new SlowOutboxMessageRepository();
        var messageHandler = new SlowOutboxMessageHandler();
        await using var provider = new ServiceCollection()
            .AddScoped(_ => new DispatchDueOutboxHandler(
                outboxMessages,
                [messageHandler],
                new NoOpOutboxDispatchObserver(),
                new DirectUnitOfWork()))
            .BuildServiceProvider();
        var worker = new OutboxDispatcherWorker(
            ServiceBusConfiguration(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxDispatcherWorker>.Instance,
            TimeSpan.FromMilliseconds(10));

        await worker.StartAsync(CancellationToken.None);
        await messageHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var stopTask = worker.StopAsync(CancellationToken.None);
        await Task.Delay(100);
        stopTask.IsCompleted.Should().BeFalse();

        messageHandler.Release.SetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(10));

        messageHandler.Completed.Task.IsCompletedSuccessfully.Should().BeTrue();
        outboxMessages.MarkedSent.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExpiredReservationCleanupWorker_StopAsync_completes_in_flight_iteration()
    {
        var reservations = new SlowUsageReservationRepository();
        await using var provider = new ServiceCollection()
            .AddScoped(_ => new ReleaseExpiredReservationsHandler(
                reservations,
                new DirectUnitOfWork(),
                NullLogger<ReleaseExpiredReservationsHandler>.Instance))
            .BuildServiceProvider();
        var worker = new ExpiredReservationCleanupWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExpiredReservationCleanupWorker>.Instance,
            TimeSpan.FromMilliseconds(10));

        await worker.StartAsync(CancellationToken.None);
        await reservations.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var stopTask = worker.StopAsync(CancellationToken.None);
        await Task.Delay(100);
        stopTask.IsCompleted.Should().BeFalse();

        reservations.Release.SetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(10));

        reservations.Transitioned.Task.IsCompletedSuccessfully.Should().BeTrue();
        reservations.CounterReleased.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ServiceBusRewriteWorker_StopAsync_stops_processor()
    {
        var processor = new RecordingRewriteProcessor();
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var worker = new ServiceBusRewriteWorker(
            ServiceBusConfiguration(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ServiceBusRewriteWorker>.Instance,
            new RecordingRewriteProcessorFactory(processor));

        await worker.StartAsync(CancellationToken.None);
        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        processor.HasHandlers.Should().BeTrue();

        var stopTask = worker.StopAsync(CancellationToken.None);
        await processor.StopStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        stopTask.IsCompleted.Should().BeFalse();

        processor.StopToken.CanBeCanceled.Should().BeTrue();
        processor.ReleaseStop.SetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(10));

        processor.StopCompleted.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    private static IConfiguration ServiceBusConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SERVICEBUS_CONNECTION_STRING"] = "Endpoint=sb://unit-test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
                ["SERVICEBUS_QUEUE_NAME"] = "rewrite-jobs",
            })
            .Build();

    private sealed class SlowOutboxMessageHandler : IOutboxMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string MessageType => "RewriteJobCreated";

        public async Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(ct);
            Completed.SetResult();
        }
    }

    private sealed class SlowOutboxMessageRepository : IOutboxMessageRepository
    {
        private readonly OutboxMessage _message = new()
        {
            MessageType = "RewriteJobCreated",
            PayloadJson = "{}",
        };

        private int _claimCount;

        public TaskCompletionSource MarkedSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task AddAsync(OutboxMessage message, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
            DateTimeOffset now,
            string lockedBy,
            int batchSize,
            TimeSpan claimLease,
            CancellationToken ct = default)
        {
            var messages = Interlocked.Increment(ref _claimCount) == 1
                ? (IReadOnlyList<OutboxMessage>)[_message]
                : [];
            return Task.FromResult(messages);
        }

        public Task<OutboxMessage?> ClaimByIdAsync(
            Guid messageId,
            DateTimeOffset now,
            string lockedBy,
            TimeSpan claimLease,
            CancellationToken ct = default) =>
            Task.FromResult<OutboxMessage?>(null);

        public Task MarkSentAsync(Guid messageId, DateTimeOffset now, CancellationToken ct = default)
        {
            MarkedSent.SetResult();
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> GetOldestIncompleteCreatedAtAsync(CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);

        public Task<OutboxMessageFailureInfo> MarkFailedAttemptAsync(
            Guid messageId,
            DateTimeOffset now,
            string error,
            CancellationToken ct = default) =>
            Task.FromResult(new OutboxMessageFailureInfo(0, 1, OutboxMessageStatus.Pending, now));
    }

    private sealed class NoOpOutboxDispatchObserver : IOutboxDispatchObserver
    {
        public Task OnTerminalFailureAsync(OutboxMessage message, string error, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class SlowUsageReservationRepository : IUsageReservationRepository
    {
        private readonly UsageReservation _reservation;
        private int _listCount;

        public SlowUsageReservationRepository()
        {
            var attempt = new RewriteAttempt
            {
                UserId = Guid.NewGuid(),
                IdempotencyKey = "stop-test",
                RequestHash = "hash",
                RequestJson = "{}",
                Status = RewriteAttemptStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            };
            _reservation = new UsageReservation
            {
                UserId = attempt.UserId,
                UsagePeriodId = Guid.NewGuid(),
                RewriteAttemptId = attempt.Id,
                RewriteAttempt = attempt,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            };
        }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Transitioned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CounterReleased { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task AddAsync(UsageReservation reservation, CancellationToken ct = default) => Task.CompletedTask;

        public Task<UsageReservation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken ct = default) =>
            Task.FromResult<UsageReservation?>(null);

        public Task<int> TryTransitionFromPendingAsync(
            Guid reservationId,
            UsageReservationStatus targetStatus,
            DateTimeOffset now,
            CancellationToken ct = default)
        {
            Transitioned.SetResult();
            _reservation.Status = targetStatus;
            return Task.FromResult(1);
        }

        public Task<int> ReleaseClaimedCounterAsync(
            Guid reservationId,
            Guid usagePeriodId,
            Guid? rewriteCreditId,
            DateTimeOffset now,
            CancellationToken ct = default)
        {
            CounterReleased.SetResult();
            return Task.FromResult(1);
        }

        public async Task<IReadOnlyList<UsageReservation>> ListExpiredPendingBatchAsync(
            DateTimeOffset now,
            int batchSize,
            CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _listCount) != 1)
            {
                return [];
            }

            Started.SetResult();
            await Release.Task.WaitAsync(ct);
            return [_reservation];
        }

        public Task<IReadOnlyList<UsageReservation>> ListByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UsageReservation>>([]);
    }

    private sealed class DirectUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);

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

    private sealed class RecordingRewriteProcessorFactory(RecordingRewriteProcessor processor) : IRewriteServiceBusProcessorFactory
    {
        public IRewriteServiceBusProcessor CreateProcessor(
            IConfiguration configuration,
            Func<ProcessMessageEventArgs, Task> processMessageAsync,
            Func<ProcessErrorEventArgs, Task> processErrorAsync)
        {
            processor.ProcessMessageAsync += processMessageAsync;
            processor.ProcessErrorAsync += processErrorAsync;
            return processor;
        }
    }

    private sealed class RecordingRewriteProcessor : IRewriteServiceBusProcessor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken StopToken { get; private set; }

        public event Func<ProcessMessageEventArgs, Task>? ProcessMessageAsync;
        public event Func<ProcessErrorEventArgs, Task>? ProcessErrorAsync;

        public Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            Started.SetResult();
            return Task.CompletedTask;
        }

        public async Task StopProcessingAsync(CancellationToken cancellationToken)
        {
            StopToken = cancellationToken;
            StopStarted.SetResult();
            await ReleaseStop.Task.WaitAsync(cancellationToken);
            StopCompleted.SetResult();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public bool HasHandlers => ProcessMessageAsync is not null && ProcessErrorAsync is not null;
    }
}
