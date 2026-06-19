using FluentAssertions;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.UseCases.Quota;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Functions;
using ReplyInMyVoice.Infrastructure.Services;
using ReplyInMyVoice.Tests.TestDoubles;

namespace ReplyInMyVoice.Tests;

public sealed class ExpiredReservationCleanupTimerFunctionTests
{
    [Fact]
    public async Task Run_logs_and_emits_metric_on_cleanup_failure()
    {
        var logger = new RecordingLogger<ExpiredReservationCleanupTimerFunction>();
        var metrics = new RecordingBusinessMetrics();
        var function = CreateFunction(new TimeoutException("cleanup timed out"), logger, metrics);

        await function.Run(timer: null!, CancellationToken.None);

        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error &&
            entry.Exception is TimeoutException &&
            entry.Message.Contains("Expired reservation cleanup failed.", StringComparison.Ordinal));
        metrics.Records.Should().ContainSingle(record =>
            record.Name == BusinessMetricNames.StuckReservationsCleanupFailedTotal &&
            record.Value == 1 &&
            record.DimensionName == BusinessMetricDimensions.Reason &&
            record.DimensionValue == nameof(TimeoutException));
    }

    [Fact]
    public async Task Run_does_not_swallow_OperationCanceledException()
    {
        var logger = new RecordingLogger<ExpiredReservationCleanupTimerFunction>();
        var metrics = new RecordingBusinessMetrics();
        var function = CreateFunction(new OperationCanceledException(), logger, metrics);

        var act = () => function.Run(timer: null!, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        metrics.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_completes_normally_when_no_reservations_to_release()
    {
        var logger = new RecordingLogger<ExpiredReservationCleanupTimerFunction>();
        var metrics = new RecordingBusinessMetrics();
        var function = CreateFunction(failure: null, logger, metrics);

        await function.Run(timer: null!, CancellationToken.None);

        logger.Entries.Should().BeEmpty();
        metrics.Records.Should().BeEmpty();
    }

    private static ExpiredReservationCleanupTimerFunction CreateFunction(
        Exception? failure,
        RecordingLogger<ExpiredReservationCleanupTimerFunction> logger,
        IBusinessMetrics metrics)
    {
        var handler = new ReleaseExpiredReservationsHandler(
            new StubUsageReservationRepository(failure),
            new RecordingUnitOfWork());
        var cleanup = new ExpiredReservationCleanupService(handler);
        return new ExpiredReservationCleanupTimerFunction(cleanup, logger, metrics);
    }

    private sealed class StubUsageReservationRepository(Exception? failure) : IUsageReservationRepository
    {
        public Task AddAsync(UsageReservation reservation, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<UsageReservation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> TryTransitionFromPendingAsync(
            Guid reservationId,
            UsageReservationStatus targetStatus,
            DateTimeOffset now,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> ReleaseClaimedCounterAsync(
            Guid reservationId,
            Guid usagePeriodId,
            Guid? rewriteCreditId,
            DateTimeOffset now,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UsageReservation>> ListExpiredPendingBatchAsync(
            DateTimeOffset now,
            int batchSize,
            CancellationToken ct = default)
        {
            if (failure is not null)
            {
                throw failure;
            }

            return Task.FromResult<IReadOnlyList<UsageReservation>>(Array.Empty<UsageReservation>());
        }

        public Task<IReadOnlyList<UsageReservation>> ListByUserIdAsync(
            Guid userId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
