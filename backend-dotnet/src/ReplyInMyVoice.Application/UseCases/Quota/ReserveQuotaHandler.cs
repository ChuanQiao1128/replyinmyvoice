using System.Data;
using System.Text.Json;
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
    IUnitOfWork unitOfWork)
{
    private const int ReservationRaceMaxAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ReserveQuotaResult> HandleAsync(
        ReserveQuotaCommand command,
        CancellationToken ct = default) =>
        await unitOfWork.ExecuteInTransactionAsync(
            transactionCt => ReserveCoreAsync(command, transactionCt),
            IsolationLevel.Serializable,
            ReservationRaceMaxAttempts,
            ct);

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
        var usedCount = period?.UsedCount ?? 0;
        var reservedCount = period?.ReservedCount ?? 0;
        if (usedCount + reservedCount >= command.QuotaLimit)
        {
            var credit = await credits.GetUsableForReservationAsync(
                command.UserId,
                command.Now,
                ct);
            if (credit is null)
            {
                return ReserveQuotaResult.QuotaExceeded();
            }

            period = await PrepareUsagePeriodAsync(period, command, ct);
            var creditAttempt = CreatePendingAttempt(command);
            credit.AmountConsumed += 1;
            credit.RowVersion = Guid.NewGuid();

            await attempts.AddAsync(creditAttempt, ct);
            await reservations.AddAsync(new UsageReservation
            {
                UserId = command.UserId,
                UsagePeriod = period,
                RewriteAttempt = creditAttempt,
                RewriteCredit = credit,
                Status = UsageReservationStatus.Pending,
                CreatedAt = command.Now,
                ExpiresAt = command.Now.Add(command.ReservationTtl),
            }, ct);
            await outboxMessages.AddAsync(CreateRewriteJobOutboxMessage(creditAttempt.Id, command.Now), ct);
            await unitOfWork.SaveChangesAsync(ct);

            return new ReserveQuotaResult(
                ReserveQuotaResultKind.Created,
                creditAttempt.Id,
                creditAttempt.Status);
        }

        period = await PrepareUsagePeriodAsync(period, command, ct);
        var attempt = CreatePendingAttempt(command);
        period.ReservedCount += 1;
        period.RowVersion = Guid.NewGuid();
        await attempts.AddAsync(attempt, ct);
        await reservations.AddAsync(new UsageReservation
        {
            UserId = command.UserId,
            UsagePeriod = period,
            RewriteAttempt = attempt,
            Status = UsageReservationStatus.Pending,
            CreatedAt = command.Now,
            ExpiresAt = command.Now.Add(command.ReservationTtl),
        }, ct);
        await outboxMessages.AddAsync(CreateRewriteJobOutboxMessage(attempt.Id, command.Now), ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ReserveQuotaResult(
            ReserveQuotaResultKind.Created,
            attempt.Id,
            attempt.Status);
    }

    private async Task<UsagePeriod> PrepareUsagePeriodAsync(
        UsagePeriod? period,
        ReserveQuotaCommand command,
        CancellationToken ct)
    {
        if (period is null)
        {
            period = new UsagePeriod
            {
                UserId = command.UserId,
                PeriodKey = command.PeriodKey,
                QuotaLimit = command.QuotaLimit,
                CreatedAt = command.Now,
                UpdatedAt = command.Now,
            };
            await usagePeriods.AddAsync(period, ct);
            return period;
        }

        period.QuotaLimit = command.QuotaLimit;
        period.UpdatedAt = command.Now;
        period.RowVersion = Guid.NewGuid();
        return period;
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

    private sealed record RewriteJobCreatedPayload(Guid AttemptId);
}
