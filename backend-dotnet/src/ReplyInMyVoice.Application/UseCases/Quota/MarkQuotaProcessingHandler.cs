using System.Data;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed class MarkQuotaProcessingHandler(
    IRewriteAttemptRepository attempts,
    IUnitOfWork unitOfWork,
    ILogger<MarkQuotaProcessingHandler> logger)
{
    private const string QuotaMarkedProcessingEvent = "quota_marked_processing";
    private const string QuotaMarkProcessingSkippedEvent = "quota_mark_processing_skipped";

    public async Task<bool> HandleAsync(
        MarkQuotaProcessingCommand command,
        CancellationToken ct = default)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var attempt = await RequireAttemptAsync(command.AttemptId, transactionCt);

                if (attempt.Status is not RewriteAttemptStatus.Pending)
                {
                    return new MarkQuotaProcessingLogResult(false, attempt.Id, attempt.Status);
                }

                attempt.Status = RewriteAttemptStatus.Processing;
                await unitOfWork.SaveChangesAsync(transactionCt);
                return new MarkQuotaProcessingLogResult(true, attempt.Id, RewriteAttemptStatus.Pending);
            },
            IsolationLevel.Serializable,
            ct);

        if (result.Marked)
        {
            logger.LogInformation(
                "{QuotaLifecycleEvent} Marked quota attempt {AttemptId} processing from status {PreviousStatus} to status {CurrentStatus}.",
                QuotaMarkedProcessingEvent,
                result.AttemptId,
                result.PreviousStatus,
                RewriteAttemptStatus.Processing);
        }
        else
        {
            logger.LogDebug(
                "{QuotaLifecycleEvent} Skipped marking quota attempt {AttemptId} processing because status was {AttemptStatus}.",
                QuotaMarkProcessingSkippedEvent,
                result.AttemptId,
                result.PreviousStatus);
        }

        return result.Marked;
    }

    private async Task<RewriteAttempt> RequireAttemptAsync(Guid attemptId, CancellationToken ct) =>
        await attempts.GetByIdAsync(attemptId, ct) ??
        throw new InvalidOperationException($"Rewrite attempt '{attemptId}' was not found.");

    private sealed record MarkQuotaProcessingLogResult(
        bool Marked,
        Guid AttemptId,
        RewriteAttemptStatus PreviousStatus);
}
