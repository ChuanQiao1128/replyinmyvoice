using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class WebhookDispatcherTimerFunction(
    WebhookDispatcherService dispatcher,
    ILogger<WebhookDispatcherTimerFunction> logger)
{
    [Function("DispatchWebhookDeliveries")]
    public async Task Run(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await dispatcher.DispatchDueAsync(
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            batchSize: 10,
            cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Processed {Count} webhook deliveries.", count);
        }
    }
}
