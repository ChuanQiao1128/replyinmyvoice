using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class WebhookDispatcherTimerFunction(
    DispatchDueWebhooksHandler dispatchDueWebhooksHandler,
    ILogger<WebhookDispatcherTimerFunction> logger)
{
    [Function("DispatchWebhookDeliveries")]
    public async Task Run(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await dispatchDueWebhooksHandler.HandleAsync(
            new DispatchDueWebhooksCommand(
                DateTimeOffset.UtcNow,
                Environment.MachineName,
                BatchSize: 10),
            cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Processed {Count} webhook deliveries.", count);
        }
    }
}
