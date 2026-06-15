using System.Data;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class ReleaseQuotaHandler(
    IRewriteAttemptRepository attempts,
    IUsageReservationRepository reservations,
    IUsagePeriodRepository usagePeriods,
    IRewriteCreditRepository credits,
    IUnitOfWork unitOfWork,
    ILogger<ReleaseQuotaHandler> logger)
{
    private const int ReservationRaceMaxAttempts = 3;
    private const string QuotaReleasedEvent = "quota_released";

    public async Task HandleAsync(
        ReleaseQuotaCommand command,
        CancellationToken ct = default)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var reservation = await RequireReservationAsync(command.AttemptId, transactionCt);
                var claimed = await reservations.TryTransitionFromPendingAsync(
                    reservation.Id,
                    UsageReservationStatus.Released,
                    command.Now,
                    transactionCt) == 1;
                var releasePath = "not_claimed";
                if (claimed)
                {
                    if (reservation.RewriteCreditId is { } creditId)
                    {
                        await credits.ReleaseConsumedAsync(creditId, transactionCt);
                        releasePath = "credit";
                    }
                    else
                    {
                        await usagePeriods.ReleaseReservedSlotAsync(reservation.UsagePeriodId, command.Now, transactionCt);
                        releasePath = "slot";
                    }
                }

                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);
                var previousAttemptStatus = attempt.Status;
                if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
                {
                    attempt.Status = RewriteAttemptStatus.Failed;
                    attempt.ErrorCode = command.ErrorCode;
                    attempt.CompletedAt = command.Now;
                }

                await unitOfWork.SaveChangesAsync(transactionCt);
                return new ReleaseQuotaLogResult(
                    claimed,
                    command.AttemptId,
                    reservation.Id,
                    releasePath,
                    command.ErrorCode,
                    previousAttemptStatus,
                    attempt.Status);
            },
            IsolationLevel.ReadCommitted,
            ReservationRaceMaxAttempts,
            ct);

        if (result.Claimed)
        {
            logger.LogInformation(
                "{QuotaLifecycleEvent} Released quota reservation {ReservationId} for attempt {AttemptId}, path {ReleasePath}, previous attempt status {PreviousAttemptStatus}, current attempt status {CurrentAttemptStatus}, error {ErrorCode}.",
                QuotaReleasedEvent,
                result.ReservationId,
                result.AttemptId,
                result.ReleasePath,
                result.PreviousAttemptStatus,
                result.CurrentAttemptStatus,
                result.ErrorCode);
        }
        else
        {
            logger.LogDebug(
                "{QuotaLifecycleEvent} Release quota skipped for reservation {ReservationId}, attempt {AttemptId}, path {ReleasePath}, previous attempt status {PreviousAttemptStatus}, current attempt status {CurrentAttemptStatus}, error {ErrorCode}.",
                QuotaReleasedEvent,
                result.ReservationId,
                result.AttemptId,
                result.ReleasePath,
                result.PreviousAttemptStatus,
                result.CurrentAttemptStatus,
                result.ErrorCode);
        }
    }

    private async Task<RewriteAttempt> RequireAttemptAsync(Guid attemptId, CancellationToken ct) =>
        await attempts.GetByIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Rewrite attempt '{attemptId}' was not found.");

    private async Task<UsageReservation> RequireReservationAsync(Guid attemptId, CancellationToken ct) =>
        await reservations.GetByAttemptIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Usage reservation for attempt '{attemptId}' was not found.");

    private sealed record ReleaseQuotaLogResult(
        bool Claimed,
        Guid AttemptId,
        Guid ReservationId,
        string ReleasePath,
        string ErrorCode,
        RewriteAttemptStatus PreviousAttemptStatus,
        RewriteAttemptStatus CurrentAttemptStatus);
}
