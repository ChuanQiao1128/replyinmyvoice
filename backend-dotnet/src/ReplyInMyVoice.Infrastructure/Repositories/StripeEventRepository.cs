using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class StripeEventRepository(AppDbContext db) : IStripeEventRepository
{
    public async Task AddAsync(StripeEvent stripeEvent, CancellationToken ct = default)
    {
        await db.StripeEvents.AddAsync(stripeEvent, ct);
    }

    public async Task<StripeEvent?> GetByEventIdAsync(
        string eventId,
        CancellationToken ct = default)
    {
        var local = db.StripeEvents.Local.FirstOrDefault(x => x.EventId == eventId);
        if (local is not null)
        {
            await db.Entry(local).ReloadAsync(ct);
            return db.Entry(local).State == EntityState.Detached ? null : local;
        }

        return await db.StripeEvents
            .AsTracking()
            .SingleOrDefaultAsync(x => x.EventId == eventId, ct);
    }

    public async Task<IReadOnlyList<StripeEvent>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        TimeSpan lease,
        string? eventId,
        CancellationToken ct = default)
    {
        List<StripeEvent> events;
        if (db.Database.IsSqlite())
        {
            var candidates = await db.StripeEvents
                .AsTracking()
                .Where(x =>
                    x.Status == StripeEventStatus.Pending ||
                    x.Status == StripeEventStatus.Processing)
                .ToListAsync(ct);

            events = candidates
                .Where(x => eventId is null || x.EventId == eventId)
                .Where(x =>
                    (x.Status == StripeEventStatus.Pending &&
                        (x.LockedUntil is null || x.LockedUntil <= now)) ||
                    (x.Status == StripeEventStatus.Processing &&
                        x.LockedUntil <= now))
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ToList();
        }
        else
        {
            var query = db.StripeEvents
                .AsTracking()
                .Where(x =>
                    (x.Status == StripeEventStatus.Pending &&
                        (x.LockedUntil == null || x.LockedUntil.Value <= now)) ||
                    (x.Status == StripeEventStatus.Processing &&
                        x.LockedUntil != null &&
                        x.LockedUntil.Value <= now));

            if (eventId is not null)
            {
                query = query.Where(x => x.EventId == eventId);
            }

            events = await query
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ToListAsync(ct);
        }

        foreach (var stripeEvent in events)
        {
            stripeEvent.Status = StripeEventStatus.Processing;
            stripeEvent.LockedUntil = now.Add(lease);
            stripeEvent.AttemptCount += 1;
            stripeEvent.LastAttemptAt = now;
        }

        return events;
    }

    public void MarkProcessed(StripeEvent stripeEvent, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Processed;
        stripeEvent.ProcessedAt = now;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastError = null;
        stripeEvent.PayloadJson = null;
    }

    public void MarkRetryScheduled(
        StripeEvent stripeEvent,
        string error,
        DateTimeOffset nextAttemptAt,
        DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Pending;
        stripeEvent.ProcessedAt = null;
        stripeEvent.LockedUntil = nextAttemptAt;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LastError = error.Length > 1000 ? error[..1000] : error;
    }

    public void MarkFailed(StripeEvent stripeEvent, string error, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Failed;
        stripeEvent.ProcessedAt = null;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LastError = error.Length > 1000 ? error[..1000] : error;
    }

    public bool IsDuplicateEventWriteFailure(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("StripeEvents.EventId", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("PK_StripeEvents", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("StripeEvents", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("EventId", StringComparison.OrdinalIgnoreCase));
    }
}
