using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.ApiKey;

public sealed class GetWebhookDeliveryStatusHandler(
    IApiKeyRepository apiKeys,
    IWebhookDeliveryRepository webhookDeliveries)
{
    public async Task<GetWebhookDeliveryStatusResultDto?> HandleAsync(
        GetWebhookDeliveryStatusQuery query,
        CancellationToken ct = default)
    {
        var apiKey = await apiKeys.GetByIdForUserAsync(query.UserId, query.ApiKeyId, ct);
        if (apiKey is null || apiKey.RevokedAt is not null)
        {
            return null;
        }

        var limit = Math.Clamp(query.Limit, 1, 50);
        var deliveries = await webhookDeliveries.GetWebhookDeliveryStatusAsync(
            query.ApiKeyId,
            limit,
            ct);

        return new GetWebhookDeliveryStatusResultDto(
            deliveries,
            new WebhookDeliveryStatusSummaryDto(
                deliveries.Count,
                deliveries.Count(x => x.Status == WebhookDeliveryStatus.Pending),
                deliveries.Count(x => x.Status == WebhookDeliveryStatus.InProgress),
                deliveries.Count(x => x.Status == WebhookDeliveryStatus.Delivered),
                deliveries.Count(x => x.Status == WebhookDeliveryStatus.Failed)));
    }
}
