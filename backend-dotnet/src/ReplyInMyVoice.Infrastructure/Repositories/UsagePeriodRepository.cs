using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class UsagePeriodRepository(AppDbContext db) : IUsagePeriodRepository
{
    public async Task AddAsync(UsagePeriod usagePeriod, CancellationToken ct = default)
    {
        await db.UsagePeriods.AddAsync(usagePeriod, ct);
    }

    public async Task<UsagePeriod?> GetByIdAsync(
        Guid usagePeriodId,
        CancellationToken ct = default) =>
        await db.UsagePeriods
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == usagePeriodId, ct);

    public async Task<UsagePeriod?> GetByUserIdAndPeriodKeyAsync(
        Guid userId,
        string periodKey,
        CancellationToken ct = default) =>
        await db.UsagePeriods
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.PeriodKey == periodKey,
                ct);

    public async Task<int> TryReserveSlotAsync(
        Guid usagePeriodId,
        int quotaLimit,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE UsagePeriods
            SET ReservedCount = ReservedCount + 1,
                QuotaLimit = {quotaLimit},
                UpdatedAt = {now},
                RowVersion = {rowVersion}
            WHERE Id = {usagePeriodId}
              AND UsedCount + ReservedCount < {quotaLimit}
            """,
            ct);
    }

    public async Task<int> RefreshQuotaLimitAsync(
        Guid usagePeriodId,
        int quotaLimit,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE UsagePeriods
            SET QuotaLimit = {quotaLimit},
                UpdatedAt = {now},
                RowVersion = {rowVersion}
            WHERE Id = {usagePeriodId}
            """,
            ct);
    }

    public async Task<int> FinalizeReservedSlotAsync(
        Guid usagePeriodId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE UsagePeriods
            SET ReservedCount = CASE WHEN ReservedCount > 0 THEN ReservedCount - 1 ELSE 0 END,
                UsedCount = UsedCount + 1,
                UpdatedAt = {now},
                RowVersion = {rowVersion}
            WHERE Id = {usagePeriodId}
            """,
            ct);
    }

    public async Task<int> ReleaseReservedSlotAsync(
        Guid usagePeriodId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE UsagePeriods
            SET ReservedCount = CASE WHEN ReservedCount > 0 THEN ReservedCount - 1 ELSE 0 END,
                UpdatedAt = {now},
                RowVersion = {rowVersion}
            WHERE Id = {usagePeriodId}
            """,
            ct);
    }

    public async Task<IReadOnlyList<UsagePeriod>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.UsagePeriods
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
}
