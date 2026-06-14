using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class ReleaseQuotaHandler(
    IRewriteAttemptRepository attempts,
    IUsageReservationRepository reservations,
    IUsagePeriodRepository usagePeriods,
    IRewriteCreditRepository credits,
    IUnitOfWork unitOfWork)
{
    private const int ReservationRaceMaxAttempts = 3;

    public async Task HandleAsync(
        ReleaseQuotaCommand command,
        CancellationToken ct = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var reservation = await RequireReservationAsync(command.AttemptId, transactionCt);
                var claimed = await reservations.TryTransitionFromPendingAsync(
                    reservation.Id,
                    UsageReservationStatus.Released,
                    command.Now,
                    transactionCt) == 1;
                if (claimed)
                {
                    if (reservation.RewriteCreditId is { } creditId)
                    {
                        await credits.ReleaseConsumedAsync(creditId, transactionCt);
                    }
                    else
                    {
                        await usagePeriods.ReleaseReservedSlotAsync(reservation.UsagePeriodId, command.Now, transactionCt);
                    }
                }

                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);
                if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
                {
                    attempt.Status = RewriteAttemptStatus.Failed;
                    attempt.ErrorCode = command.ErrorCode;
                    attempt.CompletedAt = command.Now;
                }

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
