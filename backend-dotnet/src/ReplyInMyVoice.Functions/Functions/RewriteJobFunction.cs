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

        await processRewriteJobHandler.HandleAsync(
            new ProcessRewriteJobCommand(job.AttemptId),
            cancellationToken);
    }
}
