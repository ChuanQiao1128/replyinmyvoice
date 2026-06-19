using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class OutboxDispatchTelemetryObserver(
    IDeadLetterMessageRepository deadLetters,
    ILogger<OutboxDispatchTelemetryObserver> logger,
    TelemetryClient? telemetryClient = null) : IOutboxDispatchObserver
{
    public async Task OnTerminalFailureAsync(
        OutboxMessage message,
        string error,
        CancellationToken ct = default)
    {
        try
        {
            await deadLetters.AddAsync(
                DeadLetterMessageSupport.FromOutboxMessage(
                    message,
                    error,
                    message.LastAttemptAt ?? DateTimeOffset.UtcNow),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try
            {
                logger.LogWarning(
                    ex,
                    "Could not persist terminal outbox dead-letter record for message {MessageId}.",
                    message.Id);
            }
            catch (Exception)
            {
            }
        }

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
    }
}
