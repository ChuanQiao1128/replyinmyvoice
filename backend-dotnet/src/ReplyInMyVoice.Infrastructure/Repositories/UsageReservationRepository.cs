using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
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
