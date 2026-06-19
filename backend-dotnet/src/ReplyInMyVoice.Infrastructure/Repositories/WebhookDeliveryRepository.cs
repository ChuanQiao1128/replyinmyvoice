using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class WebhookDeliveryRepository(AppDbContext db) : IWebhookDeliveryRepository
{
    private const int MetricsWindowHours = 24;

    public async Task<IReadOnlyList<WebhookDelivery>> ClaimDueAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        TimeSpan claimLease,
        CancellationToken ct = default)
    {
        List<WebhookDelivery> deliveries;
        var query = db.WebhookDeliveries
            .IgnoreQueryFilters()
            .AsTracking()
            .Include(x => x.ApiKey)
            .Include(x => x.RewriteAttempt)
            .Where(x => x.Status == WebhookDeliveryStatus.Pending || x.Status == WebhookDeliveryStatus.InProgress);

        if (db.Database.IsSqlite())
        {
            var candidates = await query.ToListAsync(ct);
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
                .ToListAsync(ct);
        }

        foreach (var delivery in deliveries)
        {
            delivery.Status = WebhookDeliveryStatus.InProgress;
            delivery.LockedBy = lockedBy;
            delivery.LockedUntil = now.Add(claimLease);
            delivery.LastAttemptAt = now;
            delivery.RowVersion = Guid.NewGuid();
        }

        return deliveries;
    }

    public async Task MarkDeliveredAsync(
        Guid deliveryId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var delivery = await db.WebhookDeliveries
            .AsTracking()
            .SingleAsync(x => x.Id == deliveryId, ct);

        delivery.Status = WebhookDeliveryStatus.Delivered;
        delivery.AttemptCount += 1;
        delivery.DeliveredAt = now;
        delivery.LastError = null;
        delivery.LockedBy = null;
        delivery.LockedUntil = null;
        delivery.RowVersion = Guid.NewGuid();
    }

    public async Task<WebhookDeliveryFailureInfo> MarkFailedAttemptAsync(
        Guid deliveryId,
        DateTimeOffset now,
        string error,
        CancellationToken ct = default)
    {
        var delivery = await db.WebhookDeliveries
            .AsTracking()
            .SingleAsync(x => x.Id == deliveryId, ct);

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

        return new WebhookDeliveryFailureInfo(
            delivery.AttemptCount,
            delivery.MaxAttempts,
            delivery.Status,
            delivery.NextAttemptAt);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetByApiKeyAsync(
        Guid apiKeyId,
        int limit,
        CancellationToken ct = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var query = db.WebhookDeliveries
            .AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId);

        if (db.Database.IsSqlite())
        {
            var deliveries = await query.ToListAsync(ct);
            return deliveries
                .OrderByDescending(x => x.CreatedAt)
                .Take(boundedLimit)
                .ToList();
        }

        return await db.WebhookDeliveries
            .AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(boundedLimit)
            .ToListAsync(ct);
    }

    public async Task<WebhookDeliveryFailureMetrics> GetFailureMetricsAsync(
        Guid apiKeyId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var deliveries = await db.WebhookDeliveries
            .AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId)
            .Select(x => new DeliveryMetricRow(
                x.Status,
                x.CreatedAt,
                x.LastAttemptAt,
                x.DeliveredAt))
            .ToListAsync(ct);

        var terminalDeliveries = deliveries
            .Where(x => IsTerminal(x.Status))
            .OrderByDescending(GetActivityAt)
            .ToArray();
        var consecutiveFailures = terminalDeliveries
            .TakeWhile(x => x.Status == WebhookDeliveryStatus.Failed)
            .Count();

        var windowStart = now.AddHours(-MetricsWindowHours);
        var completedInWindow = terminalDeliveries
            .Where(x =>
            {
                var activityAt = GetActivityAt(x);
                return activityAt >= windowStart && activityAt <= now;
            })
            .ToArray();
        var failedInWindow = completedInWindow
            .Count(x => x.Status == WebhookDeliveryStatus.Failed);
        var completedCount = completedInWindow.Length;

        return new WebhookDeliveryFailureMetrics(
            consecutiveFailures,
            deliveries.Count(x => x.Status is WebhookDeliveryStatus.Pending or WebhookDeliveryStatus.InProgress),
            failedInWindow,
            completedCount,
            completedCount == 0 ? 0 : (double)failedInWindow / completedCount);
    }

    public async Task<WebhookDeliveryRetryResult> RetryFailedAsync(
        Guid deliveryId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var delivery = await db.WebhookDeliveries
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == deliveryId, ct);

        if (delivery is null)
        {
            return new WebhookDeliveryRetryResult(WebhookDeliveryRetryResultKind.NotFound, null);
        }

        if (delivery.Status != WebhookDeliveryStatus.Failed)
        {
            return new WebhookDeliveryRetryResult(WebhookDeliveryRetryResultKind.NotFailed, delivery);
        }

        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.AttemptCount = 0;
        delivery.NextAttemptAt = now;
        delivery.LockedBy = null;
        delivery.LockedUntil = null;
        delivery.LastError = null;
        delivery.RowVersion = Guid.NewGuid();

        return new WebhookDeliveryRetryResult(WebhookDeliveryRetryResultKind.Success, delivery);
    }

    private static bool IsTerminal(WebhookDeliveryStatus status) =>
        status is WebhookDeliveryStatus.Delivered or WebhookDeliveryStatus.Failed;

    private static DateTimeOffset GetActivityAt(DeliveryMetricRow row) =>
        row.DeliveredAt ?? row.LastAttemptAt ?? row.CreatedAt;

    private sealed record DeliveryMetricRow(
        WebhookDeliveryStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset? DeliveredAt);
}
