using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
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
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        RewriteJob? job;
        try
        {
            job = JsonSerializer.Deserialize<RewriteJob>(message.Body);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Service Bus message was not valid RewriteJob JSON.");
            await messageActions.DeadLetterMessageAsync(
                message,
                propertiesToModify: null,
                deadLetterReason: "invalid_json",
                deadLetterErrorDescription: ex.Message,
                cancellationToken: cancellationToken);
            return;
        }

        if (job is null)
        {
            await messageActions.DeadLetterMessageAsync(
                message,
                propertiesToModify: null,
                deadLetterReason: "invalid_job",
                deadLetterErrorDescription: "Message body did not contain a rewrite job.",
                cancellationToken: cancellationToken);
            return;
        }

        if (job.AttemptId == Guid.Empty)
        {
            await messageActions.DeadLetterMessageAsync(
                message,
                propertiesToModify: null,
                deadLetterReason: "invalid_job",
                deadLetterErrorDescription: "Message body did not contain a valid attempt id.",
                cancellationToken: cancellationToken);
            return;
        }

        using var activity = StartConsumerActivity(job);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = job.CorrelationId,
            ["AttemptId"] = job.AttemptId,
        });

        try
        {
            await processRewriteJobHandler.HandleAsync(
                new ProcessRewriteJobCommand(job.AttemptId),
                cancellationToken);
            await messageActions.CompleteMessageAsync(message, cancellationToken);
        }
        catch (RewriteJobAttemptNotFoundException ex)
        {
            logger.LogWarning(ex, "Dead-lettering rewrite job for missing attempt {AttemptId}", job.AttemptId);
            await messageActions.DeadLetterMessageAsync(
                message,
                propertiesToModify: null,
                deadLetterReason: "attempt_not_found",
                deadLetterErrorDescription: ex.Message,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rewrite job failed for attempt {AttemptId}", job.AttemptId);
            throw;
        }
    }

    public Task Run(string messageBody, CancellationToken cancellationToken) =>
        Run(
            ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(messageBody)),
            new NoOpMessageActions(),
            cancellationToken);

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

    private sealed class NoOpMessageActions : ServiceBusMessageActions
    {
        public override Task CompleteMessageAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public override Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            Dictionary<string, object>? propertiesToModify = null,
            string? deadLetterReason = null,
            string? deadLetterErrorDescription = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
