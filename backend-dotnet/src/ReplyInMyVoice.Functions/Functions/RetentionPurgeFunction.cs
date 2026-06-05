using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class RetentionPurgeFunction(
    RetentionService retention,
    ILogger<RetentionPurgeFunction> logger)
{
    [Function("PurgeRewriteAttemptPayloads")]
    public async Task Run(
        [TimerTrigger("0 30 2 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await retention.ScrubExpiredRawContentAsync(
            DateTimeOffset.UtcNow,
            cancellationToken: cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Purged payloads from {Count} rewrite attempts.", count);
        }
    }
}
