using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ExpiredReservationCleanupTimerFunction(
    ExpiredReservationCleanupService cleanup,
    ILogger<ExpiredReservationCleanupTimerFunction> logger,
    IBusinessMetrics? metrics = null)
{
    private readonly IBusinessMetrics _metrics = metrics ?? NoOpBusinessMetrics.Instance;

    [Function("ReleaseExpiredReservations")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await cleanup.RunOnceAsync(DateTimeOffset.UtcNow, cancellationToken);
            if (count > 0)
            {
                logger.LogInformation("Released {Count} expired reservations.", count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Expired reservation cleanup failed.");
            // Metric emitted on timer execution failure to alert operational teams via Azure Application Insights when stuck reservation cleanup does not run.
            _metrics.Record(
                BusinessMetricNames.StuckReservationsCleanupFailedTotal,
                1,
                BusinessMetricDimensions.Reason,
                ex.GetType().Name);
        }
    }
}
