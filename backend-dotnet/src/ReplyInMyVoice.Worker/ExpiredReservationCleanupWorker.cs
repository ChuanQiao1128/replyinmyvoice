using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Worker;

public sealed class ExpiredReservationCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredReservationCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<ExpiredReservationCleanupService>();
                var released = await cleanup.RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken);
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
        }
    }
}
