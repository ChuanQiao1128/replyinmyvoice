using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class OutboxDispatchTelemetryObserver(
    ILogger<OutboxDispatchTelemetryObserver> logger,
    TelemetryClient? telemetryClient = null) : IOutboxDispatchObserver
{
    public Task OnTerminalFailureAsync(
        OutboxMessage message,
        string error,
        CancellationToken ct = default)
    {
        try
        {
            logger.LogError(
                "Outbox message {MessageType} {MessageId} (correlation {CorrelationId}) permanently failed after {AttemptCount}/{MaxAttempts} attempts: {Error}",
                message.MessageType,
                message.Id,
                message.CorrelationId,
                message.AttemptCount,
                message.MaxAttempts,
                error);
        }
        catch (Exception)
        {
        }

        try
        {
            telemetryClient?.TrackEvent(
                "OutboxMessageTerminalFailure",
                new Dictionary<string, string>
                {
                    ["messageType"] = message.MessageType,
                    ["messageId"] = message.Id.ToString(),
                    ["correlationId"] = message.CorrelationId ?? string.Empty,
                    ["attemptCount"] = message.AttemptCount.ToString(),
                });
        }
        catch (Exception ex)
        {
            try
            {
                logger.LogWarning(
                    ex,
                    "Could not track terminal outbox failure telemetry for message {MessageId}.",
                    message.Id);
            }
            catch (Exception)
            {
            }
        }

        return Task.CompletedTask;
    }
}
