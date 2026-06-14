using ReplyInMyVoice.Application.UseCases.WebhookOutbox;
using ReplyInMyVoice.Infrastructure.Configuration;

namespace ReplyInMyVoice.Worker;

public sealed class OutboxDispatcherWorker(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

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

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
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
                logger.LogError(ex, "Outbox dispatch loop failed.");
            }
        }
    }
}
