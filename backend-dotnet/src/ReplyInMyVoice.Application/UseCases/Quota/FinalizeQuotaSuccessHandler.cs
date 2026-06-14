using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class FinalizeQuotaSuccessHandler(
    IRewriteAttemptRepository attempts,
    IUsageReservationRepository reservations,
    IUsagePeriodRepository usagePeriods,
    IUnitOfWork unitOfWork)
{
    private const int ReservationRaceMaxAttempts = 3;

    public async Task HandleAsync(
        FinalizeQuotaSuccessCommand command,
        CancellationToken ct = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);
                var reservation = await RequireReservationAsync(command.AttemptId, transactionCt);

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

                var claimed = await reservations.TryTransitionFromPendingAsync(
                    reservation.Id,
                    UsageReservationStatus.Finalized,
                    command.Now,
                    transactionCt) == 1;
                if (!claimed)
                {
                    return false;
                }

                if (reservation.RewriteCreditId is null)
                {
                    await usagePeriods.FinalizeReservedSlotAsync(reservation.UsagePeriodId, command.Now, transactionCt);
                }

                attempt.Status = RewriteAttemptStatus.Succeeded;
                attempt.ResultJson = command.ResultJson;
                attempt.CompletedAt = command.Now;
                attempt.RowVersion = Guid.NewGuid();

                await unitOfWork.SaveChangesAsync(transactionCt);
                return true;
            },
            IsolationLevel.ReadCommitted,
            ReservationRaceMaxAttempts,
            ct);
    }

    private async Task<RewriteAttempt> RequireAttemptAsync(Guid attemptId, CancellationToken ct) =>
        await attempts.GetByIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Rewrite attempt '{attemptId}' was not found.");

    private async Task<UsageReservation> RequireReservationAsync(Guid attemptId, CancellationToken ct) =>
        await reservations.GetByAttemptIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Usage reservation for attempt '{attemptId}' was not found.");
}
