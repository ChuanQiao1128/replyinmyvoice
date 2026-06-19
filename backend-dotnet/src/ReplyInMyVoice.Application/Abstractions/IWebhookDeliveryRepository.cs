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

    Task<IReadOnlyList<WebhookDelivery>> GetByApiKeyAsync(
        Guid apiKeyId,
        int limit,
        CancellationToken ct = default);

    Task<WebhookDeliveryFailureMetrics> GetFailureMetricsAsync(
        Guid apiKeyId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<WebhookDeliveryRetryResult> RetryFailedAsync(
        Guid deliveryId,
        DateTimeOffset now,
        CancellationToken ct = default);
}

public sealed record WebhookDeliveryFailureInfo(
    int AttemptCount,
    int MaxAttempts,
    WebhookDeliveryStatus Status,
    DateTimeOffset NextAttemptAt);

public sealed record WebhookDeliveryFailureMetrics(
    int ConsecutiveFailures,
    int BacklogCount,
    int FailedLast24Hours,
    int CompletedLast24Hours,
    double FailureRate);

public sealed record WebhookDeliveryRetryResult(
    WebhookDeliveryRetryResultKind Kind,
    WebhookDelivery? Delivery);

public enum WebhookDeliveryRetryResultKind
{
    Success = 0,
    NotFound = 1,
    NotFailed = 2,
}
