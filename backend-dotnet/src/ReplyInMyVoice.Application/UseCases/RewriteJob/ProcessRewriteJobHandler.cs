using System.Data;
using System.Text.Json;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.RewriteJob;

public sealed class RewriteJobAttemptNotFoundException : InvalidOperationException
{
    public RewriteJobAttemptNotFoundException(Guid attemptId)
        : base($"Rewrite attempt {attemptId} was not found.")
    {
        AttemptId = attemptId;
    }

    public RewriteJobAttemptNotFoundException(Guid attemptId, Exception innerException)
        : base($"Rewrite attempt {attemptId} was not found.", innerException)
    {
        AttemptId = attemptId;
    }

    public Guid AttemptId { get; }
}

public sealed class ProcessRewriteJobHandler(
    IRewriteAttemptRepository attempts,
    IUsageReservationRepository reservations,
    IUsagePeriodRepository usagePeriods,
    IRewriteCreditRepository credits,
    IUnitOfWork unitOfWork,
    IRewriteEngineClient rewriteEngine,
    IRewriteCostLogger costLogger,
    IBusinessMetrics? metrics = null)
{
    private const int ReservationRaceMaxAttempts = 3;
    private readonly IBusinessMetrics _metrics = metrics ?? NoOpBusinessMetrics.Instance;

    public async Task HandleAsync(
        ProcessRewriteJobCommand command,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = await attempts.GetByIdNoTrackingAsync(command.AttemptId, ct);
        if (attempt is null)
        {
            throw new RewriteJobAttemptNotFoundException(command.AttemptId);
        }

        if (attempt.Status is RewriteAttemptStatus.Succeeded or RewriteAttemptStatus.Failed or RewriteAttemptStatus.Expired)
        {
            return;
        }

        if (attempt.ExpiresAt <= now)
        {
            await ExecuteAttemptMutationAsync(
                command.AttemptId,
                mutationCt => ReleaseAsync(command.AttemptId, RewriteEngineErrorCodes.ReservationExpired, now, mutationCt),
                ct);
            RecordQuotaReleased(RewriteEngineErrorCodes.ReservationExpired);
            return;
        }

        RewriteRequest request;
        try
        {
            request = JsonSerializer.Deserialize<RewriteRequest>(
                attempt.RequestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            await ExecuteAttemptMutationAsync(
                command.AttemptId,
                mutationCt => ReleaseAsync(command.AttemptId, RewriteEngineErrorCodes.RequestJsonParseFailed, now, mutationCt),
                ct);
            RecordQuotaReleased(RewriteEngineErrorCodes.RequestJsonParseFailed);
            return;
        }

        var claimed = await ExecuteAttemptMutationAsync(
            command.AttemptId,
            mutationCt => MarkProcessingAsync(command.AttemptId, now, mutationCt),
            ct);
        if (!claimed)
        {
            return;
        }

        var rewriteStartedAt = DateTimeOffset.UtcNow;
        RewriteEngineResult result;
        try
        {
            result = await rewriteEngine.RewriteAsync(command.AttemptId, request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var timeoutFinishedAt = DateTimeOffset.UtcNow;
            const string timeoutErrorCode = RewriteEngineErrorCodes.ProviderTimeout;
            await WriteCostLogAsync(
                command.AttemptId,
                request,
                null,
                [],
                "failed",
                timeoutErrorCode,
                rewriteStartedAt,
                timeoutFinishedAt,
                ct);
            await ExecuteAttemptMutationAsync(
                command.AttemptId,
                mutationCt => ReleaseAsync(command.AttemptId, timeoutErrorCode, timeoutFinishedAt, mutationCt),
                ct);
            RecordQuotaReleased(timeoutErrorCode);
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failureFinishedAt = DateTimeOffset.UtcNow;
            const string failureErrorCode = RewriteEngineErrorCodes.ProviderFailed;
            await WriteCostLogAsync(
                command.AttemptId,
                request,
                null,
                [],
                "failed",
                failureErrorCode,
                rewriteStartedAt,
                failureFinishedAt,
                ct);
            await ExecuteAttemptMutationAsync(
                command.AttemptId,
                mutationCt => ReleaseAsync(command.AttemptId, failureErrorCode, failureFinishedAt, mutationCt),
                ct);
            RecordQuotaReleased(failureErrorCode);
            return;
        }

        var rewriteFinishedAt = DateTimeOffset.UtcNow;
        if (result.Success && IsValidResultJson(result.ResultJson))
        {
            await ExecuteAttemptMutationAsync(
                command.AttemptId,
                mutationCt => FinalizeSuccessAsync(command.AttemptId, result.ResultJson!, rewriteFinishedAt, mutationCt),
                ct);
            await WriteCostLogAsync(
                command.AttemptId,
                request,
                result.ResultJson,
                result.ProviderCalls,
                "succeeded",
                null,
                rewriteStartedAt,
                rewriteFinishedAt,
                ct);
            return;
        }

        var errorCode = result.Success
            ? RewriteEngineErrorCodes.ProviderJsonParseFailed
            : result.ErrorCode ?? RewriteEngineErrorCodes.ProviderFailed;
        await WriteCostLogAsync(
            command.AttemptId,
            request,
            result.ResultJson,
            result.ProviderCalls,
            "failed",
            errorCode,
            rewriteStartedAt,
            rewriteFinishedAt,
            ct);
        await ExecuteAttemptMutationAsync(
            command.AttemptId,
            mutationCt => ReleaseAsync(command.AttemptId, errorCode, rewriteFinishedAt, mutationCt),
            ct);
        RecordFinalFailureMetrics(errorCode);
    }

    private void RecordQuotaReleased(string errorCode) =>
        _metrics.Record(
            BusinessMetricNames.QuotaReleasedTotal,
            1,
            BusinessMetricDimensions.ErrorCode,
            errorCode);

    private void RecordFinalFailureMetrics(string errorCode)
    {
        RecordQuotaReleased(errorCode);
        if (RewriteQualityFailureCodes.All.Contains(errorCode))
        {
            _metrics.Record(
                BusinessMetricNames.RewriteQualityFailureTotal,
                1,
                BusinessMetricDimensions.ErrorCode,
                errorCode);
        }
    }

    private async Task ExecuteAttemptMutationAsync(
        Guid attemptId,
        Func<CancellationToken, Task> action,
        CancellationToken ct)
    {
        try
        {
            await action(ct);
        }
        catch (InvalidOperationException ex)
        {
            await ThrowIfAttemptIsMissingAsync(attemptId, ex, ct);
            throw;
        }
    }

    private async Task<T> ExecuteAttemptMutationAsync<T>(
        Guid attemptId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        try
        {
            return await action(ct);
        }
        catch (InvalidOperationException ex)
        {
            await ThrowIfAttemptIsMissingAsync(attemptId, ex, ct);
            throw;
        }
    }

    private async Task ThrowIfAttemptIsMissingAsync(
        Guid attemptId,
        InvalidOperationException innerException,
        CancellationToken ct)
    {
        var attemptExists = await attempts.GetByIdNoTrackingAsync(attemptId, ct) is not null;
        if (!attemptExists)
        {
            throw new RewriteJobAttemptNotFoundException(attemptId, innerException);
        }
    }

    private async Task<bool> MarkProcessingAsync(
        Guid attemptId,
        DateTimeOffset now,
        CancellationToken ct) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(attemptId, transactionCt);
                if (attempt.Status is not RewriteAttemptStatus.Pending)
                {
                    return false;
                }

                attempt.Status = RewriteAttemptStatus.Processing;
                await unitOfWork.SaveChangesAsync(transactionCt);
                return true;
            },
            IsolationLevel.Serializable,
            ct);

    private async Task FinalizeSuccessAsync(
        Guid attemptId,
        string resultJson,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(attemptId, transactionCt);
                var reservation = await RequireReservationAsync(attemptId, transactionCt);

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
                    now,
                    transactionCt) == 1;
                if (!claimed)
                {
                    return false;
                }

                if (reservation.RewriteCreditId is null)
                {
                    await usagePeriods.FinalizeReservedSlotAsync(reservation.UsagePeriodId, now, transactionCt);
                }

                attempt.Status = RewriteAttemptStatus.Succeeded;
                attempt.ResultJson = resultJson;
                attempt.CompletedAt = now;

                await unitOfWork.SaveChangesAsync(transactionCt);
                return true;
            },
            IsolationLevel.ReadCommitted,
            ReservationRaceMaxAttempts,
            ct);
    }

    private async Task ReleaseAsync(
        Guid attemptId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var reservation = await RequireReservationAsync(attemptId, transactionCt);
                var claimed = await reservations.TryTransitionFromPendingAsync(
                    reservation.Id,
                    UsageReservationStatus.Released,
                    now,
                    transactionCt) == 1;
                if (claimed)
                {
                    if (reservation.RewriteCreditId is { } creditId)
                    {
                        await credits.ReleaseConsumedAsync(creditId, transactionCt);
                    }
                    else
                    {
                        await usagePeriods.ReleaseReservedSlotAsync(reservation.UsagePeriodId, now, transactionCt);
                    }
                }

                var attempt = await RequireAttemptAsync(attemptId, transactionCt);
                if (attempt.Status is RewriteAttemptStatus.Pending or RewriteAttemptStatus.Processing)
                {
                    attempt.Status = RewriteAttemptStatus.Failed;
                    attempt.ErrorCode = errorCode;
                    attempt.CompletedAt = now;
                }

                await unitOfWork.SaveChangesAsync(transactionCt);
                return true;
            },
            IsolationLevel.ReadCommitted,
            ReservationRaceMaxAttempts,
            ct);
    }

    private async Task WriteCostLogAsync(
        Guid attemptId,
        RewriteRequest request,
        string? resultJson,
        IReadOnlyList<RewriteEngineCallMetric> providerCalls,
        string status,
        string? errorCode,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        CancellationToken ct)
    {
        try
        {
            await costLogger.WriteAsync(
                new RewriteCostLogEntry(
                    attemptId,
                    request,
                    resultJson,
                    providerCalls,
                    status,
                    errorCode,
                    startedAt,
                    finishedAt),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not RewriteJobAttemptNotFoundException)
        {
        }
    }

    private async Task<RewriteAttempt> RequireAttemptAsync(Guid attemptId, CancellationToken ct) =>
        await attempts.GetByIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Rewrite attempt '{attemptId}' was not found.");

    private async Task<UsageReservation> RequireReservationAsync(Guid attemptId, CancellationToken ct) =>
        await reservations.GetByAttemptIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Usage reservation for attempt '{attemptId}' was not found.");

    private static bool IsValidResultJson(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            return root.TryGetProperty("rewrittenText", out var rewrittenText) &&
                rewrittenText.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(rewrittenText.GetString()) &&
                root.TryGetProperty("changeSummary", out var changeSummary) &&
                changeSummary.ValueKind == JsonValueKind.Array &&
                root.TryGetProperty("riskNotes", out var riskNotes) &&
                riskNotes.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
