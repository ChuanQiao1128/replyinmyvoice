using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class QuotaService(
    Func<AppDbContext> dbContextFactory,
    ILogger<QuotaService>? logger = null,
    IWebhookDeliveryEnqueuer? webhookDeliveryEnqueuer = null)
{
    private const int ExpiredReservationSweepBatchSize = 500;
    private const int ReservationRaceMaxAttempts = 3;

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
        for (var attempt = 1; attempt <= ReservationRaceMaxAttempts; attempt++)
        {
            try
            {
                return await ReserveCoreAsync(
                    userId,
                    idempotencyKey,
                    requestHash,
                    requestJson,
                    periodKey,
                    quotaLimit,
                    now,
                    reservationTtl,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < ReservationRaceMaxAttempts)
            {
                await DelayReservationRaceRetryAsync(attempt, cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < ReservationRaceMaxAttempts && IsReservationRaceException(ex))
            {
                await DelayReservationRaceRetryAsync(attempt, cancellationToken);
            }
            catch (SqliteException ex) when (attempt < ReservationRaceMaxAttempts && IsDatabaseLocked(ex))
            {
                await DelayReservationRaceRetryAsync(attempt, cancellationToken);
            }
        }

        return await ReserveCoreAsync(
            userId,
            idempotencyKey,
            requestHash,
            requestJson,
            periodKey,
            quotaLimit,
            now,
            reservationTtl,
            cancellationToken);
    }

    private async Task<ReserveRewriteResult> ReserveCoreAsync(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        string requestJson,
        string periodKey,
        int quotaLimit,
        DateTimeOffset now,
        TimeSpan reservationTtl,
        CancellationToken cancellationToken)
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
        var transitioned = await ExecuteInTransactionAsync(async db =>
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
                return false;
            }

            if (reservation.Status != UsageReservationStatus.Pending ||
                attempt.Status is RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired)
            {
                return false;
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
            return true;
        }, cancellationToken);

        if (transitioned)
        {
            await TryEnqueueWebhookDeliveryAsync(attemptId, now, cancellationToken);
        }
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
        var releaseLog = await ExecuteInTransactionAsync(async db =>
        {
            var attempt = await db.RewriteAttempts
                .AsTracking()
                .SingleAsync(x => x.Id == attemptId, cancellationToken);
            var reservation = await db.UsageReservations
                .AsTracking()
                .SingleAsync(x => x.RewriteAttemptId == attemptId, cancellationToken);
            var reservationSource = reservation.RewriteCreditId is null ? "period" : "credit";
            var reservationReleased = false;
            var attemptTransitioned = false;

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
                reservationReleased = true;
            }

            if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
            {
                attempt.Status = RewriteAttemptStatus.Failed;
                attempt.ErrorCode = errorCode;
                attempt.CompletedAt = now;
                attempt.RowVersion = Guid.NewGuid();
                attemptTransitioned = true;
            }

            await db.SaveChangesAsync(cancellationToken);

            return new ReservationReleaseLogEntry(
                attemptId,
                errorCode,
                reservationReleased,
                attemptTransitioned,
                reservationSource);
        }, cancellationToken);

        LogReservationRelease(releaseLog);
        if (releaseLog.AttemptTransitioned)
        {
            await TryEnqueueWebhookDeliveryAsync(attemptId, now, cancellationToken);
        }
    }

    public async Task<int> ReleaseExpiredReservationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await ReleaseExpiredReservationsAsync(now, ExpiredReservationSweepBatchSize, cancellationToken);

    public async Task<int> ReleaseExpiredReservationsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        var releasedCount = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var releaseLogs = await ExecuteInTransactionAsync(async db =>
            {
                var expiredReservations = await LoadExpiredReservationBatchAsync(db, now, batchSize, cancellationToken);
                var batchReleaseLogs = new List<ReservationReleaseLogEntry>(expiredReservations.Count);

                foreach (var reservation in expiredReservations)
                {
                    var reservationSource = reservation.RewriteCreditId is null ? "period" : "credit";
                    var reason = reservation.RewriteAttempt!.Status is RewriteAttemptStatus.Processing
                        ? "processing_timed_out"
                        : "reservation_expired";

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

                    reservation.RewriteAttempt.Status = RewriteAttemptStatus.Expired;
                    reservation.RewriteAttempt.ErrorCode = reason;
                    reservation.RewriteAttempt.CompletedAt = now;
                    reservation.RewriteAttempt.RowVersion = Guid.NewGuid();

                    batchReleaseLogs.Add(new ReservationReleaseLogEntry(
                        reservation.RewriteAttempt.Id,
                        reason,
                        ReservationReleased: true,
                        AttemptTransitioned: true,
                        reservationSource));
                }

                await db.SaveChangesAsync(cancellationToken);
                return batchReleaseLogs;
            }, cancellationToken);

            foreach (var releaseLog in releaseLogs)
            {
                LogReservationRelease(releaseLog);
                if (releaseLog.AttemptTransitioned)
                {
                    await TryEnqueueWebhookDeliveryAsync(releaseLog.AttemptId, now, cancellationToken);
                }
            }

            releasedCount += releaseLogs.Count;
            if (releaseLogs.Count < batchSize)
            {
                return releasedCount;
            }
        }
    }

    private static async Task<List<UsageReservation>> LoadExpiredReservationBatchAsync(
        AppDbContext db,
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var query = db.UsageReservations
            .AsTracking()
            .Include(x => x.RewriteAttempt)
            .Include(x => x.UsagePeriod)
            .Include(x => x.RewriteCredit);

        if (db.Database.IsSqlite())
        {
            var expiredCandidates = await query
                .Where(x => x.Status == UsageReservationStatus.Pending)
                .ToListAsync(cancellationToken);

            return expiredCandidates
                .Where(x =>
                    x.ExpiresAt <= now &&
                    x.RewriteAttempt!.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
                .OrderBy(x => x.ExpiresAt)
                .ThenBy(x => x.Id)
                .Take(batchSize)
                .ToList();
        }

        return await query
            .Where(x =>
                x.Status == UsageReservationStatus.Pending &&
                x.ExpiresAt <= now &&
                (x.RewriteAttempt!.Status == RewriteAttemptStatus.Pending ||
                    x.RewriteAttempt.Status == RewriteAttemptStatus.Processing))
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    private void LogReservationRelease(ReservationReleaseLogEntry releaseLog)
    {
        using var scope = logger?.BeginScope(new Dictionary<string, object>
        {
            ["attemptId"] = releaseLog.AttemptId,
        });

        logger?.LogInformation(
            "Rewrite reservation released for attempt {AttemptId} with reason {ReleaseReason}. ReservationReleased: {ReservationReleased}. AttemptTransitioned: {AttemptTransitioned}. ReservationSource: {ReservationSource}.",
            releaseLog.AttemptId,
            releaseLog.Reason,
            releaseLog.ReservationReleased,
            releaseLog.AttemptTransitioned,
            releaseLog.ReservationSource);
    }

    private async Task TryEnqueueWebhookDeliveryAsync(
        Guid attemptId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (webhookDeliveryEnqueuer is null)
        {
            return;
        }

        try
        {
            await webhookDeliveryEnqueuer.EnqueueForTerminalAttemptAsync(
                attemptId,
                now,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(
                ex,
                "Webhook delivery enqueue failed for attempt {AttemptId}; rewrite state was already finalized.",
                attemptId);
        }
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

    private static Task DelayReservationRaceRetryAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), cancellationToken);

    private static bool IsReservationRaceException(DbUpdateException exception)
    {
        var message = exception.ToString();
        return IsDatabaseLocked(exception) ||
            message.Contains("IX_UsagePeriods_UserId_PeriodKey", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("UsagePeriods.UserId", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("UsagePeriods.PeriodKey", StringComparison.OrdinalIgnoreCase)) ||
            message.Contains("IX_RewriteAttempts_UserId_IdempotencyKey", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("RewriteAttempts.UserId", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("RewriteAttempts.IdempotencyKey", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDatabaseLocked(Exception exception) =>
        exception is SqliteException { SqliteErrorCode: 5 or 6 } ||
        exception.ToString().Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("database table is locked", StringComparison.OrdinalIgnoreCase);

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

    private sealed record ReservationReleaseLogEntry(
        Guid AttemptId,
        string Reason,
        bool ReservationReleased,
        bool AttemptTransitioned,
        string ReservationSource);
}
