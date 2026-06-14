using System.Data;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class ReleaseExpiredReservationsHandler(
    IUsageReservationRepository reservations,
    IUnitOfWork unitOfWork,
    ILogger<ReleaseExpiredReservationsHandler> logger)
{
    private const string QuotaReservationExpiredEvent = "quota_reservation_expired";
    private const string QuotaExpiredReservationsBatchEvent = "quota_expired_reservations_batch";

    public async Task<int> HandleAsync(
        ReleaseExpiredReservationsCommand command,
        CancellationToken ct = default)
    {
        if (command.BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.BatchSize), "Batch size must be greater than zero.");
        }

        var releasedCount = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batchResult = await unitOfWork.ExecuteInTransactionAsync(
                async transactionCt =>
                {
                    var expiredReservations = await reservations.ListExpiredPendingBatchAsync(
                        command.Now,
                        command.BatchSize,
                        transactionCt);

                    var claimedCount = 0;
                    var releasedReservations = new List<ExpiredReservationLogInfo>();
                    foreach (var reservation in expiredReservations)
                    {
                        var reason = reservation.RewriteAttempt!.Status is RewriteAttemptStatus.Processing
                            ? "processing_timed_out"
                            : "reservation_expired";

                        var claimed = await reservations.TryTransitionFromPendingAsync(
                            reservation.Id,
                            UsageReservationStatus.Expired,
                            command.Now,
                            transactionCt) == 1;
                        if (!claimed)
                        {
                            continue;
                        }

                        await reservations.ReleaseClaimedCounterAsync(
                            reservation.Id,
                            reservation.UsagePeriodId,
                            reservation.RewriteCreditId,
                            command.Now,
                            transactionCt);

                        reservation.RewriteAttempt.Status = RewriteAttemptStatus.Expired;
                        reservation.RewriteAttempt.ErrorCode = reason;
                        reservation.RewriteAttempt.CompletedAt = command.Now;
                        claimedCount++;
                        releasedReservations.Add(new ExpiredReservationLogInfo(
                            reservation.Id,
                            reservation.RewriteAttemptId,
                            reason));
                    }

                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return new BatchReleaseResult(claimedCount, expiredReservations.Count, releasedReservations);
                },
                IsolationLevel.ReadCommitted,
                ct);

            foreach (var reservation in batchResult.ReleasedReservations)
            {
                logger.LogInformation(
                    "{QuotaLifecycleEvent} Expired quota reservation {ReservationId} for attempt {AttemptId} with reason {Reason}.",
                    QuotaReservationExpiredEvent,
                    reservation.ReservationId,
                    reservation.AttemptId,
                    reservation.Reason);
            }

            releasedCount += batchResult.ClaimedCount;
            if (batchResult.ListedCount < command.BatchSize)
            {
                logger.LogInformation(
                    "{QuotaLifecycleEvent} Released {ReleasedCount} expired quota reservations.",
                    QuotaExpiredReservationsBatchEvent,
                    releasedCount);
                return releasedCount;
            }
        }
    }

    private sealed record BatchReleaseResult(
        int ClaimedCount,
        int ListedCount,
        IReadOnlyList<ExpiredReservationLogInfo> ReleasedReservations);

    private sealed record ExpiredReservationLogInfo(
        Guid ReservationId,
        Guid AttemptId,
        string Reason);
}
