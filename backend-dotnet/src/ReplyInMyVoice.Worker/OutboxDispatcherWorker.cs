using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Infrastructure.Configuration;

namespace ReplyInMyVoice.Worker;

public sealed class OutboxDispatcherWorker : BackgroundService
{
    private const int GracefulStopTimeoutSeconds = 60;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan GracefulStopTimeout = TimeSpan.FromSeconds(GracefulStopTimeoutSeconds);

    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private TaskCompletionSource _iterationInProgress = CompletedIterationInProgress();
    private int _stopRequested;

    public OutboxDispatcherWorker(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcherWorker> logger)
        : this(configuration, scopeFactory, logger, DefaultPollInterval)
    {
    }

    internal OutboxDispatcherWorker(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcherWorker> logger,
        TimeSpan pollInterval)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = pollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = _configuration.GetConnectionString("ServiceBus")
            ?? _configuration["SERVICEBUS_CONNECTION_STRING"]
            ?? _configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"];
        var managedIdentityConfigured = ManagedIdentityConfiguration.IsEnabled(_configuration) &&
            ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(_configuration) is not null;

        if (string.IsNullOrWhiteSpace(connectionString) && !managedIdentityConfigured)
        {
            _logger.LogWarning("Service Bus is not configured (connection string or managed identity); outbox dispatcher is idle.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

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
                var handler = scope.ServiceProvider.GetRequiredService<DispatchDueOutboxHandler>();
                await handler.HandleAsync(
                    new DispatchDueOutboxCommand(DateTimeOffset.UtcNow, _instanceId, BatchSize: 25),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch loop failed.");
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
                    "Outbox dispatch iteration did not finish within {TimeoutSeconds} second shutdown timeout; proceeding with hard cancellation.",
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
