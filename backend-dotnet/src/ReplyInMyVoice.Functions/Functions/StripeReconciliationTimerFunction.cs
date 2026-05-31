using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class StripeReconciliationTimerFunction(
    StripeReconciliationService reconciliation,
    ILogger<StripeReconciliationTimerFunction> logger)
{
    [Function("ReconcileStripePayments")]
    public async Task Run(
        [TimerTrigger("0 15 2 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var windowEnd = completedAt;
        var windowStart = windowEnd.AddDays(-1);

        var report = await reconciliation.ReconcileAsync(
            windowStart,
            windowEnd,
            completedAt,
            cancellationToken);

        logger.LogInformation(
            "Stripe reconciliation finished with {DiscrepancyCount} discrepancy rows for {WindowStart:o} to {WindowEnd:o}.",
            report.DiscrepancyCount,
            report.WindowStart,
            report.WindowEnd);
    }
}
