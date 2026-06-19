using ReplyInMyVoice.Application.UseCases.ApiKey;

namespace ReplyInMyVoice.Worker;

public sealed class ApiKeyPepperRehashWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ApiKeyPepperRehashWorker> logger,
    TimeSpan? rehashInterval = null) : BackgroundService
{
    private static readonly TimeSpan DefaultRehashInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _rehashInterval = rehashInterval ?? DefaultRehashInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_rehashInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<RehashPendingApiKeysHandler>();
                var result = await handler.HandleAsync(new RehashPendingApiKeysCommand(), stoppingToken);
                if (result.Cleared > 0)
                {
                    logger.LogInformation("Cleared {Count} pending API key hash updates.", result.Cleared);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "API key hash maintenance failed.");
            }
        }
    }
}
