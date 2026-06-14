using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.UseCases.StripeEvent;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class StripeEventProcessorTimerFunction(
    ProcessPendingStripeEventsHandler processPendingStripeEventsHandler,
    ILogger<StripeEventProcessorTimerFunction> logger)
{
    [Function("ProcessStripeEvents")]
    public async Task Run(
        [TimerTrigger("*/15 * * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await processPendingStripeEventsHandler.HandleAsync(
            new ProcessPendingStripeEventsCommand(
                DateTimeOffset.UtcNow,
                BatchSize: 10),
            cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Processed {Count} pending Stripe events.", count);
        }
    }
}
