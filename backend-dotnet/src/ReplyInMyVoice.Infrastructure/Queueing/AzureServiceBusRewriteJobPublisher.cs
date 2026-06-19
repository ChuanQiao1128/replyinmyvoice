using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Observability;

namespace ReplyInMyVoice.Infrastructure.Queueing;

public sealed class AzureServiceBusRewriteJobPublisher(ServiceBusSender sender) : IRewriteJobPublisher
{
    public async Task PublishAsync(
        RewriteJob job,
        CancellationToken cancellationToken,
        string? correlationId = null)
    {
        var message = CreateMessage(job, correlationId);
        await sender.SendMessageAsync(message, cancellationToken);
    }

    public static ServiceBusMessage CreateMessage(RewriteJob job, string? correlationId = null)
    {
        var message = new ServiceBusMessage(JsonSerializer.Serialize(job))
        {
            MessageId = job.AttemptId.ToString("N"),
            ContentType = "application/json",
            Subject = "rewrite-attempt",
        };
        var traceparent = DistributedTracingContext.ResolveServiceBusTraceparent(correlationId);
        if (!string.IsNullOrWhiteSpace(traceparent))
        {
            message.ApplicationProperties["traceparent"] = traceparent;
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            message.ApplicationProperties[DistributedTracingContext.CorrelationIdPropertyName] = correlationId;
        }
        else if (!string.IsNullOrWhiteSpace(traceparent))
        {
            message.ApplicationProperties[DistributedTracingContext.CorrelationIdPropertyName] = traceparent;
        }

        return message;
    }
}
