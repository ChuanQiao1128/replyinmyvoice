using System.Diagnostics;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.UseCases.RewriteJob;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class RewriteJobFunction(
    ProcessRewriteJobHandler processRewriteJobHandler,
    ILogger<RewriteJobFunction> logger)
{
    [Function("ProcessRewriteJob")]
    public async Task Run(
        [ServiceBusTrigger("%SERVICEBUS_QUEUE_NAME%", Connection = "ServiceBus")]
        string messageBody,
        CancellationToken cancellationToken)
    {
        RewriteJob? job;
        try
        {
            job = JsonSerializer.Deserialize<RewriteJob>(messageBody);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Service Bus message was not valid RewriteJob JSON.");
            throw;
        }

        if (job is null || job.AttemptId == Guid.Empty)
        {
            throw new InvalidOperationException("Service Bus message did not contain a valid attempt id.");
        }

        using var activity = StartConsumerActivity(job);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = job.CorrelationId,
            ["AttemptId"] = job.AttemptId,
        });

        await processRewriteJobHandler.HandleAsync(
            new ProcessRewriteJobCommand(job.AttemptId),
            cancellationToken);
    }

    private static Activity? StartConsumerActivity(RewriteJob job)
    {
        if (string.IsNullOrWhiteSpace(job.Traceparent) ||
            !ActivityContext.TryParse(job.Traceparent, null, out _))
        {
            return null;
        }

        var activity = new Activity("ProcessRewriteJob");
        activity.SetParentId(job.Traceparent);
        activity.SetTag("correlation_id", job.CorrelationId);
        activity.SetTag("attempt_id", job.AttemptId);
        return activity.Start();
    }
}
