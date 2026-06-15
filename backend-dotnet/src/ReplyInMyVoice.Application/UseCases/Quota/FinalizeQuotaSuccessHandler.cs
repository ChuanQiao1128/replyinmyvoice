using System.Data;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class FinalizeQuotaSuccessHandler(
    IRewriteAttemptRepository attempts,
    IUsageReservationRepository reservations,
    IUsagePeriodRepository usagePeriods,
    IUnitOfWork unitOfWork,
    ILogger<FinalizeQuotaSuccessHandler> logger)
{
    private const int ReservationRaceMaxAttempts = 3;
    private const string QuotaFinalizedEvent = "quota_finalized";
    private const string QuotaFinalizeNoopEvent = "quota_finalize_noop";

    public async Task HandleAsync(
        FinalizeQuotaSuccessCommand command,
        CancellationToken ct = default)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);
                var reservation = await RequireReservationAsync(command.AttemptId, transactionCt);

                if (attempt.Status == RewriteAttemptStatus.Succeeded &&
                    reservation.Status == UsageReservationStatus.Finalized)
                {
                    return FinalizeQuotaLogResult.Noop(
                        attempt.Id,
                        reservation.Id,
                        attempt.Status,
                        reservation.Status,
                        "already_finalized");
                }

                if (reservation.Status != UsageReservationStatus.Pending ||
                    attempt.Status is RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired)
                {
                    return FinalizeQuotaLogResult.Noop(
                        attempt.Id,
                        reservation.Id,
                        attempt.Status,
                        reservation.Status,
                        "ineligible_state");
                }

                var claimed = await reservations.TryTransitionFromPendingAsync(
                    reservation.Id,
                    UsageReservationStatus.Finalized,
                    command.Now,
                    transactionCt) == 1;
                if (!claimed)
                {
                    return FinalizeQuotaLogResult.Noop(
                        attempt.Id,
                        reservation.Id,
                        attempt.Status,
                        reservation.Status,
                        "claim_lost");
                }

                if (reservation.RewriteCreditId is null)
                {
                    await usagePeriods.FinalizeReservedSlotAsync(reservation.UsagePeriodId, command.Now, transactionCt);
                }

                attempt.Status = RewriteAttemptStatus.Succeeded;
                attempt.ResultJson = command.ResultJson;
                attempt.CompletedAt = command.Now;

                await unitOfWork.SaveChangesAsync(transactionCt);
                return FinalizeQuotaLogResult.Success(attempt.Id, reservation.Id);
            },
            IsolationLevel.ReadCommitted,
            ReservationRaceMaxAttempts,
            ct);

        if (result.Succeeded)
        {
            logger.LogInformation(
                "{QuotaLifecycleEvent} Finalized quota for attempt {AttemptId}, reservation {ReservationId}.",
                QuotaFinalizedEvent,
                result.AttemptId,
                result.ReservationId);
        }
        else
        {
            logger.LogDebug(
                "{QuotaLifecycleEvent} Skipped quota finalization for attempt {AttemptId}, reservation {ReservationId}, attempt status {AttemptStatus}, reservation status {ReservationStatus}, reason {Reason}.",
                QuotaFinalizeNoopEvent,
                result.AttemptId,
                result.ReservationId,
                result.AttemptStatus,
                result.ReservationStatus,
                result.Reason);
        }
    }

    private async Task<RewriteAttempt> RequireAttemptAsync(Guid attemptId, CancellationToken ct) =>
        await attempts.GetByIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Rewrite attempt '{attemptId}' was not found.");

    private async Task<UsageReservation> RequireReservationAsync(Guid attemptId, CancellationToken ct) =>
        await reservations.GetByAttemptIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Usage reservation for attempt '{attemptId}' was not found.");

    private sealed record FinalizeQuotaLogResult(
        bool Succeeded,
        Guid AttemptId,
        Guid ReservationId,
        RewriteAttemptStatus? AttemptStatus,
        UsageReservationStatus? ReservationStatus,
        string? Reason)
    {
        public static FinalizeQuotaLogResult Success(Guid attemptId, Guid reservationId) =>
            new(true, attemptId, reservationId, null, null, null);

        public static FinalizeQuotaLogResult Noop(
            Guid attemptId,
            Guid reservationId,
            RewriteAttemptStatus attemptStatus,
            UsageReservationStatus reservationStatus,
            string reason) =>
            new(false, attemptId, reservationId, attemptStatus, reservationStatus, reason);
    }
}
