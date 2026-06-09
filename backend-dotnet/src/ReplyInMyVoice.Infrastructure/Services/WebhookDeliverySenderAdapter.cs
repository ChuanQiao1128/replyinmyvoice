using AppWebhookSendRequest = ReplyInMyVoice.Application.Abstractions.WebhookSendRequest;
using AppWebhookSendResult = ReplyInMyVoice.Application.Abstractions.WebhookSendResult;
using AppWebhookDeliverySender = ReplyInMyVoice.Application.Abstractions.IWebhookDeliverySender;
using LegacyWebhookDeliverySender = ReplyInMyVoice.Infrastructure.Services.IWebhookDeliverySender;
using LegacyWebhookSendRequest = ReplyInMyVoice.Infrastructure.Services.WebhookSendRequest;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class WebhookDeliverySenderAdapter(LegacyWebhookDeliverySender sender) : AppWebhookDeliverySender
{
    public async Task<AppWebhookSendResult> SendAsync(
        AppWebhookSendRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new LegacyWebhookSendRequest(
                request.Url,
                request.RawBody,
                request.Signature,
                request.Timestamp,
                request.DeliveryId,
                request.EventId),
            cancellationToken);

        return new AppWebhookSendResult(result.StatusCode);
    }
}
