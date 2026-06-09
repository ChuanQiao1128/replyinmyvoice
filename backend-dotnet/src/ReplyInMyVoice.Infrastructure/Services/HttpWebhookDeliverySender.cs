using System.Net.Http.Headers;
using System.Text;

namespace ReplyInMyVoice.Infrastructure.Services;

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

public sealed class HttpWebhookDeliverySender(HttpClient httpClient) : IWebhookDeliverySender
{
    public async Task<WebhookSendResult> SendAsync(
        WebhookSendRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiKeyWebhookUrl.TryNormalizeWebhookUrl(request.Url, out var normalizedUrl))
        {
            throw new InvalidOperationException("Webhook URL is not allowed.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, normalizedUrl)
        {
            Content = new StringContent(request.RawBody, Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Add("X-RIMV-Signature", request.Signature);
        httpRequest.Headers.Add("X-RIMV-Timestamp", request.Timestamp);
        httpRequest.Headers.Add("X-RIMV-Delivery-Id", request.DeliveryId.ToString("D"));
        httpRequest.Headers.Add("X-RIMV-Event-Id", request.EventId.ToString("D"));
        httpRequest.Headers.Add("Idempotency-Key", request.DeliveryId.ToString("D"));
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        return new WebhookSendResult((int)response.StatusCode);
    }
}
