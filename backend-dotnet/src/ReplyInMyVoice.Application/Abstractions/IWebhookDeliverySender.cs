namespace ReplyInMyVoice.Application.Abstractions;

public sealed record WebhookSendRequest(
    string Url,
    string RawBody,
    string Signature,
    string Timestamp,
    Guid DeliveryId,
    Guid EventId);

public sealed record WebhookSendResult(int StatusCode)
{
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
}

public interface IWebhookDeliverySender
{
    Task<WebhookSendResult> SendAsync(
        WebhookSendRequest request,
        CancellationToken cancellationToken);
}
