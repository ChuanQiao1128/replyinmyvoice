using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class WebhookStatusHttpFunction(
    AppDbContext db,
    IWebhookDeliveryRepository webhookDeliveries)
{
    private const int RecentDeliveryLimit = 25;

    [Function("GetWebhookDeliveryStatus")]
    public async Task<IActionResult> GetWebhookDeliveryStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "webhook/status")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var auth = await ApiKeyAuthResolver.ResolveAsync(
            request,
            db,
            now,
            cancellationToken);

        if (auth.ApiKeyId is null)
        {
            return FunctionHttpResults.Problem(
                "A valid API key is required.",
                "A valid API key is required.",
                StatusCodes.Status401Unauthorized,
                "invalid_key");
        }

        var metrics = await webhookDeliveries.GetFailureMetricsAsync(
            auth.ApiKeyId.Value,
            now,
            cancellationToken);
        var deliveries = await webhookDeliveries.GetByApiKeyAsync(
            auth.ApiKeyId.Value,
            RecentDeliveryLimit,
            cancellationToken);

        return new OkObjectResult(
            new WebhookDeliveryStatusResponse(
                auth.ApiKeyId.Value,
                ToResponse(metrics),
                deliveries.Select(ToResponse).ToArray()));
    }

    private static WebhookDeliveryFailureMetricsResponse ToResponse(
        WebhookDeliveryFailureMetrics metrics) =>
        new(
            metrics.ConsecutiveFailures,
            metrics.BacklogCount,
            metrics.FailedLast24Hours,
            metrics.CompletedLast24Hours,
            metrics.FailureRate);

    private static WebhookDeliveryStatusItem ToResponse(WebhookDelivery delivery) =>
        new(
            delivery.Id,
            delivery.Status.ToString(),
            delivery.AttemptCount,
            delivery.MaxAttempts,
            delivery.CreatedAt,
            delivery.NextAttemptAt,
            delivery.LastAttemptAt,
            delivery.DeliveredAt,
            delivery.LastError);
}

public sealed record WebhookDeliveryStatusResponse(
    Guid ApiKeyId,
    WebhookDeliveryFailureMetricsResponse Metrics,
    IReadOnlyList<WebhookDeliveryStatusItem> Deliveries);

public sealed record WebhookDeliveryFailureMetricsResponse(
    int ConsecutiveFailures,
    int BacklogCount,
    int FailedLast24Hours,
    int CompletedLast24Hours,
    double FailureRate);

public sealed record WebhookDeliveryStatusItem(
    Guid Id,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? DeliveredAt,
    string? LastError);
