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
        CancellationToken ct = default)
    {
        // Admission is decided inside conditional UPDATE statements under row locks; unique-index races are retried by UnitOfWork.
        return await unitOfWork.ExecuteInTransactionAsync(
            transactionCt => HandleCoreAsync(command, transactionCt),
            IsolationLevel.ReadCommitted,
            ReservationRaceMaxAttempts,
            ct);
    }

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
                return ApplicationResult<RewriteAttemptDto>.QuotaExceeded();
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

        var attempt = CreatePendingAttempt(command, requestHash);
        await attempts.AddAsync(attempt, ct);

        var reservation = new UsageReservation
        {
            UserId = command.UserId,
            RewriteAttempt = attempt,
            RewriteCreditId = consumedCreditId,
            Status = UsageReservationStatus.Pending,
            CreatedAt = command.Now,
            ExpiresAt = command.Now.Add(ReservationTtl),
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
        return ApplicationResult<RewriteAttemptDto>.Created(RewriteAttemptDto.FromAttempt(attempt));
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
