using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Queueing;

public sealed class AzureServiceBusRewriteJobPublisher(ServiceBusSender sender) : IRewriteJobPublisher
{
    public async Task PublishAsync(RewriteJob job, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(JsonSerializer.Serialize(job))
        {
            MessageId = job.AttemptId.ToString("N"),
            ContentType = "application/json",
            Subject = "rewrite-attempt",
        };
        if (!string.IsNullOrWhiteSpace(job.CorrelationId))
        {
            message.CorrelationId = job.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(job.Traceparent))
        {
            message.ApplicationProperties["traceparent"] = job.Traceparent;
        }

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
