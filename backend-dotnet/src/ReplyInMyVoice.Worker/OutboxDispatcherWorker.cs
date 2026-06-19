using System.Diagnostics;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Infrastructure.Configuration;

namespace ReplyInMyVoice.Worker;

public sealed class OutboxDispatcherWorker(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherWorker> logger,
    TimeSpan? dispatchInterval = null) : BackgroundService
{
    private static readonly TimeSpan DefaultDispatchInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan GracefulDrainTimeout = TimeSpan.FromSeconds(60);
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private readonly TimeSpan _dispatchInterval = dispatchInterval ?? DefaultDispatchInterval;
    private Task? _inFlightIteration;
    private int _stopRequested;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Starting graceful drain for outbox dispatcher worker.");
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
        var connectionString = configuration.GetConnectionString("ServiceBus")
            ?? configuration["SERVICEBUS_CONNECTION_STRING"]
            ?? configuration["AZURE_SERVICE_BUS_CONNECTION_STRING"];
        var managedIdentityConfigured = ManagedIdentityConfiguration.IsEnabled(configuration) &&
            ManagedIdentityConfiguration.ResolveServiceBusFullyQualifiedNamespace(configuration) is not null;

        if (string.IsNullOrWhiteSpace(connectionString) && !managedIdentityConfigured)
        {
            logger.LogWarning("Service Bus is not configured (connection string or managed identity); outbox dispatcher is idle.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        using var timer = new PeriodicTimer(_dispatchInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (Volatile.Read(ref _stopRequested) == 1)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<DispatchDueOutboxHandler>();
                var iteration = handler.HandleAsync(
                    new DispatchDueOutboxCommand(DateTimeOffset.UtcNow, _instanceId, BatchSize: 25),
                    stoppingToken);
                Volatile.Write(ref _inFlightIteration, ObserveCompletion(iteration));
                await iteration;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch loop failed.");
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
