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

            var batchCount = await unitOfWork.ExecuteInTransactionAsync(
                async transactionCt =>
                {
                    var expiredReservations = await reservations.ListExpiredPendingBatchAsync(
                        command.Now,
                        command.BatchSize,
                        transactionCt);

                    foreach (var reservation in expiredReservations)
                    {
                        var reason = reservation.RewriteAttempt!.Status is RewriteAttemptStatus.Processing
                            ? "processing_timed_out"
                            : "reservation_expired";

                        reservation.Status = UsageReservationStatus.Expired;
                        reservation.ReleasedAt = command.Now;
                        reservation.RowVersion = Guid.NewGuid();

                        if (reservation.RewriteCredit is not null)
                        {
                            reservation.RewriteCredit.AmountConsumed = Math.Max(
                                0,
                                reservation.RewriteCredit.AmountConsumed - 1);
                            reservation.RewriteCredit.RowVersion = Guid.NewGuid();
                        }
                        else
                        {
                            reservation.UsagePeriod!.ReservedCount = Math.Max(
                                0,
                                reservation.UsagePeriod.ReservedCount - 1);
                            reservation.UsagePeriod.UpdatedAt = command.Now;
                            reservation.UsagePeriod.RowVersion = Guid.NewGuid();
                        }

                        reservation.RewriteAttempt.Status = RewriteAttemptStatus.Expired;
                        reservation.RewriteAttempt.ErrorCode = reason;
                        reservation.RewriteAttempt.CompletedAt = command.Now;
                        reservation.RewriteAttempt.RowVersion = Guid.NewGuid();
                    }

                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return expiredReservations.Count;
                },
                IsolationLevel.Serializable,
                ct);

            releasedCount += batchCount;
            if (batchCount < command.BatchSize)
            {
                return releasedCount;
            }
        }
    }
}
