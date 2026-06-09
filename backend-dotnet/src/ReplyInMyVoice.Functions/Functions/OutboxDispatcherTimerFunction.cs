using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.UseCases.WebhookOutbox;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class OutboxDispatcherTimerFunction(
    DispatchDueOutboxHandler dispatchDueOutboxHandler,
    ILogger<OutboxDispatcherTimerFunction> logger)
{
    [Function("DispatchOutboxMessages")]
    public async Task Run(
        [TimerTrigger("*/15 * * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await dispatchDueOutboxHandler.HandleAsync(
            new DispatchDueOutboxCommand(
                DateTimeOffset.UtcNow,
                Environment.MachineName,
                BatchSize: 10),
            cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Dispatched {Count} outbox messages.", count);
        }
    }
}
