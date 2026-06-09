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
    public async Task HandleAsync(
        FinalizeQuotaSuccessCommand command,
        CancellationToken ct = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);
                var reservation = await RequireReservationAsync(command.AttemptId, transactionCt);
                UsagePeriod? period = null;
                if (reservation.RewriteCreditId is null)
                {
                    period = await RequireUsagePeriodAsync(reservation.UsagePeriodId, transactionCt);
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

                if (reservation.RewriteCreditId is null)
                {
                    var usagePeriod = period!;
                    usagePeriod.ReservedCount = Math.Max(0, usagePeriod.ReservedCount - 1);
                    usagePeriod.UsedCount += 1;
                    usagePeriod.UpdatedAt = command.Now;
                    usagePeriod.RowVersion = Guid.NewGuid();
                }

                reservation.Status = UsageReservationStatus.Finalized;
                reservation.FinalizedAt = command.Now;
                reservation.RowVersion = Guid.NewGuid();

                attempt.Status = RewriteAttemptStatus.Succeeded;
                attempt.ResultJson = command.ResultJson;
                attempt.CompletedAt = command.Now;
                attempt.RowVersion = Guid.NewGuid();

                await unitOfWork.SaveChangesAsync(transactionCt);
                return true;
            },
            IsolationLevel.Serializable,
            ct);
    }

    private async Task<RewriteAttempt> RequireAttemptAsync(Guid attemptId, CancellationToken ct) =>
        await attempts.GetByIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Rewrite attempt '{attemptId}' was not found.");

    private async Task<UsageReservation> RequireReservationAsync(Guid attemptId, CancellationToken ct) =>
        await reservations.GetByAttemptIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Usage reservation for attempt '{attemptId}' was not found.");

    private async Task<UsagePeriod> RequireUsagePeriodAsync(Guid usagePeriodId, CancellationToken ct) =>
        await usagePeriods.GetByIdAsync(usagePeriodId, ct) ??
        throw new InvalidOperationException($"Usage period '{usagePeriodId}' was not found.");
}
