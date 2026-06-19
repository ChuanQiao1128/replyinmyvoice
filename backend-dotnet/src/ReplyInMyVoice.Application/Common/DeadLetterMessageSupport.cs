using System.Text.Json;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Common;

public static class DeadLetterSourceTypes
{
    public const string OutboxMessage = "OutboxMessage";
    public const string StripeEvent = "StripeEvent";

    public static bool TryNormalize(string? sourceType, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return true;
        }

        if (string.Equals(sourceType, OutboxMessage, StringComparison.OrdinalIgnoreCase))
        {
            normalized = OutboxMessage;
            return true;
        }

        if (string.Equals(sourceType, StripeEvent, StringComparison.OrdinalIgnoreCase))
        {
            normalized = StripeEvent;
            return true;
        }

        return false;
    }
}

public static class DeadLetterMessageSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static DeadLetterMessage FromOutboxMessage(
        OutboxMessage message,
        string failureReason,
        DateTimeOffset createdAt) =>
        new()
        {
            SourceType = DeadLetterSourceTypes.OutboxMessage,
            SourceId = message.Id.ToString("D"),
            SourceData = JsonSerializer.Serialize(new
            {
                sourceType = DeadLetterSourceTypes.OutboxMessage,
                sourceId = message.Id.ToString("D"),
                message.MessageType,
                message.PayloadJson,
                status = message.Status.ToString(),
                message.CreatedAt,
                message.NextAttemptAt,
                message.AttemptCount,
                message.MaxAttempts,
                message.SentAt,
                message.LastAttemptAt,
                lastError = message.LastError ?? failureReason,
                message.CorrelationId,
            }, JsonOptions),
            FailureReason = Truncate(failureReason, 1000),
            CreatedAt = createdAt,
        };

    public static DeadLetterMessage FromStripeEvent(
        StripeEvent stripeEvent,
        string failureReason,
        DateTimeOffset createdAt) =>
        new()
        {
            SourceType = DeadLetterSourceTypes.StripeEvent,
            SourceId = stripeEvent.EventId,
            SourceData = JsonSerializer.Serialize(new
            {
                sourceType = DeadLetterSourceTypes.StripeEvent,
                sourceId = stripeEvent.EventId,
                stripeEvent.Type,
                status = stripeEvent.Status.ToString(),
                stripeEvent.AttemptCount,
                lastError = stripeEvent.LastError ?? failureReason,
                stripeEvent.PayloadJson,
                stripeEvent.LastAttemptAt,
                stripeEvent.CreatedAt,
                stripeEvent.ProcessedAt,
            }, JsonOptions),
            FailureReason = Truncate(failureReason, 1000),
            CreatedAt = createdAt,
        };

    public static AdminDeadLetterListItemDto ToListItem(DeadLetterMessage message)
    {
        var metadata = ReadMetadata(message);
        return new AdminDeadLetterListItemDto(
            message.Id,
            message.SourceType,
            message.SourceId,
            message.FailureReason,
            message.CreatedAt,
            message.RequeuedAt,
            message.RequeuedAt is not null,
            metadata.AttemptCount,
            metadata.LastError);
    }

    public static AdminDeadLetterDetailDto ToDetail(DeadLetterMessage message)
    {
        var metadata = ReadMetadata(message);
        return new AdminDeadLetterDetailDto(
            message.Id,
            message.SourceType,
            message.SourceId,
            message.SourceData,
            message.FailureReason,
            message.CreatedAt,
            message.RequeuedAt,
            message.RequeuedAt is not null,
            metadata.AttemptCount,
            metadata.LastError);
    }

    public static AdminDeadLetterAuditDetailsDto ToAuditDetails(DeadLetterMessage? message) =>
        message is null
            ? new AdminDeadLetterAuditDetailsDto(null, null, null, null)
            : ToAuditDetails(message.SourceType, message.SourceId, ReadMetadata(message));

    public static AdminDeadLetterAuditDetailsDto ToAuditDetails(
        string? sourceType,
        string? sourceId,
        DeadLetterMetadata metadata) =>
        new(sourceType, sourceId, metadata.AttemptCount, metadata.LastError);

    public static DeadLetterMetadata ReadMetadata(DeadLetterMessage message)
    {
        try
        {
            using var document = JsonDocument.Parse(message.SourceData);
            var root = document.RootElement;
            var attemptCount = root.TryGetProperty("attemptCount", out var attemptCountProperty) &&
                attemptCountProperty.ValueKind == JsonValueKind.Number &&
                attemptCountProperty.TryGetInt32(out var parsedAttemptCount)
                    ? parsedAttemptCount
                    : (int?)null;
            var lastError = root.TryGetProperty("lastError", out var lastErrorProperty) &&
                lastErrorProperty.ValueKind == JsonValueKind.String
                    ? lastErrorProperty.GetString()
                    : null;

            return new DeadLetterMetadata(attemptCount, lastError ?? message.FailureReason);
        }
        catch (JsonException)
        {
            return new DeadLetterMetadata(null, message.FailureReason);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}

public sealed record DeadLetterMetadata(
    int? AttemptCount,
    string? LastError);
