using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed record GetWebhookDeliveryStatusQuery(
    Guid UserId,
    Guid ApiKeyId,
    int Limit = 10);

public sealed record GetWebhookDeliveryStatusResultDto(
    IReadOnlyList<WebhookDeliveryStatusDto> Deliveries,
    WebhookDeliveryStatusSummaryDto Summary);

public sealed record WebhookDeliveryStatusSummaryDto(
    int Total,
    int Pending,
    int InProgress,
    int Delivered,
    int Failed);
