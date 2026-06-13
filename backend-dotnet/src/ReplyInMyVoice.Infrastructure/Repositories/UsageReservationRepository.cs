using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class UsageReservationRepository(AppDbContext db) : IUsageReservationRepository
{
    public async Task AddAsync(UsageReservation reservation, CancellationToken ct = default)
    {
        await db.UsageReservations.AddAsync(reservation, ct);
    }

    public async Task<UsageReservation?> GetByAttemptIdAsync(
        Guid attemptId,
        CancellationToken ct = default) =>
        await db.UsageReservations
            .AsTracking()
            .SingleOrDefaultAsync(x => x.RewriteAttemptId == attemptId, ct);

    public async Task<int> TryTransitionFromPendingAsync(
        Guid reservationId,
        UsageReservationStatus targetStatus,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        const string pending = nameof(UsageReservationStatus.Pending);

        return targetStatus switch
        {
            UsageReservationStatus.Finalized => await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE UsageReservations
                SET Status = {nameof(UsageReservationStatus.Finalized)},
                    FinalizedAt = {now},
                    RowVersion = {rowVersion}
                WHERE Id = {reservationId}
                  AND Status = {pending}
                """,
                ct),
            UsageReservationStatus.Released => await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE UsageReservations
                SET Status = {nameof(UsageReservationStatus.Released)},
                    ReleasedAt = {now},
                    RowVersion = {rowVersion}
                WHERE Id = {reservationId}
                  AND Status = {pending}
                """,
                ct),
            UsageReservationStatus.Expired => await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE UsageReservations
                SET Status = {nameof(UsageReservationStatus.Expired)},
                    ReleasedAt = {now},
                    RowVersion = {rowVersion}
                WHERE Id = {reservationId}
                  AND Status = {pending}
                """,
                ct),
            _ => throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, "Unsupported reservation transition target."),
        };
    }

    public async Task<int> ReleaseClaimedCounterAsync(
        Guid reservationId,
        Guid usagePeriodId,
        Guid? rewriteCreditId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var rowVersion = Guid.NewGuid();
        if (rewriteCreditId is { } creditId)
        {
            return await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE RewriteCredits
                SET AmountConsumed = CASE WHEN AmountConsumed > 0 THEN AmountConsumed - 1 ELSE 0 END,
                    RowVersion = {rowVersion}
                WHERE Id = {creditId}
                  AND EXISTS (
                    SELECT 1
                    FROM UsageReservations
                    WHERE Id = {reservationId}
                      AND RewriteCreditId = {creditId}
                  )
                """,
                ct);
        }

        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE UsagePeriods
            SET ReservedCount = CASE WHEN ReservedCount > 0 THEN ReservedCount - 1 ELSE 0 END,
                UpdatedAt = {now},
                RowVersion = {rowVersion}
            WHERE Id = {usagePeriodId}
              AND EXISTS (
                SELECT 1
                FROM UsageReservations
                WHERE Id = {reservationId}
                  AND UsagePeriodId = {usagePeriodId}
                  AND RewriteCreditId IS NULL
              )
            """,
            ct);
    }

    public async Task<IReadOnlyList<UsageReservation>> ListExpiredPendingBatchAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default)
    {
        var query = db.UsageReservations
            .AsTracking()
            .Include(x => x.RewriteAttempt)
            .Include(x => x.UsagePeriod)
            .Include(x => x.RewriteCredit);

        if (db.Database.IsSqlite())
        {
            var expiredCandidates = await query
                .Where(x => x.Status == Domain.Enums.UsageReservationStatus.Pending)
                .ToListAsync(ct);

            return expiredCandidates
                .Where(x =>
                    x.ExpiresAt <= now &&
                    x.RewriteAttempt!.Status is
                        Domain.Enums.RewriteAttemptStatus.Pending or
                        Domain.Enums.RewriteAttemptStatus.Processing)
                .OrderBy(x => x.ExpiresAt)
                .ThenBy(x => x.Id)
                .Take(batchSize)
                .ToList();
        }

        return await query
            .Where(x =>
                x.Status == Domain.Enums.UsageReservationStatus.Pending &&
                x.ExpiresAt <= now &&
                (x.RewriteAttempt!.Status == Domain.Enums.RewriteAttemptStatus.Pending ||
                    x.RewriteAttempt.Status == Domain.Enums.RewriteAttemptStatus.Processing))
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UsageReservation>> ListByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await db.UsageReservations
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
}
