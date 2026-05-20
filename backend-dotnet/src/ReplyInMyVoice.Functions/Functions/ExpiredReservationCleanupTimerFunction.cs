using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ExpiredReservationCleanupTimerFunction(
    ExpiredReservationCleanupService cleanup,
    ILogger<ExpiredReservationCleanupTimerFunction> logger)
{
    [Function("ReleaseExpiredReservations")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await cleanup.RunOnceAsync(DateTimeOffset.UtcNow, cancellationToken);
        if (count > 0)
        {
            logger.LogInformation("Released {Count} expired reservations.", count);
        }
    }
}
