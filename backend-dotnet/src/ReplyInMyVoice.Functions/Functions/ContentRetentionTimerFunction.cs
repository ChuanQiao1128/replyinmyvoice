using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ContentRetentionTimerFunction(
    Func<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<ContentRetentionTimerFunction> logger)
{
    private const string RetentionDaysSetting = "REWRITE_CONTENT_RETENTION_DAYS";

    // REWRITE_CONTENT_RETENTION_DAYS controls raw rewrite content TTL; default is 90 days.
    [Function("ScrubExpiredRewriteContent")]
    public async Task Run(
        [TimerTrigger("0 30 2 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var retention = new RetentionService(dbContextFactory);
        var retentionDays = ResolveRetentionDays();
        var count = await retention.ScrubExpiredRawContentAsync(
            DateTimeOffset.UtcNow,
            retentionDays,
            cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Scrubbed raw content from {Count} rewrite attempts.", count);
        }
    }

    private int ResolveRetentionDays()
    {
        var rawValue = configuration[RetentionDaysSetting];
        if (int.TryParse(rawValue, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return RetentionService.DefaultRetentionDays;
    }
}
