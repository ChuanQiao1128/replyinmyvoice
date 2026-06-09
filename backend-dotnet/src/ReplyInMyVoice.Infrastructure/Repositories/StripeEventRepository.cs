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
        CancellationToken ct = default) =>
        await db.StripeEvents
            .AsTracking()
            .SingleOrDefaultAsync(x => x.EventId == eventId, ct);

    public async Task<StripeEvent?> BeginProcessingAsync(
        string eventId,
        string type,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var stripeEvent = await GetByEventIdAsync(eventId, ct);
        if (stripeEvent is null)
        {
            stripeEvent = new StripeEvent
            {
                EventId = eventId,
                Type = type,
                Status = StripeEventStatus.Processing,
                AttemptCount = 1,
                CreatedAt = now,
                LastAttemptAt = now,
                LockedUntil = now.AddMinutes(2),
            };
            await AddAsync(stripeEvent, ct);
            return stripeEvent;
        }

        if (stripeEvent.Status == StripeEventStatus.Processed)
        {
            return null;
        }

        if (stripeEvent.Status == StripeEventStatus.Processing && stripeEvent.LockedUntil > now)
        {
            return null;
        }

        stripeEvent.Type = type;
        stripeEvent.Status = StripeEventStatus.Processing;
        stripeEvent.AttemptCount += 1;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LockedUntil = now.AddMinutes(2);
        stripeEvent.LastError = null;
        stripeEvent.RowVersion = Guid.NewGuid();
        return stripeEvent;
    }

    public void MarkProcessed(StripeEvent stripeEvent, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Processed;
        stripeEvent.ProcessedAt = now;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastError = null;
        stripeEvent.RowVersion = Guid.NewGuid();
    }

    public void MarkFailed(StripeEvent stripeEvent, string error, DateTimeOffset now)
    {
        stripeEvent.Status = StripeEventStatus.Failed;
        stripeEvent.ProcessedAt = null;
        stripeEvent.LockedUntil = null;
        stripeEvent.LastAttemptAt = now;
        stripeEvent.LastError = error.Length > 1000 ? error[..1000] : error;
        stripeEvent.RowVersion = Guid.NewGuid();
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
