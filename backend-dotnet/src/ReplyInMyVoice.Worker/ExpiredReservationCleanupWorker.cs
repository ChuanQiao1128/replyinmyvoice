using System.Diagnostics;
using ReplyInMyVoice.Application.UseCases.Quota;

namespace ReplyInMyVoice.Worker;

public sealed class ExpiredReservationCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredReservationCleanupWorker> logger,
    TimeSpan? cleanupInterval = null) : BackgroundService
{
    private static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GracefulDrainTimeout = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _cleanupInterval = cleanupInterval ?? DefaultCleanupInterval;
    private Task? _inFlightIteration;
    private int _stopRequested;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Starting graceful drain for expired reservation cleanup worker.");
        Interlocked.Exchange(ref _stopRequested, 1);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(GracefulDrainTimeout);

        try
        {
            var inFlightIteration = Volatile.Read(ref _inFlightIteration);
            if (inFlightIteration is not null)
            {
                await inFlightIteration.WaitAsync(timeoutCts.Token);
            }

            await base.StopAsync(timeoutCts.Token);
            logger.LogInformation("Graceful drain completed within {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            logger.LogWarning(
                "Graceful drain timed out after {TimeoutMilliseconds} ms.",
                GracefulDrainTimeout.TotalMilliseconds);
            try
            {
                await base.StopAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_cleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (Volatile.Read(ref _stopRequested) == 1)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ReleaseExpiredReservationsHandler>();
                var iteration = handler.HandleAsync(
                    new ReleaseExpiredReservationsCommand(DateTimeOffset.UtcNow),
                    stoppingToken);
                Volatile.Write(ref _inFlightIteration, ObserveCompletion(iteration));
                var released = await iteration;
                if (released > 0)
                {
                    logger.LogInformation("Released {Count} expired rewrite reservations.", released);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Expired reservation cleanup failed.");
            }
            finally
            {
                Volatile.Write(ref _inFlightIteration, null);
            }
        }
    }

    private static Task ObserveCompletion(Task iteration) =>
        iteration.ContinueWith(
            static _ => { },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
