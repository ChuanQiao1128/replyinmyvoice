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
}

public sealed record WebhookDeliveryFailureInfo(
    int AttemptCount,
    int MaxAttempts,
    WebhookDeliveryStatus Status,
    DateTimeOffset NextAttemptAt);
