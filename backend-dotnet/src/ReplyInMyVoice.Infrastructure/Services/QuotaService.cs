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
        await using var db = dbContextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var existingAttempt = await db.RewriteAttempts
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existingAttempt is not null)
        {
            await transaction.CommitAsync(cancellationToken);
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

        if (period.UsedCount + period.ReservedCount >= quotaLimit)
        {
            await transaction.CommitAsync(cancellationToken);
            return ReserveRewriteResult.QuotaExceeded();
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

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReserveRewriteResult(ReserveRewriteResultKind.Created, attempt.Id, attempt.Status);
    }

    public async Task FinalizeSuccessAsync(
        Guid attemptId,
        string resultJson,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var attempt = await db.RewriteAttempts
            .AsTracking()
            .SingleAsync(x => x.Id == attemptId, cancellationToken);
        var reservation = await db.UsageReservations
            .AsTracking()
            .SingleAsync(x => x.RewriteAttemptId == attemptId, cancellationToken);
        var period = await db.UsagePeriods
            .AsTracking()
            .SingleAsync(x => x.Id == reservation.UsagePeriodId, cancellationToken);

        if (attempt.Status == RewriteAttemptStatus.Succeeded ||
            reservation.Status == UsageReservationStatus.Finalized)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (reservation.Status == UsageReservationStatus.Pending)
        {
            period.ReservedCount = Math.Max(0, period.ReservedCount - 1);
            period.UsedCount += 1;
            period.UpdatedAt = now;
            period.RowVersion = Guid.NewGuid();
            reservation.Status = UsageReservationStatus.Finalized;
            reservation.FinalizedAt = now;
            reservation.RowVersion = Guid.NewGuid();
        }

        attempt.Status = RewriteAttemptStatus.Succeeded;
        attempt.ResultJson = resultJson;
        attempt.CompletedAt = now;
        attempt.RowVersion = Guid.NewGuid();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> MarkProcessingAsync(
        Guid attemptId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var attempt = await db.RewriteAttempts
            .AsTracking()
            .SingleAsync(x => x.Id == attemptId, cancellationToken);

        if (attempt.Status is not RewriteAttemptStatus.Pending)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        attempt.Status = RewriteAttemptStatus.Processing;
        attempt.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task ReleaseAsync(
        Guid attemptId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var attempt = await db.RewriteAttempts
            .AsTracking()
            .SingleAsync(x => x.Id == attemptId, cancellationToken);
        var reservation = await db.UsageReservations
            .AsTracking()
            .SingleAsync(x => x.RewriteAttemptId == attemptId, cancellationToken);
        var period = await db.UsagePeriods
            .AsTracking()
            .SingleAsync(x => x.Id == reservation.UsagePeriodId, cancellationToken);

        if (reservation.Status == UsageReservationStatus.Pending)
        {
            period.ReservedCount = Math.Max(0, period.ReservedCount - 1);
            period.UpdatedAt = now;
            period.RowVersion = Guid.NewGuid();
            reservation.Status = UsageReservationStatus.Released;
            reservation.ReleasedAt = now;
            reservation.RowVersion = Guid.NewGuid();
        }

        if (attempt.Status is not RewriteAttemptStatus.Succeeded)
        {
            attempt.Status = RewriteAttemptStatus.Failed;
            attempt.ErrorCode = errorCode;
            attempt.CompletedAt = now;
            attempt.RowVersion = Guid.NewGuid();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> ReleaseExpiredReservationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var expiredCandidates = await db.UsageReservations
            .AsTracking()
            .Include(x => x.RewriteAttempt)
            .Include(x => x.UsagePeriod)
            .ToListAsync(cancellationToken);
        var expiredReservations = expiredCandidates
            .Where(x => x.Status == UsageReservationStatus.Pending && x.ExpiresAt <= now)
            .ToList();

        foreach (var reservation in expiredReservations)
        {
            reservation.Status = UsageReservationStatus.Released;
            reservation.ReleasedAt = now;
            reservation.RowVersion = Guid.NewGuid();
            reservation.UsagePeriod!.ReservedCount = Math.Max(0, reservation.UsagePeriod.ReservedCount - 1);
            reservation.UsagePeriod.UpdatedAt = now;
            reservation.UsagePeriod.RowVersion = Guid.NewGuid();

            if (reservation.RewriteAttempt!.Status is not RewriteAttemptStatus.Succeeded)
            {
                reservation.RewriteAttempt.Status = RewriteAttemptStatus.Expired;
                reservation.RewriteAttempt.ErrorCode = "reservation_expired";
                reservation.RewriteAttempt.CompletedAt = now;
                reservation.RewriteAttempt.RowVersion = Guid.NewGuid();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return expiredReservations.Count;
    }

    private sealed record RewriteJobCreatedPayload(Guid AttemptId);
}
