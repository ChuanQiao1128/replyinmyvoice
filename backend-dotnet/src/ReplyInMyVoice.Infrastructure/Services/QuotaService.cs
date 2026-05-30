using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class QuotaService(Func<AppDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ReserveRewriteResult> ReserveAsync(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        string requestJson,
        string periodKey,
        int quotaLimit,
        DateTimeOffset now,
        TimeSpan reservationTtl,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            var existingAttempt = await db.RewriteAttempts
                .AsTracking()
                .SingleOrDefaultAsync(
                    x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existingAttempt is not null)
            {
                if (!string.Equals(existingAttempt.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    return ReserveRewriteResult.Conflict(existingAttempt.Id, existingAttempt.Status);
                }

                return new ReserveRewriteResult(
                    ReserveRewriteResultKind.Existing,
                    existingAttempt.Id,
                    existingAttempt.Status,
                    existingAttempt.ResultJson,
                    existingAttempt.ErrorCode);
            }

            var period = await db.UsagePeriods
                .AsTracking()
                .SingleOrDefaultAsync(
                    x => x.UserId == userId && x.PeriodKey == periodKey,
                    cancellationToken);

            if (period is null)
            {
                period = new UsagePeriod
                {
                    UserId = userId,
                    PeriodKey = periodKey,
                    QuotaLimit = quotaLimit,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.UsagePeriods.Add(period);
            }
            else
            {
                period.QuotaLimit = quotaLimit;
                period.UpdatedAt = now;
                period.RowVersion = Guid.NewGuid();
            }

            var attempt = new RewriteAttempt
            {
                UserId = userId,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                RequestJson = requestJson,
                Status = RewriteAttemptStatus.Pending,
                CreatedAt = now,
                ExpiresAt = now.Add(reservationTtl),
            };

            if (period.UsedCount + period.ReservedCount >= quotaLimit)
            {
                var credit = await FindUsableCreditAsync(db, userId, now, cancellationToken);
                if (credit is null)
                {
                    return ReserveRewriteResult.QuotaExceeded();
                }

                credit.AmountConsumed += 1;
                credit.RowVersion = Guid.NewGuid();

                db.RewriteAttempts.Add(attempt);
                db.UsageReservations.Add(new UsageReservation
                {
                    UserId = userId,
                    UsagePeriod = period,
                    RewriteAttempt = attempt,
                    RewriteCredit = credit,
                    Status = UsageReservationStatus.Pending,
                    CreatedAt = now,
                    ExpiresAt = now.Add(reservationTtl),
                });
                AddRewriteJobOutbox(db, attempt, now);

                await db.SaveChangesAsync(cancellationToken);

                return new ReserveRewriteResult(ReserveRewriteResultKind.Created, attempt.Id, attempt.Status);
            }

            var reservation = new UsageReservation
            {
                UserId = userId,
                UsagePeriod = period,
                RewriteAttempt = attempt,
                Status = UsageReservationStatus.Pending,
                CreatedAt = now,
                ExpiresAt = now.Add(reservationTtl),
            };

            period.ReservedCount += 1;
            period.RowVersion = Guid.NewGuid();
            db.RewriteAttempts.Add(attempt);
            db.UsageReservations.Add(reservation);
            AddRewriteJobOutbox(db, attempt, now);

            await db.SaveChangesAsync(cancellationToken);

            return new ReserveRewriteResult(ReserveRewriteResultKind.Created, attempt.Id, attempt.Status);
        }, cancellationToken);
    }

    public async Task FinalizeSuccessAsync(
        Guid attemptId,
        string resultJson,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async db =>
        {
            var attempt = await db.RewriteAttempts
                .AsTracking()
                .SingleAsync(x => x.Id == attemptId, cancellationToken);
            var reservation = await db.UsageReservations
                .AsTracking()
                .SingleAsync(x => x.RewriteAttemptId == attemptId, cancellationToken);
            UsagePeriod? period = null;
            if (reservation.RewriteCreditId is null)
            {
                period = await db.UsagePeriods
                    .AsTracking()
                    .SingleAsync(x => x.Id == reservation.UsagePeriodId, cancellationToken);
            }

            if (attempt.Status == RewriteAttemptStatus.Succeeded &&
                reservation.Status == UsageReservationStatus.Finalized)
            {
                return;
            }

            if (reservation.Status != UsageReservationStatus.Pending ||
                attempt.Status is RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired)
            {
                return;
            }

            if (reservation.Status == UsageReservationStatus.Pending)
            {
                if (reservation.RewriteCreditId is null)
                {
                    var usagePeriod = period!;
                    usagePeriod.ReservedCount = Math.Max(0, usagePeriod.ReservedCount - 1);
                    usagePeriod.UsedCount += 1;
                    usagePeriod.UpdatedAt = now;
                    usagePeriod.RowVersion = Guid.NewGuid();
                }

                reservation.Status = UsageReservationStatus.Finalized;
                reservation.FinalizedAt = now;
                reservation.RowVersion = Guid.NewGuid();
            }

            attempt.Status = RewriteAttemptStatus.Succeeded;
            attempt.ResultJson = resultJson;
            attempt.CompletedAt = now;
            attempt.RowVersion = Guid.NewGuid();

            await db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<bool> MarkProcessingAsync(
        Guid attemptId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            var attempt = await db.RewriteAttempts
                .AsTracking()
                .SingleAsync(x => x.Id == attemptId, cancellationToken);

            if (attempt.Status is not RewriteAttemptStatus.Pending)
            {
                return false;
            }

            attempt.Status = RewriteAttemptStatus.Processing;
            attempt.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task ReleaseAsync(
        Guid attemptId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async db =>
        {
            var attempt = await db.RewriteAttempts
                .AsTracking()
                .SingleAsync(x => x.Id == attemptId, cancellationToken);
            var reservation = await db.UsageReservations
                .AsTracking()
                .SingleAsync(x => x.RewriteAttemptId == attemptId, cancellationToken);

            if (reservation.Status == UsageReservationStatus.Pending)
            {
                if (reservation.RewriteCreditId is { } creditId)
                {
                    var credit = await db.RewriteCredits
                        .AsTracking()
                        .SingleAsync(x => x.Id == creditId, cancellationToken);
                    credit.AmountConsumed = Math.Max(0, credit.AmountConsumed - 1);
                    credit.RowVersion = Guid.NewGuid();
                }
                else
                {
                    var period = await db.UsagePeriods
                        .AsTracking()
                        .SingleAsync(x => x.Id == reservation.UsagePeriodId, cancellationToken);
                    period.ReservedCount = Math.Max(0, period.ReservedCount - 1);
                    period.UpdatedAt = now;
                    period.RowVersion = Guid.NewGuid();
                }

                reservation.Status = UsageReservationStatus.Released;
                reservation.ReleasedAt = now;
                reservation.RowVersion = Guid.NewGuid();
            }

            if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
            {
                attempt.Status = RewriteAttemptStatus.Failed;
                attempt.ErrorCode = errorCode;
                attempt.CompletedAt = now;
                attempt.RowVersion = Guid.NewGuid();
            }

            await db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ReleaseExpiredReservationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async db =>
        {
            var expiredCandidates = await db.UsageReservations
                .AsTracking()
                .Where(x => x.Status == UsageReservationStatus.Pending)
                .Include(x => x.RewriteAttempt)
                .Include(x => x.UsagePeriod)
                .Include(x => x.RewriteCredit)
                .ToListAsync(cancellationToken);
            var expiredReservations = expiredCandidates
                .Where(x =>
                    x.ExpiresAt <= now &&
                    x.RewriteAttempt!.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
                .ToList();

            foreach (var reservation in expiredReservations)
            {
                reservation.Status = UsageReservationStatus.Expired;
                reservation.ReleasedAt = now;
                reservation.RowVersion = Guid.NewGuid();
                if (reservation.RewriteCredit is not null)
                {
                    reservation.RewriteCredit.AmountConsumed = Math.Max(0, reservation.RewriteCredit.AmountConsumed - 1);
                    reservation.RewriteCredit.RowVersion = Guid.NewGuid();
                }
                else
                {
                    reservation.UsagePeriod!.ReservedCount = Math.Max(0, reservation.UsagePeriod.ReservedCount - 1);
                    reservation.UsagePeriod.UpdatedAt = now;
                    reservation.UsagePeriod.RowVersion = Guid.NewGuid();
                }

                if (reservation.RewriteAttempt!.Status is RewriteAttemptStatus.Pending)
                {
                    reservation.RewriteAttempt.Status = RewriteAttemptStatus.Expired;
                    reservation.RewriteAttempt.ErrorCode = "reservation_expired";
                    reservation.RewriteAttempt.CompletedAt = now;
                    reservation.RewriteAttempt.RowVersion = Guid.NewGuid();
                }
                else if (reservation.RewriteAttempt!.Status is RewriteAttemptStatus.Processing)
                {
                    reservation.RewriteAttempt.Status = RewriteAttemptStatus.Expired;
                    reservation.RewriteAttempt.ErrorCode = "processing_timed_out";
                    reservation.RewriteAttempt.CompletedAt = now;
                    reservation.RewriteAttempt.RowVersion = Guid.NewGuid();
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return expiredReservations.Count;
        }, cancellationToken);
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<AppDbContext, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var strategyDb = dbContextFactory();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var db = dbContextFactory();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var result = await operation(db);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private Task ExecuteInTransactionAsync(
        Func<AppDbContext, Task> operation,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(
            async db =>
            {
                await operation(db);
                return true;
            },
            cancellationToken);

    private static async Task<RewriteCredit?> FindUsableCreditAsync(
        AppDbContext db,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Materialize by user id, then filter/order in memory: SQLite (test DB)
        // cannot translate DateTimeOffset comparisons/ordering in SQL. Earliest-expiring
        // usable grant first (non-null expiry before null), then oldest grant.
        var userCredits = await db.RewriteCredits
            .AsTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return userCredits
            .Where(x => (x.ExpiresAt == null || x.ExpiresAt > now) && x.AmountGranted - x.AmountConsumed > 0)
            .OrderBy(x => x.ExpiresAt.HasValue ? 0 : 1)
            .ThenBy(x => x.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.GrantedAt)
            .FirstOrDefault();
    }

    private static void AddRewriteJobOutbox(
        AppDbContext db,
        RewriteAttempt attempt,
        DateTimeOffset now)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            MessageType = "RewriteJobCreated",
            PayloadJson = JsonSerializer.Serialize(new RewriteJobCreatedPayload(attempt.Id), JsonOptions),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            AttemptCount = 0,
            MaxAttempts = 10,
            CorrelationId = attempt.Id.ToString(),
        });
    }

    private sealed record RewriteJobCreatedPayload(Guid AttemptId);
}
