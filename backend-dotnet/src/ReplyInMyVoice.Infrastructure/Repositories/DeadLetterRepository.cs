using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class DeadLetterRepository(AppDbContext db) : IDeadLetterRepository
{
    private const int MaxFailureReasonLength = 1000;

    public async Task RecordFailureAsync(
        DeadLetterEntityType entityType,
        string entityId,
        string reason,
        CancellationToken ct = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        var normalizedReason = NormalizeFailureReason(reason);
        var now = DateTimeOffset.UtcNow;
        var existing = await db.DeadLetterMessages
            .AsTracking()
            .SingleOrDefaultAsync(x =>
                x.EntityType == entityType &&
                x.EntityId == normalizedEntityId,
                ct);

        if (existing is not null)
        {
            existing.FailureReason = normalizedReason;
            existing.FailureCount += 1;
            existing.LastFailedAt = now;
            existing.UpdatedAt = now;
            existing.RowVersion = Guid.NewGuid();
            return;
        }

        await db.DeadLetterMessages.AddAsync(new DeadLetterMessage
        {
            EntityType = entityType,
            EntityId = normalizedEntityId,
            OutboxMessageId = entityType == DeadLetterEntityType.Outbox &&
                Guid.TryParse(normalizedEntityId, out var outboxMessageId)
                    ? outboxMessageId
                    : null,
            StripeEventId = entityType == DeadLetterEntityType.Stripe
                ? normalizedEntityId
                : null,
            FailureReason = normalizedReason,
            FailureCount = 1,
            FirstFailedAt = now,
            LastFailedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        }, ct);
    }

    public async Task<DeadLetterFailurePage> GetFailuresAsync(
        DeadLetterEntityType? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var query = db.DeadLetterMessages.AsNoTracking();
        if (entityType is not null)
        {
            query = query.Where(x => x.EntityType == entityType.Value);
        }

        var totalCount = await query.CountAsync(ct);
        List<DeadLetterFailureDto> items;
        if (db.Database.IsSqlite())
        {
            var failures = await query.ToListAsync(ct);
            items = failures
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDto)
                .ToList();
        }
        else
        {
            items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new DeadLetterFailureDto(
                    x.Id,
                    x.EntityType,
                    x.EntityId,
                    x.FailureReason,
                    x.FailureCount,
                    x.FirstFailedAt,
                    x.LastFailedAt,
                    x.CreatedAt))
                .ToListAsync(ct);
        }

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling((double)totalCount / pageSize);

        return new DeadLetterFailurePage(
            page,
            pageSize,
            totalCount,
            totalPages,
            items);
    }

    public async Task<DeadLetterRequeueResult> RequeueAsync(
        string entityId,
        DeadLetterEntityType entityType,
        CancellationToken ct = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        var existing = await db.DeadLetterMessages
            .AsTracking()
            .SingleOrDefaultAsync(x =>
                x.EntityType == entityType &&
                x.EntityId == normalizedEntityId,
                ct);

        if (existing is null)
        {
            return new DeadLetterRequeueResult(DeadLetterRequeueResultKind.NotFound, null);
        }

        db.DeadLetterMessages.Remove(existing);
        return new DeadLetterRequeueResult(DeadLetterRequeueResultKind.Success, existing.Id);
    }

    private static string NormalizeEntityId(string entityId) => entityId.Trim();

    private static DeadLetterFailureDto ToDto(DeadLetterMessage message) =>
        new(
            message.Id,
            message.EntityType,
            message.EntityId,
            message.FailureReason,
            message.FailureCount,
            message.FirstFailedAt,
            message.LastFailedAt,
            message.CreatedAt);

    private static string NormalizeFailureReason(string reason)
    {
        var normalized = string.IsNullOrWhiteSpace(reason)
            ? "Terminal failure"
            : reason.Trim();
        return normalized.Length > MaxFailureReasonLength
            ? normalized[..MaxFailureReasonLength]
            : normalized;
    }
}
