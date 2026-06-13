using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Application.UseCases.StripeReconciliation;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class StripeReconciliationTimerFunction(
    ReconcileStripeHandler reconcileStripeHandler,
    StripeReconciliationOptions options,
    ILogger<StripeReconciliationTimerFunction> logger)
{
    [Function("ReconcileStripePayments")]
    public async Task Run(
        [TimerTrigger("0 15 2 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var windowEnd = completedAt;
        var windowStart = windowEnd.AddDays(-options.WindowDays);

        var report = await reconcileStripeHandler.HandleAsync(
            new ReconcileStripeCommand(
                windowStart,
                windowEnd,
                completedAt),
            cancellationToken);

        logger.LogInformation(
            "Stripe reconciliation finished with {DiscrepancyCount} discrepancy rows for {WindowStart:o} to {WindowEnd:o}.",
            report.DiscrepancyCount,
            report.WindowStart,
            report.WindowEnd);
    }
}
