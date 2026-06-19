using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class OutboxDispatchTelemetryObserver(
    ILogger<OutboxDispatchTelemetryObserver> logger,
    IDeadLetterRepository deadLetters,
    IUnitOfWork unitOfWork,
    TelemetryClient? telemetryClient = null) : IOutboxDispatchObserver
{
    public async Task OnTerminalFailureAsync(
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

        try
        {
            await deadLetters.RecordFailureAsync(
                DeadLetterEntityType.Outbox,
                message.Id.ToString("D"),
                error,
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try
            {
                logger.LogWarning(
                    ex,
                    "Could not record terminal outbox failure for message {MessageId}.",
                    message.Id);
            }
            catch (Exception)
            {
            }
        }
    }
}
