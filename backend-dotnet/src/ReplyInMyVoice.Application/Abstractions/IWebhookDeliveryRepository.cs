using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IWebhookDeliveryRepository
{
    Task<IReadOnlyList<WebhookDelivery>> ClaimDueAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        TimeSpan claimLease,
        CancellationToken ct = default);

    Task MarkDeliveredAsync(
        Guid deliveryId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<WebhookDeliveryFailureInfo> MarkFailedAttemptAsync(
        Guid deliveryId,
        DateTimeOffset now,
        string error,
        CancellationToken ct = default);

    Task<IReadOnlyList<WebhookDeliveryStatusDto>> GetWebhookDeliveryStatusAsync(
        Guid apiKeyId,
        int limit,
        CancellationToken ct = default);

    Task<WebhookDeliveryRetryResult> RetryFailedDeliveryAsync(
        Guid deliveryId,
        DateTimeOffset now,
        CancellationToken ct = default);
}

public sealed record WebhookDeliveryFailureInfo(
    int AttemptCount,
    int MaxAttempts,
    WebhookDeliveryStatus Status,
    DateTimeOffset NextAttemptAt);

public sealed record WebhookDeliveryStatusDto(
    Guid Id,
    WebhookDeliveryStatus Status,
    int AttemptCount,
    int MaxAttempts,
    string? LastError,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset CreatedAt);

public sealed record WebhookDeliveryRetryResult(
    WebhookDeliveryRetryResultKind Kind,
    Guid? DeliveryId = null,
    DateTimeOffset? NextAttemptAt = null);

public enum WebhookDeliveryRetryResultKind
{
    Success = 0,
    NotFound = 1,
    InvalidState = 2,
}
