using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class OutboxDispatcherTimerFunction(
    OutboxDispatcherService dispatcher,
    ILogger<OutboxDispatcherTimerFunction> logger)
{
    [Function("DispatchOutboxMessages")]
    public async Task Run(
        [TimerTrigger("*/15 * * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await dispatcher.DispatchDueAsync(
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            batchSize: 10,
            cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Dispatched {Count} outbox messages.", count);
        }
    }
}
