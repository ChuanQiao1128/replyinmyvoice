using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class WebhookDeliveryRepository(AppDbContext db) : IWebhookDeliveryRepository
{
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
}
