using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.WebhookOutbox;

public sealed class DispatchDueWebhooksHandler(
    IWebhookDeliveryRepository webhookDeliveries,
    IWebhookDeliverySender sender,
    IUnitOfWork unitOfWork,
    IBusinessMetrics? metrics = null)
{
    private const int ClaimRaceMaxAttempts = 5;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(45);
    private readonly IBusinessMetrics _metrics = metrics ?? NoOpBusinessMetrics.Instance;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<int> HandleAsync(
        DispatchDueWebhooksCommand command,
        CancellationToken ct = default)
    {
        var deliveries = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var claimed = await webhookDeliveries.ClaimDueAsync(
                    command.Now,
                    command.LockedBy,
                    command.BatchSize,
                    ClaimLease,
                    transactionCt);
                await unitOfWork.SaveChangesAsync(transactionCt);
                return claimed;
            },
            IsolationLevel.Serializable,
            ClaimRaceMaxAttempts,
            ct);

        var touchedApiKeyIds = new HashSet<Guid>();
        foreach (var delivery in deliveries)
        {
            touchedApiKeyIds.Add(delivery.ApiKeyId);
            try
            {
                var request = BuildRequest(delivery, command.Now);
                var result = await sender.SendAsync(request, ct);
                if (result.IsSuccessStatusCode)
                {
                    await webhookDeliveries.MarkDeliveredAsync(delivery.Id, command.Now, ct);
                    await unitOfWork.SaveChangesAsync(ct);
                }
                else
                {
                    await MarkFailedAttemptAsync(
                        delivery.Id,
                        delivery.ApiKeyId,
                        command.Now,
                        $"HTTP {result.StatusCode}",
                        ct);
                }
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                await MarkFailedAttemptAsync(delivery.Id, delivery.ApiKeyId, command.Now, ex.Message, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await MarkFailedAttemptAsync(delivery.Id, delivery.ApiKeyId, command.Now, ex.Message, ct);
            }
        }

        foreach (var apiKeyId in touchedApiKeyIds)
        {
            var failureMetrics = await webhookDeliveries.GetFailureMetricsAsync(apiKeyId, command.Now, ct);
            _metrics.Record(
                BusinessMetricNames.WebhookDeliveryBacklog,
                failureMetrics.BacklogCount,
                BusinessMetricDimensions.ApiKeyId,
                apiKeyId.ToString("D"));
        }

        return deliveries.Count;
    }

    private async Task MarkFailedAttemptAsync(
        Guid deliveryId,
        Guid apiKeyId,
        DateTimeOffset now,
        string error,
        CancellationToken ct)
    {
        var failure = await webhookDeliveries.MarkFailedAttemptAsync(deliveryId, now, error, ct);
        await unitOfWork.SaveChangesAsync(ct);
        if (failure.Status == WebhookDeliveryStatus.Failed)
        {
            _metrics.Record(
                BusinessMetricNames.WebhookDeliveryFailedTotal,
                1,
                BusinessMetricDimensions.ApiKeyId,
                apiKeyId.ToString("D"));
        }
    }

    private static WebhookSendRequest BuildRequest(WebhookDelivery delivery, DateTimeOffset now)
    {
        if (delivery.ApiKey is null ||
            string.IsNullOrWhiteSpace(delivery.ApiKey.WebhookSecret) ||
            delivery.RewriteAttempt is null)
        {
            throw new InvalidOperationException("Webhook delivery is missing required data.");
        }

        var rawBody = BuildRawBody(delivery.RewriteAttempt);
        var timestamp = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return new WebhookSendRequest(
            delivery.Url,
            rawBody,
            ComputeSignature(delivery.ApiKey.WebhookSecret, timestamp, rawBody),
            timestamp,
            delivery.Id,
            delivery.RewriteAttemptId);
    }

    private static string BuildRawBody(RewriteAttempt attempt)
    {
        if (attempt.Status == RewriteAttemptStatus.Succeeded &&
            TryReadSucceededResult(attempt.ResultJson, out var rewrittenText, out var draftSignal, out var rewriteSignal))
        {
            return JsonSerializer.Serialize(
                new WebhookPayload(
                    attempt.Id,
                    "succeeded",
                    rewrittenText,
                    new WebhookSignal(draftSignal, rewriteSignal),
                    null),
                JsonOptions);
        }

        var code = string.IsNullOrWhiteSpace(attempt.ErrorCode)
            ? "engine_unavailable"
            : attempt.ErrorCode;
        return JsonSerializer.Serialize(
            new WebhookPayload(
                attempt.Id,
                "failed",
                null,
                null,
                new WebhookError(code, FailureMessage(attempt))),
            JsonOptions);
    }

    private static bool TryReadSucceededResult(
        string? resultJson,
        out string rewrittenText,
        out decimal draftSignal,
        out decimal rewriteSignal)
    {
        rewrittenText = string.Empty;
        draftSignal = 0;
        rewriteSignal = 0;

        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(resultJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("rewrittenText", out var rewrittenTextElement) ||
                rewrittenTextElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(rewrittenTextElement.GetString()))
            {
                return false;
            }

            if (!root.TryGetProperty("naturalness", out var naturalness) ||
                naturalness.ValueKind != JsonValueKind.Object ||
                !TryGetDecimal(naturalness, "draftAiLikePercent", out draftSignal) ||
                !TryGetDecimal(naturalness, "rewriteAiLikePercent", out rewriteSignal))
            {
                return false;
            }

            rewrittenText = rewrittenTextElement.GetString()!;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetDecimal(JsonElement parent, string propertyName, out decimal value)
    {
        value = 0;
        return parent.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDecimal(out value);
    }

    private static string FailureMessage(RewriteAttempt attempt) =>
        attempt.Status == RewriteAttemptStatus.Expired
            ? "The rewrite did not finish in time. Please submit a new request."
            : "The rewrite could not be completed. Please try again.";

    private static string ComputeSignature(string signingValue, string timestamp, string rawBody)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingValue),
            Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private sealed record WebhookPayload(
        Guid Id,
        string Status,
        string? RewrittenText,
        WebhookSignal? Signal,
        WebhookError? Error);

    private sealed record WebhookSignal(decimal Draft, decimal Rewrite);

    private sealed record WebhookError(string Code, string Message);
}
