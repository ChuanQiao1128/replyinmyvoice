using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class ReserveQuotaHandler(
    IUsagePeriodRepository usagePeriods,
    IRewriteAttemptRepository attempts,
    IUsageReservationRepository reservations,
    IRewriteCreditRepository credits,
    IOutboxMessageRepository outboxMessages,
    IUnitOfWork unitOfWork,
    ILogger<ReserveQuotaHandler> logger)
{
    private const int ReservationRaceMaxAttempts = 3;
    private const string QuotaReservedEvent = "quota_reserved";
    private const string QuotaReserveExistingEvent = "quota_reserve_existing";
    private const string QuotaReserveConflictEvent = "quota_reserve_conflict";
    private const string QuotaExhaustedEvent = "quota_exhausted";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ReserveQuotaResult> HandleAsync(
        ReserveQuotaCommand command,
        CancellationToken ct = default)
    {
        // Admission is decided inside conditional UPDATE statements under row locks; unique-index races are retried by UnitOfWork.
        var result = await unitOfWork.ExecuteInTransactionAsync(
            transactionCt => ReserveCoreAsync(command, transactionCt),
            IsolationLevel.ReadCommitted,
            ReservationRaceMaxAttempts,
            ct);

        LogResult(command, result);
        return result;
    }

    private async Task<ReserveQuotaResult> ReserveCoreAsync(
        ReserveQuotaCommand command,
        CancellationToken ct)
    {
        var existingAttempt = await attempts.GetByUserIdAndIdempotencyKeyAsync(
            command.UserId,
            command.IdempotencyKey,
            ct);

        if (existingAttempt is not null)
        {
            if (!string.Equals(existingAttempt.RequestHash, command.RequestHash, StringComparison.Ordinal))
            {
                return ReserveQuotaResult.Conflict(existingAttempt.Id, existingAttempt.Status);
            }

            return new ReserveQuotaResult(
                ReserveQuotaResultKind.Existing,
                existingAttempt.Id,
                existingAttempt.Status,
                existingAttempt.ResultJson,
                existingAttempt.ErrorCode);
        }

        var period = await usagePeriods.GetByUserIdAndPeriodKeyAsync(
            command.UserId,
            command.PeriodKey,
            ct);
        var wonPeriodSlot = period is null
            ? command.QuotaLimit > 0
            : await usagePeriods.TryReserveSlotAsync(
                period.Id,
                command.QuotaLimit,
                command.Now,
                ct) == 1;

        Guid? consumedCreditId = null;
        if (!wonPeriodSlot)
        {
            var creditIds = await credits.ListUsableForReservationIdsAsync(
                command.UserId,
                command.Now,
                ct);

            foreach (var creditId in creditIds)
            {
                if (await credits.TryConsumeForReservationAsync(creditId, ct) == 1)
                {
                    consumedCreditId = creditId;
                    break;
                }
            }

            if (consumedCreditId is null)
            {
                return ReserveQuotaResult.QuotaExceeded();
            }
        }

        UsagePeriod? newPeriod = null;
        Guid? existingPeriodId = period?.Id;
        if (period is null)
        {
            newPeriod = new UsagePeriod
            {
                UserId = command.UserId,
                PeriodKey = command.PeriodKey,
                QuotaLimit = command.QuotaLimit,
                ReservedCount = wonPeriodSlot ? 1 : 0,
                CreatedAt = command.Now,
                UpdatedAt = command.Now,
            };
            await usagePeriods.AddAsync(newPeriod, ct);
        }
        else if (!wonPeriodSlot)
        {
            await usagePeriods.RefreshQuotaLimitAsync(period.Id, command.QuotaLimit, command.Now, ct);
        }

        var attempt = CreatePendingAttempt(command);
        await attempts.AddAsync(attempt, ct);

        var reservation = new UsageReservation
        {
            UserId = command.UserId,
            RewriteAttempt = attempt,
            RewriteCreditId = consumedCreditId,
            Status = UsageReservationStatus.Pending,
            CreatedAt = command.Now,
            ExpiresAt = command.Now.Add(command.ReservationTtl),
        };
        if (newPeriod is not null)
        {
            reservation.UsagePeriod = newPeriod;
        }
        else
        {
            reservation.UsagePeriodId = existingPeriodId!.Value;
        }

        await reservations.AddAsync(reservation, ct);
        await outboxMessages.AddAsync(CreateRewriteJobOutboxMessage(attempt.Id, command.Now), ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ReserveQuotaResult(
            ReserveQuotaResultKind.Created,
            attempt.Id,
            attempt.Status);
    }

    private static RewriteAttempt CreatePendingAttempt(ReserveQuotaCommand command) =>
        new()
        {
            UserId = command.UserId,
            IdempotencyKey = command.IdempotencyKey,
            RequestHash = command.RequestHash,
            RequestJson = command.RequestJson,
            ApiKeyId = command.ApiKeyId,
            Status = RewriteAttemptStatus.Pending,
            CreatedAt = command.Now,
            ExpiresAt = command.Now.Add(command.ReservationTtl),
        };

    private static OutboxMessage CreateRewriteJobOutboxMessage(
        Guid attemptId,
        DateTimeOffset now) =>
        new()
        {
            MessageType = "RewriteJobCreated",
            PayloadJson = JsonSerializer.Serialize(new RewriteJobCreatedPayload(attemptId), JsonOptions),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            AttemptCount = 0,
            MaxAttempts = 10,
            CorrelationId = attemptId.ToString(),
        };

    private void LogResult(ReserveQuotaCommand command, ReserveQuotaResult result)
    {
        switch (result.Kind)
        {
            case ReserveQuotaResultKind.Created:
                logger.LogInformation(
                    "{QuotaLifecycleEvent} Reserved quota for user {UserId}, attempt {AttemptId}, status {AttemptStatus}, result {ResultKind}.",
                    QuotaReservedEvent,
                    command.UserId,
                    result.AttemptId,
                    result.Status,
                    result.Kind);
                break;
            case ReserveQuotaResultKind.Existing:
                logger.LogInformation(
                    "{QuotaLifecycleEvent} Reused existing quota reservation for user {UserId}, attempt {AttemptId}, status {AttemptStatus}, result {ResultKind}.",
                    QuotaReserveExistingEvent,
                    command.UserId,
                    result.AttemptId,
                    result.Status,
                    result.Kind);
                break;
            case ReserveQuotaResultKind.Conflict:
                logger.LogWarning(
                    "{QuotaLifecycleEvent} Quota reservation conflict for user {UserId}, attempt {AttemptId}, status {AttemptStatus}, result {ResultKind}, error {ErrorCode}.",
                    QuotaReserveConflictEvent,
                    command.UserId,
                    result.AttemptId,
                    result.Status,
                    result.Kind,
                    result.ErrorCode);
                break;
            case ReserveQuotaResultKind.QuotaExceeded:
                logger.LogWarning(
                    "{QuotaLifecycleEvent} Quota exhausted for user {UserId}, result {ResultKind}, error {ErrorCode}.",
                    QuotaExhaustedEvent,
                    command.UserId,
                    result.Kind,
                    result.ErrorCode);
                break;
        }
    }

    private sealed record RewriteJobCreatedPayload(Guid AttemptId);
}
