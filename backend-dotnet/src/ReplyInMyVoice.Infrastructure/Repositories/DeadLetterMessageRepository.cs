using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class DeadLetterMessageRepository(AppDbContext db) : IDeadLetterMessageRepository
{
    public async Task AddAsync(
        DeadLetterMessage message,
        CancellationToken ct = default)
    {
        var hasLocalActiveDuplicate = db.DeadLetterMessages.Local.Any(x =>
            string.Equals(x.SourceType, message.SourceType, StringComparison.Ordinal) &&
            string.Equals(x.SourceId, message.SourceId, StringComparison.Ordinal) &&
            x.RequeuedAt is null);
        if (hasLocalActiveDuplicate)
        {
            return;
        }

        var hasActiveDuplicate = await db.DeadLetterMessages
            .AsNoTracking()
            .AnyAsync(x =>
                x.SourceType == message.SourceType &&
                x.SourceId == message.SourceId &&
                x.RequeuedAt == null,
                ct);
        if (hasActiveDuplicate)
        {
            return;
        }

        await db.DeadLetterMessages.AddAsync(message, ct);
    }

    public async Task<DeadLetterMessagePage> GetPagedAsync(
        int page,
        int pageSize,
        string? sourceType,
        CancellationToken ct = default)
    {
        var query = db.DeadLetterMessages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            query = query.Where(x => x.SourceType == sourceType);
        }

        if (db.Database.IsSqlite())
        {
            var rows = await query.ToListAsync(ct);
            var sqliteItems = rows
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new DeadLetterMessagePage(page, pageSize, rows.Count, sqliteItems);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new DeadLetterMessagePage(page, pageSize, totalCount, items);
    }

    public async Task<DeadLetterMessage?> GetByIdAsync(
        Guid id,
        bool track = false,
        CancellationToken ct = default)
    {
        var query = track
            ? db.DeadLetterMessages.AsTracking()
            : db.DeadLetterMessages.AsNoTracking();

        return await query.SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<bool> UpdateRequeuedAtAsync(
        Guid id,
        DateTimeOffset requeuedAt,
        CancellationToken ct = default)
    {
        var local = db.DeadLetterMessages.Local.FirstOrDefault(x => x.Id == id);
        var message = local ?? await db.DeadLetterMessages
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (message is null || message.RequeuedAt is not null)
        {
            return false;
        }

        message.RequeuedAt = requeuedAt;
        return true;
    }
}
