using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Rewrite;

public sealed class CreateRewriteAttemptHandler(
    IAppUserRepository appUsers,
    IUsagePeriodRepository usagePeriods,
    IRewriteAttemptRepository attempts,
    IUsageReservationRepository reservations,
    IRewriteCreditRepository credits,
    IOutboxMessageRepository outboxMessages,
    IUnitOfWork unitOfWork)
{
    private const int ReservationRaceMaxAttempts = 3;

    private static readonly JsonSerializerOptions OutboxJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(15);

    public async Task<ApplicationResult<RewriteAttemptDto>> HandleAsync(
        CreateRewriteAttemptCommand command,
        CancellationToken ct = default) =>
        await unitOfWork.ExecuteInTransactionAsync(
            transactionCt => HandleCoreAsync(command, transactionCt),
            IsolationLevel.Serializable,
            ReservationRaceMaxAttempts,
            ct);

    private async Task<ApplicationResult<RewriteAttemptDto>> HandleCoreAsync(
        CreateRewriteAttemptCommand command,
        CancellationToken ct)
    {
        var user = await appUsers.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return ApplicationResult<RewriteAttemptDto>.NotFound();
        }

        if (user.SuspendedAt is not null)
        {
            return ApplicationResult<RewriteAttemptDto>.QuotaExceeded("user_suspended");
        }

        var requestHash = ComputeRequestHash(command.Request);
        var existingAttempt = await attempts.GetByUserIdAndIdempotencyKeyAsync(
            command.UserId,
            command.IdempotencyKey,
            ct);
        if (existingAttempt is not null)
        {
            if (!string.Equals(existingAttempt.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return ApplicationResult<RewriteAttemptDto>.Conflict(
                    RewriteAttemptDto.FromAttempt(existingAttempt));
            }

            return ApplicationResult<RewriteAttemptDto>.Existing(
                RewriteAttemptDto.FromAttempt(existingAttempt));
        }

        var period = await usagePeriods.GetByUserIdAndPeriodKeyAsync(
            command.UserId,
            command.PeriodKey,
            ct);
        var attempt = CreatePendingAttempt(command, requestHash);
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
                return ApplicationResult<RewriteAttemptDto>.QuotaExceeded();
            }

            period = await PrepareUsagePeriodAsync(period, command, ct);
            credit.AmountConsumed += 1;
            credit.RowVersion = Guid.NewGuid();

            await attempts.AddAsync(attempt, ct);
            await reservations.AddAsync(new UsageReservation
            {
                UserId = command.UserId,
                UsagePeriod = period,
                RewriteAttempt = attempt,
                RewriteCredit = credit,
                Status = UsageReservationStatus.Pending,
                CreatedAt = command.Now,
                ExpiresAt = command.Now.Add(ReservationTtl),
            }, ct);
            await outboxMessages.AddAsync(CreateRewriteJobOutboxMessage(attempt.Id, command.Now), ct);
            await unitOfWork.SaveChangesAsync(ct);
            return ApplicationResult<RewriteAttemptDto>.Created(RewriteAttemptDto.FromAttempt(attempt));
        }

        period = await PrepareUsagePeriodAsync(period, command, ct);
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
            ExpiresAt = command.Now.Add(ReservationTtl),
        }, ct);
        await outboxMessages.AddAsync(CreateRewriteJobOutboxMessage(attempt.Id, command.Now), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult<RewriteAttemptDto>.Created(RewriteAttemptDto.FromAttempt(attempt));
    }

    private async Task<UsagePeriod> PrepareUsagePeriodAsync(
        UsagePeriod? period,
        CreateRewriteAttemptCommand command,
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

    private static RewriteAttempt CreatePendingAttempt(
        CreateRewriteAttemptCommand command,
        string requestHash) =>
        new()
        {
            UserId = command.UserId,
            IdempotencyKey = command.IdempotencyKey,
            RequestHash = requestHash,
            RequestJson = JsonSerializer.Serialize(command.Request),
            ApiKeyId = command.ApiKeyId,
            Status = RewriteAttemptStatus.Pending,
            CreatedAt = command.Now,
            ExpiresAt = command.Now.Add(ReservationTtl),
        };

    private static string ComputeRequestHash(RewriteRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static OutboxMessage CreateRewriteJobOutboxMessage(
        Guid attemptId,
        DateTimeOffset now) =>
        new()
        {
            MessageType = "RewriteJobCreated",
            PayloadJson = JsonSerializer.Serialize(new RewriteJobCreatedPayload(attemptId), OutboxJsonOptions),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            AttemptCount = 0,
            MaxAttempts = 10,
            CorrelationId = attemptId.ToString(),
        };

    private sealed record RewriteJobCreatedPayload(Guid AttemptId);
}
