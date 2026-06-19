using ReplyInMyVoice.Application.UseCases.Quota;

namespace ReplyInMyVoice.Worker;

public sealed class ExpiredReservationCleanupWorker : BackgroundService
{
    private const int GracefulStopTimeoutSeconds = 60;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GracefulStopTimeout = TimeSpan.FromSeconds(GracefulStopTimeoutSeconds);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredReservationCleanupWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private TaskCompletionSource _iterationInProgress = CompletedIterationInProgress();
    private int _stopRequested;

    public ExpiredReservationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredReservationCleanupWorker> logger)
        : this(scopeFactory, logger, DefaultPollInterval)
    {
    }

    internal ExpiredReservationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredReservationCleanupWorker> logger,
        TimeSpan pollInterval)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = pollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (Volatile.Read(ref _stopRequested) == 1)
            {
                break;
            }

            var iteration = BeginIterationInProgress();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ReleaseExpiredReservationsHandler>();
                var released = await handler.HandleAsync(
                    new ReleaseExpiredReservationsCommand(DateTimeOffset.UtcNow),
                    stoppingToken);
                if (released > 0)
                {
                    _logger.LogInformation("Released {Count} expired rewrite reservations.", released);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expired reservation cleanup failed.");
            }
            finally
            {
                iteration.TrySetResult();
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _stopRequested, 1);

        var iterationTask = _iterationInProgress.Task;
        if (!iterationTask.IsCompleted)
        {
            var completedTask = await Task.WhenAny(
                iterationTask,
                Task.Delay(GracefulStopTimeout, cancellationToken));
            if (completedTask != iterationTask)
            {
                _logger.LogWarning(
                    "Expired reservation cleanup iteration did not finish within {TimeoutSeconds} second shutdown timeout; proceeding with hard cancellation.",
                    GracefulStopTimeoutSeconds);
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private TaskCompletionSource BeginIterationInProgress()
    {
        var iteration = NewIterationInProgress();
        var previous = _iterationInProgress;
        _iterationInProgress = iteration;
        previous.TrySetResult();
        return iteration;
    }

    private static TaskCompletionSource CompletedIterationInProgress()
    {
        var completion = NewIterationInProgress();
        completion.TrySetResult();
        return completion;
    }

    private static TaskCompletionSource NewIterationInProgress() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
