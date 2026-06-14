using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class ReleaseExpiredReservationsHandler(
    IUsageReservationRepository reservations,
    IUnitOfWork unitOfWork)
{
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
                        reservation.RewriteAttempt.RowVersion = Guid.NewGuid();
                        claimedCount++;
                    }

                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return new BatchReleaseResult(claimedCount, expiredReservations.Count);
                },
                IsolationLevel.ReadCommitted,
                ct);

            releasedCount += batchResult.ClaimedCount;
            if (batchResult.ListedCount < command.BatchSize)
            {
                return releasedCount;
            }
        }
    }

    private sealed record BatchReleaseResult(int ClaimedCount, int ListedCount);
}
