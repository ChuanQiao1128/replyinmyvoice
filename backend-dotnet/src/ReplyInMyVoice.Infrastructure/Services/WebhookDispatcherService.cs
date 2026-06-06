using System.Data;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed record WebhookSendRequest(
    string Url,
    string RawBody,
    string Signature);

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
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.Url)
        {
            Content = new StringContent(request.RawBody, Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Add("X-RIMV-Signature", request.Signature);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        return new WebhookSendResult((int)response.StatusCode);
    }
}

public sealed class WebhookDispatcherService(
    Func<AppDbContext> dbContextFactory,
    IWebhookDeliverySender sender,
    ILogger<WebhookDispatcherService>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<int> DispatchDueAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var deliveries = await ClaimDueDeliveriesAsync(now, lockedBy, batchSize, cancellationToken);
        foreach (var delivery in deliveries)
        {
            try
            {
                var request = BuildRequest(delivery);
                var result = await sender.SendAsync(request, cancellationToken);
                if (result.IsSuccessStatusCode)
                {
                    await MarkDeliveredAsync(delivery.Id, now, cancellationToken);
                    logger?.LogInformation(
                        "Webhook delivery posted. DeliveryId: {WebhookDeliveryId}. RewriteAttemptId: {RewriteAttemptId}.",
                        delivery.Id,
                        delivery.RewriteAttemptId);
                }
                else
                {
                    await MarkFailedAttemptAsync(
                        delivery.Id,
                        now,
                        $"HTTP {result.StatusCode}",
                        cancellationToken);
                }
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                await MarkFailedAttemptAsync(delivery.Id, now, ex.Message, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await MarkFailedAttemptAsync(delivery.Id, now, ex.Message, cancellationToken);
                logger?.LogWarning(
                    ex,
                    "Webhook delivery failed. DeliveryId: {WebhookDeliveryId}. RewriteAttemptId: {RewriteAttemptId}.",
                    delivery.Id,
                    delivery.RewriteAttemptId);
            }
        }

        return deliveries.Count;
    }

    private async Task<List<WebhookDelivery>> ClaimDueDeliveriesAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            List<WebhookDelivery> deliveries;
            var query = db.WebhookDeliveries
                .AsTracking()
                .Include(x => x.ApiKey)
                .Include(x => x.RewriteAttempt)
                .Where(x => x.Status == WebhookDeliveryStatus.Pending);

            if (db.Database.IsSqlite())
            {
                var candidates = await query.ToListAsync(cancellationToken);
                deliveries = candidates
                    .Where(x => x.NextAttemptAt <= now && (x.LockedUntil is null || x.LockedUntil <= now))
                    .OrderBy(x => x.CreatedAt)
                    .Take(batchSize)
                    .ToList();
            }
            else
            {
                deliveries = await query
                    .Where(x => x.NextAttemptAt <= now)
                    .Where(x => x.LockedUntil == null || x.LockedUntil.Value <= now)
                    .OrderBy(x => x.CreatedAt)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
            }

            foreach (var delivery in deliveries)
            {
                delivery.LockedBy = lockedBy;
                delivery.LockedUntil = now.AddSeconds(30);
                delivery.LastAttemptAt = now;
                delivery.RowVersion = Guid.NewGuid();
            }

            await db.SaveChangesAsync(cancellationToken);
            return deliveries;
        }, cancellationToken);
    }

    private WebhookSendRequest BuildRequest(WebhookDelivery delivery)
    {
        if (delivery.ApiKey is null ||
            string.IsNullOrWhiteSpace(delivery.ApiKey.WebhookSecret) ||
            delivery.RewriteAttempt is null)
        {
            throw new InvalidOperationException("Webhook delivery is missing required data.");
        }

        var rawBody = BuildRawBody(delivery.RewriteAttempt);
        return new WebhookSendRequest(
            delivery.Url,
            rawBody,
            ComputeSignature(delivery.ApiKey.WebhookSecret, rawBody));
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

    private static string ComputeSignature(string signingValue, string rawBody)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingValue),
            Encoding.UTF8.GetBytes(rawBody));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private async Task MarkDeliveredAsync(
        Guid deliveryId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var delivery = await db.WebhookDeliveries
            .AsTracking()
            .SingleAsync(x => x.Id == deliveryId, cancellationToken);

        delivery.Status = WebhookDeliveryStatus.Delivered;
        delivery.AttemptCount += 1;
        delivery.DeliveredAt = now;
        delivery.LastError = null;
        delivery.LockedBy = null;
        delivery.LockedUntil = null;
        delivery.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAttemptAsync(
        Guid deliveryId,
        DateTimeOffset now,
        string error,
        CancellationToken cancellationToken)
    {
        await using var db = dbContextFactory();
        var delivery = await db.WebhookDeliveries
            .AsTracking()
            .SingleAsync(x => x.Id == deliveryId, cancellationToken);

        var nextAttemptCount = delivery.AttemptCount + 1;
        delivery.AttemptCount = nextAttemptCount;
        delivery.LastError = error.Length > 1000 ? error[..1000] : error;
        delivery.LockedBy = null;
        delivery.LockedUntil = null;
        delivery.RowVersion = Guid.NewGuid();

        if (nextAttemptCount >= delivery.MaxAttempts)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
        }
        else
        {
            delivery.Status = WebhookDeliveryStatus.Pending;
            var delaySeconds = Math.Min(300, Math.Pow(2, nextAttemptCount));
            delivery.NextAttemptAt = now.AddSeconds(delaySeconds);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<AppDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var db = dbContextFactory();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var result = await operation(db);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
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
