using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class OutboxMessageRepository(AppDbContext db) : IOutboxMessageRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await db.OutboxMessages.AddAsync(message, ct);
    }

    public async Task<OutboxMessage?> GetByIdAsync(
        Guid messageId,
        CancellationToken ct = default)
    {
        return await db.OutboxMessages
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == messageId, ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        string lockedBy,
        int batchSize,
        TimeSpan claimLease,
        CancellationToken ct = default)
    {
        List<OutboxMessage> messages;
        if (db.Database.IsSqlite())
        {
            var candidates = await db.OutboxMessages
                .AsTracking()
                .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing)
                .ToListAsync(ct);

            messages = candidates
                .Where(x => x.NextAttemptAt <= now && (x.LockedUntil is null || x.LockedUntil <= now))
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ToList();
        }
        else
        {
            messages = await db.OutboxMessages
                .AsTracking()
                .Where(x => x.NextAttemptAt <= now)
                .Where(x => x.LockedUntil == null || x.LockedUntil.Value <= now)
                .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing)
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ToListAsync(ct);
        }

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.LockedBy = lockedBy;
            message.LockedUntil = now.Add(claimLease);
            message.LastAttemptAt = now;
            message.RowVersion = Guid.NewGuid();
        }

        return messages;
    }

    public async Task<OutboxMessage?> ClaimByIdAsync(
        Guid messageId,
        DateTimeOffset now,
        string lockedBy,
        TimeSpan claimLease,
        CancellationToken ct = default)
    {
        var message = await db.OutboxMessages
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == messageId, ct);

        if (message is null ||
            (message.Status != OutboxMessageStatus.Pending && message.Status != OutboxMessageStatus.Processing) ||
            message.NextAttemptAt > now ||
            (message.LockedUntil is not null && message.LockedUntil > now))
        {
            return null;
        }

        message.Status = OutboxMessageStatus.Processing;
        message.LockedBy = lockedBy;
        message.LockedUntil = now.Add(claimLease);
        message.LastAttemptAt = now;
        message.RowVersion = Guid.NewGuid();

        return message;
    }

    public async Task MarkSentAsync(
        Guid messageId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var message = await db.OutboxMessages
            .AsTracking()
            .SingleAsync(x => x.Id == messageId, ct);

        message.Status = OutboxMessageStatus.Sent;
        message.SentAt = now;
        message.LastError = null;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.RowVersion = Guid.NewGuid();
    }

    public async Task<DateTimeOffset?> GetOldestIncompleteCreatedAtAsync(CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var createdAtValues = await db.OutboxMessages
                .AsNoTracking()
                .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing)
                .Select(x => x.CreatedAt)
                .ToListAsync(ct);

            return createdAtValues.Count == 0 ? null : createdAtValues.Min();
        }

        return await db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (DateTimeOffset?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OutboxMessageFailureInfo> MarkFailedAttemptAsync(
        Guid messageId,
        DateTimeOffset now,
        string error,
        CancellationToken ct = default)
    {
        var message = await db.OutboxMessages
            .AsTracking()
            .SingleAsync(x => x.Id == messageId, ct);

        var nextAttemptCount = message.AttemptCount + 1;
        message.AttemptCount = nextAttemptCount;
        message.LastError = error.Length > 1000 ? error[..1000] : error;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.RowVersion = Guid.NewGuid();

        if (nextAttemptCount >= message.MaxAttempts)
        {
            message.Status = OutboxMessageStatus.Failed;
        }
        else
        {
            message.Status = OutboxMessageStatus.Pending;
            var delaySeconds = Math.Min(300, Math.Pow(2, nextAttemptCount));
            message.NextAttemptAt = now.AddSeconds(delaySeconds);
        }

        return new OutboxMessageFailureInfo(
            message.AttemptCount,
            message.MaxAttempts,
            message.Status,
            message.NextAttemptAt);
    }
}
