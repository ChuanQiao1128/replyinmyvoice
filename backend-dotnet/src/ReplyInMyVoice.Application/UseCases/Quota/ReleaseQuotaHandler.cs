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
    public async Task HandleAsync(
        ReleaseQuotaCommand command,
        CancellationToken ct = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);
                var reservation = await RequireReservationAsync(command.AttemptId, transactionCt);

                if (reservation.Status == UsageReservationStatus.Pending)
                {
                    if (reservation.RewriteCreditId is { } creditId)
                    {
                        var credit = await RequireCreditAsync(creditId, transactionCt);
                        credit.AmountConsumed = Math.Max(0, credit.AmountConsumed - 1);
                        credit.RowVersion = Guid.NewGuid();
                    }
                    else
                    {
                        var period = await RequireUsagePeriodAsync(reservation.UsagePeriodId, transactionCt);
                        period.ReservedCount = Math.Max(0, period.ReservedCount - 1);
                        period.UpdatedAt = command.Now;
                        period.RowVersion = Guid.NewGuid();
                    }

                    reservation.Status = UsageReservationStatus.Released;
                    reservation.ReleasedAt = command.Now;
                    reservation.RowVersion = Guid.NewGuid();
                }

                if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
                {
                    attempt.Status = RewriteAttemptStatus.Failed;
                    attempt.ErrorCode = command.ErrorCode;
                    attempt.CompletedAt = command.Now;
                    attempt.RowVersion = Guid.NewGuid();
                }

                await unitOfWork.SaveChangesAsync(transactionCt);
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

    private async Task<RewriteCredit> RequireCreditAsync(Guid creditId, CancellationToken ct) =>
        await credits.GetByIdAsync(creditId, ct) ??
        throw new InvalidOperationException($"Rewrite credit '{creditId}' was not found.");
}
